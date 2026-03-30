/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using LanGameServer.Entities;
using LanGameServer.Networking;
using LanGameShared.Protocol;

namespace LanGameServer.Gameplay;

public class GameServer
{
    private readonly int port;
    private TcpListener? listener;
    private readonly List<ClientConnection> clients = new();
    private readonly List<Task> clientTasks = new();
    private readonly GameState gameState = new();
    private readonly object lockObj = new();
    private readonly object lifecycleLock = new();
    private CancellationTokenSource? shutdownCts;
    private Task? acceptClientsTask;
    private Task? gameLoopTask;
    private volatile bool running;
    private bool gameOverMessageSent;

    public GameServer(int port = 5000)
    {
        this.port = port;
    }

    public bool IsRunning => running;

    public int ListeningPort => (listener?.LocalEndpoint as IPEndPoint)?.Port ?? port;

    public void Start()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    public async Task RunAsync(TextReader? commandReader = null, CancellationToken cancellationToken = default)
    {
        StartListening();

        Console.WriteLine("Press Enter to stop server...");

        commandReader ??= Console.In;

        try
        {
            await commandReader.ReadLineAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await StopAsync();
        }
    }

    public void StartListening()
    {
        lock (lifecycleLock)
        {
            if (running)
            {
                throw new InvalidOperationException("Server is already running.");
            }

            shutdownCts = new CancellationTokenSource();
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            running = true;
            acceptClientsTask = AcceptClientsAsync(shutdownCts.Token);
            gameLoopTask = GameLoopAsync(shutdownCts.Token);
        }

        var localIps = Dns.GetHostEntry(Dns.GetHostName())
            .AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.ToString())
            .ToList();

        if (localIps.Count > 0)
            Console.WriteLine("Server IPs: " + string.Join(", ", localIps));

        Console.WriteLine($"Server started on port {ListeningPort}");
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        TcpListener? listenerToStop;
        Task? acceptTask;
        Task? loopTask;
        List<Task> pendingClientTasks;
        List<ClientConnection> openClients;

        lock (lifecycleLock)
        {
            if (!running && listener is null && acceptClientsTask is null && gameLoopTask is null)
            {
                return;
            }

            running = false;
            cts = shutdownCts;
            shutdownCts = null;
            listenerToStop = listener;
            listener = null;
            acceptTask = acceptClientsTask;
            loopTask = gameLoopTask;
            acceptClientsTask = null;
            gameLoopTask = null;
            pendingClientTasks = clientTasks.ToList();
            openClients = clients.ToList();
        }

        cts?.Cancel();

        try
        {
            listenerToStop?.Stop();
        }
        catch
        {
        }

        foreach (var client in openClients)
        {
            client.Close();
        }

        var tasksToAwait = new List<Task>(pendingClientTasks.Count + 2);
        if (acceptTask is not null)
        {
            tasksToAwait.Add(acceptTask);
        }

        if (loopTask is not null)
        {
            tasksToAwait.Add(loopTask);
        }

        tasksToAwait.AddRange(pendingClientTasks);

        if (tasksToAwait.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasksToAwait);
            }
            catch
            {
            }
        }

        cts?.Dispose();
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await listener!.AcceptTcpClientAsync(cancellationToken);
                tcpClient.NoDelay = true;
                var client = new ClientConnection(tcpClient, this);
                TrackClientTask(client.HandleClient());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
            }
        }
    }

    private async Task GameLoopAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        const long tickInterval = 1000 / 60;
        const long broadcastInterval = 1000 / 20;
        long lastBroadcast = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var frameStart = sw.ElapsedMilliseconds;

            lock (lockObj)
            {
                if (clients.Count > 0)
                {
                    gameState.Update(clients);

                    if (frameStart - lastBroadcast >= broadcastInterval)
                    {
                        BroadcastGameState();
                        lastBroadcast = frameStart;
                    }
                }
                else
                {
                    gameOverMessageSent = false;
                }
            }

            var elapsed = sw.ElapsedMilliseconds - frameStart;
            var sleepTime = (int)(tickInterval - elapsed);
            if (sleepTime > 0)
            {
                try
                {
                    await Task.Delay(sleepTime, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private void TrackClientTask(Task clientTask)
    {
        lock (lifecycleLock)
        {
            clientTasks.Add(clientTask);
        }

        _ = clientTask.ContinueWith(
            completedTask =>
            {
                lock (lifecycleLock)
                {
                    clientTasks.Remove(completedTask);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }

    public bool AddClient(ClientConnection client)
    {
        lock (lockObj)
        {
            if (clients.Count >= 4)
            {
                client.Send("JOIN_FULL");
                return false;
            }

            var colors = new[] { "Red", "Blue", "Green", "Yellow" };
            var usedColors = clients.Select(c => c.Player.Color).ToHashSet();
            var color = colors.First(c => !usedColors.Contains(c));

            var usedIds = clients.Select(c => c.Player.Id).ToHashSet();
            var playerId = Enumerable.Range(0, 4).First(i => !usedIds.Contains(i));

            client.Player = new Player
            {
                Id = playerId,
                Name = client.Nickname,
                Color = color,
                Score = 0,
            };

            var isFirstClient = clients.Count == 0;
            clients.Add(client);

            if (isFirstClient)
            {
                gameState.ResetRound(clients.Select(existingClient => existingClient.Player).ToList());
                gameOverMessageSent = false;
            }
            else
            {
                gameState.PlacePlayerAtSpawn(
                    client.Player,
                    clients
                        .Where(existingClient => !ReferenceEquals(existingClient, client))
                        .Select(existingClient => existingClient.Player)
                );
            }

            client.Send($"JOIN_OK|{playerId}|{color}");
            return true;
        }
    }

    public void RemoveClient(ClientConnection client)
    {
        lock (lockObj)
        {
            clients.Remove(client);

            if (clients.Count == 0)
            {
                gameOverMessageSent = false;
            }
        }
    }

    public void HandleInput(ClientConnection client, string input)
    {
        lock (lockObj)
        {
            client.InputState = ProtocolRules.NormalizeInputState(input);
        }
    }

    public bool HandleRestart()
    {
        lock (lockObj)
        {
            foreach (var client in clients)
            {
                client.InputState = string.Empty;
            }

            if (!gameState.CanRestartRound)
                return false;

            gameState.ResetRound(clients.Select(client => client.Player).ToList());
            gameOverMessageSent = false;
            return true;
        }
    }

    private void BroadcastGameState()
    {
        var playerData = string.Join(
            ";",
            clients.Select(client =>
                $"P:{client.Player.Id}:{client.Player.X}:{client.Player.Y}:{client.Player.Score}:{client.Player.Name}:{client.Player.Color}"
            )
        );

        var coinData = string.Join(";", gameState.Coins.Select(coin => $"C:{coin.X}:{coin.Y}"));
        var snakeData = string.Join(";", gameState.Snake.Segments.Select(segment => $"S:{segment.X}:{segment.Y}"));
        var wallData = string.Join(
            ";",
            gameState.Walls.Select(wall => $"W:{wall.X}:{wall.Y}:{wall.Width}:{wall.Height}")
        );

        var phase = gameState.Phase;
        var stateMessage = $"STATE|{phase}|{playerData}|{coinData}|{snakeData}|{wallData}";

        foreach (var client in clients.ToList())
        {
            client.Send(stateMessage);
        }

        if (phase == "GAME_OVER" && !gameOverMessageSent && clients.Count > 0)
        {
            var winner = clients.OrderByDescending(client => client.Player.Score).First();
            var ranking = string.Join(
                ";",
                clients
                    .OrderByDescending(client => client.Player.Score)
                    .Select(client => $"{client.Player.Name}:{client.Player.Score}")
            );
            var gameOverMessage = $"GAME_OVER|{winner.Player.Id}|{ranking}";

            foreach (var client in clients.ToList())
            {
                client.Send(gameOverMessage);
            }

            gameOverMessageSent = true;
        }
        else if (phase != "GAME_OVER")
        {
            gameOverMessageSent = false;
        }
    }
}
