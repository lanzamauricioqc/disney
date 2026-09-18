param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("start", "stop")]
    [string]$Operation,

    [Parameter(Mandatory = $true)]
    [string]$ResourceId
)

$ErrorActionPreference = "Stop"
$managementEndpoint = "https://management.azure.com"
$apiVersion = "2025-08-01"
$tokenUri = "$($env:IDENTITY_ENDPOINT)?resource=$([uri]::EscapeDataString("$managementEndpoint/"))&api-version=2019-08-01"
$tokenHeaders = @{
    "X-IDENTITY-HEADER" = $env:IDENTITY_HEADER
    Metadata            = "True"
}

$tokenResponse = Invoke-RestMethod `
    -Method Get `
    -Uri $tokenUri `
    -Headers $tokenHeaders

$authorizationHeaders = @{
    Authorization = "Bearer $($tokenResponse.access_token)"
}

$serverUri = "$managementEndpoint${ResourceId}?api-version=$apiVersion"
$server = Invoke-RestMethod `
    -Method Get `
    -Uri $serverUri `
    -Headers $authorizationHeaders

if ($Operation -eq "start" -and $server.properties.state -eq "Ready") {
    Write-Output "PostgreSQL is already ready."
    return
}

if ($Operation -eq "stop" -and $server.properties.state -eq "Stopped") {
    Write-Output "PostgreSQL is already stopped."
    return
}

$operationUri = "$managementEndpoint${ResourceId}/${Operation}?api-version=$apiVersion"
$response = Invoke-WebRequest `
    -Method Post `
    -Uri $operationUri `
    -Headers $authorizationHeaders

if ($response.StatusCode -notin 200, 202) {
    throw "PostgreSQL $Operation request failed with HTTP status $($response.StatusCode)."
}

Write-Output "PostgreSQL $Operation request accepted with HTTP status $($response.StatusCode)."
