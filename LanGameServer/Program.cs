/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

using LanGameServer.Gameplay;

namespace LanGameServer;

internal class Program
{
    private static async Task Main()
    {
        using var shutdownCts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdownCts.Cancel();
        };

        var server = new GameServer();
        await server.RunAsync(cancellationToken: shutdownCts.Token);
    }
}
