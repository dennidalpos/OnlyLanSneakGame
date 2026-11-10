/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

using System.Globalization;
using LanGameShared.Models;

namespace LanGameShared.Protocol;

public static class ProtocolMessageParser
{
    public static bool TryParseStateMessage(string message, out StateMessageInfo state)
    {
        state = new StateMessageInfo();

        if (!message.StartsWith("STATE|", StringComparison.Ordinal))
            return false;

        var parts = message.Split('|', 6);
        if (parts.Length < 6)
            return false;

        state = new StateMessageInfo
        {
            Phase = parts[1],
            Players = ParsePlayers(parts[2]),
            Coins = ParseCoins(parts[3]),
            SnakeSegments = ParseSnake(parts[4]),
            Walls = ParseWalls(parts[5]),
        };

        return true;
    }

    public static bool TryParseGameOverMessage(string message, out GameOverSummaryInfo summary)
    {
        summary = new GameOverSummaryInfo();

        if (!message.StartsWith("GAME_OVER|", StringComparison.Ordinal))
            return false;

        var parts = message.Split('|', 3);
        if (
            parts.Length < 3
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var winnerId)
        )
        {
            return false;
        }

        summary = new GameOverSummaryInfo
        {
            WinnerId = winnerId,
            Ranking = ParseGameOverEntries(parts[2]),
        };

        return true;
    }

    private static List<PlayerInfo> ParsePlayers(string data)
    {
        var players = new List<PlayerInfo>();
        if (string.IsNullOrEmpty(data))
            return players;

        foreach (var playerStr in data.Split(';'))
        {
            var parts = playerStr.Split(':');
            if (parts.Length < 7 || parts[0] != "P")
                continue;

            if (
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                || !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score)
            )
            {
                continue;
            }

            players.Add(
                new PlayerInfo
                {
                    Id = id,
                    X = x,
                    Y = y,
                    Score = score,
                    Name = parts[5],
                    Color = parts[6],
                }
            );
        }

        return players;
    }

    private static List<CoinInfo> ParseCoins(string data)
    {
        var coins = new List<CoinInfo>();
        if (string.IsNullOrEmpty(data))
            return coins;

        foreach (var coinStr in data.Split(';'))
        {
            var parts = coinStr.Split(':');
            if (parts.Length < 3 || parts[0] != "C")
                continue;

            if (
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            )
            {
                continue;
            }

            coins.Add(new CoinInfo { X = x, Y = y });
        }

        return coins;
    }

    private static List<SnakeSegmentInfo> ParseSnake(string data)
    {
        var segments = new List<SnakeSegmentInfo>();
        if (string.IsNullOrWhiteSpace(data))
            return segments;

        foreach (var segmentStr in data.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(segmentStr))
                continue;

            var parts = segmentStr.Split(':');
            if (parts.Length < 3 || parts[0] != "S")
                continue;

            if (
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            )
            {
                continue;
            }

            segments.Add(new SnakeSegmentInfo { X = x, Y = y });
        }

        return segments;
    }

    private static List<WallInfo> ParseWalls(string data)
    {
        var walls = new List<WallInfo>();
        if (string.IsNullOrWhiteSpace(data))
            return walls;

        foreach (var wallStr in data.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(wallStr))
                continue;

            var parts = wallStr.Split(':');
            if (parts.Length < 5 || parts[0] != "W")
                continue;

            if (
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                || !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            )
            {
                continue;
            }

            walls.Add(
                new WallInfo
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                }
            );
        }

        return walls;
    }

    private static List<GameOverEntryInfo> ParseGameOverEntries(string data)
    {
        var entries = new List<GameOverEntryInfo>();
        if (string.IsNullOrWhiteSpace(data))
            return entries;

        foreach (var entryStr in data.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(entryStr))
                continue;

            var parts = entryStr.Split(':', 2);
            if (
                parts.Length == 2
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score)
            )
            {
                entries.Add(
                    new GameOverEntryInfo
                    {
                        Name = parts[0],
                        Score = score,
                    }
                );
            }
        }

        return entries;
    }
}
