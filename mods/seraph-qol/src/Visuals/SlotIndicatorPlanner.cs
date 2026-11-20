namespace SeraphQoL.Visuals;

public enum SlotIndicatorKind
{
    None,
    Freshness,
    Durability,
    Preparation,
    LiquidVolume,
    CrucibleContent,
    FoodContainer,
    NightVisionFuel
}

public sealed record SlotIndicatorOptions(
    bool Enabled = true,
    bool ShowFreshness = true,
    bool ShowDurability = true,
    bool ShowPreparation = true,
    bool ShowLiquid = true,
    bool ShowCrucible = true,
    bool ShowFoodContainer = true,
    bool ShowNightVision = true);

public readonly record struct SlotIndicatorInput(
    bool HasItem,
    float? PerishRatioRemaining,
    float? DurabilityRatioRemaining,
    float? PreparationProgress,
    float? LiquidFillRatio,
    float? CrucibleFillRatio,
    bool CrucibleIsMolten,
    float? FoodContainerFillRatio,
    float? NightVisionFuelRatio);

public enum SlotBarPosition
{
    Top,
    Middle
}

public readonly record struct SlotIndicatorResult(
    SlotIndicatorKind Kind,
    float FillRatio,
    bool IsMolten,
    bool IsUrgent,
    SlotBarPosition BarPosition = SlotBarPosition.Top)
{
    public static SlotIndicatorResult None => new(SlotIndicatorKind.None, 0f, false, false, SlotBarPosition.Top);
}

/// <summary>
/// Clasificador puro y priorizado para barras e indicadores de estado en slots de inventario.
/// </summary>
public static class SlotIndicatorPlanner
{
    public static SlotIndicatorResult Evaluate(in SlotIndicatorInput input, SlotIndicatorOptions options)
    {
        if (!options.Enabled || !input.HasItem)
        {
            return SlotIndicatorResult.None;
        }

        if (options.ShowNightVision && input.NightVisionFuelRatio is float nv)
        {
            float clamped = Math.Clamp(nv, 0f, 1f);
            return new SlotIndicatorResult(SlotIndicatorKind.NightVisionFuel, clamped, false, clamped <= 0.15f);
        }

        if (options.ShowCrucible && input.CrucibleFillRatio is float crucible && crucible > 0.001f)
        {
            return new SlotIndicatorResult(SlotIndicatorKind.CrucibleContent, Math.Clamp(crucible, 0f, 1f), input.CrucibleIsMolten, false);
        }

        if (options.ShowLiquid && input.LiquidFillRatio is float liquid && liquid > 0.001f)
        {
            return new SlotIndicatorResult(SlotIndicatorKind.LiquidVolume, Math.Clamp(liquid, 0f, 1f), false, false, SlotBarPosition.Middle);
        }

        if (options.ShowFoodContainer && input.FoodContainerFillRatio is float food && food > 0.001f)
        {
            return new SlotIndicatorResult(SlotIndicatorKind.FoodContainer, Math.Clamp(food, 0f, 1f), false, false);
        }

        if (options.ShowPreparation && input.PreparationProgress is float prep && prep > 0.001f && prep < 0.999f)
        {
            return new SlotIndicatorResult(SlotIndicatorKind.Preparation, Math.Clamp(prep, 0f, 1f), false, false);
        }

        if (options.ShowFreshness && input.PerishRatioRemaining is float fresh)
        {
            float clamped = Math.Clamp(fresh, 0f, 1f);
            return new SlotIndicatorResult(SlotIndicatorKind.Freshness, clamped, false, clamped <= 0.30f);
        }

        if (options.ShowDurability && input.DurabilityRatioRemaining is float dur && dur < 0.999f)
        {
            float clamped = Math.Clamp(dur, 0f, 1f);
            return new SlotIndicatorResult(SlotIndicatorKind.Durability, clamped, false, clamped <= 0.20f);
        }

        return SlotIndicatorResult.None;
    }
}
