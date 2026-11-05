# Architecture and text protocol

[Retained Italian source](architecture.it.md)

## Components

| Component | Responsibility |
| --- | --- |
| LanGameShared/Protocol/ProtocolRules.cs | Nickname validation/input normalization |
| LanGameShared/Protocol/ProtocolMessageParser.cs | STATE and GAME_OVER parsing |
| LanGameShared/Models/GameModels.cs | Shared DTOs |
| Server Gameplay/GameServer.cs | Main loop, connections and broadcasting |
| Server Gameplay/GameState.cs | Rounds, wall/coin spawning and victory |
| Server Gameplay/Snake.cs | Snake movement/chasing |
| Server Networking/ClientConnection.cs | TCP JOIN handshake and input |
| Client MainForm.cs | Arena/HUD rendering and local input |
| Client NetworkClient.cs | TCP communication using shared parsing |

## Messages

JOIN|nickname uses letters/digits/spaces/underscore/hyphen, up to 16 characters. Initial replies are JOIN_OK|id|color, JOIN_FULL or JOIN_INVALID|reason.

STATE|phase|players|coins|snake|walls encodes players as P:id:x:y:score:name:color separated by semicolons, coins as C:x:y, snake points as S:x:y and walls as W:x:y:width:height.

GAME_OVER|winnerId|name:score;... is sent at the end and drives the client's ranking. RESTART is accepted only during game over; it resets the full round rather than merely changing one player's score.

## Boundaries

This is an unauthenticated trusted-LAN protocol, not a secure internet service. Nickname field validation protects delimiters, but does not provide authentication, transport confidentiality or an overall TCP line-length/deadline limit. Source inspection of these boundaries is not a completed hostile-network test.

Use repository tests for protocol/gameplay regressions; native rendering/input and actual multi-PC behavior need separate observations.
