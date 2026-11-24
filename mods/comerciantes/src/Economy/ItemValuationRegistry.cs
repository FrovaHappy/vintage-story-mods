using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Comerciantes;

public sealed class ItemValuesFile
{
    [JsonProperty("blacklistPrefixes")]
    public List<string> BlacklistPrefixes { get; set; } = [];

    [JsonProperty("values")]
    public Dictionary<string, double> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record InventoryValuationHint(bool IsValuable, double BaseValue, int LotSize, int LotGearPayout);

/// <summary>
/// Resuelve el valor intrinseco base de cualquier CollectibleObject combinando:
/// 1) item_values.json + lista negra
/// 2) Listas de comerciantes vanilla (tradelists)
/// 3) Familias de objetos, metales y atributos (ej. faros/linternas)
/// 4) Propagacion automatica por recetas de crafteo (GridRecipes)
/// </summary>
public sealed class ItemValuationRegistry
{
    private readonly List<string> _blacklistPrefixes = [];
    private readonly Dictionary<string, double> _explicitValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _tradelistValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _recipeValues = new(StringComparer.OrdinalIgnoreCase);
    // Exact-match overrides from ComerciantesConfig.ItemValueOverrides (highest priority).
    private readonly Dictionary<string, double> _configOverrideExact = new(StringComparer.OrdinalIgnoreCase);
    // Wildcard (suffix-*) overrides sorted descending by prefix length so the longest prefix wins.
    private readonly List<(string Prefix, double Value)> _configOverrideWildcards = [];

    public void LoadFromAssets(ICoreAPI api)
    {
        LoadExplicitConfig(api);
        LoadVanillaTradelistValues(api);
        InferRecipeValues(api);
    }

    private void LoadExplicitConfig(ICoreAPI api)
    {
        IAsset? asset = api.Assets.TryGet(new AssetLocation("tradersoverhaul", "config/item_values.json"));
        if (asset == null)
        {
            api.Logger.Warning("[TradersOverhaul] No se encontro config/item_values.json; usando valores inferidos.");
            return;
        }

        ItemValuesFile? parsed = asset.ToObject<ItemValuesFile>();
        if (parsed != null)
        {
            ApplyExplicitConfig(parsed);
        }
    }

    public void ApplyExplicitConfig(ItemValuesFile parsed)
    {
        _blacklistPrefixes.Clear();
        _blacklistPrefixes.AddRange(parsed.BlacklistPrefixes);

        _explicitValues.Clear();
        foreach ((string key, double value) in parsed.Values)
        {
            _explicitValues[key] = value;
        }
    }

    /// <summary>
    /// Applies per-item value overrides from <see cref="ComerciantesConfig.ItemValueOverrides"/>.
    /// Keys ending in '*' are treated as prefix wildcards; the longest matching prefix wins.
    /// Exact keys always beat wildcards. Calling this again replaces all previous overrides.
    /// </summary>
    public void ApplyConfigOverrides(IReadOnlyDictionary<string, double> overrides)
    {
        _configOverrideExact.Clear();
        _configOverrideWildcards.Clear();

        foreach ((string key, double value) in overrides)
        {
            if (key.EndsWith('*'))
            {
                string prefix = key[..^1]; // strip trailing '*'
                _configOverrideWildcards.Add((prefix, value));
            }
            else
            {
                _configOverrideExact[key] = value;
            }
        }

        // Sort descending by prefix length so the longest (most specific) wildcard is checked first.
        _configOverrideWildcards.Sort((a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
    }

    private void LoadVanillaTradelistValues(ICoreAPI api)
    {
        _tradelistValues.Clear();
        Dictionary<AssetLocation, TradeProperties> tradelists = api.Assets.GetMany<TradeProperties>(api.Logger, "config/tradelists/", "survival");
        Dictionary<string, List<double>> buyingSamples = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<double>> sellingSamples = new(StringComparer.OrdinalIgnoreCase);

        foreach (TradeProperties props in tradelists.Values)
        {
            CollectSamples(props.Buying?.List, buyingSamples, 1.0);
            CollectSamples(props.Selling?.List, sellingSamples, 0.5);
        }

        foreach ((string code, List<double> samples) in sellingSamples)
        {
            _tradelistValues[code] = samples.Average();
        }
        foreach ((string code, List<double> samples) in buyingSamples)
        {
            _tradelistValues[code] = samples.Average();
        }
    }

    private static void CollectSamples(TradeItem[]? list, Dictionary<string, List<double>> target, double factor)
    {
        if (list == null) return;
        foreach (TradeItem item in list)
        {
            if (item?.Code == null || item.Price == null) continue;
            string fullCode = item.Code.Domain == "game" || !string.IsNullOrEmpty(item.Code.Domain)
                ? item.Code.ToString()
                : $"game:{item.Code.Path}";
            int stack = Math.Max(1, item.StackSize);
            double unitVal = (item.Price.avg / stack) * factor;
            if (unitVal <= 0) continue;

            if (!target.TryGetValue(fullCode, out List<double>? listVals))
            {
                listVals = [];
                target[fullCode] = listVals;
            }
            listVals.Add(unitVal);
        }
    }

    public void InferRecipeValues(ICoreAPI api)
    {
        _recipeValues.Clear();
        List<GridRecipe>? recipes = api.World?.GridRecipes;
        if (recipes == null || recipes.Count == 0) return;

        // Dos pasadas para propagar ingredientes intermedios (ej. tablones/paneles -> muebles/contenedores)
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (GridRecipe recipe in recipes)
            {
                if (!recipe.Enabled || recipe.Output?.ResolvedItemStack?.Collectible?.Code == null) continue;
                ItemStack outStack = recipe.Output.ResolvedItemStack;
                if (IsBlacklisted(outStack.Collectible)) continue;

                string outCode = outStack.Collectible.Code.ToString();
                if (_explicitValues.ContainsKey(outCode) || _tradelistValues.ContainsKey(outCode)) continue;

                double sum = 0.0;
                bool valid = true;
                CraftingRecipeIngredient?[]? ings = recipe.ResolvedIngredients;
                if (ings == null || ings.Length == 0) continue;

                foreach (CraftingRecipeIngredient? ing in ings)
                {
                    if (ing == null) continue;
                    if (ing.IsTool)
                    {
                        sum += 0.05;
                        continue;
                    }

                    double ingVal = 0.0;
                    if (ing.ResolvedItemStack != null)
                    {
                        ingVal = GetBaseValue(ing.ResolvedItemStack);
                    }
                    else if (ing.Code != null)
                    {
                        ingVal = EstimateByPath(ing.Code.Path, null) ?? 0.08;
                    }

                    if (ingVal <= 0.0)
                    {
                        valid = false;
                        break;
                    }
                    sum += ingVal * Math.Max(1, ing.Quantity);
                }

                if (!valid || sum <= 0.0) continue;

                double unitCost = (sum / Math.Max(1, outStack.StackSize)) * 0.85;
                if (unitCost > 0.02)
                {
                    if (!_recipeValues.TryGetValue(outCode, out double prev) || unitCost < prev)
                    {
                        _recipeValues[outCode] = unitCost;
                    }
                }
            }
        }
    }

    public bool IsBlacklisted(CollectibleObject? collectible)
    {
        if (collectible?.Code == null) return true;
        string fullCode = collectible.Code.ToString();
        if (fullCode.Equals("game:gear-rusty", StringComparison.OrdinalIgnoreCase)) return true;

        foreach (string prefix in _blacklistPrefixes)
        {
            if (fullCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public double GetBaseValue(ItemStack stack)
    {
        if (stack?.Collectible == null || IsBlacklisted(stack.Collectible)) return 0.0;

        string fullCode = stack.Collectible.Code.ToString();

        // 1. Exact config override (highest priority, can also zero-out an item)
        if (_configOverrideExact.TryGetValue(fullCode, out double configExact))
        {
            return configExact;
        }

        // 2. Wildcard config override (longest matching prefix wins)
        foreach ((string prefix, double wildcardValue) in _configOverrideWildcards)
        {
            if (fullCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return wildcardValue;
            }
        }

        // 3. Explicit asset value (item_values.json)
        if (_explicitValues.TryGetValue(fullCode, out double explicitVal))
        {
            return explicitVal;
        }

        // 4. Family/path heuristics
        double? familyVal = EstimateByPath(stack.Collectible.Code.Path, stack);
        if (familyVal.HasValue)
        {
            return familyVal.Value;
        }

        // 5. Vanilla tradelist average
        if (_tradelistValues.TryGetValue(fullCode, out double tradelistVal))
        {
            return tradelistVal;
        }

        // 6. Recipe-inferred value
        if (_recipeValues.TryGetValue(fullCode, out double recipeVal))
        {
            return recipeVal;
        }

        return stack.Collectible.ItemClass == EnumItemClass.Item ? 0.25 : 0.20;
    }

    public InventoryValuationHint GetInventoryValuationHint(ItemStack? stack)
    {
        if (stack?.Collectible == null) return new InventoryValuationHint(false, 0.0, 0, 0);
        double baseVal = GetBaseValue(stack);
        if (!PriceCalculator.TryComputeStandardizedLot(baseVal, stack.Collectible.MaxStackSize, out int lot, out int payout))
        {
            return new InventoryValuationHint(false, 0.0, 0, 0);
        }
        return new InventoryValuationHint(true, baseVal, lot, payout);
    }

    private static double? EstimateByPath(string path, ItemStack? stack)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // Iluminacion (faros/linternas, candelabros, lamparas)
        if (path.StartsWith("lantern-", StringComparison.OrdinalIgnoreCase))
        {
            string? mat = stack?.Attributes?.GetString("material");
            return GetMetalScale(mat ?? path, baseValue: 5.0);
        }
        if (path.StartsWith("paperlantern-", StringComparison.OrdinalIgnoreCase)) return 1.5;
        if (path.StartsWith("chandelier-", StringComparison.OrdinalIgnoreCase)) return 10.0;
        if (path.StartsWith("oillamp-", StringComparison.OrdinalIgnoreCase)) return 0.75;
        if (path.Equals("candle", StringComparison.OrdinalIgnoreCase)) return 0.5;
        if (path.StartsWith("torch-", StringComparison.OrdinalIgnoreCase)) return 0.05;

        // Engranaje temporal y reliquias
        if (path.Equals("gear-temporal", StringComparison.OrdinalIgnoreCase)) return 12.0;

        // Almacenamiento y muebles clave
        if (path.StartsWith("chest-", StringComparison.OrdinalIgnoreCase)) return 8.0;
        if (path.StartsWith("storagevessel-", StringComparison.OrdinalIgnoreCase)) return 4.0;
        if (path.StartsWith("crate", StringComparison.OrdinalIgnoreCase)) return 1.25;
        if (path.Equals("barrel", StringComparison.OrdinalIgnoreCase)) return 2.5;
        if (path.StartsWith("backpack-", StringComparison.OrdinalIgnoreCase)) return 5.0;
        if (path.StartsWith("bed-", StringComparison.OrdinalIgnoreCase)) return 2.5;
        if (path.StartsWith("door-", StringComparison.OrdinalIgnoreCase)) return 1.5;

        // Metales y herreria
        if (path.StartsWith("anvil-", StringComparison.OrdinalIgnoreCase)) return GetMetalScale(path, baseValue: 8.0);
        if (path.StartsWith("metalplate-", StringComparison.OrdinalIgnoreCase)) return GetMetalScale(path, baseValue: 2.0);
        if (path.StartsWith("ingot-", StringComparison.OrdinalIgnoreCase)) return GetMetalScale(path, baseValue: 1.5);
        if (path.StartsWith("nugget-", StringComparison.OrdinalIgnoreCase)) return GetMetalScale(path, baseValue: 0.15);
        if (path.StartsWith("ore-", StringComparison.OrdinalIgnoreCase)) return 0.2;
        if (path.StartsWith("metalnailsandstrips-", StringComparison.OrdinalIgnoreCase)) return 0.5;

        // Materiales comunes y alimentos
        if (path.StartsWith("gem-", StringComparison.OrdinalIgnoreCase)) return 10.0;
        if (path.StartsWith("painting-", StringComparison.OrdinalIgnoreCase)) return 9.0;
        if (path.StartsWith("planks-", StringComparison.OrdinalIgnoreCase)) return 0.08;
        if (path.StartsWith("log-", StringComparison.OrdinalIgnoreCase)) return 0.125;
        if (path.Equals("firewood", StringComparison.OrdinalIgnoreCase)) return 0.04;
        if (path.Equals("stick", StringComparison.OrdinalIgnoreCase)) return 0.02;
        if (path.StartsWith("burnedbrick-", StringComparison.OrdinalIgnoreCase)) return 0.25;
        if (path.StartsWith("shingle-", StringComparison.OrdinalIgnoreCase)) return 0.22;
        if (path.StartsWith("glass-", StringComparison.OrdinalIgnoreCase)) return 0.30;
        if (path.StartsWith("crock-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("cookingpot-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("jug-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("bowl-", StringComparison.OrdinalIgnoreCase)) return 0.40;
        if (path.StartsWith("pie-", StringComparison.OrdinalIgnoreCase)) return 1.0;

        // Herramientas, armas y armaduras
        if (stack?.Collectible != null)
        {
            return InferToolOrEquipmentValue(stack.Collectible, path);
        }

        return null;
    }

    private static double GetMetalScale(string hint, double baseValue)
    {
        if (hint.Contains("steel", StringComparison.OrdinalIgnoreCase)) return baseValue * 2.2;
        if (hint.Contains("meteoriciron", StringComparison.OrdinalIgnoreCase)) return baseValue * 1.8;
        if (hint.Contains("iron", StringComparison.OrdinalIgnoreCase)) return baseValue * 1.5;
        if (hint.Contains("gold", StringComparison.OrdinalIgnoreCase)) return baseValue * 1.6;
        if (hint.Contains("silver", StringComparison.OrdinalIgnoreCase)) return baseValue * 1.3;
        if (hint.Contains("blackbronze", StringComparison.OrdinalIgnoreCase)) return baseValue * 1.3;
        if (hint.Contains("bismuthbronze", StringComparison.OrdinalIgnoreCase)) return baseValue * 1.15;
        if (hint.Contains("tinbronze", StringComparison.OrdinalIgnoreCase) || hint.Contains("brass", StringComparison.OrdinalIgnoreCase)) return baseValue * 1.0;
        if (hint.Contains("copper", StringComparison.OrdinalIgnoreCase)) return baseValue * 0.7;
        return baseValue;
    }

    private static double? InferToolOrEquipmentValue(CollectibleObject collectible, string path)
    {
        bool isGearOrTool =
            collectible.Tool != null
            || path.StartsWith("axe-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("pickaxe-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("prospectingpick-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("shovel-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("snowshovel-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("hoe-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("scythe-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("saw-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("hammer-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("knife-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("cleaver-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("chisel-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("shears-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("wrench-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("tongs", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("crowbar-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("solderingiron-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("padlock-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("blade-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("spear-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("sword-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("shield-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("roundshield-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("armor-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("bow-", StringComparison.OrdinalIgnoreCase);

        if (!isGearOrTool)
        {
            if (path.StartsWith("clothes-", StringComparison.OrdinalIgnoreCase)) return 2.5;
            return null;
        }

        string metalVariant = collectible.Variant?["metal"]
            ?? collectible.Variant?["material"]
            ?? path;

        if (metalVariant.Contains("steel", StringComparison.OrdinalIgnoreCase)) return 10.0;
        if (metalVariant.Contains("meteoriciron", StringComparison.OrdinalIgnoreCase)) return 8.0;
        if (metalVariant.Contains("iron", StringComparison.OrdinalIgnoreCase)) return 7.0;
        if (metalVariant.Contains("gold", StringComparison.OrdinalIgnoreCase)) return 6.0;
        if (metalVariant.Contains("silver", StringComparison.OrdinalIgnoreCase)) return 5.0;
        if (metalVariant.Contains("blackbronze", StringComparison.OrdinalIgnoreCase)) return 6.0;
        if (metalVariant.Contains("bismuthbronze", StringComparison.OrdinalIgnoreCase)) return 5.0;
        if (metalVariant.Contains("tinbronze", StringComparison.OrdinalIgnoreCase)) return 4.0;
        if (metalVariant.Contains("copper", StringComparison.OrdinalIgnoreCase)) return 2.5;

        if (metalVariant.Contains("flint", StringComparison.OrdinalIgnoreCase)
            || metalVariant.Contains("chert", StringComparison.OrdinalIgnoreCase)
            || metalVariant.Contains("granite", StringComparison.OrdinalIgnoreCase)
            || metalVariant.Contains("basalt", StringComparison.OrdinalIgnoreCase)
            || metalVariant.Contains("andesite", StringComparison.OrdinalIgnoreCase)
            || metalVariant.Contains("obsidian", StringComparison.OrdinalIgnoreCase)
            || metalVariant.Contains("bone", StringComparison.OrdinalIgnoreCase)
            || metalVariant.Contains("wood", StringComparison.OrdinalIgnoreCase))
        {
            return 0.0;
        }

        return 1.5;
    }

    public double GetConditionMultiplier(ItemStack stack, ComerciantesConfig config)
    {
        if (stack?.Collectible == null) return 0.0;

        int maxDurability = stack.Collectible.GetMaxDurability(stack);
        if (maxDurability > 0)
        {
            double ratio = (double)stack.Collectible.GetRemainingDurability(stack) / maxDurability;
            if (ratio < config.MinDurabilityRatio)
            {
                return 0.0;
            }
            return Math.Clamp(ratio, 0.0, 1.0);
        }

        return 1.0;
    }
}
