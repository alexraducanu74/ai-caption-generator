<#
azure-deploy-gpu-vm.ps1

Create a GPU VM in Azure for short demo use and provide the exact commands to run on the VM
to install NVIDIA drivers, Docker, nvidia-docker2 and to pull & run your caption-service image
from Azure Container Registry (ACR).

This script is intentionally non-destructive: it prompts before creating resources and will
only enable ACR admin user to make pulling the image straightforward for a short demo.
Disable the admin user after the demo.

Usage (example):
.\azure-deploy-gpu-vm.ps1 -ResourceGroupName caption-demo-rg -VmName caption-gpu-vm -AcrName captiongeneratoreqh42kmb646vq

Parameters:
 -ResourceGroupName (default: caption-demo-rg)
 -VmName (default: caption-gpu-vm)
 -Location (default: westeurope)
 -VmSize (interactive if omitted)
 -AcrName (required to produce docker pull/login instructions)
 -AdminUsername (default: azureuser)
 -UseSpot (switch to create a spot VM for cheaper testing)

Notes / warnings:
 - You are responsible for costs; stop/delete the VM after your demo to avoid charges.
 - This script enables ACR admin user temporarily to make docker pull on the VM easier.
   After the demo, run `az acr update -n <acr> --admin-enabled false` to disable admin user.
#>

param(
    [string]$ResourceGroupName = "caption-demo-rg",
    [string]$VmName = "caption-gpu-vm",
    [string]$Location = "uksouth",
    [string]$VmSize = "",
    [string]$AcrName = "",
    [string]$AdminUsername = "azureuser",
    [switch]$UseSpot
)

function AbortIfNoAz() {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        Write-Error "Azure CLI (az) not found. Install it from https://aka.ms/installazurecliwindows and sign in with 'az login' before running this script."
        exit 1
    }
}

AbortIfNoAz

Write-Host "This script will create an Ubuntu VM and show the commands to install NVIDIA drivers, Docker, and nvidia-docker2."
Write-Host "It will also print the exact commands you should run over SSH on the VM to pull and run your container with GPU access."
Write-Host "Press Ctrl+C to cancel now or Enter to continue..."
Read-Host | Out-Null

if ([string]::IsNullOrWhiteSpace($VmSize)) {
    Write-Host "Listing GPU-capable VM SKUs in region $Location (this may take a few seconds)..."
    & az vm list-skus --location $Location --size * --query "[?contains(name,'NC')||contains(name,'ND')||contains(name,'NV')||contains(name,'NCas')||contains(name,'NDv3')||contains(name,'NCv3')].{name:name,family:family}" -o table
    $VmSize = Read-Host "Enter a VM size from the list above (example: Standard_NC6s_v3 or Standard_NC4as_T4_v3). If unsure, enter Standard_NC4as_T4_v3 (T4 / 16GB class)"
    if ([string]::IsNullOrWhiteSpace($VmSize)) {
        Write-Error "No VM size provided. Exiting."
        exit 1
    }
}

if ([string]::IsNullOrWhiteSpace($AcrName)) {
    Write-Host "ACR name not provided. You can still create the VM, but you'll need to push the image to a registry accessible from the VM (or provide credentials)."
    $proceed = Read-Host "Continue without ACR name? (y/N)"
    if ($proceed -ne 'y' -and $proceed -ne 'Y') { Write-Host "Aborted."; exit 1 }
}

Write-Host "Creating resource group '$ResourceGroupName' in '$Location' (if it doesn't exist)..."
& az group create --name $ResourceGroupName --location $Location | Out-Null

$createCmd = "az vm create --resource-group $ResourceGroupName --name $VmName --image UbuntuLTS --admin-username $AdminUsername --generate-ssh-keys --no-wait"
if (-not [string]::IsNullOrWhiteSpace($VmSize)) { $createCmd += " --size $VmSize" }
if ($UseSpot.IsPresent) { $createCmd += " --priority Spot --eviction-policy Deallocate --max-price -1" }

Write-Host "About to run: $createCmd"
$accept = Read-Host "Run the VM create command now? (y/N)"
if ($accept -ne 'y' -and $accept -ne 'Y') { Write-Host "Aborted by user."; exit 1 }

Write-Host "Creating VM (this may take several minutes)..."
if ($UseSpot.IsPresent) {
    & az vm create --resource-group $ResourceGroupName --name $VmName --image UbuntuLTS --admin-username $AdminUsername --generate-ssh-keys --size $VmSize --priority Spot --eviction-policy Deallocate --max-price -1
} else {
    & az vm create --resource-group $ResourceGroupName --name $VmName --image UbuntuLTS --admin-username $AdminUsername --generate-ssh-keys --size $VmSize
}

Write-Host "Opening port 8000 (app) and 22 (ssh) in the VM's NSG..."
& az vm open-port --resource-group $ResourceGroupName --name $VmName --port 8000 --priority 1001 | Out-Null

Write-Host "Fetching VM public IP..."
$publicIp = & az vm list-ip-addresses --resource-group $ResourceGroupName --name $VmName --query "[0].virtualMachine.network.publicIpAddresses[0].ipAddress" -o tsv
if ([string]::IsNullOrWhiteSpace($publicIp)) { Write-Warning "Could not determine public IP. The VM may not have a public IP or provisioning is still in progress." }
else { Write-Host "VM public IP: $publicIp" }

if (-not [string]::IsNullOrWhiteSpace($AcrName)) {
    Write-Host "Looking up ACR '$AcrName'..."
    try {
        $acrLoginServer = & az acr show --name $AcrName --query loginServer -o tsv 2>&1
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($acrLoginServer)) {
            Write-Warning "Could not find ACR with name '$AcrName' in the current subscription. Skipping ACR steps."
            $acrLoginServer = $null
        }
    } catch {
        Write-Warning "Error while checking ACR: $_"
        $acrLoginServer = $null
    }

    if ($acrLoginServer) {
        Write-Host "Enabling admin user on ACR '$AcrName' so you can docker login from the VM (temporary)."
        & az acr update --name $AcrName --admin-enabled true | Out-Null
        $creds = & az acr credential show --name $AcrName --query "{username:username,password:passwords[0].value}" -o tsv
        $parts = $creds -split "\s+"
        $acrUser = $parts[0]
        $acrPassword = $parts[1]

        Write-Host "ACR login server: $acrLoginServer"
        Write-Host "ACR admin username: $acrUser"
        Write-Host "(The admin password is shown below; keep it secret and disable admin after your demo.)"
        Write-Host $acrPassword
    }
}

Write-Host ""
Write-Host "==== SSH into the VM and run these commands (copy & paste after connecting) ===="
if (-not [string]::IsNullOrWhiteSpace($publicIp)) {
    $sshKeyPath = Join-Path $HOME ".ssh\id_rsa"
    Write-Host "SSH (Windows PowerShell): ssh -i $sshKeyPath $AdminUsername@$publicIp"
    Write-Host "If you used a different key path, substitute the correct path."
}

Write-Host "On the VM, run the following (these commands install NVIDIA drivers, Docker and nvidia-docker2):"

Write-Host "----- BEGIN ON-VM COMMANDS -----"
Write-Host "sudo apt-get update && sudo apt-get install -y build-essential linux-headers-\$(uname -r) ubuntu-drivers-common"
Write-Host "sudo ubuntu-drivers autoinstall  # installs recommended NVIDIA driver (reboot may be required)"
Write-Host "# Install Docker CE"
Write-Host "sudo apt-get install -y ca-certificates curl gnupg lsb-release"
Write-Host "sudo mkdir -p /etc/apt/keyrings"
Write-Host "curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg"
Write-Host "echo \"deb [arch=\$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \\$(lsb_release -cs) stable\" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null"
Write-Host "sudo apt-get update && sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin"
Write-Host "sudo systemctl enable --now docker"
Write-Host "# Install nvidia-docker2 (nvidia container runtime)"
Write-Host "distribution=\$(. /etc/os-release;echo \$ID\$VERSION_ID)"
Write-Host "curl -s -L https://nvidia.github.io/nvidia-docker/gpgkey | sudo apt-key add -"
Write-Host "curl -s -L https://nvidia.github.io/nvidia-docker/\$distribution/nvidia-docker.list | sudo tee /etc/apt/sources.list.d/nvidia-docker.list"
Write-Host "sudo apt-get update && sudo apt-get install -y nvidia-docker2"
Write-Host "sudo systemctl restart docker"
Write-Host "# Quick GPU test"
Write-Host "sudo docker run --rm --gpus all nvidia/cuda:12.2.0-runtime-ubuntu22.04 nvidia-smi"

if (-not [string]::IsNullOrWhiteSpace($AcrName) -and $acrLoginServer) {
    Write-Host "# Login to ACR (use the values shown earlier). Copy & paste the next line and replace <password> with the password printed above:"
    Write-Host "docker login $acrLoginServer -u $acrUser -p <ACR_PASSWORD>"
    Write-Host "# Pull and run your caption-service container (replace tag if needed):"
    Write-Host "docker pull $acrLoginServer/caption-service:latest"
    Write-Host "docker run -d --gpus all --name caption-service -e MODEL_NAME=llava-hf/llava-1.5-7b-hf -e CAPTION_MAX_LENGTH=280 -p 8000:8000 $acrLoginServer/caption-service:latest"
}

Write-Host "----- END ON-VM COMMANDS -----"

Write-Host ""
Write-Host "After the demo:"
Write-Host " - Stop the VM when not needed: az vm stop -g $ResourceGroupName -n $VmName"
Write-Host " - Deallocate (avoid charges): az vm deallocate -g $ResourceGroupName -n $VmName"
if (-not [string]::IsNullOrWhiteSpace($AcrName) -and $acrLoginServer) {
    Write-Host " - Disable ACR admin user to re-secure the registry: az acr update -n $AcrName --admin-enabled false"
}

Write-Host "Script finished. If you need me to also run the on-VM commands automatically, tell me and I will provide the alternative (requires installing Azure CLI or using run-command)."


