using SeraphQoL.Visuals;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class VisualIndicatorsTests
{
    [Fact]
    public void SlotIndicatorPlanner_PrioritizesConfiguredIndicatorsCleanly()
    {
        var opts = new SlotIndicatorOptions();

        SlotIndicatorResult perish = SlotIndicatorPlanner.Evaluate(
            new SlotIndicatorInput(
                HasItem: true,
                PerishRatioRemaining: 0.25f,
                DurabilityRatioRemaining: null,
                PreparationProgress: null,
                LiquidFillRatio: null,
                CrucibleFillRatio: null,
                CrucibleIsMolten: false,
                FoodContainerFillRatio: null,
                NightVisionFuelRatio: null),
            opts);

        Assert.Equal(SlotIndicatorKind.Freshness, perish.Kind);
        Assert.Equal(0.25f, perish.FillRatio, 3);
        Assert.True(perish.IsUrgent);

        SlotIndicatorResult crucible = SlotIndicatorPlanner.Evaluate(
            new SlotIndicatorInput(
                HasItem: true,
                PerishRatioRemaining: null,
                DurabilityRatioRemaining: null,
                PreparationProgress: null,
                LiquidFillRatio: null,
                CrucibleFillRatio: 0.75f,
                CrucibleIsMolten: true,
                FoodContainerFillRatio: null,
                NightVisionFuelRatio: null),
            opts);

        Assert.Equal(SlotIndicatorKind.CrucibleContent, crucible.Kind);
        Assert.True(crucible.IsMolten);
        Assert.Equal(0.75f, crucible.FillRatio, 3);

        SlotIndicatorResult disabledFreshness = SlotIndicatorPlanner.Evaluate(
            new SlotIndicatorInput(
                HasItem: true,
                PerishRatioRemaining: 0.5f,
                DurabilityRatioRemaining: 0.6f,
                PreparationProgress: null,
                LiquidFillRatio: null,
                CrucibleFillRatio: null,
                CrucibleIsMolten: false,
                FoodContainerFillRatio: null,
                NightVisionFuelRatio: null),
            opts with { ShowFreshness = false });

        Assert.Equal(SlotIndicatorKind.Durability, disabledFreshness.Kind);
        Assert.Equal(SlotBarPosition.Top, disabledFreshness.BarPosition);

        SlotIndicatorResult liquid = SlotIndicatorPlanner.Evaluate(
            new SlotIndicatorInput(
                HasItem: true,
                PerishRatioRemaining: null,
                DurabilityRatioRemaining: null,
                PreparationProgress: null,
                LiquidFillRatio: 0.6f,
                CrucibleFillRatio: null,
                CrucibleIsMolten: false,
                FoodContainerFillRatio: null,
                NightVisionFuelRatio: null),
            opts);

        Assert.Equal(SlotIndicatorKind.LiquidVolume, liquid.Kind);
        Assert.Equal(SlotBarPosition.Middle, liquid.BarPosition);
    }

    [Fact]
    public void AnimalSexClassifier_OnlyMarksLivingDomesticatedGeneration1Plus()
    {
        Assert.Equal(AnimalBiologicalState.None,
            AnimalSexClassifier.Resolve("game:pig-eurasian-adult-female", "female", generation: 0, isAlive: true, isPregnant: false));

        Assert.Equal(AnimalBiologicalState.None,
            AnimalSexClassifier.Resolve("game:pig-eurasian-adult-male", "male", generation: 2, isAlive: false, isPregnant: false));

        Assert.Equal(AnimalBiologicalState.Male,
            AnimalSexClassifier.Resolve("game:pig-eurasian-adult-male", "male", generation: 1, isAlive: true, isPregnant: false));

        Assert.Equal(AnimalBiologicalState.Female,
            AnimalSexClassifier.Resolve("game:pig-eurasian-adult-female", "female", generation: 2, isAlive: true, isPregnant: false));

        Assert.Equal(AnimalBiologicalState.PregnantFemale,
            AnimalSexClassifier.Resolve("game:pig-eurasian-adult-female", "female", generation: 2, isAlive: true, isPregnant: true));

        Assert.Equal(AnimalBiologicalState.Female,
            AnimalSexClassifier.Resolve("game:chicken-hen", null, generation: 1, isAlive: true, isPregnant: false));

        Assert.Equal(AnimalBiologicalState.Male,
            AnimalSexClassifier.Resolve("game:chicken-roosterpoult", null, generation: 1, isAlive: true, isPregnant: false));

        Assert.Equal(AnimalBiologicalState.Female,
            AnimalSexClassifier.Resolve("game:pig-eurasian-adult-female", "female", generation: 0, isAlive: true, isPregnant: false, includeWild: true));

        Assert.Equal("I", AnimalIndicatorFormatter.ToRoman(1));
        Assert.Equal("IV", AnimalIndicatorFormatter.ToRoman(4));
        Assert.Equal("V", AnimalIndicatorFormatter.ToRoman(5));
        Assert.Equal("IX", AnimalIndicatorFormatter.ToRoman(9));
        Assert.Equal("XIV", AnimalIndicatorFormatter.ToRoman(14));
        Assert.Equal(string.Empty, AnimalIndicatorFormatter.ToRoman(0));
        Assert.Equal("\u2642 V", AnimalIndicatorFormatter.FormatBadgeText(AnimalBiologicalState.Male, 5));
        Assert.Equal("\u2640+ III", AnimalIndicatorFormatter.FormatBadgeText(AnimalBiologicalState.PregnantFemale, 3));
        Assert.True(AnimalIndicatorGeometry.ShouldShowForPlayer(true, isSneaking: true));
        Assert.False(AnimalIndicatorGeometry.ShouldShowForPlayer(true, isSneaking: false));
    }

    [Fact]
    public void AnimalIndicatorGeometry_FadesSmoothlyWithDistance()
    {
        Assert.Equal(1.0f, AnimalIndicatorGeometry.ComputeDistanceAlpha(2.0, maxRange: 8.0), 3);
        Assert.InRange(AnimalIndicatorGeometry.ComputeDistanceAlpha(6.5, maxRange: 8.0), 0.1f, 0.99f);
        Assert.Equal(0.0f, AnimalIndicatorGeometry.ComputeDistanceAlpha(9.0, maxRange: 8.0), 3);
    }

    [Fact]
    public void ResourceBlockClassifier_MatchesConfiguredCategoriesOnly()
    {
        var allOn = new ResourceParticlesOptions(true, true, true);

        Assert.Equal(ResourceCategory.Resin, ResourceBlockClassifier.Classify("game:log-resin-pine-ud", allOn));
        Assert.Equal(ResourceCategory.None, ResourceBlockClassifier.Classify("game:log-resinharvested-pine-ud", allOn));
        Assert.Equal(ResourceCategory.TerraPreta, ResourceBlockClassifier.Classify("game:soil-compost-none", allOn));
        Assert.Equal(ResourceCategory.MeteoricIron, ResourceBlockClassifier.Classify("game:meteorite-iron", allOn));
        Assert.Equal(ResourceCategory.None, ResourceBlockClassifier.Classify("game:rawclay-fire-verysparse", allOn));

        Assert.Equal(ResourceCategory.None, ResourceBlockClassifier.Classify("game:meteorite-iron", allOn with { MeteoricIron = false }));
    }

    [Fact]
    public void AnimalIndicatorFormatter_BuildSquareTokens_SeparatesSexPregnancyAndGeneration()
    {
        IReadOnlyList<AnimalSquareBadge> maleSquares = AnimalIndicatorFormatter.BuildSquareTokens(AnimalBiologicalState.Male, 4);
        Assert.Equal(2, maleSquares.Count);
        Assert.Equal(AnimalSquareKind.SexMale, maleSquares[0].Kind);
        Assert.Equal("\u2642", maleSquares[0].Text);
        Assert.Equal(AnimalSquareKind.Generation, maleSquares[1].Kind);
        Assert.Equal("IV", maleSquares[1].Text);

        IReadOnlyList<AnimalSquareBadge> pregnantSquares = AnimalIndicatorFormatter.BuildSquareTokens(AnimalBiologicalState.PregnantFemale, 2);
        Assert.Equal(3, pregnantSquares.Count);
        Assert.Equal(AnimalSquareKind.SexFemale, pregnantSquares[0].Kind);
        Assert.Equal("\u2640", pregnantSquares[0].Text);
        Assert.Equal(AnimalSquareKind.Pregnant, pregnantSquares[1].Kind);
        Assert.Equal("+", pregnantSquares[1].Text);
        Assert.Equal(AnimalSquareKind.Generation, pregnantSquares[2].Kind);
        Assert.Equal("II", pregnantSquares[2].Text);

        IReadOnlyList<AnimalSquareBadge> wildSquares = AnimalIndicatorFormatter.BuildSquareTokens(AnimalBiologicalState.Female, 0);
        Assert.Single(wildSquares);
        Assert.Equal(AnimalSquareKind.SexFemale, wildSquares[0].Kind);
    }

    [Fact]
    public void EntityHudClassifier_ClassifiesAndFiltersByConfiguredEntityType()
    {
        var drifter = new EntityHudDescriptor("game:drifter-normal", "EntityDrifter", IsPlayer: false, IsTraderOrNpc: false, IsAlive: true, 12f, 20f, IsHostileAi: true);
        var wolf = new EntityHudDescriptor("game:wolf-male", "EntityAgent", IsPlayer: false, IsTraderOrNpc: false, IsAlive: true, 10f, 10f, IsHostileAi: false);
        var deer = new EntityHudDescriptor("game:deer-elk-female", "EntityAgent", IsPlayer: false, IsTraderOrNpc: false, IsAlive: true, 16f, 16f, IsHostileAi: false);
        var trader = new EntityHudDescriptor("game:humanoid-trader-artisan", "EntityTrader", IsPlayer: false, IsTraderOrNpc: true, IsAlive: true, 20f, 20f, IsHostileAi: false);
        var player = new EntityHudDescriptor("game:player", "EntityPlayer", IsPlayer: true, IsTraderOrNpc: false, IsAlive: true, 15f, 15f, IsHostileAi: false);
        var dead = new EntityHudDescriptor("game:drifter-normal", "EntityDrifter", IsPlayer: false, IsTraderOrNpc: false, IsAlive: false, 0f, 20f, IsHostileAi: true);

        Assert.Equal(EntityHudCategory.Hostile, EntityHudClassifier.Classify(drifter));
        Assert.Equal(EntityHudCategory.Hostile, EntityHudClassifier.Classify(wolf));
        Assert.Equal(EntityHudCategory.PassiveFauna, EntityHudClassifier.Classify(deer));
        Assert.Equal(EntityHudCategory.TraderNpc, EntityHudClassifier.Classify(trader));
        Assert.Equal(EntityHudCategory.Player, EntityHudClassifier.Classify(player));
        Assert.Equal(EntityHudCategory.Excluded, EntityHudClassifier.Classify(dead));

        var filter = new EntityHealthBarFilter(
            Enabled: true,
            ShowHostiles: true,
            ShowPassiveFauna: false,
            ShowTradersAndNpcs: false,
            ShowPlayers: true,
            OnlyWhenDamaged: false,
            RequireSneak: false);

        Assert.True(EntityHudClassifier.ShouldShowHealthBar(EntityHudCategory.Hostile, 12f, 20f, isSneaking: false, filter));
        Assert.False(EntityHudClassifier.ShouldShowHealthBar(EntityHudCategory.PassiveFauna, 16f, 16f, isSneaking: true, filter));
        Assert.False(EntityHudClassifier.ShouldShowHealthBar(EntityHudCategory.TraderNpc, 20f, 20f, isSneaking: true, filter));
        Assert.True(EntityHudClassifier.ShouldShowHealthBar(EntityHudCategory.Player, 15f, 15f, isSneaking: false, filter));

        var damagedOnly = filter with { OnlyWhenDamaged = true, ShowPassiveFauna = true };
        Assert.False(EntityHudClassifier.ShouldShowHealthBar(EntityHudCategory.PassiveFauna, 16f, 16f, isSneaking: false, damagedOnly));
        Assert.True(EntityHudClassifier.ShouldShowHealthBar(EntityHudCategory.Hostile, 12f, 20f, isSneaking: false, damagedOnly));

        Assert.Equal("12/20", EntityHudClassifier.FormatHealthText(12f, 20f));
        Assert.Equal("0.4/20", EntityHudClassifier.FormatHealthText(0.4f, 20f));
    }

    [Fact]
    public void EntityHealthBarVisibilityState_RespectsLookAtAndLingerDelayAndAlwaysShowOption()
    {
        var state = new EntityHealthBarVisibilityState();

        // By default (alwaysShow = false), unlooked entities are hidden
        Assert.False(state.IsEntityVisible(entityId: 10, lookedAtEntityId: null, alwaysShow: false, nowMs: 1000, lingerSeconds: 2.5));

        // Looking at entity 10 makes it visible
        Assert.True(state.IsEntityVisible(entityId: 10, lookedAtEntityId: 10, alwaysShow: false, nowMs: 1000, lingerSeconds: 2.5));

        // After looking away, stays visible during the 2.5s (2500ms) linger window
        Assert.True(state.IsEntityVisible(entityId: 10, lookedAtEntityId: 20, alwaysShow: false, nowMs: 3499, lingerSeconds: 2.5));
        // Looking at entity 20 shows entity 20 immediately too
        Assert.True(state.IsEntityVisible(entityId: 20, lookedAtEntityId: 20, alwaysShow: false, nowMs: 3499, lingerSeconds: 2.5));

        // At exactly 1000 + 2500 = 3500ms, entity 10 expires while entity 20 lingers
        Assert.False(state.IsEntityVisible(entityId: 10, lookedAtEntityId: null, alwaysShow: false, nowMs: 3500, lingerSeconds: 2.5));
        Assert.True(state.IsEntityVisible(entityId: 20, lookedAtEntityId: null, alwaysShow: false, nowMs: 5998, lingerSeconds: 2.5));
        Assert.False(state.IsEntityVisible(entityId: 20, lookedAtEntityId: null, alwaysShow: false, nowMs: 5999, lingerSeconds: 2.5));

        // Zero linger hides immediately once not looked at
        Assert.True(state.IsEntityVisible(entityId: 30, lookedAtEntityId: 30, alwaysShow: false, nowMs: 7000, lingerSeconds: 0.0));
        Assert.False(state.IsEntityVisible(entityId: 30, lookedAtEntityId: null, alwaysShow: false, nowMs: 7001, lingerSeconds: 0.0));

        // AlwaysShow bypasses look-at requirement
        Assert.True(state.IsEntityVisible(entityId: 99, lookedAtEntityId: null, alwaysShow: true, nowMs: 8000, lingerSeconds: 2.5));

        state.Reset();
        Assert.False(state.IsEntityVisible(entityId: 20, lookedAtEntityId: null, alwaysShow: false, nowMs: 4000, lingerSeconds: 2.5));
    }
}
