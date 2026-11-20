namespace SeraphQoL.Client;

/// <summary>
/// Acorta titulos largos de contenedores ("Contenidos del cofre" -> "Cofre", "Chest Contents" -> "Chest")
/// para liberar espacio en la barra de titulo junto a los botones de inventario.
/// </summary>
public static class ContainerTitleFormatter
{
    private static readonly string[] Prefixes =
    [
        "contenidos de las ",
        "contenidos de los ",
        "contenidos del ",
        "contenidos de la ",
        "contenidos de ",
        "contenido de las ",
        "contenido de los ",
        "contenido del ",
        "contenido de ",
        "contents of the ",
        "contents of ",
        "содержимое "
    ];

    private static readonly string[] Suffixes =
    [
        " contents",
        " inventory"
    ];

    public static string ShortenTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return string.Empty;

        string trimmed = rawTitle.Trim();
        foreach (string prefix in Prefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > prefix.Length)
            {
                return CapitalizeFirst(trimmed[prefix.Length..].Trim());
            }
        }

        foreach (string suffix in Suffixes)
        {
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > suffix.Length)
            {
                return CapitalizeFirst(trimmed[..^suffix.Length].Trim());
            }
        }

        return trimmed;
    }

    private static string CapitalizeFirst(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length == 1) return char.ToUpper(value[0]).ToString();
        return char.ToUpper(value[0]) + value[1..];
    }
}
