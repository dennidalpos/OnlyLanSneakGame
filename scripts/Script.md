# Script operativi

## `build.ps1`
- Responsabilità: ripristino, build Release e publish framework-dependent di server e client.
- Quando usarlo: per verificare il repository localmente o produrre le due cartelle distribuibili `publish/server` e `publish/client`.
- Avvio:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
  ```
- Output attesi:
  - `publish/server/LanGameServer.exe`
  - `publish/client/LanGameClient.exe`
- Note operative:
  - lo script ripulisce `publish/` all'inizio per evitare file stantii negli output;
  - il publish usa `win-x64` framework-dependent con `exe` di avvio per entrambi i progetti;
  - lo script fallisce immediatamente se un comando `dotnet` termina con exit code diverso da zero.

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
