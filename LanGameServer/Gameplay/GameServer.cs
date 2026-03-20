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
    private TcpListener? listener;
    private readonly List<ClientConnection> clients = new();
    private readonly GameState gameState = new();
    private readonly object lockObj = new();
    private bool running = true;
    private bool gameOverMessageSent;

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
                await Task.Delay(sleepTime);
        }
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
