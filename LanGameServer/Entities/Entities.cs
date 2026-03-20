/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

namespace LanGameServer.Entities;

public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Score { get; set; }
}

public class Coin
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class SnakeSegment
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class Wall
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
