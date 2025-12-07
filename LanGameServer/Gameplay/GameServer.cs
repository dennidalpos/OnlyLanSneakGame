using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using LanGameServer.Entities;
using LanGameServer.Networking;

namespace LanGameServer.Gameplay;

public class GameServer
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
        const long tickInterval = 1000 / 60;
        const long broadcastInterval = 1000 / 20;
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
