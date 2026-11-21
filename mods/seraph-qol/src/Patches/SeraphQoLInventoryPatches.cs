using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SeraphQoL.Backpacks;
using SeraphQoL.Config;
using SeraphQoL.Inventory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace SeraphQoL.Patches;

/// <summary>
/// Parches Harmony ligeros compartidos en ambos lados (servidor y cliente).
/// </summary>
[HarmonyPatch]
public static class SeraphQoLCommonPatches
{
    public static SeraphQoLModSystem? ServerInstance { get; set; }
    public static SeraphQoLModSystem? ClientInstance { get; set; }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.GetSuitability))]
    public static void PostfixGetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, ref float __result)
    {
        if (__result <= 0f) return;
        if (SlotProtectionPolicy.IsRestrictedWildcardSlot(targetSlot))
        {
            __result = 0f;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CollectibleBehaviorHeldBag), nameof(CollectibleBehaviorHeldBag.GetOrCreateSlots))]
    public static void PrefixGetOrCreateSlots(
        CollectibleBehaviorHeldBag __instance,
        ItemStack bagstack,
        InventoryBase parentinv,
        IWorldAccessor world)
    {
        if (bagstack == null || world == null) return;

        SeraphQoLModSystem? mod = world.Side == EnumAppSide.Server
            ? ServerInstance
            : (ClientInstance ?? ServerInstance);
        if (mod == null) return;

        int targetSlots = __instance.GetQuantitySlots(bagstack);
        mod.OnHeldBagGetOrCreateSlots(bagstack, targetSlots, parentinv, world);
    }
}

/// <summary>
/// Parches Harmony ligeros de interfaz cliente que delegan inmediatamente en SeraphQoLModSystem.
/// </summary>
[HarmonyPatch]
public static class SeraphQoLClientPatches
{
    private static readonly FieldInfo? SlotGridInventoryField =
        AccessTools.Field(typeof(GuiElementItemSlotGridBase), "inventory");

    private static readonly FieldInfo? SlotGridApiField =
        AccessTools.Field(typeof(GuiElement), "api");

    private static readonly FieldInfo? SurvivalInvComposerField =
        AccessTools.Field(typeof(GuiDialogInventory), "survivalInvDialog");

    private static readonly FieldInfo? DialogCapiField =
        AccessTools.Field(typeof(GuiDialog), "capi");

    private static readonly MethodInfo? TargetRenderItemstackMethod = AccessTools.Method(
        typeof(IRenderAPI),
        nameof(IRenderAPI.RenderItemstackToGui),
        [typeof(ItemSlot), typeof(double), typeof(double), typeof(double), typeof(float), typeof(int), typeof(float), typeof(bool), typeof(bool), typeof(bool)]);

    private static readonly MethodInfo? TargetPopScissorMethod = AccessTools.Method(
        typeof(IRenderAPI),
        nameof(IRenderAPI.PopScissor));

    private static readonly MethodInfo? HookRenderItemstackMethod = AccessTools.Method(
        typeof(SeraphQoLClientPatches),
        nameof(RenderItemstackWithIndicator));

    private static readonly MethodInfo? HookPopScissorMethod = AccessTools.Method(
        typeof(SeraphQoLClientPatches),
        nameof(PopScissorWithIndicator));

    public static SeraphQoLModSystem? ClientInstance { get; set; }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GuiElementItemSlotGridBase), nameof(GuiElementItemSlotGridBase.RenderInteractiveElements))]
    public static IEnumerable<CodeInstruction> TranspileRenderSlotGrid(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction inst in instructions)
        {
            if (inst.opcode == OpCodes.Callvirt && TargetRenderItemstackMethod != null && inst.Calls(TargetRenderItemstackMethod) && HookRenderItemstackMethod != null)
            {
                yield return new CodeInstruction(OpCodes.Call, HookRenderItemstackMethod)
                {
                    labels = inst.labels,
                    blocks = inst.blocks
                };
                continue;
            }

            if (inst.opcode == OpCodes.Callvirt && TargetPopScissorMethod != null && inst.Calls(TargetPopScissorMethod) && HookPopScissorMethod != null)
            {
                yield return new CodeInstruction(OpCodes.Call, HookPopScissorMethod)
                {
                    labels = inst.labels,
                    blocks = inst.blocks
                };
                continue;
            }

            yield return inst;
        }
    }

    public static void RenderItemstackWithIndicator(
        IRenderAPI renderer,
        ItemSlot slot,
        double posX,
        double posY,
        double posZ,
        float size,
        int color,
        float dt,
        bool shading,
        bool rotate,
        bool showStackSize)
    {
        ClientInstance?.OnRenderSlotItemBefore(renderer, slot, posX, posY, size);
        renderer.RenderItemstackToGui(slot, posX, posY, posZ, size, color, dt, shading, rotate, showStackSize);
    }

    public static void PopScissorWithIndicator(IRenderAPI renderer)
    {
        renderer.PopScissor();
        ClientInstance?.OnRenderSlotItemAfterScissor(renderer);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiDialogInventory), "ComposeSurvivalInvDialog")]
    public static void PostfixComposeSurvivalInvDialog(GuiDialogInventory __instance)
    {
        if (ClientInstance == null) return;
        if (SurvivalInvComposerField?.GetValue(__instance) is not GuiComposer composer) return;
        ClientInstance.OnSurvivalInventoryComposed(composer);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiDialogInventory), "CloseIconPressed")]
    public static void PostfixInventoryClosed()
    {
        ClientInstance?.OnInventoryDialogClosed();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiDialogBlockEntityInventory), nameof(GuiDialogBlockEntityInventory.OnGuiOpened))]
    public static void PostfixContainerOpened(GuiDialogBlockEntityInventory __instance)
    {
        if (ClientInstance == null || __instance.SingleComposer == null) return;
        if (DialogCapiField?.GetValue(__instance) is not ICoreClientAPI) return;
        ClientInstance.OnContainerOpened(__instance.SingleComposer, __instance.Inventory);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElementItemSlotGridBase), nameof(GuiElementItemSlotGridBase.RenderInteractiveElements))]
    public static void PostfixRenderSlotGrid(GuiElementItemSlotGridBase __instance)
    {
        if (ClientInstance == null) return;
        if (SlotGridInventoryField?.GetValue(__instance) is not IInventory inventory) return;
        ClientInstance.OnRenderSlotGrid(__instance, inventory);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GuiElementItemSlotGridBase), "SlotClick")]
    public static bool PrefixSlotClick(GuiElementItemSlotGridBase __instance, int slotId)
    {
        if (ClientInstance == null) return true;
        if (SlotGridApiField?.GetValue(__instance) is not ICoreClientAPI capi) return true;
        if (SlotGridInventoryField?.GetValue(__instance) is not IInventory inventory) return true;

        return !ClientInstance.TryHandleSpecialSlotClick(capi, inventory, slotId);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GuiDialogToolMode), "OnKeyCombinationToggle")]
    public static bool PrefixToolModeToggle(GuiDialogToolMode __instance, ref bool __result)
    {
        if (ClientInstance == null) return true;
        if (!ClientInstance.TryToggleRadialToolModeMenu(out bool handledResult))
        {
            return true;
        }

        __result = handledResult;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WaypointMapLayer), "RebuildMapComponents")]
    public static void PostfixRebuildMapComponents(WaypointMapLayer __instance)
    {
        ClientInstance?.OnWaypointLayerRebuilt(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiDialogBlockEntityFirepit), nameof(GuiDialogBlockEntityFirepit.OnGuiOpened))]
    public static void PostfixFirepitOpened(GuiDialogBlockEntityFirepit __instance)
    {
        ClientInstance?.OnFirepitGuiOpened(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiDialogBlockEntityFirepit), nameof(GuiDialogBlockEntityFirepit.OnGuiClosed))]
    public static void PostfixFirepitClosed(GuiDialogBlockEntityFirepit __instance)
    {
        ClientInstance?.OnFirepitGuiClosed(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiDialogBlockEntityFirepit), "OnInventorySlotModified")]
    public static void PostfixFirepitSlotModified(GuiDialogBlockEntityFirepit __instance)
    {
        ClientInstance?.OnFirepitSlotModified(__instance);
    }
}
