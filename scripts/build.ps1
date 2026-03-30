Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path $Path) {
        Remove-Item $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path $Path)) {
        throw "$Description not found at $Path."
    }
}

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
$publishRoot = Join-Path $repoRoot "publish"
$serverPublishDir = Join-Path $publishRoot "server"
$clientPublishDir = Join-Path $publishRoot "client"
Push-Location $repoRoot

try {
    Write-Host "`nResetting publish output..." -ForegroundColor Yellow
    Reset-Directory -Path $publishRoot

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
        "-r",
        "win-x64",
        "--self-contained",
        "false",
        "/p:PublishSingleFile=false",
        "/p:UseAppHost=true",
        "/p:DebugType=None",
        "/p:DebugSymbols=false",
        "-o",
        $serverPublishDir
    )

    Invoke-DotNetCommand -Description "Publishing Client" -Arguments @(
        "publish",
        ".\LanGameClient\LanGameClient.csproj",
        "-c",
        "Release",
        "-r",
        "win-x64",
        "--self-contained",
        "false",
        "/p:PublishSingleFile=false",
        "/p:UseAppHost=true",
        "/p:DebugType=None",
        "/p:DebugSymbols=false",
        "-o",
        $clientPublishDir
    )

    Assert-PathExists -Path (Join-Path $serverPublishDir "LanGameServer.exe") -Description "Published server executable"
    Assert-PathExists -Path (Join-Path $clientPublishDir "LanGameClient.exe") -Description "Published client executable"

    Write-Host "`nBuild completed!" -ForegroundColor Green
    Write-Host "Server executable: publish/server/LanGameServer.exe" -ForegroundColor Cyan
    Write-Host "Client executable: publish/client/LanGameClient.exe" -ForegroundColor Cyan
}
finally {
    Pop-Location
}
