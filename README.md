# OnlyLanSneakGame

Gioco multiplayer locale client/server basato su .NET che combina raccolta di monete con un serpente ostile e muri dinamici. Include un server console e un client Windows Forms.

## Struttura
- **LanGameServer**: logica di gioco, gestione connessioni e broadcasting dello stato.
- **LanGameClient**: interfaccia grafica Windows Forms e networking del giocatore.
- **docs/**: note operative e di progettazione aggiuntive.

## Requisiti
- .NET 8 SDK
- Per il client è necessario un ambiente Windows per Windows Forms.

## Esecuzione
1. Avviare il server:
   ```bash
   dotnet run --project LanGameServer/LanGameServer.csproj
   ```
2. Avviare il client su Windows:
   ```bash
   dotnet run --project LanGameClient/LanGameClient.csproj
   ```
3. Inserire nickname, IP del server e porta (default 5000) e connettersi.

## Obiettivi di gioco
- Raccogliere monete fino a raggiungere il punteggio vittorioso.
- Evitare il serpente che insegue i giocatori in base al punteggio e muri che cambiano forma.
- In caso di sconfitta, premere **N** sul client per richiedere un nuovo round.

## Difficoltà
- Serpente più rapido, più lungo e con raggio di inseguimento maggiore.
- Punteggio di vittoria aumentato e monete presenti in numero più limitato.
- Muri rigenerati frequentemente con dimensioni più ampie, che riducono gli spazi sicuri.
