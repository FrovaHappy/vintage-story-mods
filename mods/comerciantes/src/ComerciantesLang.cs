using Vintagestory.API.Config;

namespace Comerciantes;

/// <summary>
/// Helpers centralizados de localizacion (i18n) bajo el dominio tradersoverhaul.
/// </summary>
public static class ComerciantesLang
{
    public const string Domain = "tradersoverhaul";

    private static bool IsLangInitialized =>
        !string.IsNullOrEmpty(Lang.CurrentLocale)
        && Lang.AvailableLanguages.ContainsKey(Lang.CurrentLocale);

    public static string Get(string key, params object[] args) =>
        IsLangInitialized
            ? Lang.Get($"{Domain}:{key}", args)
            : key;

    public static string GetArchetypeName(string? archetype)
    {
        if (string.IsNullOrWhiteSpace(archetype))
        {
            return Get("trader-default-name");
        }

        string fullKey = $"{Domain}:archetype-{archetype.Trim().ToLowerInvariant()}";
        return IsLangInitialized && Lang.HasTranslation(fullKey) ? Lang.Get(fullKey) : archetype;
    }

    public static string GetEconomyModeName(string? modeCodeOrLabel)
    {
        if (string.IsNullOrWhiteSpace(modeCodeOrLabel))
        {
            return Get("mode-balanced");
        }

        string fullKey = $"{Domain}:mode-{modeCodeOrLabel.Trim().ToLowerInvariant()}";
        return IsLangInitialized && Lang.HasTranslation(fullKey) ? Lang.Get(fullKey) : modeCodeOrLabel;
    }
}
