# PROJECT_SPEC

## Obiettivi

- Fornire un gioco multiplayer LAN client/server basato su .NET.
- Consentire fino a 4 giocatori contemporanei in un'arena condivisa.
- Far competere i giocatori nella raccolta di monete evitando serpente e muri dinamici.

## Architettura

- `LanGameShared/`: libreria cross-platform con contratti di protocollo, parser dei messaggi e modelli condivisi.
- `LanGameServer/`: server console con loop di gioco, gestione connessioni TCP e serializzazione dello stato.
- `LanGameClient/`: client Windows Forms con rendering arena, input locale e networking TCP.
- `docs/architecture.md`: panoramica sintetica dei componenti e del protocollo testuale tra client e server.

## Comportamento Atteso

- Il server ascolta di default sulla porta TCP `5000`.
- Il client invia `JOIN|nickname`, riceve assegnazione giocatore e aggiorna lo stato tramite messaggi `STATE|...`.
- I nickname ammessi contengono solo lettere, numeri, spazi, `-` e `_`, con lunghezza massima 16 caratteri.
- La partita termina quando un giocatore raggiunge il punteggio vittorioso configurato dal server.
- Dopo il game over un client puo' richiedere un nuovo round completo tramite `RESTART`; il round resetta punteggi, posizioni, muri, monete e serpente.
- Se la lobby torna vuota, il primo giocatore della sessione successiva deve entrare in un round fresco senza ereditarne stato residuo.
- Monete e muri dinamici non devono sovrapporsi a giocatori, serpente o pickup gia' presenti.

## Vincoli

- Target principale: .NET 8.
- Il server resta portabile come applicazione `net8.0` framework-dependent, eseguibile con runtime .NET 8 installato.
- Il client richiede Windows e Windows Forms (`net8.0-windows`).
- Il protocollo di gioco e' testuale e delimitato da caratteri speciali.
- Il protocollo dipende da separatori testuali, quindi i campi utente devono essere validati prima della serializzazione.
- Gli artefatti locali generati da build e test devono essere rimovibili tramite `scripts/clean.ps1`, che pulisce `bin/`, `obj/`, `build/`, `dist/`, `out/`, `publish/`, `target/`, `tmp/`, `TestResults/` e `.vs/`.
