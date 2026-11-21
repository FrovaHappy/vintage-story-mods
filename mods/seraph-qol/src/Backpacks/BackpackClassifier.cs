namespace SeraphQoL.Backpacks;

/// <summary>
/// Clasificador puro de mochilas, bolsas especializadas, alforjas y mochilas de mods terceros.
/// </summary>
public sealed class BackpackClassifier
{
    private static readonly string[] SpecializedCodeTokens =
    [
        "miningbag",
        "quiver",
        "butcherybag",
        "woodpack",
        "foragebasket",
        "toolbag",
        "ammobag",
        "seedbag",
        "orebag"
    ];

    public BackpackCategory Classify(BackpackCollectibleDescriptor item)
    {
        if (string.IsNullOrWhiteSpace(item.Code) || item.BaseQuantitySlots <= 0)
        {
            return BackpackCategory.Excluded;
        }

        string codeLower = item.Code.ToLowerInvariant();
        string classLower = item.ClassName.ToLowerInvariant();

        if (classLower.Contains("glider", StringComparison.Ordinal)
            || codeLower.Contains("glider", StringComparison.Ordinal))
        {
            return BackpackCategory.Excluded;
        }

        if (codeLower.Contains("saddlebag", StringComparison.Ordinal))
        {
            return BackpackCategory.Saddlebags;
        }

        if (item.HasRestrictedStorageFlags || item.HasStorageTags || ContainsAny(codeLower, SpecializedCodeTokens))
        {
            return BackpackCategory.SpecializedBags;
        }

        string domain = ExtractDomain(codeLower);
        return string.Equals(domain, "game", StringComparison.Ordinal)
            ? BackpackCategory.StandardBackpacks
            : BackpackCategory.ModdedBackpacks;
    }

    private static string ExtractDomain(string codeLower)
    {
        int colonIdx = codeLower.IndexOf(':');
        return colonIdx > 0 ? codeLower[..colonIdx] : "game";
    }

    private static bool ContainsAny(string haystack, string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (haystack.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
