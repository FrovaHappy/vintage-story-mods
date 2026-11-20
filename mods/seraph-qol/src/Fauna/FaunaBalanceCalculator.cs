using SeraphQoL.Config;

namespace SeraphQoL.Fauna;

/// <summary>
/// Calcula vida objetivo y drops escalados respetando la distincion entre fauna salvaje y domesticada.
/// </summary>
public sealed class FaunaBalanceCalculator
{
    public double CalculateTargetMaxHealth(
        double baseMaxHealth,
        FaunaCategory category,
        int generation,
        EffectiveServerPolicy policy)
    {
        if (baseMaxHealth <= 0 || category == FaunaCategory.Excluded || !policy.EnableFaunaRebalance)
        {
            return baseMaxHealth;
        }

        bool isDomesticated = generation > 0;
        if (isDomesticated && !policy.ScaleDomesticatedHealth)
        {
            return baseMaxHealth;
        }

        double multiplier = category switch
        {
            FaunaCategory.SmallPrey => policy.SmallPreyHealthMultiplier,
            FaunaCategory.MediumLargePrey => policy.MediumLargePreyHealthMultiplier,
            FaunaCategory.Predator => policy.PredatorHealthMultiplier,
            _ => 1.0
        };

        return Math.Round(baseMaxHealth * multiplier, 2);
    }

    public (double Avg, double Var) CalculateScaledDrop(
        double baseAvg,
        double baseVar,
        FaunaResourceKind kind,
        int generation,
        EffectiveServerPolicy policy)
    {
        if (kind == FaunaResourceKind.None || !policy.EnableFaunaRebalance)
        {
            return (Math.Max(0, baseAvg), Math.Max(0, baseVar));
        }

        bool isDomesticated = generation > 0;
        if (isDomesticated && !policy.ScaleDomesticatedDrops)
        {
            return (Math.Max(0, baseAvg), Math.Max(0, baseVar));
        }

        double multiplier = kind switch
        {
            FaunaResourceKind.Meat => policy.MeatDropMultiplier,
            FaunaResourceKind.Fat => policy.FatDropMultiplier,
            FaunaResourceKind.Hide => policy.HideDropMultiplier,
            FaunaResourceKind.Byproduct => policy.ByproductDropMultiplier,
            _ => 1.0
        };

        return (
            Math.Round(Math.Max(0, baseAvg) * multiplier, 3),
            Math.Round(Math.Max(0, baseVar) * multiplier, 3));
    }
}
