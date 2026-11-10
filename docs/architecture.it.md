# Architettura

## Shared
- **LanGameShared/Protocol/ProtocolRules.cs**: validazione nickname e normalizzazione input condivise.
- **LanGameShared/Protocol/ProtocolMessageParser.cs**: parsing condiviso dei messaggi `STATE` e `GAME_OVER`.
- **LanGameShared/Models/GameModels.cs**: modelli DTO condivisi tra client, server e test.

## Server
- **Gameplay/GameServer.cs**: ciclo principale, gestione connessioni e broadcast dello stato.
- **Gameplay/GameState.cs**: logica di round, spawn di muri/monete e vittoria.
- **Gameplay/Snake.cs**: intelligenza e movimento del serpente ostile.
- **Networking/ClientConnection.cs**: gestione connessioni TCP, handshake `JOIN` e input dei client.
- **Entities/Entities.cs**: modelli dati condivisi fra i componenti server.

## Client
- **MainForm.cs**: loop di rendering e input, visualizzazione dell'arena e HUD.
- **NetworkClient.cs**: connessione TCP al server, uso del parser condiviso e invio input.

## Protocollo
`JOIN|nickname`
- Nickname consentiti: lettere, numeri, spazi, `_` e `-`, massimo 16 caratteri.
- Risposte iniziali: `JOIN_OK|id|colore`, `JOIN_FULL`, `JOIN_INVALID|motivo`.

`STATE|fase|giocatori|monete|serpente|muri`
- Giocatori: `P:id:x:y:score:nome:colore` separati da `;`.
- Monete: `C:x:y`
- Serpente: `S:x:y`
- Muri: `W:x:y:width:height`

`GAME_OVER|idVincitore|nome:score;...` viene inviato una volta al termine della partita e il client lo usa per il ranking finale mostrato a schermo.

`RESTART` viene accettato solo durante `GAME_OVER` e resetta l'intero round: punteggi, posizioni giocatori, serpente, muri e monete.
