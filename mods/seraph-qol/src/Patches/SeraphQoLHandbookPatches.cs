using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using SeraphQoL.Handbook;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace SeraphQoL.Patches;

/// <summary>
/// Parches Harmony ligeros para las mejoras de recetas del Handbook (scroll de variantes,
/// auto-relleno de grilla, ícono de herramienta/durabilidad y tamaño máximo de stack).
/// </summary>
[HarmonyPatch]
public static class SeraphQoLHandbookPatches
{
    public static SeraphQoLModSystem? ClientInstance { get; set; }
    public static CraftingRecipeIngredient? CurrentIngredient;

    private static readonly MethodInfo? PointInsideMethod =
        AccessTools.Method(typeof(Rectangled), nameof(Rectangled.PointInside), [typeof(double), typeof(double)]);

    private static readonly MethodInfo? UpdateGridHookMethod =
        AccessTools.Method(typeof(SeraphQoLHandbookPatches), nameof(UpdateGridSlideshow));

    private static readonly MethodInfo? UpdateStackHookMethod =
        AccessTools.Method(typeof(SeraphQoLHandbookPatches), nameof(UpdateItemstackSlideshow));

    private static readonly FieldInfo? CurrentIngredientField =
        AccessTools.Field(typeof(SeraphQoLHandbookPatches), nameof(CurrentIngredient));

    public static void UpdateGridSlideshow(
        bool mouseOver,
        ref int index,
        ref int secondCounter,
        RichTextComponentBase component)
    {
        ClientInstance?.HandbookService?.UpdateSlideshowIndex(mouseOver, ref index, ref secondCounter, component);
    }

    public static void UpdateItemstackSlideshow(
        bool mouseOver,
        ref int index,
        RichTextComponentBase component)
    {
        int unused = 0;
        ClientInstance?.HandbookService?.UpdateSlideshowIndex(mouseOver, ref index, ref unused, component);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SlideshowGridRecipeTextComponent), nameof(SlideshowGridRecipeTextComponent.RenderInteractiveElements))]
    public static IEnumerable<CodeInstruction> TranspileSlideshowGrid(IEnumerable<CodeInstruction> instructions) =>
        TranspileSlideshow(instructions, typeof(SlideshowGridRecipeTextComponent), isGrid: true);

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SlideshowItemstackTextComponent), nameof(SlideshowItemstackTextComponent.RenderInteractiveElements))]
    public static IEnumerable<CodeInstruction> TranspileSlideshowStack(IEnumerable<CodeInstruction> instructions) =>
        TranspileSlideshow(instructions, typeof(SlideshowItemstackTextComponent), isGrid: false);

    private static IEnumerable<CodeInstruction> TranspileSlideshow(
        IEnumerable<CodeInstruction> instructions,
        Type componentType,
        bool isGrid)
    {
        FieldInfo? indexField = AccessTools.Field(componentType, isGrid ? "currentItemIndex" : "curItemIndex");
        FieldInfo? secondCounterField = isGrid ? AccessTools.Field(componentType, "secondCounter") : null;
        MethodInfo? hook = isGrid ? UpdateGridHookMethod : UpdateStackHookMethod;

        bool lookForElement = isGrid;
        bool lookForPointInside = indexField != null && hook != null && (!isGrid || secondCounterField != null);

        if (isGrid)
        {
            CurrentIngredient = null;
        }

        foreach (CodeInstruction inst in instructions)
        {
            if (isGrid && inst.opcode == OpCodes.Ret && CurrentIngredientField != null)
            {
                yield return new CodeInstruction(OpCodes.Ldnull)
                {
                    labels = inst.labels,
                    blocks = inst.blocks
                };
                yield return new CodeInstruction(OpCodes.Stsfld, CurrentIngredientField);
                yield return new CodeInstruction(OpCodes.Ret);
                lookForElement = false;
                continue;
            }

            yield return inst;

            if (lookForElement
                && CurrentIngredientField != null
                && inst.operand is MethodInfo called
                && called.Name == "GetElementInGrid")
            {
                yield return new CodeInstruction(OpCodes.Stsfld, CurrentIngredientField);
                yield return new CodeInstruction(OpCodes.Ldsfld, CurrentIngredientField);
                lookForElement = false;
            }

            if (lookForPointInside
                && PointInsideMethod != null
                && inst.Calls(PointInsideMethod))
            {
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldflda, indexField!);
                if (isGrid)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, secondCounterField!);
                }
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, hook!);
                lookForPointInside = false;
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GuiDialog), nameof(GuiDialog.OnMouseWheel))]
    public static void PrefixDialogMouseWheel(GuiDialog __instance, MouseWheelEventArgs args)
    {
        if (__instance is GuiDialogHandbook)
        {
            ClientInstance?.HandbookService?.OnHandbookMouseWheel(args);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GuiDialog), nameof(GuiDialog.OnMouseMove))]
    public static void PrefixDialogMouseMove(GuiDialog __instance)
    {
        if (__instance is GuiDialogHandbook)
        {
            ClientInstance?.HandbookService?.OnHandbookMouseMove();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GuiDialogHandbook), nameof(GuiDialogHandbook.OnRenderGUI))]
    public static void PrefixHandbookRenderGui()
    {
        ClientInstance?.HandbookService?.OnHandbookRenderBegin();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiDialogHandbook), nameof(GuiDialogHandbook.OnGuiOpened))]
    public static void PostfixHandbookOpened()
    {
        ClientInstance?.HandbookService?.RequestNearbyScanIfNeeded(force: true);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHandbookRecipeRender))]
    public static void PostfixHandbookRecipeRender(CollectibleObject __instance, IRecipeBase? recipe, double x, double y, double size)
    {
        ClientInstance?.HandbookService?.OnHandbookRecipeRender(__instance, recipe, CurrentIngredient, x, y, size);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetHeldItemInfo))]
    public static void PostfixGetHeldItemInfo(CollectibleObject __instance, StringBuilder dsc)
    {
        ClientInstance?.HandbookService?.OnGetHeldItemInfo(__instance, dsc);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo), "addCreatedByInfo")]
    public static void PostfixAddCreatedByInfo(List<RichTextComponentBase> components)
    {
        ClientInstance?.HandbookService?.OnCreatedByInfo(components);
    }
}
