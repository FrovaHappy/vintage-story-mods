using SeraphQoL.Common;
using SeraphQoL.Config;

namespace SeraphQoL.Inventory;

/// <summary>
/// Politica de elegibilidad de bloques contenedores para Store Nearby.
/// </summary>
public static class ContainerWhitelistPolicy
{
    private static readonly string[] BuiltInWhitelist =
    [
        "game:chest-*",
        "game:crate",
        "game:labeledchest-*",
        "game:stationarybasket-*",
        "game:storagevessel-*",
        "game:trunk-*",
        "bettercrates:*"
    ];

    public static bool IsAllowed(string? blockCode, EffectiveServerPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(blockCode)) return false;

        if (CodePatternMatcher.MatchesAny(blockCode, policy.ContainerBlacklist))
        {
            return false;
        }

        return CodePatternMatcher.MatchesAny(blockCode, BuiltInWhitelist)
            || CodePatternMatcher.MatchesAny(blockCode, policy.AdditionalContainerWhitelist);
    }
}
