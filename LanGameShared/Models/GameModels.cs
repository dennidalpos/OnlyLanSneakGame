/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

namespace LanGameShared.Models;

public class PlayerInfo
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Score { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
}

public class CoinInfo
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class SnakeSegmentInfo
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class WallInfo
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class GameOverEntryInfo
{
    public string Name { get; set; } = "";
    public int Score { get; set; }
}

public class StateMessageInfo
{
    public string Phase { get; set; } = "";
    public List<PlayerInfo> Players { get; set; } = new();
    public List<CoinInfo> Coins { get; set; } = new();
    public List<SnakeSegmentInfo> SnakeSegments { get; set; } = new();
    public List<WallInfo> Walls { get; set; } = new();
}

public class GameOverSummaryInfo
{
    public int WinnerId { get; set; }
    public List<GameOverEntryInfo> Ranking { get; set; } = new();
}
