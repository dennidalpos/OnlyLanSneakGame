namespace LanGameClient.Models;

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
