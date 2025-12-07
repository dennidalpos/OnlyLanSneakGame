using LanGameServer.Entities;

namespace LanGameServer.Gameplay;

public class Snake
{
    public List<SnakeSegment> Segments { get; } = new();
    private int directionX = 1;
    private int directionY = 0;
    private readonly Random random = new();
    private int moveCounter = 0;
    private const int MoveInterval = 3;
    private const int SegmentSize = 20;
    private const int SnakeLength = 20;
    private const int ChaseRadius = 400;

    public Snake(int startX, int startY)
    {
        for (int i = 0; i < SnakeLength; i++)
        {
            Segments.Add(new SnakeSegment { X = startX - i * SegmentSize, Y = startY });
        }
    }

    public void Update(int arenaWidth, int arenaHeight, List<Wall> walls, List<Player> players)
    {
        moveCounter++;
        if (moveCounter < MoveInterval)
            return;
        moveCounter = 0;

        var head = Segments[0];

        Player? target = null;
        int minDistSq = int.MaxValue;

        foreach (var p in players.OrderByDescending(p => p.Score))
        {
            int dxp = p.X - head.X;
            int dyp = p.Y - head.Y;
            int distSq = dxp * dxp + dyp * dyp;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                target = p;
            }
        }

        if (target != null && minDistSq <= ChaseRadius * ChaseRadius)
        {
            int dxp = target.X - head.X;
            int dyp = target.Y - head.Y;
            if (Math.Abs(dxp) > Math.Abs(dyp))
            {
                directionX = Math.Sign(dxp);
                directionY = 0;
            }
            else
            {
                directionX = 0;
                directionY = Math.Sign(dyp);
            }
        }
        else
        {
            if (random.Next(0, 10) < 3)
            {
                var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
                var dir = directions[random.Next(directions.Length)];
                directionX = dir.Item1;
                directionY = dir.Item2;
            }
        }

        var newX = head.X + directionX * SegmentSize;
        var newY = head.Y + directionY * SegmentSize;

        if (
            newX < 0
            || newX >= arenaWidth - SegmentSize
            || newY < 0
            || newY >= arenaHeight - SegmentSize
        )
        {
            directionX = -directionX;
            directionY = -directionY;
            newX = head.X + directionX * SegmentSize;
            newY = head.Y + directionY * SegmentSize;
        }

        foreach (var wall in walls)
        {
            if (
                newX < wall.X + wall.Width
                && newX + SegmentSize > wall.X
                && newY < wall.Y + wall.Height
                && newY + SegmentSize > wall.Y
            )
            {
                directionX = -directionX;
                directionY = -directionY;
                newX = head.X + directionX * SegmentSize;
                newY = head.Y + directionY * SegmentSize;
                break;
            }
        }

        Segments.Insert(0, new SnakeSegment { X = newX, Y = newY });
        Segments.RemoveAt(Segments.Count - 1);
    }
}
