# Quick ngrok installer
$downloadUrl = "https://bin.equinox.io/c/bNyj1mQVY4c/ngrok-v3-stable-windows-amd64.zip"
$ngrokZip = "$env:TEMP\ngrok.zip"
$ngrokDir = "C:\ngrok"

Write-Host "Downloading ngrok..."
Invoke-WebRequest -Uri $downloadUrl -OutFile $ngrokZip

Write-Host "Extracting ngrok..."
New-Item -ItemType Directory -Path $ngrokDir -Force | Out-Null
Expand-Archive -Path $ngrokZip -DestinationPath $ngrokDir -Force

Write-Host "Adding to PATH..."
$env:Path += ";$ngrokDir"
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";$ngrokDir", [EnvironmentVariableTarget]::User)

Write-Host ""
Write-Host "Ngrok installed to: $ngrokDir" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Get free auth token: https://dashboard.ngrok.com/get-started/your-authtoken"
Write-Host "2. Run: C:\ngrok\ngrok.exe config add-authtoken YOUR_TOKEN"
Write-Host "3. Run: C:\ngrok\ngrok.exe http 8000"
Write-Host ""
Write-Host "Or restart PowerShell and run: ngrok http 8000"
