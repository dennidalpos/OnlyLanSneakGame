using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LanGameServer;

class Program
{
    static void Main()
    {
        var server = new GameServer();
        server.Start();
    }
}

class GameServer
{
    private TcpListener? listener;
    private readonly List<ClientConnection> clients = new();
    private readonly GameState gameState = new();
    private readonly object lockObj = new();
    private bool running = true;

    public void Start()
    {
        listener = new TcpListener(IPAddress.Any, 5000);
        listener.Start();

        var localIps = Dns.GetHostEntry(Dns.GetHostName())
            .AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.ToString())
            .ToList();

        if (localIps.Count > 0)
            Console.WriteLine("Server IPs: " + string.Join(", ", localIps));

        Console.WriteLine("Server started on port 5000");

        Task.Run(AcceptClients);
        Task.Run(GameLoop);

        Console.WriteLine("Press Enter to stop server...");
        Console.ReadLine();
        running = false;
    }

    private async Task AcceptClients()
    {
        while (running)
        {
            try
            {
                var tcpClient = await listener!.AcceptTcpClientAsync();
                tcpClient.NoDelay = true;
                var client = new ClientConnection(tcpClient, this);
                _ = Task.Run(() => client.HandleClient());
            }
            catch { }
        }
    }

    private async Task GameLoop()
    {
        var sw = Stopwatch.StartNew();
        long tickInterval = 1000 / 60;
        long broadcastInterval = 1000 / 20;
        long lastBroadcast = 0;

        while (running)
        {
            var frameStart = sw.ElapsedMilliseconds;

            lock (lockObj)
            {
                gameState.Update(clients);

                if (frameStart - lastBroadcast >= broadcastInterval)
                {
                    BroadcastGameState();
                    lastBroadcast = frameStart;
                }
            }

            var elapsed = sw.ElapsedMilliseconds - frameStart;
            var sleepTime = (int)(tickInterval - elapsed);
            if (sleepTime > 0)
                await Task.Delay(sleepTime);
        }
    }

    public void AddClient(ClientConnection client)
    {
        lock (lockObj)
        {
            if (clients.Count >= 4)
            {
                client.Send("JOIN_FULL");
                return;
            }

            var colors = new[] { "Red", "Blue", "Green", "Yellow" };
            var usedColors = clients.Select(c => c.Player.Color).ToHashSet();
            var color = colors.First(c => !usedColors.Contains(c));

            var spawnPositions = new[]
            {
                new { X = 200, Y = 200 },
                new { X = 1040, Y = 200 },
                new { X = 200, Y = 460 },
                new { X = 1040, Y = 460 },
            };

            var usedIds = clients.Select(c => c.Player.Id).ToHashSet();
            var playerId = Enumerable
                .Range(0, spawnPositions.Length)
                .First(i => !usedIds.Contains(i));
            var spawn = spawnPositions[playerId];

            client.Player = new Player
            {
                Id = playerId,
                Name = client.Nickname,
                Color = color,
                X = spawn.X,
                Y = spawn.Y,
                Score = 0,
            };

            clients.Add(client);
            client.Send($"JOIN_OK|{playerId}|{color}");
        }
    }

    public void RemoveClient(ClientConnection client)
    {
        lock (lockObj)
        {
            clients.Remove(client);
        }
    }

    public void HandleInput(ClientConnection client, string input)
    {
        lock (lockObj)
        {
            client.InputState = input;
        }
    }

    public void HandleRestart()
    {
        lock (lockObj)
        {
            gameState.Reset();
            foreach (var client in clients)
            {
                client.Player.Score = 0;
            }
        }
    }

    private void BroadcastGameState()
    {
        var playerData = string.Join(
            ";",
            clients.Select(c =>
                $"P:{c.Player.Id}:{c.Player.X}:{c.Player.Y}:{c.Player.Score}:{c.Player.Name}:{c.Player.Color}"
            )
        );

        var coinData = string.Join(";", gameState.Coins.Select(coin => $"C:{coin.X}:{coin.Y}"));

        var snakeData = string.Join(
            ";",
            gameState.Snake.Segments.Select(seg => $"S:{seg.X}:{seg.Y}")
        );

        var wallData = string.Join(
            ";",
            gameState.Walls.Select(wall => $"W:{wall.X}:{wall.Y}:{wall.Width}:{wall.Height}")
        );

        var phase = gameState.Phase;
        var message = $"STATE|{phase}|{playerData}|{coinData}|{snakeData}|{wallData}";

        foreach (var client in clients.ToList())
        {
            client.Send(message);
        }

        if (phase == "GAME_OVER")
        {
            var winner = clients.OrderByDescending(c => c.Player.Score).First();
            var ranking = string.Join(
                ";",
                clients
                    .OrderByDescending(c => c.Player.Score)
                    .Select(c => $"{c.Player.Name}:{c.Player.Score}")
            );
            var gameOverMsg = $"GAME_OVER|{winner.Player.Id}|{ranking}";

            foreach (var client in clients.ToList())
            {
                client.Send(gameOverMsg);
            }
        }
    }
}

class ClientConnection
{
    private readonly TcpClient tcpClient;
    private readonly GameServer server;
    private StreamReader? reader;
    private StreamWriter? writer;

    public string Nickname { get; private set; } = "";
    public Player Player { get; set; } = new();
    public string InputState { get; set; } = "";

    public ClientConnection(TcpClient tcpClient, GameServer server)
    {
        this.tcpClient = tcpClient;
        this.server = server;
    }

    public async Task HandleClient()
    {
        try
        {
            var stream = tcpClient.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream) { AutoFlush = true };

            var joinMsg = await reader.ReadLineAsync();
            if (joinMsg?.StartsWith("JOIN|") == true)
            {
                Nickname = joinMsg.Split('|')[1];
                server.AddClient(this);

                while (tcpClient.Connected)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null)
                        break;

                    if (line.StartsWith("INPUT|"))
                    {
                        server.HandleInput(this, line.Split('|')[1]);
                    }
                    else if (line == "RESTART")
                    {
                        server.HandleRestart();
                    }
                }
            }
        }
        catch { }
        finally
        {
            server.RemoveClient(this);
            tcpClient.Close();
        }
    }

    public void Send(string message)
    {
        try
        {
            writer?.WriteLine(message);
        }
        catch { }
    }
}

class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Score { get; set; }
}

class Coin
{
    public int X { get; set; }
    public int Y { get; set; }
}

class Snake
{
    public List<SnakeSegment> Segments { get; } = new();
    private int directionX = 1;
    private int directionY = 0;
    private readonly Random random = new();
    private int moveCounter = 0;
    private const int MoveInterval = 5;
    private const int SegmentSize = 20;
    private const int SnakeLength = 15;
    private const int ChaseRadius = 250;

    public Snake(int startX, int startY)
    {
        for (int i = 0; i < SnakeLength; i++)
        {
            Segments.Add(new SnakeSegment { X = startX - i * SegmentSize, Y = startY });
        }
    }

    public void Update(int arenaWidth, int arenaHeight, List<Wall> walls, List<Player> players)
    {
        moveCounter++;
        if (moveCounter < MoveInterval)
            return;
        moveCounter = 0;

        var head = Segments[0];

        Player? target = null;
        int minDistSq = int.MaxValue;

        foreach (var p in players)
        {
            int dxp = p.X - head.X;
            int dyp = p.Y - head.Y;
            int distSq = dxp * dxp + dyp * dyp;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                target = p;
            }
        }

        if (target != null && minDistSq <= ChaseRadius * ChaseRadius)
        {
            int dxp = target.X - head.X;
            int dyp = target.Y - head.Y;
            if (Math.Abs(dxp) > Math.Abs(dyp))
            {
                directionX = Math.Sign(dxp);
                directionY = 0;
            }
            else
            {
                directionX = 0;
                directionY = Math.Sign(dyp);
            }
        }
        else
        {
            if (random.Next(0, 10) < 2)
            {
                var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
                var dir = directions[random.Next(directions.Length)];
                directionX = dir.Item1;
                directionY = dir.Item2;
            }
        }

        var newX = head.X + directionX * SegmentSize;
        var newY = head.Y + directionY * SegmentSize;

        if (
            newX < 0
            || newX >= arenaWidth - SegmentSize
            || newY < 0
            || newY >= arenaHeight - SegmentSize
        )
        {
            directionX = -directionX;
            directionY = -directionY;
            newX = head.X + directionX * SegmentSize;
            newY = head.Y + directionY * SegmentSize;
        }

        foreach (var wall in walls)
        {
            if (
                newX < wall.X + wall.Width
                && newX + SegmentSize > wall.X
                && newY < wall.Y + wall.Height
                && newY + SegmentSize > wall.Y
            )
            {
                directionX = -directionX;
                directionY = -directionY;
                newX = head.X + directionX * SegmentSize;
                newY = head.Y + directionY * SegmentSize;
                break;
            }
        }

        Segments.Insert(0, new SnakeSegment { X = newX, Y = newY });
        Segments.RemoveAt(Segments.Count - 1);
    }
}

class SnakeSegment
{
    public int X { get; set; }
    public int Y { get; set; }
}

class Wall
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

class GameState
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
    private const int MaxCoins = 8;
    private const int WinScore = 10;

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
                    player.Score = 0;
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
                    if (dx * dx + dy * dy < 150 * 150)
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

        if ((DateTime.Now - lastCoinSpawn).TotalSeconds >= 3 && Coins.Count < MaxCoins)
        {
            SpawnCoin();
            lastCoinSpawn = DateTime.Now;
        }

        if ((DateTime.Now - lastWallToggle).TotalSeconds >= 5)
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
        Coins.Add(
            new Coin { X = random.Next(20, ArenaWidth - 40), Y = random.Next(20, ArenaHeight - 40) }
        );
    }

    private void SpawnWalls()
    {
        int targetCount = random.Next(3, 7);
        int attempts = 0;

        while (Walls.Count < targetCount && attempts < targetCount * 30)
        {
            attempts++;

            int x = random.Next(40, ArenaWidth - 240);
            int y = random.Next(40, ArenaHeight - 140);
            int width = random.Next(40, 220);
            int height = random.Next(40, 160);

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
