using Cairo;
using Vintagestory.API.Client;

namespace SystemUI;

/// <summary>
/// Elemento visual dinamico y pasivo (sin captura de clics) capaz de desplazarse y recortarse
/// dentro de contenedores con scroll (<c>BeginClip</c>).
/// </summary>
public abstract class UiPassiveDynamicElement : GuiElement
{
    // Mismo plano Z base que GuiComposer.staticElementsTexture y los controles hijos (50f),
    // de modo que el orden de dibujo muestre el fondo/divisor encima del dialogo base y detras de botones/slots.
    private const float PassiveRenderZ = 50f;

    private LoadedTexture? _texture;

    protected UiPassiveDynamicElement(ICoreClientAPI capi, ElementBounds bounds)
        : base(capi, bounds)
    {
    }

    public sealed override bool IsPositionInside(int posX, int posY) => false;

    public sealed override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
    }

    public sealed override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
    }

    public sealed override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic)
    {
        int w = Math.Max(1, (int)Math.Ceiling(Bounds.OuterWidth));
        int h = Math.Max(1, (int)Math.Ceiling(Bounds.OuterHeight));

        using var surface = new ImageSurface(Format.Argb32, w, h);
        using var ctx = new Context(surface);
        DrawDynamicSurface(ctx, w, h);

        _texture ??= new LoadedTexture(api);
        api.Gui.LoadOrUpdateCairoTexture(surface, true, ref _texture);
    }

    protected abstract void DrawDynamicSurface(Context ctx, int width, int height);

    public sealed override void RenderInteractiveElements(float deltaTime)
    {
        if (_texture is not { TextureId: > 0 }) return;
        if (InsideClipBounds != null && !Bounds.PartiallyInside(InsideClipBounds)) return;

        api.Render.Render2DTexturePremultipliedAlpha(
            _texture.TextureId,
            (float)Bounds.renderX,
            (float)Bounds.renderY,
            _texture.Width,
            _texture.Height,
            PassiveRenderZ);
    }

    public override void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
        base.Dispose();
    }
}
