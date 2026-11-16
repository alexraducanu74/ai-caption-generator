<#
azure-auto-gpu-or-cpu.ps1

Try to provision a GPU VM in Azure quickly by attempting a list of common GPU SKUs across a list
of regions (spot first if requested). If none succeed (quota or availability), fall back to creating
a CPU VM so you have a working demo environment. After VM is created the script will attempt to
install Docker (and nvidia runtime on GPU VMs), then pull & run the caption-service container from
Docker Hub or ACR (ACR admin is temporarily enabled if necessary).

USAGE (example):
 .\azure-auto-gpu-or-cpu.ps1 -ResourceGroupName caption-demo-rg -VmNamePrefix caption-demo -AcrName captiongeneratoreqh42kmb646vq -DockerHubRepo yourdockerhubuser/caption-service:latest -UseSpot

Parameters:
 -ResourceGroupName (default caption-demo-rg)
 -VmNamePrefix (prefix for VM names)
 -Regions (optional comma separated regions to try; default: uksouth,westeurope,northeurope,francecentral)
 -Skus (optional comma separated SKUs to try; default list includes T4/NC and some ND/NC variants)
 -AcrName (optional; if provided the script will enable admin temporarily and docker login on the VM)
 -DockerHubRepo (optional; use this image if provided instead of ACR)
 -AdminUsername (default azureuser)
 -UseSpot (switch to try spot VMs first)

NOTES:
 - You must run this locally where Azure CLI is installed and you are signed in (az login).
 - The script will try multiple regions & SKUs; it does not request quota increases.
 - If using ACR the script enables admin temporarily; disable admin after the demo.
#>

param(
    [string]$ResourceGroupName = "caption-demo-rg",
    [string]$VmNamePrefix = "caption-demo",
    [string]$Regions = "uksouth,westeurope,northeurope,francecentral",
    [string]$Skus = "Standard_NC4as_T4_v3,Standard_NC6s_v3,Standard_NC6,Standard_NC12s_v3,Standard_ND40rs_v2,Standard_ND96asr_v4",
    [string]$AcrName = "",
    [string]$DockerHubRepo = "",
    [string]$AdminUsername = "azureuser",
    [switch]$UseSpot
)

function AbortIfNoAz() {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        Write-Error "Azure CLI (az) not found. Install it and sign in with 'az login'."
        exit 1
    }
}

function TryCreateVm($rg, $name, $sku, $location, $useSpot) {
    $args = @("--resource-group", $rg, "--name", $name, "--image", "Canonical:0001-com-ubuntu-server-jammy:22_04-lts:latest", "--size", $sku, "--location", $location, "--admin-username", $AdminUsername, "--generate-ssh-keys")
    if ($useSpot) { $args += @("--priority","Spot","--eviction-policy","Deallocate","--max-price","-1") }
    try {
        Write-Host "Attempting to create VM $name in $location (size $sku) ..."
        az vm create @args -o none
        return $true
    } catch {
        $m = $_.Exception.Message
        Write-Warning ("Create failed for {0} in {1} with sku {2}: {3}" -f $name, $location, $sku, $m)
        return $false
    }
}

function RunRemoteScript($rg, $vm, $scriptLines) {
    try {
        az vm run-command invoke --command-id RunShellScript --name $vm --resource-group $rg --scripts $scriptLines -o json
        return $true
    } catch {
        $err = $_
        Write-Warning ("run-command failed on {0}: {1}" -f $vm, $err)
        return $false
    }
}

AbortIfNoAz

$regionList = $Regions -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
$skuList = $Skus -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }

Write-Host "Ensuring resource group '$ResourceGroupName' exists..."
if (-not (az group exists --name $ResourceGroupName -o tsv)) { az group create --name $ResourceGroupName --location $regionList[0] | Out-Null }

$created = $false
$createdVmName = $null
$createdRegion = $null
$createdSku = $null

foreach ($region in $regionList) {
    foreach ($sku in $skuList) {
        $safeSku = $sku -replace '[^A-Za-z0-9]','-'
        $vmName = "${VmNamePrefix}-${region}-${safeSku}".ToLower()
        # try spot first if requested
        if ($UseSpot) {
            if (TryCreateVm $ResourceGroupName $vmName $sku $region $true) { $created = $true; $createdVmName=$vmName; $createdRegion=$region; $createdSku=$sku; break }
        }
        # try on-demand
        if (TryCreateVm $ResourceGroupName $vmName $sku $region $false) { $created = $true; $createdVmName=$vmName; $createdRegion=$region; $createdSku=$sku; break }
    }
    if ($created) { break }
}

if (-not $created) {
    Write-Warning "No GPU VM could be created across candidate regions/SKUs. Falling back to CPU VM for demo."
    $vmName = "${VmNamePrefix}-cpu-$(Get-Random -Maximum 1000)"
    $cpuSku = "Standard_D4s_v3"
    az vm create --resource-group $ResourceGroupName --name $vmName --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts:latest --size $cpuSku --admin-username $AdminUsername --generate-ssh-keys -o none
    $createdVmName = $vmName
    $createdRegion = $regionList[0]
    $createdSku = $cpuSku
    $isGpu = $false
} else {
    $isGpu = $true
}

Write-Host "VM created: $createdVmName in $createdRegion (sku: $createdSku). GPU: $isGpu"

Write-Host "Opening necessary ports..."
az vm open-port --resource-group $ResourceGroupName --name $createdVmName --port 22 --priority 1002 | Out-Null
az vm open-port --resource-group $ResourceGroupName --name $createdVmName --port 8000 --priority 1001 | Out-Null

$publicIp = az vm list-ip-addresses --resource-group $ResourceGroupName --name $createdVmName --query "[0].virtualMachine.network.publicIpAddresses[0].ipAddress" -o tsv
Write-Host "VM public IP: $publicIp"

Write-Host "Preparing remote install script..."
$installScript = @(
    '#!/bin/bash -xe',
    'apt-get update',
    'apt-get install -y ca-certificates curl gnupg lsb-release',
    'mkdir -p /etc/apt/keyrings',
    'curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg',
    'echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null',
    'apt-get update',
    'apt-get install -y docker-ce docker-ce-cli containerd.io',
    'systemctl enable --now docker || true',
    'usermod -aG docker $USER || true'
)

if ($isGpu) {
    $installScript += @(
        '# Install nvidia runtime (assumes drivers not preinstalled)',
        'distribution=$(. /etc/os-release;echo $ID$VERSION_ID)',
        'curl -s -L https://nvidia.github.io/nvidia-docker/gpgkey | apt-key add - || true',
        'curl -s -L https://nvidia.github.io/nvidia-docker/$distribution/nvidia-docker.list | tee /etc/apt/sources.list.d/nvidia-docker.list',
        'apt-get update',
        'apt-get install -y nvidia-docker2',
        'systemctl restart docker || true',
        'docker run --rm --gpus all nvidia/cuda:12.2.0-runtime-ubuntu22.04 nvidia-smi || true'
    )
}

# If DockerHubRepo provided, pull from it; otherwise use ACR if provided (enable admin temporarily)
if (-not [string]::IsNullOrWhiteSpace($DockerHubRepo)) {
    $installScript += @("docker pull $DockerHubRepo || true", "docker run -d --name caption-service -e MODEL_NAME=llava-hf/llava-1.5-7b-hf -e CAPTION_MAX_LENGTH=280 -p 8000:8000 $DockerHubRepo || true")
} elseif (-not [string]::IsNullOrWhiteSpace($AcrName)) {
    # enable admin locally and fetch creds
    Write-Host "Enabling ACR admin user (temporary) for $AcrName..."
    az acr update --name $AcrName --admin-enabled true | Out-Null
    $creds = az acr credential show --name $AcrName --query "{username:username,password:passwords[0].value}" -o tsv
    $parts = $creds -split "\s+"
    $acrUser = $parts[0]
    $acrPassword = $parts[1]
    $acrLogin = az acr show --name $AcrName --query loginServer -o tsv
    $installScript += @("docker login $acrLogin -u $acrUser -p $acrPassword", "docker pull $acrLogin/caption-service:latest || true", "docker run -d --name caption-service -e MODEL_NAME=llava-hf/llava-1.5-7b-hf -e CAPTION_MAX_LENGTH=280 -p 8000:8000 $acrLogin/caption-service:latest || true")
} else {
    $installScript += @("echo 'No DockerHubRepo or AcrName provided; please docker login and pull your image manually on the VM.'")
}

Write-Host "Running remote install & container start on VM (this may take several minutes)..."
RunRemoteScript $ResourceGroupName $createdVmName $installScript | Out-Null

Write-Host "Deployment complete. VM: $createdVmName ($createdRegion), IP: $publicIp"
Write-Host "To verify inside the VM: ssh -i $env:USERPROFILE\.ssh\id_rsa $AdminUsername@$publicIp"
Write-Host "To check container: docker ps; docker logs caption-service"
Write-Host 'To check PyTorch GPU inside container: docker exec -it caption-service python -c "import torch; print(torch.cuda.is_available()); print(torch.cuda.get_device_name(0) if torch.cuda.is_available() else None)"'

if (-not [string]::IsNullOrWhiteSpace($AcrName)) { Write-Host "Remember to disable ACR admin after the demo: az acr update -n $AcrName --admin-enabled false" }


