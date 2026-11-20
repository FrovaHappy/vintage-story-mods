using SeraphQoL.Client;
using SeraphQoL.Config;
using SeraphQoL.Inventory;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class QuickStoreAndWhitelistTests
{
    [Fact]
    public void QuickStorePlanner_TransfersOnlyMatchingNonProtectedSlots()
    {
        var source = new[]
        {
            new SortSlotSnapshot(0, "backpack", true, false, false, "game:stone-granite", "Granite", "blocks", 16, 64, false, 1.0, 0),
            new SortSlotSnapshot(1, "backpack", true, true, false, "game:stone-granite", "Granite", "blocks", 16, 64, false, 1.0, 1),
            new SortSlotSnapshot(2, "backpack", true, false, true, "game:charcoal", "Charcoal", "resources", 12, 64, false, 1.0, 2),
            new SortSlotSnapshot(3, "backpack", true, false, false, "game:stick", "Stick", "resources", 8, 64, false, 1.0, 3),
            new SortSlotSnapshot(4, "hotbar", true, false, false, "game:stone-granite", "Granite", "blocks", 4, 64, false, 1.0, 4)
        };

        var destMatchingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "game:stone-granite",
            "game:charcoal"
        };

        IReadOnlyList<int> eligibleWithoutHotbar = QuickStorePlanner.FindEligibleSourceIndices(
            source,
            destMatchingCodes,
            includeHotbar: false);

        // Slot 0 matches and is unprotected; Slot 1 is locked; Slot 2 is favorite; Slot 3 doesn't match; Slot 4 is hotbar
        Assert.Equal([0], eligibleWithoutHotbar);

        IReadOnlyList<int> eligibleWithHotbar = QuickStorePlanner.FindEligibleSourceIndices(
            source,
            destMatchingCodes,
            includeHotbar: true);
        Assert.Equal([0, 4], eligibleWithHotbar);
    }

    [Fact]
    public void ContainerWhitelistPolicy_RecognizesBuiltinsWildcardsAndBlacklist()
    {
        var cfg = new SeraphQoLServerConfig();
        cfg.AdditionalContainerWhitelist.Add("custommod:barrel-*");
        cfg.ContainerBlacklist.Add("game:crate");

        EffectiveServerPolicy policy = EffectiveServerPolicy.FromServerConfig(cfg);

        Assert.True(ContainerWhitelistPolicy.IsAllowed("game:chest-east", policy));
        Assert.True(ContainerWhitelistPolicy.IsAllowed("game:labeledchest-north", policy));
        Assert.True(ContainerWhitelistPolicy.IsAllowed("game:storagevessel-ash", policy));
        Assert.True(ContainerWhitelistPolicy.IsAllowed("bettercrates:bettercrate-copper-center-north", policy));
        Assert.True(ContainerWhitelistPolicy.IsAllowed("custommod:barrel-oak", policy));

        Assert.False(ContainerWhitelistPolicy.IsAllowed("game:crate", policy));
        Assert.False(ContainerWhitelistPolicy.IsAllowed("game:firepit-lit", policy));
    }

    [Fact]
    public void ComputeCappedTransferAmount_NeverExceedsCollectibleMaxStack_EvenWhenSlotMaxIsLarge()
    {
        int toExisting = ServerInventoryExecutor.ComputeCappedTransferAmount(
            destCurrentSize: 50,
            collectibleMaxStack: 64,
            slotMaxStack: 999999,
            sourceSize: 30);

        Assert.Equal(14, toExisting);

        int whenFull = ServerInventoryExecutor.ComputeCappedTransferAmount(
            destCurrentSize: 64,
            collectibleMaxStack: 64,
            slotMaxStack: 999999,
            sourceSize: 16);

        Assert.Equal(0, whenFull);

        int toEmptySlot = ServerInventoryExecutor.ComputeCappedTransferAmount(
            destCurrentSize: 0,
            collectibleMaxStack: 16,
            slotMaxStack: 999999,
            sourceSize: 20);

        Assert.Equal(16, toEmptySlot);
    }

    [Fact]
    public void ContainerTitleFormatter_ShortensSpanishAndEnglishPrefixes()
    {
        Assert.Equal("Cofre", ContainerTitleFormatter.ShortenTitle("Contenidos del cofre"));
        Assert.Equal("Caja", ContainerTitleFormatter.ShortenTitle("Contenidos de la caja"));
        Assert.Equal("Vasija", ContainerTitleFormatter.ShortenTitle("Contenidos de la vasija"));
        Assert.Equal("Chest", ContainerTitleFormatter.ShortenTitle("Chest Contents"));
        Assert.Equal("Cofre etiquetado", ContainerTitleFormatter.ShortenTitle("Cofre etiquetado"));
    }
}
