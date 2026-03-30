/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

using System.Net.Sockets;
using System.Reflection;
using System.Net;
using LanGameServer.Entities;
using LanGameServer.Gameplay;
using LanGameServer.Networking;
using LanGameShared.Protocol;
using Xunit;
using ServerProtocolRules = LanGameShared.Protocol.ProtocolRules;

namespace OnlyLanSneakGame.Tests;

public class GameplayTests
{
    [Fact]
    public void AddClient_WhenLobbyWasEmpty_StartsFreshRound()
    {
        var server = new GameServer();
        var firstClient = new TestClientConnection(server, "Alpha");

        Assert.True(server.AddClient(firstClient));

        var state = GetServerState(server);
        state.Coins.Add(new Coin { X = 300, Y = 300 });
        firstClient.Player.Score = 7;

        server.RemoveClient(firstClient);

        var secondClient = new TestClientConnection(server, "Beta");
        Assert.True(server.AddClient(secondClient));

        Assert.Equal(0, secondClient.Player.Score);
        Assert.Empty(state.Coins);
        Assert.Equal("PLAYING", state.Phase);
        AssertPlayersAreSafe(state, [secondClient.Player]);
        AssertSnakeIsSafe(state, [secondClient.Player]);
    }

    [Fact]
    public void SpawnCoin_PlacesCoinsAwayFromPlayersSnakeWallsAndExistingCoins()
    {
        var state = new GameState();
        var player = new Player { Id = 0, Name = "Alpha" };
        state.ResetRound([player]);
        state.Coins.Clear();

        for (int i = 0; i < 6; i++)
        {
            InvokePrivateMethod(state, "SpawnCoin", [new List<Player> { player }]);
        }

        Assert.NotEmpty(state.Coins);
        Assert.All(
            state.Coins,
            coin =>
            {
                Assert.DoesNotContain(
                    state.Walls,
                    wall => Overlap(coin.X, coin.Y, 20, 20, wall.X, wall.Y, wall.Width, wall.Height)
                );
                Assert.DoesNotContain(
                    state.Snake.Segments,
                    segment => Overlap(
                        coin.X,
                        coin.Y,
                        20,
                        20,
                        segment.X,
                        segment.Y,
                        Snake.SegmentSize,
                        Snake.SegmentSize
                    )
                );
                Assert.False(
                    Overlap(
                        coin.X,
                        coin.Y,
                        20,
                        20,
                        player.X,
                        player.Y,
                        GameState.PlayerSize,
                        GameState.PlayerSize
                    )
                );
            }
        );

        for (int i = 0; i < state.Coins.Count; i++)
        {
            for (int j = i + 1; j < state.Coins.Count; j++)
            {
                Assert.False(Overlap(state.Coins[i].X, state.Coins[i].Y, 20, 20, state.Coins[j].X, state.Coins[j].Y, 20, 20));
            }
        }
    }

    [Fact]
    public void RebuildWalls_KeepsExistingCoinsReachable()
    {
        var state = new GameState();
        var player = new Player { Id = 0, Name = "Alpha" };
        state.ResetRound([player]);

        state.Coins.Clear();
        state.Coins.Add(new Coin { X = 320, Y = 320 });
        state.Coins.Add(new Coin { X = 760, Y = 240 });

        InvokePrivateMethod(state, "RebuildWalls", [new List<Player> { player }]);

        Assert.NotEmpty(state.Walls);
        Assert.All(
            state.Coins,
            coin => Assert.DoesNotContain(
                state.Walls,
                wall => Overlap(coin.X, coin.Y, 20, 20, wall.X, wall.Y, wall.Width, wall.Height)
            )
        );
    }

    [Fact]
    public void BroadcastGameOver_ProducesRankingAndHandleRestart_ResetsRound()
    {
        var server = new GameServer();
        var alpha = new TestClientConnection(server, "Alpha");
        var beta = new TestClientConnection(server, "Beta");

        Assert.True(server.AddClient(alpha));
        Assert.True(server.AddClient(beta));

        var state = GetServerState(server);
        state.Coins.Clear();
        alpha.Player.Score = 14;
        beta.Player.Score = 3;
        state.Coins.Add(new Coin { X = alpha.Player.X, Y = alpha.Player.Y });

        state.Update([alpha, beta]);
        Assert.Equal("GAME_OVER", state.Phase);

        InvokePrivateMethod(server, "BroadcastGameState", []);

        var gameOverMessage = Assert.Single(alpha.SentMessages, message => message.StartsWith("GAME_OVER|"));
        Assert.True(ProtocolMessageParser.TryParseGameOverMessage(gameOverMessage, out var summary));
        Assert.Equal(alpha.Player.Id, summary.WinnerId);
        Assert.Collection(
            summary.Ranking,
            entry =>
            {
                Assert.Equal("Alpha", entry.Name);
                Assert.Equal(15, entry.Score);
            },
            entry =>
            {
                Assert.Equal("Beta", entry.Name);
                Assert.Equal(3, entry.Score);
            }
        );

        alpha.InputState = "UD";
        beta.InputState = "LR";

        Assert.True(server.HandleRestart());
        Assert.Equal("PLAYING", state.Phase);
        Assert.Equal(string.Empty, alpha.InputState);
        Assert.Equal(string.Empty, beta.InputState);
        Assert.All(new[] { alpha.Player, beta.Player }, player => Assert.Equal(0, player.Score));
        AssertPlayersAreSafe(state, [alpha.Player, beta.Player]);
        AssertSnakeIsSafe(state, [alpha.Player, beta.Player]);
    }

    [Fact]
    public void Update_WhenSnakeHitsPlayer_RespawnsPlayerInSafeSpace()
    {
        var state = new GameState();
        var player = new Player { Id = 0, Name = "Alpha", Score = 3 };
        state.ResetRound([player]);
        player.Score = 3;

        var head = state.Snake.Segments[0];
        player.X = head.X;
        player.Y = head.Y;

        var client = new ClientConnection(new TcpClient(), new GameServer()) { Player = player };

        state.Update([client]);

        Assert.Equal(2, player.Score);
        AssertPlayersAreSafe(state, [player]);
        AssertSnakeIsSafe(state, [player]);
    }

    [Fact]
    public void Snake_ChasesHighestScorePlayerWithinRadius()
    {
        var snake = new Snake(200, 200);
        var players = new List<Player>
        {
            new() { Id = 0, Name = "Near", Score = 1, X = 220, Y = 200 },
            new() { Id = 1, Name = "Leader", Score = 10, X = 200, Y = 260 },
        };

        snake.Update(GameState.ArenaWidth, GameState.ArenaHeight, [], players);
        snake.Update(GameState.ArenaWidth, GameState.ArenaHeight, [], players);
        snake.Update(GameState.ArenaWidth, GameState.ArenaHeight, [], players);

        Assert.Equal(200, snake.Segments[0].X);
        Assert.Equal(220, snake.Segments[0].Y);
    }

    [Fact]
    public void ProtocolRules_ValidateNicknameAndNormalizeInput()
    {
        var valid = ServerProtocolRules.TryNormalizeNickname("Player_01", out var nickname, out _);
        var invalid = ServerProtocolRules.TryNormalizeNickname("Bad:Name", out _, out _);
        var input = ServerProtocolRules.NormalizeInputState("RU|DROP");

        Assert.True(valid);
        Assert.Equal("Player_01", nickname);
        Assert.False(invalid);
        Assert.Equal("UDR", input);
    }

    [Fact]
    public void ProtocolMessageParser_ParsesStateMessage()
    {
        const string message =
            "STATE|PLAYING|P:0:10:20:3:Alpha:Red;P:1:30:40:5:Beta:Blue|C:50:60;C:70:80|S:90:100;S:110:120|W:130:140:50:60";

        Assert.True(ProtocolMessageParser.TryParseStateMessage(message, out var state));
        Assert.Equal("PLAYING", state.Phase);
        Assert.Collection(
            state.Players,
            player =>
            {
                Assert.Equal(0, player.Id);
                Assert.Equal("Alpha", player.Name);
                Assert.Equal(3, player.Score);
            },
            player =>
            {
                Assert.Equal(1, player.Id);
                Assert.Equal("Beta", player.Name);
                Assert.Equal("Blue", player.Color);
            }
        );
        Assert.Equal(2, state.Coins.Count);
        Assert.Equal(2, state.SnakeSegments.Count);
        Assert.Single(state.Walls);
    }

    [Fact]
    public async Task StopAsync_StopsListenerAndDisconnectsClients()
    {
        var server = new GameServer(port: 0);
        server.StartListening();

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, server.ListeningPort);

        using var stream = tcpClient.GetStream();
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream) { AutoFlush = true };

        await writer.WriteLineAsync("JOIN|Alpha");

        var joinResponse = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(joinResponse);
        Assert.StartsWith("JOIN_OK|", joinResponse);

        await server.StopAsync();

        Assert.False(server.IsRunning);
        Assert.Null(await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)));

        using var secondClient = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(() => secondClient.ConnectAsync(IPAddress.Loopback, server.ListeningPort));
    }

    private static GameState GetServerState(GameServer server)
    {
        var field = typeof(GameServer).GetField("gameState", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<GameState>(field!.GetValue(server));
    }

    private static void InvokePrivateMethod(object target, string methodName, object?[] parameters)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(target, parameters);
    }

    private static void AssertPlayersAreSafe(GameState state, IEnumerable<Player> players)
    {
        foreach (var player in players)
        {
            Assert.DoesNotContain(
                state.Walls,
                wall => Overlap(
                    player.X,
                    player.Y,
                    GameState.PlayerSize,
                    GameState.PlayerSize,
                    wall.X,
                    wall.Y,
                    wall.Width,
                    wall.Height
                )
            );
        }
    }

    private static void AssertSnakeIsSafe(GameState state, IEnumerable<Player> players)
    {
        foreach (var player in players)
        {
            Assert.DoesNotContain(
                state.Snake.Segments,
                segment => Overlap(
                    player.X,
                    player.Y,
                    GameState.PlayerSize,
                    GameState.PlayerSize,
                    segment.X,
                    segment.Y,
                    Snake.SegmentSize,
                    Snake.SegmentSize
                )
            );
        }
    }

    private static bool Overlap(
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

    private sealed class TestClientConnection : ClientConnection
    {
        public List<string> SentMessages { get; } = new();

        public TestClientConnection(GameServer server, string nickname)
            : base(new TcpClient(), server)
        {
            Nickname = nickname;
        }

        public override void Send(string message)
        {
            SentMessages.Add(message);
        }
    }
}
