/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

using System.IO;
using System.Net;
using System.Net.Sockets;
using LanGameServer.Gameplay;
using Xunit;

namespace OnlyLanSneakGame.Tests;

public class NetworkingIntegrationTests
{
    [Fact]
    public async Task Handshake_WithValidNickname_ReturnsJoinOk()
    {
        var server = new GameServer(port: 0);
        server.StartListening();

        try
        {
            await using var client = await TcpTestClient.ConnectAsync(server.ListeningPort);

            await client.SendLineAsync("JOIN|Alpha");

            var response = await client.ReadLineAsync();
            Assert.NotNull(response);
            Assert.StartsWith("JOIN_OK|", response);

            var parts = response.Split('|', 3);
            Assert.Equal(3, parts.Length);
            Assert.True(int.TryParse(parts[1], out var playerId));
            Assert.InRange(playerId, 0, 3);
            Assert.Contains(parts[2], new[] { "Red", "Blue", "Green", "Yellow" });
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task Handshake_WhenLobbyIsFull_ReturnsJoinFull()
    {
        var server = new GameServer(port: 0);
        server.StartListening();
        var connectedClients = new List<TcpTestClient>();

        try
        {
            foreach (var nickname in new[] { "Alpha", "Beta", "Gamma", "Delta" })
            {
                var client = await TcpTestClient.ConnectAsync(server.ListeningPort);
                connectedClients.Add(client);

                await client.SendLineAsync($"JOIN|{nickname}");

                var response = await client.ReadLineAsync();
                Assert.NotNull(response);
                Assert.StartsWith("JOIN_OK|", response);
            }

            await using var extraClient = await TcpTestClient.ConnectAsync(server.ListeningPort);
            await extraClient.SendLineAsync("JOIN|Echo");

            Assert.Equal("JOIN_FULL", await extraClient.ReadLineAsync());
            Assert.Null(await extraClient.ReadLineAsync());
        }
        finally
        {
            foreach (var client in connectedClients)
            {
                await client.DisposeAsync();
            }

            await server.StopAsync();
        }
    }

    [Fact]
    public async Task Handshake_WhenNicknameIsInvalid_ReturnsJoinInvalidAndClosesConnection()
    {
        var server = new GameServer(port: 0);
        server.StartListening();

        try
        {
            await using var client = await TcpTestClient.ConnectAsync(server.ListeningPort);

            await client.SendLineAsync("JOIN|Bad:Name");

            var response = await client.ReadLineAsync();
            Assert.NotNull(response);
            Assert.StartsWith("JOIN_INVALID|", response);
            Assert.Equal("JOIN_INVALID|Nickname contains unsupported separators", response);
            Assert.Null(await client.ReadLineAsync());
        }
        finally
        {
            await server.StopAsync();
        }
    }

    private sealed class TcpTestClient : IAsyncDisposable
    {
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);
        private readonly TcpClient tcpClient;
        private readonly StreamReader reader;
        private readonly StreamWriter writer;

        private TcpTestClient(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
            var stream = tcpClient.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream) { AutoFlush = true };
        }

        public static async Task<TcpTestClient> ConnectAsync(int port)
        {
            var tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            await tcpClient.ConnectAsync(IPAddress.Loopback, port);
            return new TcpTestClient(tcpClient);
        }

        public Task SendLineAsync(string line)
        {
            return writer.WriteLineAsync(line);
        }

        public Task<string?> ReadLineAsync()
        {
            return reader.ReadLineAsync().WaitAsync(ReadTimeout);
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                tcpClient.Client.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }

            try
            {
                reader.Dispose();
            }
            catch
            {
            }

            try
            {
                writer.Dispose();
            }
            catch
            {
            }

            try
            {
                tcpClient.Dispose();
            }
            catch
            {
            }

            return ValueTask.CompletedTask;
        }
    }
}
