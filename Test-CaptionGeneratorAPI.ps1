param(
    [string]$BaseUrl = "http://localhost:5000/api/v1",
    [string]$Password = "Test1234!"
)

$ErrorActionPreference = "Stop"

# Force TLS 1.2 for older Windows PowerShell runtime.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Trust self-signed certs for local HTTPS (safe for local testing only).
add-type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem) {
        return true;
    }
}
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy

function ConvertFrom-JsonSafe {
    param([string]$Json)

    if ([string]::IsNullOrWhiteSpace($Json)) {
        return $null
    }

    try {
        # -Depth is not available in Windows PowerShell 5.1.
        if ($PSVersionTable.PSVersion.Major -ge 6) {
            return $Json | ConvertFrom-Json -Depth 20
        }

        return $Json | ConvertFrom-Json
    } catch {
        return $null
    }
}

function Get-ErrorBody {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    if (-not $ErrorRecord.Exception.Response) {
        return $null
    }

    try {
        $stream = $ErrorRecord.Exception.Response.GetResponseStream()
        if (-not $stream) { return $null }

        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
        $reader.Close()
        return $body
    } catch {
        return $null
    }
}

function Invoke-ApiRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [hashtable]$Headers,
        [object]$Body
    )

    $requestArgs = @{
        Method          = $Method
        Uri             = $Uri
        Headers         = $Headers
        UseBasicParsing = $true
    }

    if ($null -ne $Body) {
        $requestArgs["Body"] = ($Body | ConvertTo-Json -Depth 10)
        $requestArgs["ContentType"] = "application/json"
    }

    try {
        $response = Invoke-WebRequest @requestArgs
        $parsed = $null
        if ($response.Content) {
            $parsed = ConvertFrom-JsonSafe -Json $response.Content
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            RawBody    = $response.Content
            Body       = $parsed
        }
    } catch {
        $errorBody = Get-ErrorBody -ErrorRecord $_
        $statusCode = 0

        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        $parsedError = $null
        if ($errorBody) {
            $parsedError = ConvertFrom-JsonSafe -Json $errorBody
        }

        return [pscustomobject]@{
            StatusCode = $statusCode
            RawBody    = $errorBody
            Body       = $parsedError
        }
    }
}

function Assert-StatusCode {
    param(
        [string]$Step,
        [int]$Expected,
        [object]$Response
    )

    if ($Response.StatusCode -ne $Expected) {
        Write-Host "[FAIL] $Step - expected HTTP $Expected, got $($Response.StatusCode)" -ForegroundColor Red
        if ($Response.RawBody) {
            Write-Host "Response body:" -ForegroundColor Yellow
            Write-Host $Response.RawBody
        }

        exit 1
    }

    Write-Host "[PASS] $Step (HTTP $Expected)" -ForegroundColor Green
}

$email = "testuser$([guid]::NewGuid().ToString('N').Substring(0,8))@example.com"
$imageUrl = "https://cdn.example.com/powershell-$([guid]::NewGuid().ToString('N')).jpg"

Write-Host "Base URL: $BaseUrl"
Write-Host "Test user: $email"

# 1) Register
$registerResponse = Invoke-ApiRequest -Method "Post" -Uri "$BaseUrl/auth/register" -Body @{ Email = $email; Password = $Password }
Assert-StatusCode -Step "Register" -Expected 201 -Response $registerResponse

# 2) Login
$loginResponse = Invoke-ApiRequest -Method "Post" -Uri "$BaseUrl/auth/login" -Body @{ Email = $email; Password = $Password }
Assert-StatusCode -Step "Login" -Expected 200 -Response $loginResponse

$token = $null
if ($loginResponse.Body -and $loginResponse.Body.Data) {
    $token = [string]$loginResponse.Body.Data.Token
}

if ([string]::IsNullOrWhiteSpace($token) -and $loginResponse.RawBody) {
    $raw = ConvertFrom-JsonSafe -Json $loginResponse.RawBody
    if ($raw -and $raw.data) {
        $token = [string]$raw.data.token
    }
}

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "[FAIL] Login - token missing in response" -ForegroundColor Red
    Write-Host ($loginResponse.RawBody)
    exit 1
}
Write-Host "[PASS] Login token received" -ForegroundColor Green

$authHeader = @{ Authorization = "Bearer $token" }

# 3) Create post
$createResponse = Invoke-ApiRequest -Method "Post" -Uri "$BaseUrl/posts" -Headers $authHeader -Body @{ Platform = 1; ImageUrl = $imageUrl }
Assert-StatusCode -Step "Create post" -Expected 201 -Response $createResponse

$postId = [string]$createResponse.Body.Data
if ([string]::IsNullOrWhiteSpace($postId)) {
    Write-Host "[FAIL] Create post - post id missing" -ForegroundColor Red
    Write-Host ($createResponse.RawBody)
    exit 1
}
Write-Host "[PASS] Post created with id: $postId" -ForegroundColor Green

# 4a) Get all posts
$getAllResponse = Invoke-ApiRequest -Method "Get" -Uri "$BaseUrl/posts" -Headers $authHeader
Assert-StatusCode -Step "Get all posts" -Expected 200 -Response $getAllResponse

$foundInAll = $false
if ($getAllResponse.Body -and $getAllResponse.Body.Data) {
    foreach ($item in $getAllResponse.Body.Data) {
        if ([string]$item.Id -eq $postId) {
            $foundInAll = $true
            break
        }
    }
}

if (-not $foundInAll) {
    Write-Host "[FAIL] Get all posts - created post was not found in list" -ForegroundColor Red
    Write-Host ($getAllResponse.RawBody)
    exit 1
}
Write-Host "[PASS] Created post found in get-all response" -ForegroundColor Green

# 4b) Get post by id
$getByIdResponse = Invoke-ApiRequest -Method "Get" -Uri "$BaseUrl/posts/$postId" -Headers $authHeader
Assert-StatusCode -Step "Get post by id" -Expected 200 -Response $getByIdResponse

if ([string]$getByIdResponse.Body.Data.Id -ne $postId) {
    Write-Host "[FAIL] Get post by id - returned id does not match" -ForegroundColor Red
    Write-Host ($getByIdResponse.RawBody)
    exit 1
}
Write-Host "[PASS] Get by id returned expected post" -ForegroundColor Green

# 5) Delete post
$deleteResponse = Invoke-ApiRequest -Method "Delete" -Uri "$BaseUrl/posts/$postId" -Headers $authHeader
Assert-StatusCode -Step "Delete post" -Expected 200 -Response $deleteResponse

# 6) Verify deleted (expect 404)
$verifyDeletedResponse = Invoke-ApiRequest -Method "Get" -Uri "$BaseUrl/posts/$postId" -Headers $authHeader
Assert-StatusCode -Step "Verify deletion" -Expected 404 -Response $verifyDeletedResponse
Write-Host "[PASS] Post is deleted and no longer retrievable" -ForegroundColor Green

Write-Host "`nFlow completed successfully." -ForegroundColor Cyan
exit 0
