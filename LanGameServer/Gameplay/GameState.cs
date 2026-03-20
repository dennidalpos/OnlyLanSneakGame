/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

using LanGameServer.Entities;
using LanGameServer.Networking;

namespace LanGameServer.Gameplay;

public class GameState
{
    public const int ArenaWidth = 1240;
    public const int ArenaHeight = 660;
    public const int PlayerSize = 30;

    private static readonly (int X, int Y)[] DefaultSpawnPositions =
    {
        (200, 200),
        (1040, 200),
        (200, 460),
        (1040, 460),
    };

    private readonly Random random = new();
    private DateTime lastCoinSpawn = DateTime.Now;
    private DateTime lastWallToggle = DateTime.Now;

    private const int CoinSize = 20;
    private const int MoveSpeed = 5;
    private const int MaxCoins = 6;
    private const int WinScore = 15;

    public string Phase { get; private set; } = "PLAYING";
    public List<Coin> Coins { get; } = new();
    public Snake Snake { get; private set; }
    public List<Wall> Walls { get; } = new();
    public bool CanRestartRound => Phase == "GAME_OVER";

    public GameState()
    {
        Snake = new Snake(ArenaWidth / 2, ArenaHeight / 2);
        InitializeArena([]);
    }

    public void Update(List<ClientConnection> clients)
    {
        if (Phase == "GAME_OVER")
            return;

        var players = clients.Select(client => client.Player).ToList();
        if (players.Count == 0)
            return;

        Snake.Update(ArenaWidth, ArenaHeight, Walls, players);

        foreach (var client in clients)
        {
            var input = client.InputState;
            var player = client.Player;

            var dx = 0;
            var dy = 0;

            if (input.Contains('U'))
                dy -= MoveSpeed;
            if (input.Contains('D'))
                dy += MoveSpeed;
            if (input.Contains('L'))
                dx -= MoveSpeed;
            if (input.Contains('R'))
                dx += MoveSpeed;

            var newX = Math.Clamp(player.X + dx, 0, ArenaWidth - PlayerSize);
            var newY = Math.Clamp(player.Y + dy, 0, ArenaHeight - PlayerSize);

            if (Walls.All(wall => !RectanglesOverlap(newX, newY, PlayerSize, PlayerSize, wall.X, wall.Y, wall.Width, wall.Height)))
            {
                player.X = newX;
                player.Y = newY;
            }
        }

        var snakeHitAny = false;

        foreach (var client in clients)
        {
            var player = client.Player;

            foreach (var segment in Snake.Segments)
            {
                if (IsColliding(player.X, player.Y, PlayerSize, segment.X, segment.Y, Snake.SegmentSize))
                {
                    player.Score = Math.Max(0, player.Score - 1);
                    PlacePlayerAtSpawn(player, players.Where(other => !ReferenceEquals(other, player)));
                    snakeHitAny = true;
                    break;
                }
            }

            for (int i = Coins.Count - 1; i >= 0; i--)
            {
                var coin = Coins[i];
                if (IsColliding(player.X, player.Y, PlayerSize, coin.X, coin.Y, CoinSize))
                {
                    player.Score++;
                    Coins.RemoveAt(i);

                    if (player.Score >= WinScore)
                    {
                        Phase = "GAME_OVER";
                        return;
                    }
                }
            }
        }

        if (snakeHitAny)
        {
            Snake = CreateSafeSnake(players);
        }

        if ((DateTime.Now - lastCoinSpawn).TotalSeconds >= 4 && Coins.Count < MaxCoins)
        {
            SpawnCoin(players);
            lastCoinSpawn = DateTime.Now;
        }

        if ((DateTime.Now - lastWallToggle).TotalSeconds >= 4)
        {
            RebuildWalls(players);
            lastWallToggle = DateTime.Now;
        }
    }

    public void ResetRound(IReadOnlyCollection<Player> players)
    {
        foreach (var player in players)
        {
            player.Score = 0;
        }

        InitializeArena(players);
    }

    public void PlacePlayerAtSpawn(Player player, IEnumerable<Player> otherPlayers)
    {
        var preferredSlot = Math.Clamp(player.Id, 0, DefaultSpawnPositions.Length - 1);
        foreach (var slot in EnumerateSpawnSlots(preferredSlot))
        {
            var spawn = DefaultSpawnPositions[slot];
            if (IsPlayerSpaceSafe(spawn.X, spawn.Y, otherPlayers))
            {
                player.X = spawn.X;
                player.Y = spawn.Y;
                return;
            }
        }

        for (int attempts = 0; attempts < 100; attempts++)
        {
            int x = random.Next(20, ArenaWidth - PlayerSize - 20);
            int y = random.Next(20, ArenaHeight - PlayerSize - 20);
            if (IsPlayerSpaceSafe(x, y, otherPlayers))
            {
                player.X = x;
                player.Y = y;
                return;
            }
        }

        var fallback = DefaultSpawnPositions[preferredSlot];
        player.X = fallback.X;
        player.Y = fallback.Y;
    }

    private void InitializeArena(IReadOnlyCollection<Player> players)
    {
        Phase = "PLAYING";
        Coins.Clear();
        RebuildWalls(players);

        foreach (var player in players.OrderBy(player => player.Id))
        {
            PlacePlayerAtSpawn(player, players.Where(other => !ReferenceEquals(other, player)));
        }

        Snake = CreateSafeSnake(players);
        lastCoinSpawn = DateTime.Now;
        lastWallToggle = DateTime.Now;
    }

    private IEnumerable<int> EnumerateSpawnSlots(int preferredSlot)
    {
        yield return preferredSlot;

        for (int slot = 0; slot < DefaultSpawnPositions.Length; slot++)
        {
            if (slot != preferredSlot)
            {
                yield return slot;
            }
        }
    }

    private bool IsPlayerSpaceSafe(int x, int y, IEnumerable<Player> otherPlayers)
    {
        if (x < 0 || y < 0 || x > ArenaWidth - PlayerSize || y > ArenaHeight - PlayerSize)
            return false;

        if (
            Walls.Any(wall => RectanglesOverlap(x, y, PlayerSize, PlayerSize, wall.X, wall.Y, wall.Width, wall.Height))
        )
        {
            return false;
        }

        if (
            otherPlayers.Any(player =>
                RectanglesOverlap(x, y, PlayerSize, PlayerSize, player.X, player.Y, PlayerSize, PlayerSize)
            )
        )
        {
            return false;
        }

        if (
            Snake.Segments.Any(segment =>
                RectanglesOverlap(
                    x,
                    y,
                    PlayerSize,
                    PlayerSize,
                    segment.X,
                    segment.Y,
                    Snake.SegmentSize,
                    Snake.SegmentSize
                )
            )
        )
        {
            return false;
        }

        return true;
    }

    private void SpawnCoin(IReadOnlyCollection<Player> players)
    {
        for (int attempts = 0; attempts < 30; attempts++)
        {
            int cx = random.Next(20, ArenaWidth - 40);
            int cy = random.Next(20, ArenaHeight - 40);

            if (IsCoinSpaceSafe(cx, cy, players))
            {
                Coins.Add(new Coin { X = cx, Y = cy });
                break;
            }
        }
    }

    private void RebuildWalls(IReadOnlyCollection<Player> players)
    {
        Walls.Clear();
        SpawnWalls(players);
    }

    private void SpawnWalls(IReadOnlyCollection<Player> players)
    {
        int targetCount = random.Next(5, 10);
        int attempts = 0;

        while (Walls.Count < targetCount && attempts < targetCount * 40)
        {
            attempts++;

            int x = random.Next(40, ArenaWidth - 240);
            int y = random.Next(40, ArenaHeight - 140);
            int width = random.Next(60, 240);
            int height = random.Next(60, 180);

            var newWall = new Wall
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
            };

            if (
                Walls.Any(existingWall =>
                    RectanglesOverlap(
                        newWall.X,
                        newWall.Y,
                        newWall.Width,
                        newWall.Height,
                        existingWall.X,
                        existingWall.Y,
                        existingWall.Width,
                        existingWall.Height
                    )
                )
            )
            {
                continue;
            }

            if (
                DefaultSpawnPositions.Any(spawn =>
                    RectanglesOverlap(
                        newWall.X,
                        newWall.Y,
                        newWall.Width,
                        newWall.Height,
                        spawn.X,
                        spawn.Y,
                        PlayerSize,
                        PlayerSize
                    )
                )
            )
            {
                continue;
            }

            if (
                players.Any(player =>
                    RectanglesOverlap(
                        newWall.X,
                        newWall.Y,
                        newWall.Width,
                        newWall.Height,
                        player.X,
                        player.Y,
                        PlayerSize,
                        PlayerSize
                    )
                )
            )
            {
                continue;
            }

            if (
                Snake.Segments.Any(segment =>
                    RectanglesOverlap(
                        newWall.X,
                        newWall.Y,
                        newWall.Width,
                        newWall.Height,
                        segment.X,
                        segment.Y,
                        Snake.SegmentSize,
                        Snake.SegmentSize
                    )
                )
            )
            {
                continue;
            }

            if (
                Coins.Any(coin =>
                    RectanglesOverlap(
                        newWall.X,
                        newWall.Y,
                        newWall.Width,
                        newWall.Height,
                        coin.X,
                        coin.Y,
                        CoinSize,
                        CoinSize
                    )
                )
            )
            {
                continue;
            }

            Walls.Add(newWall);
        }
    }

    private bool IsCoinSpaceSafe(int x, int y, IReadOnlyCollection<Player> players)
    {
        if (
            Walls.Any(wall => RectanglesOverlap(x, y, CoinSize, CoinSize, wall.X, wall.Y, wall.Width, wall.Height))
        )
        {
            return false;
        }

        if (
            players.Any(player =>
                RectanglesOverlap(x, y, CoinSize, CoinSize, player.X, player.Y, PlayerSize, PlayerSize)
            )
        )
        {
            return false;
        }

        if (
            Snake.Segments.Any(segment =>
                RectanglesOverlap(
                    x,
                    y,
                    CoinSize,
                    CoinSize,
                    segment.X,
                    segment.Y,
                    Snake.SegmentSize,
                    Snake.SegmentSize
                )
            )
        )
        {
            return false;
        }

        if (
            Coins.Any(coin => RectanglesOverlap(x, y, CoinSize, CoinSize, coin.X, coin.Y, CoinSize, CoinSize))
        )
        {
            return false;
        }

        return true;
    }

    private Snake CreateSafeSnake(IEnumerable<Player> players)
    {
        for (int attempts = 0; attempts < 100; attempts++)
        {
            int sx = random.Next(Snake.DefaultLength * Snake.SegmentSize, ArenaWidth - 40);
            int sy = random.Next(40, ArenaHeight - 40);
            var candidate = new Snake(sx, sy);

            if (
                candidate.Segments.All(segment =>
                    segment.X >= 0
                    && segment.X <= ArenaWidth - Snake.SegmentSize
                    && segment.Y >= 0
                    && segment.Y <= ArenaHeight - Snake.SegmentSize
                )
                && candidate.Segments.All(segment =>
                    Walls.All(wall =>
                        !RectanglesOverlap(
                            segment.X,
                            segment.Y,
                            Snake.SegmentSize,
                            Snake.SegmentSize,
                            wall.X,
                            wall.Y,
                            wall.Width,
                            wall.Height
                        )
                    )
                )
                && candidate.Segments.All(segment =>
                    players.All(player =>
                        !RectanglesOverlap(
                            segment.X,
                            segment.Y,
                            Snake.SegmentSize,
                            Snake.SegmentSize,
                            player.X,
                            player.Y,
                            PlayerSize,
                            PlayerSize
                        )
                    )
                )
                && candidate.Segments.All(segment =>
                    DefaultSpawnPositions.All(spawn =>
                        !RectanglesOverlap(
                            segment.X,
                            segment.Y,
                            Snake.SegmentSize,
                            Snake.SegmentSize,
                            spawn.X,
                            spawn.Y,
                            PlayerSize,
                            PlayerSize
                        )
                    )
                )
            )
            {
                return candidate;
            }
        }

        return new Snake(ArenaWidth / 2, ArenaHeight / 2);
    }

    private static bool IsColliding(int x1, int y1, int size1, int x2, int y2, int size2)
    {
        return x1 < x2 + size2 && x1 + size1 > x2 && y1 < y2 + size2 && y1 + size1 > y2;
    }

    private static bool RectanglesOverlap(
        int x1,
        int y1,
        int width1,
        int height1,
        int x2,
        int y2,
        int width2,
        int height2
    )
    {
        return x1 < x2 + width2
            && x1 + width1 > x2
            && y1 < y2 + height2
            && y1 + height1 > y2;
    }
}
