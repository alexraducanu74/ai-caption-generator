<#
azure-deploy-gpu-vm-auto.ps1

Automated provisioning and setup of an Azure GPU VM for a short demo.
This script will:
 - create a resource group (if needed) and an Ubuntu VM with chosen GPU SKU
 - open ports 22 and 8000
 - optionally enable ACR admin user and fetch credentials
 - run remote commands on the VM to install NVIDIA drivers, Docker, nvidia-docker2
 - reboot the VM if required and wait for it to come back
 - docker login to ACR, pull the caption-service image and run it with --gpus all

USAGE (example):
 .\azure-deploy-gpu-vm-auto.ps1 -ResourceGroupName caption-demo-rg -VmName caption-gpu-vm -AcrName captiongeneratoreqh42kmb646vq -VmSize Standard_NC4as_T4_v3

IMPORTANT:
 - This attempts to fully automate installation using `az vm run-command invoke` which runs
   scripts as root on the VM. Driver installs may require a reboot; the script handles one reboot.
 - For simplicity this script temporarily enables ACR admin and embeds the ACR password into
   the remote run command. Treat the generated logs/console carefully and disable ACR admin after demo.
 - You must run this script locally where Azure CLI is available and you are signed in (az login).
#>

param(
    [string]$ResourceGroupName = "caption-demo-rg",
    [string]$VmName = "caption-gpu-vm",
    [string]$Location = "uksouth",
    [string]$VmSize = "Standard_NC4as_T4_v3",
    [string]$AcrName = "",
    [string]$AdminUsername = "azureuser",
    [switch]$UseSpot
)

function AbortIfNoAz() {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        Write-Error "Azure CLI (az) not found. Install it and sign in with 'az login'."
        exit 1
    }
}

function Wait-ForRunCommandSuccess($rg, $vm, $scripts, $timeoutSeconds=600) {
    $start = Get-Date
    while (((Get-Date) - $start).TotalSeconds -lt $timeoutSeconds) {
        try {
            $resp = az vm run-command invoke --command-id RunShellScript --name $vm --resource-group $rg --scripts $scripts --parameters '{}' -o json 2>&1
            if ($LASTEXITCODE -eq 0) {
                return $resp
            }
        } catch {
            # swallow and retry
        }
        Start-Sleep -Seconds 10
    }
    throw "Timeout waiting for run-command to succeed on VM $vm"
}

AbortIfNoAz

Write-Host "This script will create VM '$VmName' in RG '$ResourceGroupName' at location '$Location' with size '$VmSize'."
Write-Host "Press Enter to continue or Ctrl+C to cancel."
Read-Host | Out-Null

Write-Host "Ensuring resource group exists..."
# Create the resource group only if it doesn't already exist. If it exists in a different
# location we warn but continue and explicitly pass --location to az vm create so the VM
# is created in the requested location.
$rgExists = az group exists --name $ResourceGroupName -o tsv
if ($rgExists -eq 'true') {
    $existingRgLocation = az group show --name $ResourceGroupName --query location -o tsv
    if ($existingRgLocation -ne $Location) {
        Write-Warning "Resource group '$ResourceGroupName' already exists in location '$existingRgLocation' (requested '$Location'). The VM will be created in '$Location' explicitly."
    } else {
        Write-Host "Resource group '$ResourceGroupName' already exists in location '$existingRgLocation'."
    }
} else {
    az group create --name $ResourceGroupName --location $Location | Out-Null
}

# Ensure we pass --location to az vm create so the VM is created in the desired region
$createArgs = @(
    "--resource-group", $ResourceGroupName,
    "--name", $VmName,
    # Use an explicit marketplace image reference to avoid Azure CLI interpreting 'UbuntuLTS' as a custom image
    # Canonical Ubuntu 22.04 LTS marketplace image
    "--image", "Canonical:0001-com-ubuntu-server-jammy:22_04-lts:latest",
    "--size", $VmSize,
    "--location", $Location,
    "--admin-username", $AdminUsername,
    "--generate-ssh-keys"
)
if ($UseSpot.IsPresent) { $createArgs += @("--priority","Spot","--eviction-policy","Deallocate","--max-price","-1") }

Write-Host "Creating VM (this can take several minutes)..."

# Try to create the VM in the requested location and fall back to other regions if quota prevents creation.
$candidateRegions = @($Location, 'westeurope', 'uksouth', 'northeurope', 'francecentral') | Select-Object -Unique
$created = $false
foreach ($tryLoc in $candidateRegions) {
    if ($tryLoc -ne $Location) { Write-Host "Attempting in fallback region: $tryLoc" }
    $createArgs = $createArgs | Where-Object { $_ -ne '--location' -and $_ -ne $Location } 
    # rebuild args with the new location
    $baseArgs = @(
        "--resource-group", $ResourceGroupName,
        "--name", $VmName,
        "--image", "Canonical:0001-com-ubuntu-server-jammy:22_04-lts:latest",
        "--size", $VmSize,
        "--location", $tryLoc,
        "--admin-username", $AdminUsername,
        "--generate-ssh-keys"
    )
    if ($UseSpot.IsPresent) { $baseArgs += @("--priority","Spot","--eviction-policy","Deallocate","--max-price","-1") }

    try {
        az vm create @baseArgs | Out-Null
        $created = $true
        $Location = $tryLoc
        Write-Host "VM created in region $tryLoc"
        break
    } catch {
        $msg = $_.Exception.Message
        if ($msg -match 'QuotaExceeded' -or $msg -match 'exceeding approved') {
            Write-Warning "QuotaExceeded in $tryLoc: $msg"
            # try next region
            continue
        } else {
            Write-Warning "VM create failed in $tryLoc: $msg"
            # try next region
            continue
        }
    }
}

if (-not $created) {
    Write-Error "Unable to create VM in any candidate region. Check quotas with 'az vm list-usage --location <region> -o table' and request increases or pick a different SKU/region."
    throw
}

Write-Host "Opening ports 22 and 8000..."
az vm open-port --resource-group $ResourceGroupName --name $VmName --port 22 --priority 1002 | Out-Null
az vm open-port --resource-group $ResourceGroupName --name $VmName --port 8000 --priority 1001 | Out-Null

Write-Host "Fetching VM public IP..."
$publicIp = az vm list-ip-addresses --resource-group $ResourceGroupName --name $VmName --query "[0].virtualMachine.network.publicIpAddresses[0].ipAddress" -o tsv
Write-Host "VM IP: $publicIp"

$acrLoginServer = $null
$acrUser = $null
$acrPassword = $null
if (-not [string]::IsNullOrWhiteSpace($AcrName)) {
    Write-Host "Checking ACR '$AcrName'..."
    try {
        $acrLoginServer = az acr show --name $AcrName --query loginServer -o tsv
        if (-not $acrLoginServer) { Write-Warning "ACR not found or inaccessible." }
        else {
            Write-Host "Enabling ACR admin user (temporary) for demo..."
            az acr update --name $AcrName --admin-enabled true | Out-Null
            $creds = az acr credential show --name $AcrName --query "{username:username,password:passwords[0].value}" -o tsv
            $parts = $creds -split "\s+"
            $acrUser = $parts[0]
            $acrPassword = $parts[1]
            Write-Host "ACR login server: $acrLoginServer"
        }
    } catch {
        Write-Warning "Error getting ACR info: $_"
    }
}

if (-not $publicIp) { Write-Warning "VM public IP not available. You may need to check portal or run 'az vm list-ip-addresses' later." }

Write-Host "Running remote install of NVIDIA drivers (this may cause a reboot)."
$script1 = @(
    '#!/bin/bash -xe',
    'apt-get update',
    'DEBIAN_FRONTEND=noninteractive apt-get install -y build-essential linux-headers-$(uname -r) ubuntu-drivers-common curl gnupg lsb-release ca-certificates',
    'ubuntu-drivers autoinstall || true',
    '# If drivers installed require reboot, do it now',
    'if [ -f /var/run/reboot-required ] || [ $? -ne 0 ]; then',
    '  echo "Rebooting machine to complete driver install";',
    '  reboot',
    'fi'
)

try {
    # run first script; this may fail if a reboot occurs - we ignore non-zero and poll for readiness
    az vm run-command invoke --command-id RunShellScript --name $VmName --resource-group $ResourceGroupName --scripts $script1 -o json | Out-Null
} catch {
    Write-Host "Run-command invoked; if VM rebooted this is expected. Waiting for VM to become available..."
}

Write-Host "Waiting for VM agent to become available after reboot (if any). This may take 1-3 minutes..."
# wait until run-command succeeds
$waitScripts = @("echo waiting-for-agent")
try {
    Wait-ForRunCommandSuccess $ResourceGroupName $VmName $waitScripts 600 | Out-Null
} catch {
    Write-Warning "Timed out waiting for VM agent. Proceeding — you may need to run commands manually if the agent is not available."
}

Write-Host "Installing Docker and nvidia-docker2, then pulling and running container on the VM."
$dockerCommands = @(
    '#!/bin/bash -xe',
    'apt-get update',
    'apt-get install -y ca-certificates curl gnupg lsb-release',
    'mkdir -p /etc/apt/keyrings',
    'curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg',
    'echo ''deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable'' | tee /etc/apt/sources.list.d/docker.list > /dev/null',
    'apt-get update',
    'apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin',
    'systemctl enable --now docker || true',
    '# Install nvidia-docker2',
    'distribution=$(. /etc/os-release;echo $ID$VERSION_ID)',
    'curl -s -L https://nvidia.github.io/nvidia-docker/gpgkey | apt-key add - || true',
    'curl -s -L https://nvidia.github.io/nvidia-docker/$distribution/nvidia-docker.list | tee /etc/apt/sources.list.d/nvidia-docker.list',
    'apt-get update',
    'apt-get install -y nvidia-docker2',
    'systemctl restart docker || true',
    '# Test GPU via docker',
    'docker run --rm --gpus all nvidia/cuda:12.2.0-runtime-ubuntu22.04 nvidia-smi || true',
    '# If ACR credentials were provided, login, pull and run the caption service',
    ''
)

if ($acrLoginServer -and $acrUser -and $acrPassword) {
    $dockerCommands += @(
        "docker login $acrLoginServer -u $acrUser -p $acrPassword",
        "docker pull $acrLoginServer/caption-service:latest || true",
        "docker run -d --gpus all --name caption-service -e MODEL_NAME=llava-hf/llava-1.5-7b-hf -e CAPTION_MAX_LENGTH=280 -p 8000:8000 $acrLoginServer/caption-service:latest || true"
    )
} else {
    $dockerCommands += @("echo 'No ACR credentials were provided. Please docker login and pull your image manually.'")
}

try {
    Write-Host "Invoking remote install & docker run script (this may take several minutes)..."
    az vm run-command invoke --command-id RunShellScript --name $VmName --resource-group $ResourceGroupName --scripts $dockerCommands -o json | Out-Null
    Write-Host "Remote commands executed. If docker run succeeded you'll have a container named 'caption-service' mapped to port 8000."
} catch {
    Write-Warning "Remote run-command failed or timed out. Check VM's run command logs in portal or SSH into the VM to inspect the state. Error: $_"
}

Write-Host "Final VM IP: $publicIp"
if ($acrLoginServer) { Write-Host "ACR login server: $acrLoginServer (admin user temporarily enabled)." }

Write-Host "To verify the running container and GPU from your local machine (after SSH or using run-command):"
Write-Host " - SSH: ssh -i $env:USERPROFILE\.ssh\id_rsa $AdminUsername@$publicIp"
Write-Host ' - Inside VM: docker ps; docker exec -it caption-service python -c "import torch; print(torch.cuda.is_available()); print(torch.cuda.get_device_name(0) if torch.cuda.is_available() else None)"'

Write-Host "Cleanup reminder: disable ACR admin and deallocate VM when finished."
if ($acrLoginServer) { Write-Host "Disable ACR admin: az acr update -n $AcrName --admin-enabled false" }
Write-Host "Deallocate VM: az vm deallocate -g $ResourceGroupName -n $VmName"






