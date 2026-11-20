using SeraphQoL.Config;
using SeraphQoL.Fauna;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class FaunaBalanceTests
{
    private static EffectiveServerPolicy DefaultPolicy => EffectiveServerPolicy.FromServerConfig(new SeraphQoLServerConfig());

    private static FaunaEntityDescriptor MakeEntity(
        string code,
        double maxHealth = 10,
        bool hasHealth = true,
        bool hasHarvestable = true,
        bool isTraderOrNpc = false,
        bool isTemporal = false,
        bool isAquatic = false,
        string className = "EntityAgent",
        params string[] tags)
    {
        var behaviors = new List<string>();
        if (hasHealth) behaviors.Add("health");
        if (hasHarvestable) behaviors.Add("harvestable");

        return new FaunaEntityDescriptor(
            Code: code,
            ClassName: className,
            MaxHealth: maxHealth,
            Tags: tags,
            BehaviorCodes: behaviors,
            DropCodes: [],
            IsPlayer: code == "game:player",
            IsTraderOrNpc: isTraderOrNpc,
            IsTemporalCreature: isTemporal,
            IsAquatic: isAquatic);
    }

    [Theory]
    [InlineData("game:player", false, false, false, "EntityPlayer")]
    [InlineData("game:humanoid-trader-survivalgoods", false, true, false, "EntityTrader")]
    [InlineData("game:drifter-normal", false, false, true, "EntityDrifter")]
    [InlineData("game:locust-bronze", false, false, true, "EntityLocust")]
    [InlineData("game:shiver-surface", false, false, true, "EntityShiver")]
    [InlineData("game:bowtorn-surface", false, false, true, "EntityBowtorn")]
    public void Classifier_ExcludesPlayersTradersAndTemporalCreatures(
        string code,
        bool isAquatic,
        bool isTrader,
        bool isTemporal,
        string className)
    {
        var classifier = new FaunaClassifier();
        FaunaEntityDescriptor entity = MakeEntity(
            code,
            maxHealth: 20,
            isTraderOrNpc: isTrader,
            isTemporal: isTemporal,
            isAquatic: isAquatic,
            className: className);

        Assert.Equal(FaunaCategory.Excluded, classifier.Classify(entity, DefaultPolicy));
    }

    [Fact]
    public void Classifier_ExcludesAquaticByDefault_UnlessEnabledOrWhitelisted()
    {
        var classifier = new FaunaClassifier();
        FaunaEntityDescriptor salmon = MakeEntity("game:salmon", maxHealth: 4, isAquatic: true);

        Assert.Equal(FaunaCategory.Excluded, classifier.Classify(salmon, DefaultPolicy));

        var includeAquaticCfg = new SeraphQoLServerConfig { IncludeAquaticFauna = true };
        Assert.Equal(
            FaunaCategory.SmallPrey,
            classifier.Classify(salmon, EffectiveServerPolicy.FromServerConfig(includeAquaticCfg)));

        var whitelistedCfg = new SeraphQoLServerConfig();
        whitelistedCfg.FaunaWhitelist.Add("game:salmon");
        Assert.Equal(
            FaunaCategory.SmallPrey,
            classifier.Classify(salmon, EffectiveServerPolicy.FromServerConfig(whitelistedCfg)));
    }

    [Theory]
    [InlineData("game:hare-adult", 4.0, FaunaCategory.SmallPrey)]
    [InlineData("game:chicken-hen", 4.0, FaunaCategory.SmallPrey)]
    [InlineData("game:fox-adult-red", 10.0, FaunaCategory.SmallPrey)]
    [InlineData("game:deer-buck-whitetailed", 20.0, FaunaCategory.MediumLargePrey)]
    [InlineData("game:pig-wild-boar", 24.0, FaunaCategory.MediumLargePrey)]
    [InlineData("game:sheep-bighorn-ram", 22.0, FaunaCategory.MediumLargePrey)]
    [InlineData("game:wolf-timber-male", 22.0, FaunaCategory.Predator)]
    [InlineData("game:hyena-male", 25.0, FaunaCategory.Predator)]
    [InlineData("game:bear-brown-male", 65.0, FaunaCategory.Predator)]
    public void Classifier_AssignsExpectedCategoriesForVanillaFauna(
        string code,
        double maxHealth,
        FaunaCategory expected)
    {
        var classifier = new FaunaClassifier();
        FaunaEntityDescriptor entity = MakeEntity(code, maxHealth: maxHealth);

        Assert.Equal(expected, classifier.Classify(entity, DefaultPolicy));
    }

    [Fact]
    public void Classifier_RespectsBlacklistEvenForValidWildlife()
    {
        var cfg = new SeraphQoLServerConfig();
        cfg.FaunaBlacklist.Add("game:wolf-*");
        var classifier = new FaunaClassifier();
        FaunaEntityDescriptor wolf = MakeEntity("game:wolf-timber-male", maxHealth: 22);

        Assert.Equal(FaunaCategory.Excluded, classifier.Classify(wolf, EffectiveServerPolicy.FromServerConfig(cfg)));
    }

    [Theory]
    [InlineData("game:redmeat-raw", FaunaResourceKind.Meat)]
    [InlineData("game:bushmeat-raw", FaunaResourceKind.Meat)]
    [InlineData("game:poultry-raw", FaunaResourceKind.Meat)]
    [InlineData("game:fat", FaunaResourceKind.Fat)]
    [InlineData("game:hide-raw-large", FaunaResourceKind.Hide)]
    [InlineData("game:pelt-wolf", FaunaResourceKind.Hide)]
    [InlineData("game:feather", FaunaResourceKind.Byproduct)]
    [InlineData("game:bone", FaunaResourceKind.Byproduct)]
    [InlineData("game:antler-deer", FaunaResourceKind.Byproduct)]
    [InlineData("game:gear-rusty", FaunaResourceKind.None)]
    public void DropClassifier_MapsItemCodesToResourceFamilies(string itemCode, FaunaResourceKind expected)
    {
        Assert.Equal(expected, FaunaDropClassifier.Classify(itemCode));
    }

    [Fact]
    public void BalanceCalculator_ScalesWildHealthAndDrops_AndPreservesDomesticatedHealthByDefault()
    {
        var calc = new FaunaBalanceCalculator();
        EffectiveServerPolicy policy = DefaultPolicy;

        Assert.Equal(15.0, calc.CalculateTargetMaxHealth(10.0, FaunaCategory.SmallPrey, generation: 0, policy));
        Assert.Equal(40.0, calc.CalculateTargetMaxHealth(20.0, FaunaCategory.MediumLargePrey, generation: 0, policy));
        Assert.Equal(50.0, calc.CalculateTargetMaxHealth(20.0, FaunaCategory.Predator, generation: 0, policy));

        // Domesticated animal (generation > 0) keeps vanilla health by default, but scales drops
        Assert.Equal(20.0, calc.CalculateTargetMaxHealth(20.0, FaunaCategory.MediumLargePrey, generation: 1, policy));

        (double avgMeat, double varMeat) = calc.CalculateScaledDrop(4.0, 1.0, FaunaResourceKind.Meat, generation: 1, policy);
        Assert.Equal(7.0, avgMeat, 3);
        Assert.Equal(1.75, varMeat, 3);

        (double avgHide, double varHide) = calc.CalculateScaledDrop(2.0, 0.5, FaunaResourceKind.Hide, generation: 0, policy);
        Assert.Equal(3.0, avgHide, 3);
        Assert.Equal(0.75, varHide, 3);
    }
}
