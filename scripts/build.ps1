Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-DotNetCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host "`n$Description..." -ForegroundColor Yellow
    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Building LAN Game..." -ForegroundColor Green

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot

try {
    Invoke-DotNetCommand -Description "Restoring packages" -Arguments @("restore", ".\LanGame.sln")

    Invoke-DotNetCommand -Description "Building solution" -Arguments @(
        "build",
        ".\LanGame.sln",
        "-c",
        "Release",
        "/p:UseSharedCompilation=false",
        "-m:1"
    )

    Invoke-DotNetCommand -Description "Publishing Server" -Arguments @(
        "publish",
        ".\LanGameServer\LanGameServer.csproj",
        "-c",
        "Release",
        "/p:UseAppHost=false",
        "-o",
        ".\publish\server"
    )

    Invoke-DotNetCommand -Description "Publishing Client" -Arguments @(
        "publish",
        ".\LanGameClient\LanGameClient.csproj",
        "-c",
        "Release",
        "-r",
        "win-x64",
        "--self-contained",
        "true",
        "/p:PublishSingleFile=true",
        "-o",
        ".\publish\client"
    )

    Write-Host "`nBuild completed!" -ForegroundColor Green
    Write-Host "Server entrypoint: publish/server/LanGameServer.dll" -ForegroundColor Cyan
    Write-Host "Client executable: publish/client/LanGameClient.exe" -ForegroundColor Cyan
}
finally {
    Pop-Location
}
