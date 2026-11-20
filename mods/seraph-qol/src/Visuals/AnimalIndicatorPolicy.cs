namespace SeraphQoL.Visuals;

public enum AnimalBiologicalState
{
    None,
    Male,
    Female,
    PregnantFemale
}

/// <summary>
/// Determina sexo y prenez de animales (generacion >= 1 por defecto, u opcionalmente salvajes) sin depender del cliente grafico.
/// </summary>
public static class AnimalSexClassifier
{
    public static AnimalBiologicalState Resolve(
        string? entityCode,
        string? variantGender,
        int generation,
        bool isAlive,
        bool isPregnant,
        bool includeWild = false)
    {
        if (!isAlive) return AnimalBiologicalState.None;
        if (!includeWild && generation < 1) return AnimalBiologicalState.None;

        if (isPregnant)
        {
            return AnimalBiologicalState.PregnantFemale;
        }

        if (string.Equals(variantGender, "male", StringComparison.OrdinalIgnoreCase))
        {
            return AnimalBiologicalState.Male;
        }

        if (string.Equals(variantGender, "female", StringComparison.OrdinalIgnoreCase))
        {
            return AnimalBiologicalState.Female;
        }

        if (string.IsNullOrWhiteSpace(entityCode))
        {
            return AnimalBiologicalState.None;
        }

        string lower = entityCode.ToLowerInvariant();
        if (ContainsGenderToken(lower, "female")
            || ContainsGenderToken(lower, "hen")
            || ContainsGenderToken(lower, "henpoult")
            || ContainsGenderToken(lower, "ewe")
            || ContainsGenderToken(lower, "sow")
            || ContainsGenderToken(lower, "doe"))
        {
            return AnimalBiologicalState.Female;
        }

        if (ContainsGenderToken(lower, "male")
            || ContainsGenderToken(lower, "rooster")
            || ContainsGenderToken(lower, "roosterpoult")
            || ContainsGenderToken(lower, "ram")
            || ContainsGenderToken(lower, "boar")
            || ContainsGenderToken(lower, "buck"))
        {
            return AnimalBiologicalState.Male;
        }

        return AnimalBiologicalState.None;
    }

    private static bool ContainsGenderToken(string code, string token)
    {
        return code.EndsWith("-" + token, StringComparison.Ordinal)
            || code.Contains("-" + token + "-", StringComparison.Ordinal)
            || code.Contains(":" + token + "-", StringComparison.Ordinal);
    }
}

public static class AnimalIndicatorGeometry
{
    public static float ComputeDistanceAlpha(double distance, double maxRange)
    {
        if (maxRange <= 0.1 || distance > maxRange) return 0f;
        double fadeStart = maxRange * 0.65;
        if (distance <= fadeStart) return 1f;

        double span = Math.Max(0.1, maxRange - fadeStart);
        return (float)Math.Clamp(1.0 - ((distance - fadeStart) / span), 0.0, 1.0);
    }

    public static bool ShouldShowForPlayer(bool featureEnabled, bool isSneaking, bool alwaysShow = false) =>
        featureEnabled && (alwaysShow || isSneaking);
}

public static class AnimalIndicatorFormatter
{
    private static readonly (int Value, string Numeral)[] RomanNumerals =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
        (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
        (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
    ];

    public static string ToRoman(int generation)
    {
        if (generation <= 0) return string.Empty;
        int remaining = Math.Min(3999, generation);
        var sb = new System.Text.StringBuilder();

        foreach ((int val, string sym) in RomanNumerals)
        {
            while (remaining >= val)
            {
                sb.Append(sym);
                remaining -= val;
            }
        }

        return sb.ToString();
    }

    public static string FormatBadgeText(AnimalBiologicalState state, int generation)
    {
        string symbol = state switch
        {
            AnimalBiologicalState.Male => "\u2642",
            AnimalBiologicalState.Female => "\u2640",
            AnimalBiologicalState.PregnantFemale => "\u2640+",
            _ => string.Empty
        };
        if (symbol.Length == 0) return string.Empty;

        string roman = ToRoman(generation);
        return roman.Length > 0 ? $"{symbol} {roman}" : symbol;
    }

    public static IReadOnlyList<AnimalSquareBadge> BuildSquareTokens(AnimalBiologicalState state, int generation)
    {
        if (state == AnimalBiologicalState.None) return Array.Empty<AnimalSquareBadge>();

        var badges = new List<AnimalSquareBadge>(3);
        badges.Add(state == AnimalBiologicalState.Male
            ? new AnimalSquareBadge(AnimalSquareKind.SexMale, "\u2642")
            : new AnimalSquareBadge(AnimalSquareKind.SexFemale, "\u2640"));

        if (state == AnimalBiologicalState.PregnantFemale)
        {
            badges.Add(new AnimalSquareBadge(AnimalSquareKind.Pregnant, "+"));
        }

        string roman = ToRoman(generation);
        if (roman.Length > 0)
        {
            badges.Add(new AnimalSquareBadge(AnimalSquareKind.Generation, roman));
        }

        return badges;
    }
}

public enum AnimalSquareKind
{
    SexMale,
    SexFemale,
    Pregnant,
    Generation
}

public sealed record AnimalSquareBadge(AnimalSquareKind Kind, string Text);

public enum EntityHudCategory
{
    Excluded,
    Hostile,
    PassiveFauna,
    TraderNpc,
    Player
}

public sealed record EntityHudDescriptor(
    string Code,
    string ClassName,
    bool IsPlayer,
    bool IsTraderOrNpc,
    bool IsAlive,
    float CurrentHealth,
    float MaxHealth,
    bool IsHostileAi);

public sealed record EntityHealthBarFilter(
    bool Enabled,
    bool ShowHostiles,
    bool ShowPassiveFauna,
    bool ShowTradersAndNpcs,
    bool ShowPlayers,
    bool OnlyWhenDamaged,
    bool RequireSneak);

/// <summary>
/// Clasifica entidades vivas en categorias de HUD y evalua la visibilidad de su barra de vida segun configuracion.
/// </summary>
public static class EntityHudClassifier
{
    private static readonly string[] HostileTokens =
    [
        "drifter", "locust", "shiver", "bowtorn", "eidolon", "bell", "temporal",
        "corrupted", "sawblade", "wolf", "hyena", "bear", "lion", "panther",
        "tiger", "cougar", "lynx", "wolverine", "jackal", "crocodile", "alligator"
    ];

    public static EntityHudCategory Classify(EntityHudDescriptor entity)
    {
        if (!entity.IsAlive || entity.MaxHealth <= 0f || string.IsNullOrWhiteSpace(entity.Code))
        {
            return EntityHudCategory.Excluded;
        }

        if (entity.IsPlayer)
        {
            return EntityHudCategory.Player;
        }

        if (entity.IsTraderOrNpc)
        {
            return EntityHudCategory.TraderNpc;
        }

        if (entity.IsHostileAi)
        {
            return EntityHudCategory.Hostile;
        }

        string codeLower = entity.Code.ToLowerInvariant();
        string classLower = (entity.ClassName ?? string.Empty).ToLowerInvariant();
        foreach (string token in HostileTokens)
        {
            if (codeLower.Contains(token, StringComparison.Ordinal) || classLower.Contains(token, StringComparison.Ordinal))
            {
                return EntityHudCategory.Hostile;
            }
        }

        return EntityHudCategory.PassiveFauna;
    }

    public static bool ShouldShowHealthBar(
        EntityHudCategory category,
        float currentHealth,
        float maxHealth,
        bool isSneaking,
        EntityHealthBarFilter filter)
    {
        if (!filter.Enabled || category == EntityHudCategory.Excluded || maxHealth <= 0f)
        {
            return false;
        }

        if (filter.RequireSneak && !isSneaking)
        {
            return false;
        }

        if (filter.OnlyWhenDamaged && currentHealth >= (maxHealth - 0.05f))
        {
            return false;
        }

        return category switch
        {
            EntityHudCategory.Hostile => filter.ShowHostiles,
            EntityHudCategory.PassiveFauna => filter.ShowPassiveFauna,
            EntityHudCategory.TraderNpc => filter.ShowTradersAndNpcs,
            EntityHudCategory.Player => filter.ShowPlayers,
            _ => false
        };
    }

    public static string FormatHealthText(float currentHealth, float maxHealth)
    {
        float clampedMax = Math.Max(1f, maxHealth);
        float clampedCur = Math.Clamp(currentHealth, 0f, clampedMax);
        string curStr = clampedCur > 0f && clampedCur < 1f
            ? clampedCur.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            : Math.Ceiling(clampedCur).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        string maxStr = Math.Round(clampedMax).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        return $"{curStr}/{maxStr}";
    }
}

/// <summary>
/// Maquina de estados pura por entidad para mostrar la barra de vida solo al mirarla (con retardo de desaparicion)
/// o siempre cuando HealthBarAlwaysShow esta activado.
/// </summary>
public sealed class EntityHealthBarVisibilityState
{
    private readonly Dictionary<long, long> _visibleUntilMsByEntityId = new();

    public bool IsEntityVisible(
        long entityId,
        long? lookedAtEntityId,
        bool alwaysShow,
        long nowMs,
        double lingerSeconds)
    {
        if (alwaysShow)
        {
            return true;
        }

        double clampedDelay = double.IsNaN(lingerSeconds) ? 0.0 : Math.Clamp(lingerSeconds, 0.0, 30.0);
        long delayMs = (long)Math.Round(clampedDelay * 1000.0);

        if (lookedAtEntityId == entityId)
        {
            if (delayMs > 0)
            {
                _visibleUntilMsByEntityId[entityId] = nowMs + delayMs;
            }
            else
            {
                _visibleUntilMsByEntityId.Remove(entityId);
            }
            return true;
        }

        if (_visibleUntilMsByEntityId.TryGetValue(entityId, out long expireAtMs))
        {
            if (nowMs < expireAtMs)
            {
                return true;
            }

            _visibleUntilMsByEntityId.Remove(entityId);
        }

        return false;
    }

    public void PruneExpired(long nowMs)
    {
        if (_visibleUntilMsByEntityId.Count == 0) return;

        List<long>? toRemove = null;
        foreach ((long entityId, long expireAtMs) in _visibleUntilMsByEntityId)
        {
            if (nowMs >= expireAtMs)
            {
                toRemove ??= new List<long>();
                toRemove.Add(entityId);
            }
        }

        if (toRemove != null)
        {
            foreach (long id in toRemove)
            {
                _visibleUntilMsByEntityId.Remove(id);
            }
        }
    }

    public void Reset() => _visibleUntilMsByEntityId.Clear();
}
