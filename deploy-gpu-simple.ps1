param(
    [string]$ResourceGroup = "caption-demo-rg",
    [string]$VmName = "caption-gpu-vm",
    [string]$AcrName = "captiongeneratoreqh42kmb646vq"
)

$ErrorActionPreference = "Stop"

# GPU SKUs to try (from cheapest/smallest to most expensive)
$gpuSkus = @(
    "Standard_NC4as_T4_v3",     # T4 GPU
    "Standard_NC6s_v3",          # V100
    "Standard_NC8as_T4_v3",      # T4 GPU
    "Standard_NC6"               # K80 (older)
)

# Regions to try
$regions = @("westeurope", "northeurope", "eastus", "westus2", "southcentralus")

Write-Host "Checking if resource group exists..."
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "false") {
    Write-Host "Creating resource group..."
    az group create --name $ResourceGroup --location westeurope
}

$vmCreated = $false
$publicIp = $null

foreach ($region in $regions) {
    foreach ($sku in $gpuSkus) {
        Write-Host "`nTrying to create GPU VM in $region with SKU $sku..."

        try {
            az vm create `
                --resource-group $ResourceGroup `
                --name $VmName `
                --image "Canonical:0001-com-ubuntu-server-jammy:22_04-lts:latest" `
                --size $sku `
                --location $region `
                --admin-username azureuser `
                --generate-ssh-keys `
                --public-ip-sku Standard `
                --output none

            $vmCreated = $true
            Write-Host "Successfully created VM in $region with $sku!"
            break
        }
        catch {
            Write-Warning "Failed to create VM with $sku in ${region}: $($_.Exception.Message)"
            continue
        }
    }

    if ($vmCreated) { break }
}

if (-not $vmCreated) {
    Write-Error "Could not create GPU VM in any region. You may need to request GPU quota."
    Write-Host "Request quota at: https://portal.azure.com/#view/Microsoft_Azure_Capacity/QuotaMenuBlade/~/myQuotas"
    exit 1
}

Write-Host "`nOpening ports for SSH (22) and API (8000)..."
az vm open-port --resource-group $ResourceGroup --name $VmName --port 22 --priority 1001
az vm open-port --resource-group $ResourceGroup --name $VmName --port 8000 --priority 1002

Write-Host "`nGetting VM public IP..."
$publicIp = az vm list-ip-addresses --resource-group $ResourceGroup --name $VmName --query "[0].virtualMachine.network.publicIpAddresses[0].ipAddress" -o tsv
Write-Host "VM Public IP: $publicIp"

Write-Host "`nEnabling ACR admin access..."
az acr update --name $AcrName --admin-enabled true

Write-Host "`nGetting ACR credentials..."
$acrCreds = az acr credential show --name $AcrName -o json | ConvertFrom-Json
$acrServer = az acr show --name $AcrName --query loginServer -o tsv
$acrUser = $acrCreds.username
$acrPass = $acrCreds.passwords[0].value

Write-Host "`nInstalling NVIDIA drivers and Docker on VM..."
$installScript = @"
#!/bin/bash
set -e

# Update system
echo "Updating system..."
sudo apt-get update
sudo DEBIAN_FRONTEND=noninteractive apt-get upgrade -y

# Install NVIDIA drivers
echo "Installing NVIDIA drivers..."
sudo apt-get install -y ubuntu-drivers-common
sudo ubuntu-drivers autoinstall

# Install Docker
echo "Installing Docker..."
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker azureuser

# Install NVIDIA Container Toolkit
echo "Installing NVIDIA Container Toolkit..."
distribution=\$(. /etc/os-release;echo \$ID\$VERSION_ID)
curl -s -L https://nvidia.github.io/libnvidia-container/gpgkey | sudo apt-key add -
curl -s -L https://nvidia.github.io/libnvidia-container/\$distribution/libnvidia-container.list | \
  sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

sudo apt-get update
sudo apt-get install -y nvidia-container-toolkit
sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker

# Login to ACR and pull image
echo "Logging into ACR..."
echo "$acrPass" | docker login $acrServer --username $acrUser --password-stdin

echo "Pulling caption-service image..."
docker pull $acrServer/caption-service:latest

# Run the container with GPU
echo "Starting caption-service container with GPU..."
docker run -d \
  --name caption-service \
  --gpus all \
  --restart unless-stopped \
  -p 8000:8000 \
  -e MODEL_NAME=llava-hf/llava-1.5-7b-hf \
  -e CAPTION_MAX_LENGTH=280 \
  $acrServer/caption-service:latest

echo "Deployment complete!"
"@

# Write script to temp file
$scriptPath = [System.IO.Path]::GetTempFileName() + ".sh"
$installScript | Out-File -FilePath $scriptPath -Encoding utf8

Write-Host "`nRunning installation script on VM (this will take 5-10 minutes)..."
az vm run-command invoke `
    --resource-group $ResourceGroup `
    --name $VmName `
    --command-id RunShellScript `
    --scripts "@$scriptPath"

Remove-Item $scriptPath

Write-Host "`n========================================="
Write-Host "DEPLOYMENT SUCCESSFUL!"
Write-Host "========================================="
Write-Host "VM Name: $VmName"
Write-Host "Public IP: $publicIp"
Write-Host "Caption Service URL: http://${publicIp}:8000"
Write-Host "Health Check: http://${publicIp}:8000/health"
Write-Host ""
Write-Host "SSH into VM: ssh azureuser@$publicIp"
Write-Host "Check GPU: ssh azureuser@$publicIp 'docker exec caption-service nvidia-smi'"
Write-Host ""
Write-Host "IMPORTANT: Disable ACR admin when done:"
Write-Host "  az acr update --name $AcrName --admin-enabled false"
