using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using LanGameClient.Models;

namespace LanGameClient;

public class NetworkClient
{
    private readonly string serverIp;
    private readonly int serverPort;
    private readonly string nickname;
    private readonly MainForm mainForm;
    private TcpClient? tcpClient;
    private StreamReader? reader;
    private StreamWriter? writer;
    private bool connected = false;
    private int localPlayerId = -1;

    public NetworkClient(string serverIp, int serverPort, string nickname, MainForm mainForm)
    {
        this.serverIp = serverIp;
        this.serverPort = serverPort;
        this.nickname = nickname;
        this.mainForm = mainForm;
    }

    public async void Connect()
    {
        try
        {
            tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            await tcpClient.ConnectAsync(serverIp, serverPort);

            var stream = tcpClient.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream) { AutoFlush = true };

            await writer.WriteLineAsync($"JOIN|{nickname}");

            var response = await reader.ReadLineAsync();
            if (response?.StartsWith("JOIN_OK") == true)
            {
                var parts = response.Split('|');
                if (parts.Length >= 3 && int.TryParse(parts[1], out var id))
                {
                    localPlayerId = id;
                    mainForm.SetLocalPlayerId(id);
                }

                connected = true;
                _ = Task.Run(ReceiveMessages);
            }
            else if (response == "JOIN_FULL")
            {
                MessageBox.Show("Server is full (4 players max)", "Connection Error");
                mainForm.Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Connection failed: {ex.Message}", "Connection Error");
            mainForm.Close();
        }
    }

    private async Task ReceiveMessages()
    {
        try
        {
            if (reader == null)
                return;

            while (connected && tcpClient?.Connected == true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                    break;

                ProcessMessage(line);
            }
        }
        catch { }
        finally
        {
            connected = false;
            if (!mainForm.IsDisposed && mainForm.IsHandleCreated)
            {
                mainForm.BeginInvoke(new Action(() => mainForm.Close()));
            }
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            var parts = message.Split('|');

            if (parts[0] == "STATE" && parts.Length >= 6)
            {
                var phase = parts[1];
                var players = ParsePlayers(parts[2]);
                var coins = ParseCoins(parts[3]);
                var snake = ParseSnake(parts[4]);
                var walls = ParseWalls(parts[5]);

                if (!mainForm.IsHandleCreated || mainForm.IsDisposed)
                    return;

                mainForm.BeginInvoke(
                    new Action(() => mainForm.UpdateGameState(phase, players, coins, snake, walls))
                );
            }
        }
        catch { }
    }

    private List<PlayerInfo> ParsePlayers(string data)
    {
        var players = new List<PlayerInfo>();
        if (string.IsNullOrEmpty(data))
            return players;

        foreach (var playerStr in data.Split(';'))
        {
            var parts = playerStr.Split(':');
            if (parts.Length >= 7 && parts[0] == "P")
            {
                players.Add(
                    new PlayerInfo
                    {
                        Id = int.Parse(parts[1], CultureInfo.InvariantCulture),
                        X = int.Parse(parts[2], CultureInfo.InvariantCulture),
                        Y = int.Parse(parts[3], CultureInfo.InvariantCulture),
                        Score = int.Parse(parts[4], CultureInfo.InvariantCulture),
                        Name = parts[5],
                        Color = parts[6],
                    }
                );
            }
        }
        return players;
    }

    private List<CoinInfo> ParseCoins(string data)
    {
        var coins = new List<CoinInfo>();
        if (string.IsNullOrEmpty(data))
            return coins;

        foreach (var coinStr in data.Split(';'))
        {
            var parts = coinStr.Split(':');
            if (parts.Length >= 3 && parts[0] == "C")
            {
                coins.Add(
                    new CoinInfo
                    {
                        X = int.Parse(parts[1], CultureInfo.InvariantCulture),
                        Y = int.Parse(parts[2], CultureInfo.InvariantCulture),
                    }
                );
            }
        }
        return coins;
    }

    private List<SnakeSegmentInfo> ParseSnake(string data)
    {
        var segments = new List<SnakeSegmentInfo>();
        if (string.IsNullOrEmpty(data) || string.IsNullOrWhiteSpace(data))
            return segments;

        foreach (var segStr in data.Split(';'))
        {
            if (string.IsNullOrEmpty(segStr))
                continue;
            var parts = segStr.Split(':');
            if (parts.Length >= 3 && parts[0] == "S")
            {
                segments.Add(
                    new SnakeSegmentInfo
                    {
                        X = int.Parse(parts[1], CultureInfo.InvariantCulture),
                        Y = int.Parse(parts[2], CultureInfo.InvariantCulture),
                    }
                );
            }
        }
        return segments;
    }

    private List<WallInfo> ParseWalls(string data)
    {
        var walls = new List<WallInfo>();
        if (string.IsNullOrEmpty(data) || string.IsNullOrWhiteSpace(data))
            return walls;

        foreach (var wallStr in data.Split(';'))
        {
            if (string.IsNullOrEmpty(wallStr))
                continue;
            var parts = wallStr.Split(':');
            if (parts.Length >= 5 && parts[0] == "W")
            {
                walls.Add(
                    new WallInfo
                    {
                        X = int.Parse(parts[1], CultureInfo.InvariantCulture),
                        Y = int.Parse(parts[2], CultureInfo.InvariantCulture),
                        Width = int.Parse(parts[3], CultureInfo.InvariantCulture),
                        Height = int.Parse(parts[4], CultureInfo.InvariantCulture),
                    }
                );
            }
        }
        return walls;
    }

    public void SendInput(string input)
    {
        try
        {
            writer?.WriteLine($"INPUT|{input}");
        }
        catch { }
    }

    public void SendRestart()
    {
        try
        {
            writer?.WriteLine("RESTART");
        }
        catch { }
    }

    public void Disconnect()
    {
        connected = false;
        tcpClient?.Close();
    }
}
