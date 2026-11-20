namespace SeraphQoL.Common;

/// <summary>
/// Evaluador reutilizable de patrones de codigo con soporte para comodines '*'.
/// </summary>
public static class CodePatternMatcher
{
    public static bool MatchesAny(string? code, IEnumerable<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;

        foreach (string pattern in patterns)
        {
            if (Matches(code, pattern)) return true;
        }

        return false;
    }

    public static bool Matches(string code, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        string trimmed = pattern.Trim();
        if (trimmed == "*") return true;

        if (!trimmed.Contains('*'))
        {
            return string.Equals(code, trimmed, StringComparison.OrdinalIgnoreCase);
        }

        string[] parts = trimmed.Split('*');
        int pos = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0) continue;

            if (i == 0 && !trimmed.StartsWith('*'))
            {
                if (!code.StartsWith(part, StringComparison.OrdinalIgnoreCase)) return false;
                pos = part.Length;
                continue;
            }

            if (i == parts.Length - 1 && !trimmed.EndsWith('*'))
            {
                return pos <= code.Length - part.Length
                    && code.EndsWith(part, StringComparison.OrdinalIgnoreCase);
            }

            int found = code.IndexOf(part, pos, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return false;
            pos = found + part.Length;
        }

        return true;
    }
}
