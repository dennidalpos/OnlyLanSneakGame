# OnlyLanSneakGame

Gioco multiplayer locale client/server basato su .NET che combina raccolta di monete con un serpente ostile e muri dinamici. Include un server console e un client Windows Forms.

## Setup
- Ripristinare le dipendenze della soluzione:
  ```powershell
  dotnet restore .\LanGame.sln
  ```

## Struttura
- **LanGameShared**: contratti di protocollo, parser e modelli condivisi tra client, server e test.
- **LanGameServer**: logica di gioco, gestione connessioni e broadcasting dello stato.
- **LanGameClient**: interfaccia grafica Windows Forms e networking del giocatore.
- **tests/**: test automatici xUnit su gameplay e protocollo.
- **scripts/**: script operativi di repository per build/publish e clean.
- **docs/**: note operative e di progettazione aggiuntive.

## Requisiti
- .NET 8 SDK
- I publish generati da `scripts/build.ps1` sono Windows x64 framework-dependent e richiedono il runtime .NET 8 sul PC di destinazione.
- Per il client è necessario un ambiente Windows per Windows Forms.

## Esecuzione
1. Avviare il server:
   ```bash
   dotnet run --project LanGameServer/LanGameServer.csproj
   ```
   Arresto server: `Invio` nella console oppure `Ctrl+C`.
2. Avviare il client su Windows:
   ```bash
   dotnet run --project LanGameClient/LanGameClient.csproj
   ```
3. Inserire nickname, IP del server e porta (default 5000) e connettersi.
4. I nickname supportano solo lettere, numeri, spazi, `-` e `_`, fino a 16 caratteri.
5. Controlli client:
   `WASD` o frecce per muoversi, `N` per richiedere un nuovo round dopo `GAME_OVER`, `F11` o `Alt+Enter` per fullscreen, `Esc` per uscire dal fullscreen.

## Build
- Script di build/publish:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
  ```
- Lo script esegue `restore`, `build -c Release` e genera solo due cartelle di output: `publish/server` e `publish/client`, entrambe Windows x64 framework-dependent con `exe` di avvio.

## Test
- Eseguire i test automatici:
  ```powershell
  dotnet test .\LanGame.sln
  ```
- I report locali possono finire in `TestResults/`, che è esclusa dal versionamento e ripulita da `clean.ps1`.

## Publish
- Per generare gli eseguibili distribuibili usare:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
  ```
- Output previsti:
  - `publish/server/LanGameServer.exe`
  - `publish/client/LanGameClient.exe`
- Le sole cartelle da distribuire sono `publish/server` per il server e `publish/client` per ciascun client.
- Avvio del server pubblicato:
  ```powershell
  .\publish\server\LanGameServer.exe
  ```

## Clean
- Script di clean:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\clean.ps1
  ```
- `clean.ps1` rimuove gli artefatti generati in `bin/`, `obj/`, `build/`, `dist/`, `out/`, `publish/`, `target/`, `tmp/`, `TestResults/`, `.vs/` e i file di coverage o report test (`*.trx`, `*.coverage`, `*.coveragexml`).

## Obiettivi di gioco
- Raccogliere monete fino a raggiungere il punteggio vittorioso.
- Evitare il serpente che insegue il giocatore col punteggio più alto entro il suo raggio di caccia e muri che cambiano forma.
- In caso di sconfitta, premere **N** sul client per avviare un nuovo round completo dopo il `GAME_OVER`.

## Difficoltà
- Serpente più rapido, più lungo e con raggio di inseguimento maggiore.
- Punteggio di vittoria aumentato e monete presenti in numero più limitato.
- Muri rigenerati frequentemente con dimensioni più ampie, che riducono gli spazi sicuri.

## Copyright
Copyright (c) 2026 Danny Perondi. All rights reserved.

Questo progetto è proprietario e riservato. La consultazione del repository è consentita
solo per visione e valutazione. Non è consentito riutilizzare, modificare, copiare,
ridistribuire o pubblicare il codice o materiali correlati senza preventiva autorizzazione
scritta di Danny Perondi.

Per i termini completi fare riferimento a `LICENSE`.
