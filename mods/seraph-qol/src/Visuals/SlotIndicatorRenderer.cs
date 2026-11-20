using System.Reflection;
using Cairo;
using HarmonyLib;
using SeraphQoL.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SeraphQoL.Visuals;

/// <summary>
/// Evalua y renderiza barras compactas de estado sobre slots usando texturas Cairo premultiplicadas:
/// omite herramientas que ya muestran durabilidad nativa, ubica liquidos al centro y el resto arriba.
/// </summary>
public sealed class SlotIndicatorRenderer : IDisposable
{
    private const double CacheWindowMs = 180;
    private static readonly FieldInfo? NvgCapacityField =
        AccessTools.Field(typeof(ItemNightvisiondevice), "fuelHoursCapacity");

    private readonly ICoreClientAPI _capi;
    private readonly SeraphQoLModSystem _modSystem;
    private readonly Dictionary<ItemSlot, CachedSlotEval> _cache = new();

    private LoadedTexture _whiteTex;
    private bool _texLoaded;
    private PendingSlotDraw _pending;

    public SlotIndicatorRenderer(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
        _whiteTex = new LoadedTexture(capi);
    }

    public void RenderBeforeItem(IRenderAPI render, ItemSlot slot, double centerX, double centerY, float itemSize)
    {
        _pending = default;

        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        if (!cfg.EnableSlotIndicators || slot.Empty || slot.Itemstack?.Collectible == null || _capi.World == null)
        {
            return;
        }

        var options = new SlotIndicatorOptions(
            Enabled: cfg.EnableSlotIndicators,
            ShowFreshness: cfg.ShowSlotFreshnessIndicator,
            ShowDurability: cfg.ShowSlotDurabilityIndicator,
            ShowPreparation: cfg.ShowSlotPreparationIndicator,
            ShowLiquid: cfg.ShowSlotLiquidIndicator,
            ShowCrucible: cfg.ShowSlotCrucibleIndicator,
            ShowFoodContainer: cfg.ShowSlotFoodContainerIndicator,
            ShowNightVision: cfg.ShowSlotNightVisionIndicator);

        SlotIndicatorInput input = GetOrComputeInput(slot, slot.Itemstack);
        SlotIndicatorResult res = SlotIndicatorPlanner.Evaluate(input, options);
        if (res.Kind == SlotIndicatorKind.None) return;

        EnsureWhiteTexture();
        if (_whiteTex.TextureId <= 0) return;

        float slotExtent = (float)Math.Max(itemSize * 1.12, GuiElement.scaled(GuiElementPassiveItemSlot.unscaledItemSize - 4.0));
        float left = (float)(centerX - (slotExtent * 0.5));
        float top = (float)(centerY - (slotExtent * 0.5));

        _pending = new PendingSlotDraw(true, res, left, top, slotExtent);
    }

    public void RenderAfterScissor(IRenderAPI render)
    {
        PendingSlotDraw draw = _pending;
        _pending = default;

        if (!draw.Active || _whiteTex.TextureId <= 0) return;

        float padX = (float)GuiElement.scaled(4.5);
        float barH = (float)Math.Max(2.5, GuiElement.scaled(3.2));
        float totalW = Math.Max(6f, draw.Size - (padX * 2f));
        float barX = draw.Left + padX;
        float barY = draw.Result.BarPosition == SlotBarPosition.Middle
            ? draw.Top + (draw.Size * 0.5f) - (barH * 0.5f)
            : draw.Top + (float)GuiElement.scaled(3.0);

        float alpha = 0.96f;
        if (draw.Result.IsMolten || draw.Result.IsUrgent)
        {
            double ms = _capi.World?.ElapsedMilliseconds ?? 0;
            alpha = (float)(0.68 + (0.30 * Math.Sin((ms % 1200) / 1200.0 * Math.PI * 2.0)));
        }

        render.GLDisableDepthTest();
        render.Render2DTexturePremultipliedAlpha(
            _whiteTex.TextureId,
            barX,
            barY,
            totalW,
            barH,
            245f,
            Premultiply(0.06f, 0.05f, 0.04f, 0.82f));

        render.Render2DTexturePremultipliedAlpha(
            _whiteTex.TextureId,
            barX,
            barY,
            Math.Max(2f, totalW * draw.Result.FillRatio),
            barH,
            246f,
            ResolvePremultipliedColor(draw.Result, alpha));
        render.GLEnableDepthTest();
    }

    public void ClearCache() => _cache.Clear();

    private SlotIndicatorInput GetOrComputeInput(ItemSlot slot, ItemStack stack)
    {
        long nowMs = _capi.World.ElapsedMilliseconds;
        int colId = stack.Collectible.Id;
        int stackSize = stack.StackSize;

        if (_cache.TryGetValue(slot, out CachedSlotEval cached)
            && cached.CollectibleId == colId
            && cached.StackSize == stackSize
            && (nowMs - cached.TimestampMs) < CacheWindowMs)
        {
            return cached.Input;
        }

        SlotIndicatorInput computed = InspectStack(slot, stack);
        if (_cache.Count > 512) _cache.Clear();
        _cache[slot] = new CachedSlotEval(colId, stackSize, nowMs, computed);
        return computed;
    }

    private SlotIndicatorInput InspectStack(ItemSlot slot, ItemStack stack)
    {
        CollectibleObject col = stack.Collectible;

        float? nvFuel = null;
        if (col is ItemNightvisiondevice nvg)
        {
            double cap = NvgCapacityField?.GetValue(nvg) is float f && f > 0.1f ? f : 24.0;
            nvFuel = (float)Math.Clamp(nvg.GetFuelHours(stack) / cap, 0.0, 1.0);
        }

        float? crucibleFill = null;
        bool crucibleMolten = false;
        if (col is BlockSmeltedContainer smelted)
        {
            KeyValuePair<ItemStack, int> contents = smelted.GetContents(_capi.World, stack);
            if (contents.Value > 0)
            {
                crucibleFill = Math.Clamp(contents.Value / 2000f, 0.06f, 1f);
                crucibleMolten = contents.Key != null && !smelted.HasSolidifed(stack, contents.Key, _capi.World);
            }
        }
        else if (col is BlockSmeltingContainer && stack.Attributes?.GetTreeAttribute("contents") is { Count: > 0 } contentsAttr)
        {
            crucibleFill = Math.Clamp(contentsAttr.Count / 4f, 0.12f, 1f);
        }

        float? liquidFill = null;
        if (col is BlockLiquidContainerBase liquidBlock
            && col.Attributes?.IsTrue("mealContainer") != true)
        {
            float cap = Math.Max(0.1f, liquidBlock.CapacityLitres);
            float cur = liquidBlock.GetCurrentLitres(stack);
            if (cur > 0.001f) liquidFill = Math.Clamp(cur / cap, 0.04f, 1f);
        }
        else if (col is BlockWateringCan wc)
        {
            float cap = Math.Max(1f, wc.CapacitySeconds);
            float cur = wc.GetRemainingWateringSeconds(stack);
            if (cur > 0.001f) liquidFill = Math.Clamp(cur / cap, 0.04f, 1f);
        }

        float? foodFill = null;
        IBlockMealContainer? mealContainer = col.GetCollectibleInterface<IBlockMealContainer>();
        if (mealContainer != null || col is BlockCookingContainer or BlockMeal or BlockCrock)
        {
            float servings = 0f;
            if (mealContainer != null)
            {
                servings = mealContainer.GetQuantityServings(_capi.World, stack);
            }
            else if (stack.Attributes?.HasAttribute("quantityServings") == true)
            {
                servings = stack.Attributes.GetFloat("quantityServings", 0f);
            }

            if (servings > 0.01f)
            {
                float cap = col.Attributes?["servingCapacity"].AsFloat(col is BlockCrock ? 4f : 6f)
                    ?? (col is BlockCrock ? 4f : 6f);
                foodFill = Math.Clamp(servings / Math.Max(1f, cap), 0.05f, 1f);
            }
            else if (stack.Attributes?.HasAttribute("pieSize") == true)
            {
                int pieSize = stack.Attributes.GetAsInt("pieSize", 0);
                if (pieSize > 0) foodFill = Math.Clamp(pieSize / 4f, 0.25f, 1f);
            }
        }

        float? perishRemaining = null;
        float? prepProgress = null;

        TransitionState[]? states = slot.Inventory != null
            ? col.UpdateAndGetTransitionStates(_capi.World, slot)
            : null;
        if (states != null)
        {
            foreach (TransitionState st in states)
            {
                if (st?.Props == null) continue;
                if (st.Props.Type == EnumTransitionType.Perish)
                {
                    perishRemaining = ComputePerishRatio(st);
                }
                else if (st.Props.Type is EnumTransitionType.Dry or EnumTransitionType.Cure or EnumTransitionType.Ripen or EnumTransitionType.Convert or EnumTransitionType.Melt or EnumTransitionType.Harden)
                {
                    if (st.TransitionLevel > 0.01f && st.TransitionLevel < 0.999f)
                    {
                        prepProgress = Math.Clamp(st.TransitionLevel, 0f, 1f);
                    }
                }
            }
        }
        else if (mealContainer != null && slot.Inventory != null)
        {
            ItemStack[]? contents = mealContainer.GetNonEmptyContents(_capi.World, stack);
            if (contents != null && contents.Length > 0)
            {
                ItemSlot? dummy = BlockCrock.GetDummySlotForFirstPerishableStack(_capi.World, contents, _capi.World.Player?.Entity, slot.Inventory as InventoryBase);
                TransitionState? mealPerish = dummy?.Itemstack?.Collectible?.UpdateAndGetTransitionState(_capi.World, dummy, EnumTransitionType.Perish);
                if (mealPerish != null)
                {
                    perishRemaining = ComputePerishRatio(mealPerish);
                }
            }
        }

        // Omitir herramientas/armas/armaduras que ya muestran la barra de durabilidad nativa de Vintage Story
        float? durabilityRemaining = null;
        if (!col.ShouldDisplayItemDamage(stack))
        {
            int maxDur = col.GetMaxDurability(stack);
            if (maxDur > 0)
            {
                durabilityRemaining = Math.Clamp((float)col.GetRemainingDurability(stack) / maxDur, 0f, 1f);
            }
            else if (stack.Attributes?.HasAttribute("condition") == true)
            {
                durabilityRemaining = Math.Clamp(stack.Attributes.GetFloat("condition", 1f), 0f, 1f);
            }
        }

        return new SlotIndicatorInput(
            HasItem: true,
            PerishRatioRemaining: perishRemaining,
            DurabilityRatioRemaining: durabilityRemaining,
            PreparationProgress: prepProgress,
            LiquidFillRatio: liquidFill,
            CrucibleFillRatio: crucibleFill,
            CrucibleIsMolten: crucibleMolten,
            FoodContainerFillRatio: foodFill,
            NightVisionFuelRatio: nvFuel);
    }

    private static float? ComputePerishRatio(TransitionState st)
    {
        if (st.TransitionedHours > 0.001f)
        {
            return Math.Clamp((1f - st.TransitionLevel) * 0.25f, 0f, 0.25f);
        }
        if (st.FreshHours > 0.01f)
        {
            return Math.Clamp(st.FreshHoursLeft / st.FreshHours, 0f, 1f);
        }
        return null;
    }

    private void EnsureWhiteTexture()
    {
        if (_texLoaded) return;
        _texLoaded = true;

        using var surface = new ImageSurface(Format.Argb32, 4, 4);
        using var ctx = new Context(surface);
        ctx.SetSourceRGBA(1.0, 1.0, 1.0, 1.0);
        ctx.Paint();
        _capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: false, ref _whiteTex);
    }

    private static Vec4f ResolvePremultipliedColor(SlotIndicatorResult res, float alpha)
    {
        return res.Kind switch
        {
            SlotIndicatorKind.Freshness or SlotIndicatorKind.Durability => HealthColor(res.FillRatio, alpha),
            SlotIndicatorKind.Preparation => Premultiply(0.84f, 0.66f, 0.34f, alpha),
            SlotIndicatorKind.LiquidVolume => Premultiply(0.30f, 0.66f, 0.94f, alpha),
            SlotIndicatorKind.CrucibleContent => res.IsMolten
                ? Premultiply(1.00f, 0.54f, 0.16f, alpha)
                : Premultiply(0.78f, 0.69f, 0.45f, alpha),
            SlotIndicatorKind.FoodContainer => Premultiply(0.88f, 0.76f, 0.36f, alpha),
            SlotIndicatorKind.NightVisionFuel => Premultiply(0.28f, 0.88f, 0.76f, alpha),
            _ => Premultiply(0.80f, 0.80f, 0.80f, alpha)
        };
    }

    private static Vec4f HealthColor(float ratio, float alpha)
    {
        if (ratio >= 0.55f) return Premultiply(0.33f, 0.84f, 0.38f, alpha);
        if (ratio >= 0.25f) return Premultiply(0.92f, 0.72f, 0.25f, alpha);
        return Premultiply(0.92f, 0.29f, 0.23f, alpha);
    }

    private static Vec4f Premultiply(float r, float g, float b, float alpha) =>
        new(r * alpha, g * alpha, b * alpha, alpha);

    public void Dispose() => _whiteTex.Dispose();

    private readonly record struct CachedSlotEval(int CollectibleId, int StackSize, long TimestampMs, SlotIndicatorInput Input);
    private readonly record struct PendingSlotDraw(bool Active, SlotIndicatorResult Result, float Left, float Top, float Size);
}
