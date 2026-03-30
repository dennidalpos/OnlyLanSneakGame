# Script operativi

## `build.ps1`
- Responsabilità: ripristino, build Release e publish degli eseguibili supportati.
- Quando usarlo: per verificare il repository localmente o produrre gli output distribuibili.
- Avvio:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
  ```
- Output attesi:
  - `publish/server/LanGameServer.dll`
  - `publish/client/LanGameClient.exe`
- Note operative: lo script ora fallisce immediatamente se un comando `dotnet` termina con exit code diverso da zero.

## `clean.ps1`
- Responsabilità: rimuovere artefatti generati da build, publish e test.
- Quando usarlo: prima di una verifica pulita o per ripristinare il repository a uno stato senza output locali.
- Avvio:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\clean.ps1
  ```
- Percorsi rimossi quando presenti:
  - `bin/`
  - `obj/`
  - `build/`
  - `dist/`
  - `out/`
  - `publish/`
  - `target/`
  - `tmp/`
  - `TestResults/`
  - `.vs/`
- File rimossi quando presenti:
  - `*.trx`
  - `*.coverage`
  - `*.coveragexml`
