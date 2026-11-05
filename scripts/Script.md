# Operational scripts

[Retained Italian source](Script.it.md)

## build.ps1

The script restores, builds Release and publishes server/client as win-x64 framework-dependent applications. Expected destinations are publish/server/LanGameServer.exe and publish/client/LanGameClient.exe.

It clears publish at startup to avoid stale outputs and fails on nonzero dotnet exit. Review/authorize that removal before using it on an existing checkout; the command is not a non-destructive verification. Distribute each entire output folder, with the required .NET runtime on the target.

## clean.ps1

The cleanup implementation removes generated directories including bin, obj, build, dist, out, publish, target, tmp, TestResults and .vs, plus test report/coverage files. A folder name alone does not prove its contents are disposable. Do not run this script merely to obtain a clean status or discard evidence.

## Safe verification

Use a fresh initialized central-runtime fixture and the repository's pinned SDK:

~~~powershell
dotnet test .\LanGame.sln -c Release
~~~

Keep logs and outputs after success/failure. An SDK prerequisite failure is an environment result, not a game-test failure. Do not relax global.json or change code to make a documentation check pass.

After a separately authorized publish, inspect output hashes and runtime requirements. Local build/test results do not prove installation, firewall access or multi-machine gameplay.
