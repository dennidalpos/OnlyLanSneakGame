/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

using System.IO;
using System.Net.Sockets;
using LanGameServer.Entities;
using LanGameServer.Gameplay;
using LanGameShared.Protocol;

namespace LanGameServer.Networking;

public class ClientConnection
{
    private readonly TcpClient tcpClient;
    private readonly GameServer server;
    private StreamReader? reader;
    private StreamWriter? writer;

    public string Nickname { get; protected set; } = "";
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
                var joinParts = joinMsg.Split('|', 2);
                var requestedNickname = joinParts.Length > 1 ? joinParts[1] : string.Empty;
                if (
                    !ProtocolRules.TryNormalizeNickname(
                        requestedNickname,
                        out var normalizedNickname,
                        out var errorMessage
                    )
                )
                {
                    Send($"JOIN_INVALID|{errorMessage}");
                    return;
                }

                Nickname = normalizedNickname;
                if (!server.AddClient(this))
                    return;

                while (tcpClient.Connected)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null)
                        break;

                    if (line.StartsWith("INPUT|"))
                    {
                        var inputParts = line.Split('|', 2);
                        var inputState = inputParts.Length > 1 ? inputParts[1] : string.Empty;
                        server.HandleInput(this, inputState);
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

    public virtual void Send(string message)
    {
        try
        {
            writer?.WriteLine(message);
        }
        catch { }
    }
}
