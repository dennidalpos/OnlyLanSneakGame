# Product specification

[Retained Italian source](PROJECT_SPEC.it.md)

OnlyLanSneakGame is a .NET LAN game for up to four players collecting coins while avoiding a hostile snake and changing walls.

## Architecture

LanGameShared defines protocol rules/parsers and DTOs. LanGameServer owns the game loop, TCP connections and state broadcasting. LanGameClient is Windows Forms rendering/input/networking. See [architecture](docs/architecture.md).

## Behavior

The server defaults to TCP port 5000. Clients join with a nickname, receive an assigned player and consume state messages. Allowed nicknames contain letters, digits, spaces, hyphen and underscore, up to 16 characters.

A configured winning score ends the round. RESTART is accepted only after game over and resets scores, positions, walls, coins and snake. When the lobby becomes empty, the next session starts a fresh round. Spawn rules must avoid overlap with occupied entities.

## Constraints and operations

Shared/server target .NET 8; client/tests also require Windows-target support. global.json pins SDK 8.0.100 with latestMinor roll-forward, so an SDK 10-only host does not satisfy the canonical build prerequisite. Published Windows x64 outputs are framework-dependent.

The text protocol has delimiters; validate user fields before serialization. Connections are unauthenticated and unencrypted; use only a trusted isolated LAN. Source does not impose a general read-line size/deadline at the TCP reader, so this is not an internet-facing service.

Build/publish and clean scripts remove generated outputs. They require review of actual paths and separate authorization before deleting retained artifacts. Documentation verification uses isolated runtime copies and keeps all outputs; passing tests does not qualify a real multi-PC round.
