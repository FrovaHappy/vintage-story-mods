namespace SeraphQoL.Fauna;

public enum FaunaCategory
{
    Excluded,
    SmallPrey,
    MediumLargePrey,
    Predator
}

public enum FaunaResourceKind
{
    None,
    Meat,
    Fat,
    Hide,
    Byproduct
}

/// <summary>
/// Descripcion pura de un tipo de entidad para clasificacion sin acoplarse al runtime de Vintage Story.
/// </summary>
public sealed record FaunaEntityDescriptor(
    string Code,
    string ClassName,
    double MaxHealth,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> BehaviorCodes,
    IReadOnlyList<string> DropCodes,
    bool IsPlayer,
    bool IsTraderOrNpc,
    bool IsTemporalCreature,
    bool IsAquatic);
