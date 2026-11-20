using SeraphQoL.Common;
using SeraphQoL.Config;

namespace SeraphQoL.Fauna;

/// <summary>
/// Clasifica entidades en categorias de caza excluyendo jugadores, NPCs, comerciantes y criaturas temporales.
/// </summary>
public sealed class FaunaClassifier
{
    private static readonly string[] TemporalTokens =
    [
        "drifter", "locust", "shiver", "bowtorn", "eidolon", "bell", "temporal"
    ];

    private static readonly string[] PredatorTokens =
    [
        "wolf", "hyena", "bear", "lion", "panther", "tiger", "cougar", "lynx", "wolverine", "jackal", "crocodile", "alligator"
    ];

    private static readonly string[] SmallPreyTokens =
    [
        "hare", "rabbit", "chicken", "rooster", "hen", "chick", "fox", "raccoon", "hedgehog", "squirrel", "duck", "goose", "pheasant", "pigeon", "turkey", "salmon", "trout", "bass", "fish"
    ];

    private static readonly string[] MediumLargePreyTokens =
    [
        "deer", "doe", "buck", "stag", "fawn", "goat", "ibex", "sheep", "ram", "ewe", "pig", "boar", "sow", "moose", "elk", "gazelle", "antelope", "bison", "aurochs", "cow", "bull", "horse"
    ];

    public FaunaCategory Classify(FaunaEntityDescriptor entity, EffectiveServerPolicy policy)
    {
        if (!policy.EnableFaunaRebalance || string.IsNullOrWhiteSpace(entity.Code))
        {
            return FaunaCategory.Excluded;
        }

        if (entity.IsPlayer || entity.IsTraderOrNpc || IsTemporal(entity))
        {
            return FaunaCategory.Excluded;
        }

        if (CodePatternMatcher.MatchesAny(entity.Code, policy.FaunaBlacklist))
        {
            return FaunaCategory.Excluded;
        }

        bool isWhitelisted = CodePatternMatcher.MatchesAny(entity.Code, policy.FaunaWhitelist);

        if (!isWhitelisted)
        {
            if (entity.IsAquatic && !policy.IncludeAquaticFauna)
            {
                return FaunaCategory.Excluded;
            }

            if (!HasBehavior(entity, "health") || !HasBehavior(entity, "harvestable"))
            {
                return FaunaCategory.Excluded;
            }
        }

        if (ContainsAnyToken(entity, PredatorTokens))
        {
            return FaunaCategory.Predator;
        }

        if (ContainsAnyToken(entity, SmallPreyTokens))
        {
            return FaunaCategory.SmallPrey;
        }

        if (ContainsAnyToken(entity, MediumLargePreyTokens))
        {
            return FaunaCategory.MediumLargePrey;
        }

        // Heuristica para fauna de mods terceros no listada explicitamente
        return entity.MaxHealth switch
        {
            <= 10.0 => FaunaCategory.SmallPrey,
            <= 28.0 => FaunaCategory.MediumLargePrey,
            _ => FaunaCategory.Predator
        };
    }

    private static bool IsTemporal(FaunaEntityDescriptor entity)
    {
        if (entity.IsTemporalCreature) return true;
        return ContainsAnyToken(entity, TemporalTokens);
    }

    private static bool HasBehavior(FaunaEntityDescriptor entity, string code) =>
        entity.BehaviorCodes.Any(b => string.Equals(b, code, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAnyToken(FaunaEntityDescriptor entity, string[] tokens)
    {
        string codeLower = entity.Code.ToLowerInvariant();
        string classLower = entity.ClassName.ToLowerInvariant();

        foreach (string token in tokens)
        {
            if (codeLower.Contains(token, StringComparison.Ordinal) || classLower.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }

            if (entity.Tags.Any(t => t.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Clasifica codigos de drop de entidades cosechables en familias de recursos.
/// </summary>
public static class FaunaDropClassifier
{
    public static FaunaResourceKind Classify(string? itemCode)
    {
        if (string.IsNullOrWhiteSpace(itemCode)) return FaunaResourceKind.None;

        string lower = itemCode.ToLowerInvariant();
        if (lower.Contains("fat", StringComparison.Ordinal))
        {
            return FaunaResourceKind.Fat;
        }

        if (lower.Contains("hide-", StringComparison.Ordinal) || lower.Contains("pelt", StringComparison.Ordinal) || lower.Contains("leather", StringComparison.Ordinal))
        {
            return FaunaResourceKind.Hide;
        }

        if (lower.Contains("meat", StringComparison.Ordinal) || lower.Contains("poultry", StringComparison.Ordinal) || lower.Contains("fish", StringComparison.Ordinal) || lower.Contains("venison", StringComparison.Ordinal))
        {
            return FaunaResourceKind.Meat;
        }

        if (lower.Contains("feather", StringComparison.Ordinal)
            || lower.Contains("bone", StringComparison.Ordinal)
            || lower.Contains("horn", StringComparison.Ordinal)
            || lower.Contains("antler", StringComparison.Ordinal)
            || lower.Contains("tooth", StringComparison.Ordinal)
            || lower.Contains("claw", StringComparison.Ordinal)
            || lower.Contains("sinew", StringComparison.Ordinal))
        {
            return FaunaResourceKind.Byproduct;
        }

        return FaunaResourceKind.None;
    }
}
