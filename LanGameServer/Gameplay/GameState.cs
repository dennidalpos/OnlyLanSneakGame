using LanGameServer.Entities;
using LanGameServer.Networking;

namespace LanGameServer.Gameplay;

public class GameState
{
    public string Phase { get; private set; } = "PLAYING";
    public List<Coin> Coins { get; } = new();
    public Snake Snake { get; private set; }
    public List<Wall> Walls { get; } = new();
    private DateTime lastCoinSpawn = DateTime.Now;
    private DateTime lastWallToggle = DateTime.Now;
    private readonly Random random = new();
    private const int ArenaWidth = 1240;
    private const int ArenaHeight = 660;
    private const int PlayerSize = 30;
    private const int CoinSize = 20;
    private const int MoveSpeed = 5;
    private const int MaxCoins = 6;
    private const int WinScore = 15;

    public GameState()
    {
        Snake = new Snake(ArenaWidth / 2, ArenaHeight / 2);
        SpawnWalls();
    }

    public void Update(List<ClientConnection> clients)
    {
        if (Phase == "GAME_OVER") return;

        var playersList = clients.Select(c => c.Player).ToList();
        Snake.Update(ArenaWidth, ArenaHeight, Walls, playersList);

        foreach (var client in clients)
        {
            var input = client.InputState;
            var player = client.Player;

            var dx = 0;
            var dy = 0;

            if (input.Contains('U')) dy -= MoveSpeed;
            if (input.Contains('D')) dy += MoveSpeed;
            if (input.Contains('L')) dx -= MoveSpeed;
            if (input.Contains('R')) dx += MoveSpeed;

            var newX = Math.Clamp(player.X + dx, 0, ArenaWidth - PlayerSize);
            var newY = Math.Clamp(player.Y + dy, 0, ArenaHeight - PlayerSize);

            bool hitWall = false;
            foreach (var wall in Walls)
            {
                if (newX < wall.X + wall.Width && newX + PlayerSize > wall.X &&
                    newY < wall.Y + wall.Height && newY + PlayerSize > wall.Y)
                {
                    hitWall = true;
                    break;
                }
            }

            if (!hitWall)
            {
                player.X = newX;
                player.Y = newY;
            }
        }

        bool snakeHitAny = false;

        foreach (var client in clients)
        {
            var player = client.Player;

            foreach (var segment in Snake.Segments)
            {
                if (IsColliding(player.X, player.Y, PlayerSize, segment.X, segment.Y, 20))
                {
                    player.X = ArenaWidth / 2 - 100 + client.Player.Id * 50;
                    player.Y = ArenaHeight / 2;
                    player.Score = Math.Max(0, player.Score - 1);
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
            var allPlayers = clients.Select(c => c.Player).ToList();
            int attempts = 0;
            while (attempts < 50)
            {
                attempts++;
                int sx = random.Next(100, ArenaWidth - 100);
                int sy = random.Next(100, ArenaHeight - 100);

                bool collidesWall = false;
                foreach (var wall in Walls)
                {
                    if (sx < wall.X + wall.Width && sx + 20 > wall.X &&
                        sy < wall.Y + wall.Height && sy + 20 > wall.Y)
                    {
                        collidesWall = true;
                        break;
                    }
                }
                if (collidesWall) continue;

                bool tooClosePlayer = false;
                foreach (var p in allPlayers)
                {
                    int dx = p.X - sx;
                    int dy = p.Y - sy;
                    if (dx * dx + dy * dy < 200 * 200)
                    {
                        tooClosePlayer = true;
                        break;
                    }
                }
                if (tooClosePlayer) continue;

                Snake = new Snake(sx, sy);
                break;
            }
        }

        if ((DateTime.Now - lastCoinSpawn).TotalSeconds >= 4 && Coins.Count < MaxCoins)
        {
            SpawnCoin();
            lastCoinSpawn = DateTime.Now;
        }

        if ((DateTime.Now - lastWallToggle).TotalSeconds >= 4)
        {
            Walls.Clear();
            SpawnWalls();
            lastWallToggle = DateTime.Now;
        }
    }

    private bool IsColliding(int x1, int y1, int size1, int x2, int y2, int size2)
    {
        return x1 < x2 + size2 && x1 + size1 > x2 && y1 < y2 + size2 && y1 + size1 > y2;
    }

    private void SpawnCoin()
    {
        for (int attempts = 0; attempts < 30; attempts++)
        {
            int cx = random.Next(20, ArenaWidth - 40);
            int cy = random.Next(20, ArenaHeight - 40);

            bool blocked = false;
            foreach (var wall in Walls)
            {
                if (cx < wall.X + wall.Width && cx + CoinSize > wall.X &&
                    cy < wall.Y + wall.Height && cy + CoinSize > wall.Y)
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
            {
                Coins.Add(new Coin { X = cx, Y = cy });
                break;
            }
        }
    }

    private void SpawnWalls()
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
                Height = height
            };

            bool intersectsExisting = false;
            foreach (var w in Walls)
            {
                if (newWall.X < w.X + w.Width && newWall.X + newWall.Width > w.X &&
                    newWall.Y < w.Y + w.Height && newWall.Y + newWall.Height > w.Y)
                {
                    intersectsExisting = true;
                    break;
                }
            }

            if (!intersectsExisting)
            {
                Walls.Add(newWall);
            }
        }
    }

    public void Reset()
    {
        Phase = "PLAYING";
        Coins.Clear();
        Walls.Clear();

        int sx = random.Next(100, ArenaWidth - 100);
        int sy = random.Next(100, ArenaHeight - 100);
        Snake = new Snake(sx, sy);

        SpawnWalls();
        lastCoinSpawn = DateTime.Now;
        lastWallToggle = DateTime.Now;
    }
}
