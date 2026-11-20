using Newtonsoft.Json.Linq;
using SeraphQoL.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SeraphQoL.Fauna;

/// <summary>
/// Aplica el rebalanceo de Fauna QoL en AssetsFinalize (definiciones) y OnEntitySpawn (salvaje vs. domesticado).
/// </summary>
public sealed class FaunaAssetsService
{
    private readonly FaunaClassifier _classifier = new();
    private readonly FaunaBalanceCalculator _calculator = new();
    private readonly Dictionary<string, double> _baseMaxHealthByEntityCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _baseCurrentHealthRatioByEntityCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (double Avg, double Var)> _baseDropQuantities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FaunaCategory> _cachedCategoryByEntityCode = new(StringComparer.OrdinalIgnoreCase);

    public void ApplyToEntityTypes(ICoreServerAPI sapi, EffectiveServerPolicy policy)
    {
        int adjustedCount = 0;
        foreach (EntityProperties entityType in sapi.World.EntityTypes)
        {
            FaunaEntityDescriptor descriptor = BuildDescriptor(entityType, sapi);
            FaunaCategory category = _classifier.Classify(descriptor, policy);
            _cachedCategoryByEntityCode[descriptor.Code] = category;
            if (category == FaunaCategory.Excluded && !_baseMaxHealthByEntityCode.ContainsKey(descriptor.Code))
            {
                continue;
            }

            FaunaCategory effectiveCategory = policy.EnableFaunaRebalance ? category : FaunaCategory.Excluded;
            if (ScaleHealthBehavior(entityType, descriptor, effectiveCategory, policy))
            {
                ScaleHarvestableBehavior(entityType, descriptor.Code, effectiveCategory, policy);
                if (effectiveCategory != FaunaCategory.Excluded)
                {
                    adjustedCount++;
                }
            }
        }

        sapi.Logger.Notification("[SeraphQoL] Fauna rebalance applied to {0} entity types.", adjustedCount);
    }

    public void AdjustLoadedEntities(ICoreServerAPI sapi, EffectiveServerPolicy policy)
    {
        if (sapi.World?.LoadedEntities == null) return;
        foreach (Entity entity in sapi.World.LoadedEntities.Values)
        {
            AdjustSpawnedEntity(entity, policy);
        }
    }

    public void AdjustSpawnedEntity(Entity entity, EffectiveServerPolicy policy)
    {
        string? code = entity.Code?.ToString();
        if (code == null || !_baseMaxHealthByEntityCode.TryGetValue(code, out double baseMaxHealth))
        {
            return;
        }

        EntityBehaviorHealth? health = entity.GetBehavior<EntityBehaviorHealth>();
        if (health == null) return;

        int generation = entity.WatchedAttributes.GetInt("generation", 0);
        FaunaCategory category = _cachedCategoryByEntityCode.TryGetValue(code, out FaunaCategory cat)
            ? cat
            : FaunaCategory.Excluded;

        double targetValue = policy.EnableFaunaRebalance && category != FaunaCategory.Excluded
            ? _calculator.CalculateTargetMaxHealth(baseMaxHealth, category, generation, policy)
            : baseMaxHealth;

        float target = (float)targetValue;
        if (Math.Abs(health.BaseMaxHealth - target) > 0.05f)
        {
            double ratio = health.MaxHealth > 0 ? Math.Clamp(health.Health / health.MaxHealth, 0.0, 1.0) : 1.0;
            health.BaseMaxHealth = target;
            health.MaxHealth = target;
            health.Health = (float)(target * ratio);
        }
    }

    private FaunaEntityDescriptor BuildDescriptor(EntityProperties props, ICoreServerAPI sapi)
    {
        string code = props.Code?.ToString() ?? string.Empty;
        string className = props.Class ?? string.Empty;
        double maxHealth = ExtractBaseMaxHealth(props);

        var behaviorCodes = new List<string>();
        var dropCodes = new List<string>();

        if (props.Server?.BehaviorsAsJsonObj != null)
        {
            foreach (JsonObject b in props.Server.BehaviorsAsJsonObj)
            {
                string? bCode = b["code"]?.AsString();
                if (!string.IsNullOrWhiteSpace(bCode))
                {
                    behaviorCodes.Add(bCode);
                    if (string.Equals(bCode, "harvestable", StringComparison.OrdinalIgnoreCase))
                    {
                        CollectDropCodes(b, dropCodes);
                    }
                }
            }
        }

        var tags = new List<string>();
        if (!props.Tags.IsEmpty && sapi.EntityTagRegistry != null)
        {
            foreach (string tag in sapi.EntityTagRegistry.SlowEnumerateTagNames(props.Tags))
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    tags.Add(tag);
                }
            }
        }

        bool isPlayer = string.Equals(code, "game:player", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Player", StringComparison.OrdinalIgnoreCase);
        bool isTraderOrNpc = className.Contains("Trader", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Humanoid", StringComparison.OrdinalIgnoreCase);
        bool isAquatic = props.Habitat == EnumHabitat.Underwater || props.Habitat == EnumHabitat.Sea;

        return new FaunaEntityDescriptor(
            code,
            className,
            maxHealth,
            tags,
            behaviorCodes,
            dropCodes,
            isPlayer,
            isTraderOrNpc,
            IsTemporalCreature: false,
            isAquatic);
    }

    private static double ExtractBaseMaxHealth(EntityProperties props)
    {
        JsonObject? healthObj = FindServerBehavior(props, "health");
        if (healthObj == null) return 0.0;
        return healthObj["maxhealth"]?.AsDouble(0.0) ?? 0.0;
    }

    private static void CollectDropCodes(JsonObject harvestableObj, List<string> target)
    {
        if (harvestableObj.Token?["drops"] is not JArray drops) return;
        foreach (JObject drop in drops.OfType<JObject>())
        {
            string? code = drop["code"]?.ToString();
            if (!string.IsNullOrWhiteSpace(code)) target.Add(code);
        }
    }

    private bool ScaleHealthBehavior(
        EntityProperties props,
        FaunaEntityDescriptor descriptor,
        FaunaCategory category,
        EffectiveServerPolicy policy)
    {
        JsonObject? healthObj = FindServerBehavior(props, "health");
        if (healthObj?.Token is not JObject token) return false;

        double currentMax = token["maxhealth"]?.Value<double>() ?? 0.0;
        if (currentMax <= 0.0) return false;

        if (!_baseMaxHealthByEntityCode.TryGetValue(descriptor.Code, out double baseMax))
        {
            baseMax = currentMax;
            _baseMaxHealthByEntityCode[descriptor.Code] = baseMax;

            double baseCurrent = token["currenthealth"]?.Value<double>() ?? baseMax;
            _baseCurrentHealthRatioByEntityCode[descriptor.Code] = baseCurrent > 0 ? baseCurrent / baseMax : 1.0;
        }

        double scaledMax = category != FaunaCategory.Excluded && policy.EnableFaunaRebalance
            ? _calculator.CalculateTargetMaxHealth(baseMax, category, generation: 0, policy)
            : baseMax;
        token["maxhealth"] = scaledMax;

        if (token["currenthealth"] != null)
        {
            double ratio = _baseCurrentHealthRatioByEntityCode.GetValueOrDefault(descriptor.Code, 1.0);
            token["currenthealth"] = Math.Round(scaledMax * ratio, 2);
        }

        return true;
    }

    private void ScaleHarvestableBehavior(
        EntityProperties props,
        string entityCode,
        FaunaCategory category,
        EffectiveServerPolicy policy)
    {
        JsonObject? harvestableObj = FindServerBehavior(props, "harvestable");
        if (harvestableObj?.Token?["drops"] is not JArray drops) return;

        bool applyScaling = category != FaunaCategory.Excluded && policy.EnableFaunaRebalance;
        int dropIdx = 0;
        foreach (JObject drop in drops.OfType<JObject>())
        {
            string? itemCode = drop["code"]?.ToString();
            FaunaResourceKind kind = FaunaDropClassifier.Classify(itemCode);
            if (kind == FaunaResourceKind.None)
            {
                dropIdx++;
                continue;
            }

            if (drop["quantity"] is not JObject qtyObj)
            {
                dropIdx++;
                continue;
            }

            string dropKey = $"{entityCode}|{dropIdx}|{itemCode}";
            dropIdx++;

            if (!_baseDropQuantities.TryGetValue(dropKey, out (double Avg, double Var) baseQty))
            {
                baseQty = (qtyObj["avg"]?.Value<double>() ?? 0.0, qtyObj["var"]?.Value<double>() ?? 0.0);
                _baseDropQuantities[dropKey] = baseQty;
            }

            if (applyScaling)
            {
                (double scaledAvg, double scaledVar) = _calculator.CalculateScaledDrop(
                    baseQty.Avg,
                    baseQty.Var,
                    kind,
                    generation: 0,
                    policy);
                qtyObj["avg"] = scaledAvg;
                qtyObj["var"] = scaledVar;
            }
            else
            {
                qtyObj["avg"] = baseQty.Avg;
                qtyObj["var"] = baseQty.Var;
            }
        }
    }

    private static JsonObject? FindServerBehavior(EntityProperties props, string behaviorCode)
    {
        if (props.Server?.BehaviorsAsJsonObj == null) return null;

        foreach (JsonObject b in props.Server.BehaviorsAsJsonObj)
        {
            if (string.Equals(b["code"]?.AsString(), behaviorCode, StringComparison.OrdinalIgnoreCase))
            {
                return b;
            }
        }

        return null;
    }
}
