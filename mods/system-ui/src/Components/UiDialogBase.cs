using Vintagestory.API.Client;

namespace SystemUI;

/// <summary>
/// Clase base para dialogos modulares de System UI sobre GuiDialog nativo.
/// </summary>
public abstract class UiDialogBase : GuiDialog
{
    protected SystemUITheme Theme { get; }
    protected abstract string ComposerKey { get; }
    protected abstract string DialogTitle { get; }
    protected virtual double DialogWidth => 780;
    protected virtual double DialogHeight => 520;

    protected UiDialogBase(ICoreClientAPI capi, SystemUITheme? theme = null) : base(capi)
    {
        Theme = theme ?? capi.ModLoader.GetModSystem<SystemUIModSystem>()?.Theme ?? new SystemUITheme();
    }

    public override string ToggleKeyCombinationCode => string.Empty;

    protected virtual ElementBounds CreateDialogBounds() =>
        ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

    protected void ComposeDialog()
    {
        ElementBounds dialogBounds = CreateDialogBounds();
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(Theme.DialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        ElementBounds contentBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight + Theme.SectionGap, DialogWidth, DialogHeight);
        bgBounds.WithChild(contentBounds);

        GuiComposer composer = capi.Gui
            .CreateCompo(ComposerKey, dialogBounds)
            .AddShadedDialogBG(bgBounds, true, 5, Theme.PanelAlpha)
            .AddDialogTitleBar(DialogTitle, OnTitleBarClose, Theme.TitleFont(), null, "titleBar")
            .BeginChildElements(bgBounds);

        ComposeContent(composer, contentBounds);

        SingleComposer = composer
            .EndChildElements()
            .Compose();

        OnComposed(SingleComposer);
    }

    protected virtual void OnComposed(GuiComposer composer)
    {
    }

    protected abstract void ComposeContent(GuiComposer composer, ElementBounds contentBounds);

    public void RefreshView()
    {
        if (IsOpened())
        {
            ComposeDialog();
        }
    }

    public override void OnGuiOpened()
    {
        ComposeDialog();
        base.OnGuiOpened();
    }

    protected virtual void OnTitleBarClose() => TryClose();
}
