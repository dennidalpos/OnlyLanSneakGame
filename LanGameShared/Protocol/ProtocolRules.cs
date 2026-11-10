/*
 * OnlyLanSneakGame
 * Copyright (c) 2026 Danny Perondi. All rights reserved.
 * Proprietary and confidential. Unauthorized use, copying, modification,
 * distribution, sublicensing, or disclosure is prohibited without prior
 * written permission from Danny Perondi.
 */

using System.Text;

namespace LanGameShared.Protocol;

public static class ProtocolRules
{
    public const int MaxNicknameLength = 16;
    private static readonly char[] ForbiddenNicknameChars = ['|', ';', ':', '\r', '\n'];

    public static bool TryNormalizeNickname(
        string? rawNickname,
        out string normalized,
        out string errorMessage
    )
    {
        normalized = (rawNickname ?? string.Empty).Trim();

        if (normalized.Length == 0)
        {
            errorMessage = "Nickname required";
            return false;
        }

        if (normalized.Length > MaxNicknameLength)
        {
            errorMessage = $"Nickname max {MaxNicknameLength} chars";
            return false;
        }

        if (normalized.IndexOfAny(ForbiddenNicknameChars) >= 0)
        {
            errorMessage = "Nickname contains unsupported separators";
            return false;
        }

        foreach (var character in normalized)
        {
            if (!(char.IsLetterOrDigit(character) || character is ' ' or '-' or '_'))
            {
                errorMessage = "Nickname supports letters, numbers, spaces, '-' and '_'";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    public static string NormalizeInputState(string? rawInput)
    {
        if (string.IsNullOrEmpty(rawInput))
            return string.Empty;

        var builder = new StringBuilder(4);
        AppendIfPresent(builder, rawInput, 'U');
        AppendIfPresent(builder, rawInput, 'D');
        AppendIfPresent(builder, rawInput, 'L');
        AppendIfPresent(builder, rawInput, 'R');
        return builder.ToString();
    }

    private static void AppendIfPresent(StringBuilder builder, string rawInput, char direction)
    {
        if (rawInput.IndexOf(direction) >= 0)
        {
            builder.Append(direction);
        }
    }
}
