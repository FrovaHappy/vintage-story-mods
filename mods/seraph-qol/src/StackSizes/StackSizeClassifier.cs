namespace SeraphQoL.StackSizes;

/// <summary>
/// Clasificador puro de coleccionables en categorías de apilado, protegiendo herramientas, armaduras, líquidos y contenedores con contenido.
/// </summary>
public sealed class StackSizeClassifier
{
    private static readonly string[] ProtectedClassTokens =
    [
        "armor", "shield", "pickaxe", "axe", "shovel", "hoe", "knife", "sword", "spear", "bow",
        "sling", "hammer", "chisel", "scythe", "saw", "shears", "prospecting", "meal", "cookedcontainer",
        "cookingcontainer", "bag"
    ];

    private static readonly string[] ProtectedCodeTokens =
    [
        "armor-", "skep-populated", "bowl-meal", "crock-sealed", "cookingpot-cooked",
        "backpack", "miningbag", "hunterbackpack", "handbasket", "elk-saddle", "glider"
    ];

    private static readonly string[] ContainerClassTokens =
    [
        "barrel", "bucket", "liquidcontainer", "crate", "chest", "basket", "scrollrack", "crock", "generictypedcontainer"
    ];

    private static readonly string[] ContainerCodeTokens =
    [
        "barrel", "bucket", "crock", "crate", "cabinet", "scrollrack", "chest", "storagevessel", "bowl-", "stationarybasket"
    ];

    private static readonly string[] FurnitureClassTokens =
    [
        "bed", "bookshelf", "door", "bloomery", "bellows", "quern", "fruitpress", "pulverizer", "pan",
        "mannequin", "torchholder", "windmill", "helvehammer", "clutch", "brake", "transmission", "lantern"
    ];

    private static readonly string[] FurnitureCodeTokens =
    [
        "bed-", "bookshelf", "door-", "trapdoor", "bloomery", "bellows", "quern", "fruitpress",
        "pulverizer", "pan-wooden", "mannequin", "torchholder", "mount-", "gear-temporal",
        "tuningcylinder", "locatormap", "book-", "skep-empty", "skep-unpopulated", "chair-",
        "table-", "lantern", "chandelier", "chute-", "hopper-"
    ];

    private static readonly string[] FoodCodeTokens =
    [
        "grain-", "bread-", "dough-", "meat", "poultry", "fishfillet", "vegetable", "fruit-",
        "berry", "cheese", "mushroom", "egg-", "seed", "sapling", "cutting", "poultice",
        "bandage", "honeycomb", "pressedmash", "pickled", "pie-", "porridge", "flour-", "jam-"
    ];

    private static readonly string[] BuildingCodeTokens =
    [
        "plank", "board", "shingle", "brick", "mortar", "daub", "stone", "cobble", "slab",
        "stair", "fence", "gate", "ladder", "roof", "beam", "log-", "logsection", "soil-",
        "dirt", "gravel", "sand-", "clay-", "peat", "path-", "glass-", "bamboo-"
    ];

    private static readonly string[] ResourceCodeTokens =
    [
        "nugget", "ingot", "metalbit", "metalplate", "nails", "ironbloom", "ore-", "crushed",
        "powder", "charcoal", "coke", "firewood", "stick", "flint", "quartz", "rope",
        "twine", "flax", "feather", "bone", "fat", "beeswax", "leather", "compost",
        "fertilizer", "drygrass", "thatch", "reed", "cattail", "candle", "jonas",
        "gear-rusty", "metalparts", "ash", "lime", "salt", "sulfur", "saltpeter",
        "borax", "alum", "resin", "hide-", "pelt", "wool", "linen", "cloth-", "paper"
    ];

    public StackCategory Classify(StackCollectibleDescriptor item)
    {
        if (string.IsNullOrWhiteSpace(item.Code) || item.VanillaMaxStackSize <= 0)
        {
            return StackCategory.ProtectedSingle;
        }

        string codeLower = item.Code.ToLowerInvariant();
        string classLower = item.ClassName.ToLowerInvariant();

        if (IsProtectedSingle(item, codeLower, classLower))
        {
            return StackCategory.ProtectedSingle;
        }

        if (item.IsWearable || codeLower.Contains("clothes-", StringComparison.Ordinal))
        {
            return StackCategory.Clothing;
        }

        if (ContainsAny(classLower, ContainerClassTokens) || ContainsAny(codeLower, ContainerCodeTokens))
        {
            return StackCategory.Containers;
        }

        if (ContainsAny(classLower, FurnitureClassTokens) || ContainsAny(codeLower, FurnitureCodeTokens))
        {
            return StackCategory.FurnitureAndUtility;
        }

        if (item.HasNutritionProps || ContainsAny(codeLower, FoodCodeTokens))
        {
            return StackCategory.FoodAndAgriculture;
        }

        if (ContainsAny(codeLower, ResourceCodeTokens)
            || item.CreativeTabs.Any(t => string.Equals(t, "resources", StringComparison.OrdinalIgnoreCase)))
        {
            return StackCategory.ResourcesAndMetals;
        }

        if (item.IsBlock || ContainsAny(codeLower, BuildingCodeTokens))
        {
            return StackCategory.BuildingBlocks;
        }

        return StackCategory.MiscOther;
    }

    private static bool IsProtectedSingle(StackCollectibleDescriptor item, string codeLower, string classLower)
    {
        if (item.Durability > 0 || item.HasTool || item.IsLiquid || item.IsBackpackStorage)
        {
            return true;
        }

        return ContainsAny(classLower, ProtectedClassTokens) || ContainsAny(codeLower, ProtectedCodeTokens);
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
