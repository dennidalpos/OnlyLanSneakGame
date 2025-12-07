using LanGameServer.Gameplay;

namespace LanGameServer;

internal class Program
{
    private static void Main()
    {
        var server = new GameServer();
        server.Start();
    }
}
