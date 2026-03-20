/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using LanGameShared.Protocol;

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
    private bool connected;

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

            if (!ProtocolRules.TryNormalizeNickname(nickname, out var normalizedNickname, out var error))
            {
                MessageBox.Show(error, "Connection Error");
                mainForm.Close();
                return;
            }

            await writer.WriteLineAsync($"JOIN|{normalizedNickname}");

            var response = await reader.ReadLineAsync();
            if (response?.StartsWith("JOIN_OK", StringComparison.Ordinal) == true)
            {
                var parts = response.Split('|', 3);
                if (parts.Length >= 3 && int.TryParse(parts[1], out var id))
                {
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
            else if (response?.StartsWith("JOIN_INVALID|", StringComparison.Ordinal) == true)
            {
                var parts = response.Split('|', 2);
                var reason = parts.Length == 2 ? parts[1] : "Nickname rejected by server";
                MessageBox.Show(reason, "Connection Error");
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
            if (ProtocolMessageParser.TryParseStateMessage(message, out var state))
            {
                if (!mainForm.IsHandleCreated || mainForm.IsDisposed)
                    return;

                mainForm.BeginInvoke(
                    new Action(() =>
                        mainForm.UpdateGameState(
                            state.Phase,
                            state.Players,
                            state.Coins,
                            state.SnakeSegments,
                            state.Walls
                        )
                    )
                );
            }
            else if (ProtocolMessageParser.TryParseGameOverMessage(message, out var summary))
            {
                if (!mainForm.IsHandleCreated || mainForm.IsDisposed)
                    return;

                mainForm.BeginInvoke(
                    new Action(() => mainForm.UpdateGameOverSummary(summary.WinnerId, summary.Ranking))
                );
            }
        }
        catch { }
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
