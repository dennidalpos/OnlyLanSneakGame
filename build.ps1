Write-Host "Building LAN Game..." -ForegroundColor Green

Write-Host "`nRestoring packages..." -ForegroundColor Yellow
dotnet restore

Write-Host "`nBuilding solution..." -ForegroundColor Yellow
dotnet build -c Release

Write-Host "`nPublishing Server..." -ForegroundColor Yellow
dotnet publish LanGameServer -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o publish/server

Write-Host "`nPublishing Client..." -ForegroundColor Yellow
dotnet publish LanGameClient -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o publish/client

Write-Host "`nBuild completed!" -ForegroundColor Green
Write-Host "Server executable: publish/server/LanGameServer.exe" -ForegroundColor Cyan
Write-Host "Client executable: publish/client/LanGameClient.exe" -ForegroundColor Cyan
