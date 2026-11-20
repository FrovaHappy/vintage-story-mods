using SeraphQoL.Inventory;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class EquipLightPlannerTests
{
    [Fact]
    public void PlanEquip_SelectsBrightestUnlockedCandidate_PreferringBackpackOnTie()
    {
        var candidates = new List<LightSourceCandidate>
        {
            new("hotbar", 2, "game:torch-basic-lit", 14, 5, 7, false),
            new("backpack", 4, "game:lantern-copper-up", 18, 5, 4, false),
            new("backpack", 5, "game:lantern-iron-up", 18, 5, 2, true),
            new("hotbar", 6, "game:lantern-brass-up", 18, 5, 4, false)
        };

        EquipLightDecision? decision = EquipLightPlanner.PlanEquip(
            candidates,
            targetInventoryClass: "hotbar",
            targetSlotIndex: 10,
            currentTargetItemCode: null,
            previousSwap: null);

        Assert.NotNull(decision);
        Assert.False(decision!.IsRestore);
        Assert.Equal("backpack", decision.SourceInventoryClass);
        Assert.Equal(4, decision.SourceSlotIndex);
        Assert.Equal("hotbar", decision.TargetInventoryClass);
        Assert.Equal(10, decision.TargetSlotIndex);
    }

    [Fact]
    public void PlanEquip_WhenTargetAlreadyHoldsPreviousLight_RestoresPreviousSlot()
    {
        var candidates = new List<LightSourceCandidate>
        {
            new("backpack", 4, "game:torch-basic-lit", 14, 5, 7, false),
            new("hotbar", 10, "game:lantern-copper-up", 18, 5, 4, false)
        };
        var previous = new EquipLightSwapState("backpack", 4, "hotbar", 10, "game:lantern-copper-up");

        EquipLightDecision? decision = EquipLightPlanner.PlanEquip(
            candidates,
            targetInventoryClass: "hotbar",
            targetSlotIndex: 10,
            currentTargetItemCode: "game:lantern-copper-up",
            previousSwap: previous);

        Assert.NotNull(decision);
        Assert.True(decision!.IsRestore);
        Assert.Equal("backpack", decision.SourceInventoryClass);
        Assert.Equal(4, decision.SourceSlotIndex);
        Assert.Equal("hotbar", decision.TargetInventoryClass);
        Assert.Equal(10, decision.TargetSlotIndex);
    }

    [Fact]
    public void PlanEquip_WhenTargetAlreadyHoldsBrightestAndNoPreviousSwap_ReturnsNull()
    {
        var candidates = new List<LightSourceCandidate>
        {
            new("hotbar", 10, "game:lantern-copper-up", 18, 5, 4, false),
            new("backpack", 0, "game:torch-basic-lit", 14, 5, 7, false)
        };

        EquipLightDecision? decision = EquipLightPlanner.PlanEquip(
            candidates,
            targetInventoryClass: "hotbar",
            targetSlotIndex: 10,
            currentTargetItemCode: "game:lantern-copper-up",
            previousSwap: null);

        Assert.Null(decision);
    }

    [Fact]
    public void EquipShieldPlanner_SelectsBestUnlockedShield_PreferringHigherProtectionThenDurabilityThenBackpack()
    {
        var candidates = new List<ShieldCandidate>
        {
            new("hotbar", 1, "game:shield-crude", DamageAbsorption: 3.0f, ProjectileDamageAbsorption: 3.0f, RemainingDurability: 100, MaxDurability: 100, IsLocked: false),
            new("backpack", 2, "game:shield-round-metal", DamageAbsorption: 5.0f, ProjectileDamageAbsorption: 10.0f, RemainingDurability: 300, MaxDurability: 400, IsLocked: false),
            new("backpack", 3, "game:shield-blackguard", DamageAbsorption: 5.5f, ProjectileDamageAbsorption: 12.0f, RemainingDurability: 500, MaxDurability: 500, IsLocked: true),
            new("hotbar", 4, "game:shield-round-metal", DamageAbsorption: 5.0f, ProjectileDamageAbsorption: 10.0f, RemainingDurability: 200, MaxDurability: 400, IsLocked: false),
            new("backpack", 5, "game:shield-broken", DamageAbsorption: 6.0f, ProjectileDamageAbsorption: 12.0f, RemainingDurability: 0, MaxDurability: 500, IsLocked: false)
        };

        EquipLightDecision? decision = EquipShieldPlanner.PlanEquip(
            candidates,
            targetInventoryClass: "hotbar",
            targetSlotIndex: 10,
            currentTargetItemCode: null,
            previousSwap: null);

        Assert.NotNull(decision);
        Assert.False(decision!.IsRestore);
        Assert.Equal("backpack", decision.SourceInventoryClass);
        Assert.Equal(2, decision.SourceSlotIndex);
        Assert.Equal("hotbar", decision.TargetInventoryClass);
        Assert.Equal(10, decision.TargetSlotIndex);
    }

    [Fact]
    public void EquipShieldPlanner_RestoresPreviousItemWhenToggledAgain()
    {
        var candidates = new List<ShieldCandidate>
        {
            new("hotbar", 10, "game:shield-blackguard", 5.5f, 12.0f, 500, 500, false)
        };
        var previous = new EquipLightSwapState("backpack", 4, "hotbar", 10, "game:shield-blackguard");

        EquipLightDecision? decision = EquipShieldPlanner.PlanEquip(
            candidates,
            targetInventoryClass: "hotbar",
            targetSlotIndex: 10,
            currentTargetItemCode: "game:shield-blackguard",
            previousSwap: previous);

        Assert.NotNull(decision);
        Assert.True(decision!.IsRestore);
        Assert.Equal("backpack", decision.SourceInventoryClass);
        Assert.Equal(4, decision.SourceSlotIndex);
    }

    [Fact]
    public void EquipShieldPlanner_WhenTargetAlreadyHoldsEqualOrBetterShield_ReturnsNull()
    {
        var candidates = new List<ShieldCandidate>
        {
            new("hotbar", 10, "game:shield-blackguard", 5.5f, 12.0f, 450, 500, false),
            new("backpack", 1, "game:shield-crude", 3.0f, 3.0f, 100, 100, false)
        };

        EquipLightDecision? decision = EquipShieldPlanner.PlanEquip(
            candidates,
            targetInventoryClass: "hotbar",
            targetSlotIndex: 10,
            currentTargetItemCode: "game:shield-blackguard",
            previousSwap: null);

        Assert.Null(decision);
    }
}
