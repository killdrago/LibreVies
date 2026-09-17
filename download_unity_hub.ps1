param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$urls = @(
    "https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-x64.exe",
    "https://public-cdn.cloud.unity3d.com/hub/3.14.3/UnityHubSetup.exe",
    "https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup.exe"
)

foreach ($url in $urls) {
    try {
        Write-Host "Tentative : $url"
        if (Test-Path $OutputPath) {
            Remove-Item $OutputPath -Force -ErrorAction SilentlyContinue
        }
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $OutputPath
        if ((Test-Path $OutputPath) -and ((Get-Item $OutputPath).Length -gt 1000000)) {
            Write-Host "Unity Hub telecharge."
            exit 0
        }
    }
    catch {
        Write-Host "URL indisponible, essai suivant."
    }
}

if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Force -ErrorAction SilentlyContinue
}
Write-Error "Aucune URL officielle Unity Hub n'a fonctionne."
exit 1
