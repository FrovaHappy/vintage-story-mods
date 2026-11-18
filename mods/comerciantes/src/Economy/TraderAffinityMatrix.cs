using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace Comerciantes;

public sealed class TraderAffinitiesFile
{
    [JsonProperty("defaultMultiplier")]
    public double DefaultMultiplier { get; set; } = 1.0;

    [JsonProperty("archetypes")]
    public Dictionary<string, Dictionary<string, double>> Archetypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Resuelve el multiplicador de afinidad entre un arquetipo de comerciante y un item.
/// </summary>
public sealed class TraderAffinityMatrix
{
    private double _defaultMultiplier = 1.0;
    private readonly Dictionary<string, Dictionary<string, double>> _archetypes = new(StringComparer.OrdinalIgnoreCase);

    public void LoadFromAssets(ICoreAPI api)
    {
        IAsset? asset = api.Assets.TryGet(new AssetLocation("tradersoverhaul", "config/trader_affinities.json"));
        if (asset == null)
        {
            api.Logger.Warning("[TradersOverhaul] No se encontro config/trader_affinities.json.");
            return;
        }

        TraderAffinitiesFile? parsed = asset.ToObject<TraderAffinitiesFile>();
        if (parsed != null)
        {
            Load(parsed);
        }
    }

    public void Load(TraderAffinitiesFile parsed)
    {
        _defaultMultiplier = parsed.DefaultMultiplier;
        _archetypes.Clear();
        foreach ((string archetype, Dictionary<string, double> map) in parsed.Archetypes)
        {
            _archetypes[archetype] = new Dictionary<string, double>(map, StringComparer.OrdinalIgnoreCase);
        }
    }

    public double GetAffinityMultiplier(string traderArchetype, CollectibleObject collectible)
    {
        if (!_archetypes.TryGetValue(traderArchetype, out Dictionary<string, double>? categories))
        {
            return _defaultMultiplier;
        }

        string category = ClassifyItemCategory(collectible);
        return categories.TryGetValue(category, out double mult) ? mult : _defaultMultiplier;
    }

    public static string ClassifyItemCategory(CollectibleObject collectible)
    {
        string path = collectible.Code?.Path ?? string.Empty;

        if (path.StartsWith("axe-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("knife-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("cleaver-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("bow-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("arrow-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("backpack-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("lantern-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("oillamp-", StringComparison.OrdinalIgnoreCase)
            || path.Equals("linensack", StringComparison.OrdinalIgnoreCase))
        {
            return "field_gear";
        }

        if (path.StartsWith("chandelier-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("paperlantern-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("chest-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("bed-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("glass-", StringComparison.OrdinalIgnoreCase))
        {
            return "furniture_glass";
        }

        if (path.StartsWith("ingot-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("nugget-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("metalplate-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("anvil-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("ore-", StringComparison.OrdinalIgnoreCase))
        {
            return path.Contains("gold", StringComparison.OrdinalIgnoreCase)
                || path.Contains("silver", StringComparison.OrdinalIgnoreCase)
                ? "luxuries_relics"
                : "base_metals";
        }

        if (path.StartsWith("blade-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("sword-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("spear-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("pickaxe-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("prospectingpick-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("shovel-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("snowshovel-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("shield-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("roundshield-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("armor-", StringComparison.OrdinalIgnoreCase))
        {
            return "weapons_mining";
        }

        if (path.StartsWith("saw-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("hammer-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("chisel-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("shears-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("wrench-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("tongs", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("crowbar-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("solderingiron-", StringComparison.OrdinalIgnoreCase))
        {
            return "crafting_tools";
        }

        if (path.StartsWith("hoe-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("scythe-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("seeds-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("legume-", StringComparison.OrdinalIgnoreCase))
        {
            return "farming";
        }

        if (path.StartsWith("bread-", StringComparison.OrdinalIgnoreCase)
            || path.Contains("cooked", StringComparison.OrdinalIgnoreCase))
        {
            return "food";
        }

        if (path.StartsWith("leather-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("linen-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("flaxtwine", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("pelt-", StringComparison.OrdinalIgnoreCase))
        {
            return "pelts_textiles";
        }

        if (path.StartsWith("clothes-", StringComparison.OrdinalIgnoreCase))
        {
            return path.Contains("noble", StringComparison.OrdinalIgnoreCase)
                || path.Contains("prince", StringComparison.OrdinalIgnoreCase)
                || path.Contains("aristocrat", StringComparison.OrdinalIgnoreCase)
                || path.Contains("royal", StringComparison.OrdinalIgnoreCase)
                ? "noble_clothing"
                : "work_clothing";
        }

        if (path.StartsWith("gem-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("painting-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("lore-", StringComparison.OrdinalIgnoreCase))
        {
            return "luxuries_relics";
        }

        if (path.StartsWith("clay-", StringComparison.OrdinalIgnoreCase)
            || path.Equals("fat", StringComparison.OrdinalIgnoreCase)
            || path.Equals("beeswax", StringComparison.OrdinalIgnoreCase)
            || path.Equals("charcoal", StringComparison.OrdinalIgnoreCase)
            || path.Equals("peatbrick", StringComparison.OrdinalIgnoreCase))
        {
            return "artisan_raw";
        }

        return "building_materials";
    }
}
