Write-Host "Building LAN Game..." -ForegroundColor Green

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot

try {
    Write-Host "`nRestoring packages..." -ForegroundColor Yellow
    dotnet restore .\LanGame.sln

    Write-Host "`nBuilding solution..." -ForegroundColor Yellow
    dotnet build .\LanGame.sln -c Release /p:UseSharedCompilation=false -m:1

    Write-Host "`nPublishing Server..." -ForegroundColor Yellow
    dotnet publish .\LanGameServer\LanGameServer.csproj -c Release /p:UseAppHost=false -o .\publish\server

    Write-Host "`nPublishing Client..." -ForegroundColor Yellow
    dotnet publish .\LanGameClient\LanGameClient.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o .\publish\client

    Write-Host "`nBuild completed!" -ForegroundColor Green
    Write-Host "Server entrypoint: publish/server/LanGameServer.dll" -ForegroundColor Cyan
    Write-Host "Client executable: publish/client/LanGameClient.exe" -ForegroundColor Cyan
}
finally {
    Pop-Location
}
