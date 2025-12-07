# Architettura

## Server
- **Gameplay/GameServer.cs**: ciclo principale, gestione connessioni e broadcast dello stato.
- **Gameplay/GameState.cs**: logica di round, spawn di muri/monete e vittoria.
- **Gameplay/Snake.cs**: intelligenza e movimento del serpente ostile.
- **Networking/ClientConnection.cs**: gestione protocolli TCP e input dei client.
- **Entities/Entities.cs**: modelli dati condivisi fra i componenti server.

## Client
- **MainForm.cs**: loop di rendering e input, visualizzazione dell'arena e HUD.
- **NetworkClient.cs**: connessione TCP al server, parsing dello stato e invio input.
- **Models/GameModels.cs**: modelli dati per lo stato ricevuto.

## Protocollo
`STATE|fase|giocatori|monete|serpente|muri`
- Giocatori: `P:id:x:y:score:nome:colore` separati da `;`.
- Monete: `C:x:y`
- Serpente: `S:x:y`
- Muri: `W:x:y:width:height`

`GAME_OVER|idVincitore|nome:score;...` inviato al termine della partita. Il client può mandare `RESTART` per azzerare il round.
