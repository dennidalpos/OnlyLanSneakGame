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
    private static void Main()
    {
        var server = new GameServer();
        server.Start();
    }
}
