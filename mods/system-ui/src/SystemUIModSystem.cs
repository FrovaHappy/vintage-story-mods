using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SystemUI;

/// <summary>
/// Punto de entrada cliente de System UI. Provee acceso centralizado al tema visual
/// y utilidades de composicion para los mods dependientes.
/// </summary>
public sealed class SystemUIModSystem : ModSystem
{
    public SystemUITheme Theme { get; } = new();

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        Mod.Logger.Notification("[SeraphUI] Inicializado en cliente.");
    }
}
