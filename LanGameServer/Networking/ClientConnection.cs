using System.IO;
using System.Net.Sockets;
using LanGameServer.Entities;
using LanGameServer.Gameplay;

namespace LanGameServer.Networking;

public class ClientConnection
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
