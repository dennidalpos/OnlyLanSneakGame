# OnlyLanSneakGame

Local multiplayer client/server game built on .NET: collect coins while avoiding a hostile snake and dynamic walls. It includes a console server and a Windows Forms client.

## Gameplay

- Collect coins until you reach the winning score.
- Avoid the snake, which chases the highest-scoring player within its hunting radius, and walls that change shape.
- After a defeat, press **N** on the client to start a complete new round after `GAME_OVER`.
- Difficulty: a faster, longer snake with a larger chase radius; a higher winning score with fewer coins; frequently regenerated, larger walls that reduce safe spaces.

## Requirements

- .NET 8 SDK.
- Windows for the client (Windows Forms).
- Published builds are Windows x64 framework-dependent and need the .NET 8 runtime on the target PC.

## Run

```powershell
dotnet restore .\LanGame.sln

# Server (stop with Enter in the console or Ctrl+C)
dotnet run --project LanGameServer/LanGameServer.csproj

# Client (Windows)
dotnet run --project LanGameClient/LanGameClient.csproj
```

In the client enter a nickname, the server IP and the port (default 5000), then connect. Nicknames accept letters, digits, spaces, `-` and `_`, up to 16 characters.

Client controls: `WASD` or arrow keys to move, `N` for a new round after `GAME_OVER`, `F11` or `Alt+Enter` for fullscreen, `Esc` to leave fullscreen.

## Build and publish

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

The script runs `restore` and `build -c Release`, then produces two output folders: `publish/server/LanGameServer.exe` and `publish/client/LanGameClient.exe`. Distribute `publish/server` for the server and `publish/client` for each client.

## Test and clean

```powershell
dotnet test .\LanGame.sln
powershell -ExecutionPolicy Bypass -File .\scripts\clean.ps1
```

`clean.ps1` removes generated artifacts (`bin/`, `obj/`, `publish/`, `TestResults/`, `.vs/`, coverage and test report files, among others).

## Project structure

| Path | Content |
| --- | --- |
| `LanGameShared` | Protocol contracts, parser and models shared by client, server and tests |
| `LanGameServer` | Game logic, connection handling and state broadcasting |
| `LanGameClient` | Windows Forms UI and player networking |
| `tests/` | xUnit tests for gameplay and protocol |
| `scripts/` | Build/publish and clean scripts |
| `docs/` | Additional operational and design notes |

## Security

The server accepts unauthenticated connections and is intended for trusted local networks only; do not expose it to the internet. See [SECURITY.md](SECURITY.md) to report a vulnerability.

## License

Copyright (c) 2026 Danny Perondi. All rights reserved. This project is proprietary: the repository may be viewed for evaluation only, and reuse, modification, copying, redistribution or publication of the code or related materials requires prior written permission. See [LICENSE](LICENSE).
