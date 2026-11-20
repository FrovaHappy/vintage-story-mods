using Vintagestory.API.Config;

namespace SeraphQoL.Config;

/// <summary>
/// Centraliza la resolucion de traducciones bajo el dominio seraphqol con fallback seguro en pruebas.
/// </summary>
public static class SeraphQoLLang
{
    public const string Domain = "seraphqol";

    private static bool IsLangInitialized =>
        !string.IsNullOrEmpty(Lang.CurrentLocale)
        && Lang.AvailableLanguages.ContainsKey(Lang.CurrentLocale);

    public static string Get(string key, params object[] args) =>
        IsLangInitialized
            ? Lang.Get($"{Domain}:{key}", args)
            : key;

    public static string GetSortOrderName(SortOrder order)
    {
        string key = $"sort-order-{order.ToString().ToLowerInvariant()}";
        string fullKey = $"{Domain}:{key}";
        return IsLangInitialized && Lang.HasTranslation(fullKey) ? Lang.Get(fullKey) : order.ToString();
    }
}
