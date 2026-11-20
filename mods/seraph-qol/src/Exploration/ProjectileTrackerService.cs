using HarmonyLib;
using SeraphQoL.Client;
using SeraphQoL.Config;
using SeraphQoL.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SeraphQoL.Exploration;

/// <summary>
/// Rastrea proyectiles lanzados por jugadores en servidor y sincroniza waypoints temporales al aterrizar/desaparecer.
/// </summary>
public sealed class ProjectileTrackerServer
{
    private readonly ICoreServerAPI _sapi;
    private readonly IServerNetworkChannel _channel;
    private readonly Func<EffectiveServerPolicy> _policyProvider;
    private readonly ProjectileTrackerState _state = new();

    private long _tickListenerId;

    public ProjectileTrackerServer(
        ICoreServerAPI sapi,
        IServerNetworkChannel channel,
        Func<EffectiveServerPolicy> policyProvider)
    {
        _sapi = sapi;
        _channel = channel;
        _policyProvider = policyProvider;
    }

    public void Start()
    {
        _sapi.Event.OnEntitySpawn += OnEntitySpawn;
        _sapi.Event.OnEntityLoaded += OnEntityLoaded;
        _sapi.Event.OnEntityDespawn += OnEntityDespawn;
        _tickListenerId = _sapi.Event.RegisterGameTickListener(OnServerTick, 250);
    }

    public void Dispose()
    {
        if (_tickListenerId != 0)
        {
            _sapi.Event.UnregisterGameTickListener(_tickListenerId);
            _tickListenerId = 0;
        }
        _state.Clear();
    }

    private void OnEntitySpawn(Entity entity)
    {
        if (!_policyProvider().AllowProjectileTracker) return;
        if (entity is not EntityProjectile projectile) return;
        if (projectile.FiredBy is not EntityPlayer playerEntity) return;

        string? uid = playerEntity.PlayerUID;
        if (string.IsNullOrWhiteSpace(uid)) return;

        string? itemName = projectile.ProjectileStack?.GetName() ?? projectile.GetName();
        _state.Register(entity.EntityId, uid, itemName, _sapi.World.ElapsedMilliseconds);
    }

    private void OnEntityLoaded(Entity entity)
    {
        _state.TryMarkChunkLoaded(entity.EntityId);
    }

    private void OnEntityDespawn(Entity entity, EntityDespawnData despawnReason)
    {
        if (despawnReason?.Reason == EnumDespawnReason.Unload)
        {
            _state.TryMarkChunkUnloaded(entity.EntityId);
            return;
        }

        if (_state.TryRemove(entity.EntityId, out TrackedProjectileRecord? removed) && removed != null && removed.HasLanded)
        {
            SendRemoveToOwner(removed);
        }
    }

    private void OnServerTick(float dt)
    {
        if (_state.Count == 0) return;

        foreach (TrackedProjectileRecord expired in _state.CollectExpired(_sapi.World.ElapsedMilliseconds, 3600))
        {
            if (expired.HasLanded)
            {
                SendRemoveToOwner(expired);
            }
        }

        bool allowed = _policyProvider().AllowProjectileTracker;
        IReadOnlyList<long> ids = _state.GetTrackedEntityIds();

        foreach (long id in ids)
        {
            if (!_state.TryGet(id, out TrackedProjectileRecord? rec) || rec == null) continue;
            if (rec.IsChunkUnloaded) continue;

            Entity? entity = _sapi.World.GetEntityById(id);
            if (entity == null || !entity.Alive)
            {
                if (_state.TryRemove(id, out TrackedProjectileRecord? gone) && gone != null && gone.HasLanded)
                {
                    SendRemoveToOwner(gone);
                }
                continue;
            }

            if (!allowed || entity is not EntityProjectile projectile) continue;

            if (projectile.Stuck || projectile.Collided || projectile.OnGround)
            {
                if (_state.TryMarkLanded(id, out TrackedProjectileRecord? landed) && landed != null)
                {
                    SendAddToOwner(landed, projectile.Pos.XYZ);
                }
            }
        }
    }

    private void SendAddToOwner(TrackedProjectileRecord record, Vec3d pos)
    {
        if (_sapi.World.PlayerByUid(record.OwnerPlayerUid) is not IServerPlayer owner) return;

        _channel.SendPacket(new ProjectileWaypointPacket
        {
            Remove = false,
            WaypointKey = record.WaypointKey,
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
            Title = record.ItemName
        }, owner);
    }

    private void SendRemoveToOwner(TrackedProjectileRecord record)
    {
        if (_sapi.World.PlayerByUid(record.OwnerPlayerUid) is not IServerPlayer owner) return;

        _channel.SendPacket(new ProjectileWaypointPacket
        {
            Remove = true,
            WaypointKey = record.WaypointKey
        }, owner);
    }
}

/// <summary>
/// Administra waypoints temporales de proyectiles en el mapa del cliente con icono de flecha,
/// persistencia al abrir/cerrar el mapa y eliminacion al recoger o expirar.
/// </summary>
public sealed class ProjectileTrackerClient
{
    public const string CustomArrowIconCode = "seraphqol-arrow";

    private static readonly System.Reflection.FieldInfo? WayPointComponentsField =
        AccessTools.Field(typeof(WaypointMapLayer), "wayPointComponents");

    private static readonly System.Reflection.FieldInfo? TmpWaypointsField =
        AccessTools.Field(typeof(WaypointMapLayer), "tmpWayPointComponents");

    private static readonly System.Reflection.FieldInfo? TexturesByIconField =
        AccessTools.Field(typeof(WaypointMapLayer), "texturesByIcon");

    private static readonly System.Reflection.FieldInfo? ComponentWaypointField =
        AccessTools.Field(typeof(WaypointMapComponent), "waypoint");

    private sealed record ActiveClientWaypoint(Waypoint Waypoint, long CreatedAtMs);

    private readonly ICoreClientAPI _capi;
    private readonly SeraphQoLModSystem _modSystem;
    private readonly Dictionary<string, ActiveClientWaypoint> _activeWaypoints = new(StringComparer.Ordinal);

    public ProjectileTrackerClient(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
    }

    public void HandlePacket(ProjectileWaypointPacket packet)
    {
        if (string.IsNullOrWhiteSpace(packet.WaypointKey)) return;

        WaypointMapLayer? layer = ResolveWaypointLayer();
        if (layer == null) return;

        if (packet.Remove)
        {
            RemoveWaypoint(layer, packet.WaypointKey);
            return;
        }

        if (!_modSystem.ClientConfig.EnableProjectileTracker || !_modSystem.EffectivePolicy.AllowProjectileTracker)
        {
            return;
        }

        EnsureCustomArrowIconRegistered(layer);
        RemoveWaypoint(layer, packet.WaypointKey);

        var waypoint = new Waypoint
        {
            Guid = packet.WaypointKey,
            Position = new Vec3d(packet.X, packet.Y, packet.Z),
            Title = ProjectileWaypointFormatter.FormatTitle(packet.Title),
            Text = SeraphQoLLang.Get("waypoint-projectile-desc"),
            Color = ColorUtil.ColorFromRgba(235, 185, 110, 255),
            Icon = CustomArrowIconCode,
            ShowInWorld = true,
            Temporary = true,
            Pinned = false
        };

        long nowMs = _capi.World?.ElapsedMilliseconds ?? 0;
        _activeWaypoints[packet.WaypointKey] = new ActiveClientWaypoint(waypoint, nowMs);
        AddComponentToLayer(layer, waypoint);
    }

    public void SyncWithLayer(WaypointMapLayer layer)
    {
        if (_activeWaypoints.Count == 0) return;

        EnsureCustomArrowIconRegistered(layer);
        if (WayPointComponentsField?.GetValue(layer) is not System.Collections.IList components) return;

        foreach (ActiveClientWaypoint active in _activeWaypoints.Values)
        {
            bool exists = false;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is WaypointMapComponent comp
                    && ReferenceEquals(ComponentWaypointField?.GetValue(comp), active.Waypoint))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                components.Add(new WaypointMapComponent(-1, active.Waypoint, layer, _capi));
            }
        }
    }

    public void TickPruneExpired()
    {
        if (_activeWaypoints.Count == 0 || _capi.World == null) return;

        int timeoutSec = _modSystem.ClientConfig.ProjectileWaypointTimeoutSeconds;
        if (timeoutSec <= 0) return;

        long nowMs = _capi.World.ElapsedMilliseconds;
        long timeoutMs = timeoutSec * 1000L;

        WaypointMapLayer? layer = null;
        foreach ((string key, ActiveClientWaypoint entry) in _activeWaypoints.ToArray())
        {
            if (nowMs - entry.CreatedAtMs < timeoutMs) continue;
            layer ??= ResolveWaypointLayer();
            if (layer != null)
            {
                RemoveWaypoint(layer, key);
            }
            else
            {
                _activeWaypoints.Remove(key);
            }
        }
    }

    public void ClearAll()
    {
        WaypointMapLayer? layer = ResolveWaypointLayer();
        if (layer != null)
        {
            foreach (string key in _activeWaypoints.Keys.ToArray())
            {
                RemoveWaypoint(layer, key);
            }
        }
        _activeWaypoints.Clear();
    }

    private void EnsureCustomArrowIconRegistered(WaypointMapLayer layer)
    {
        if (!layer.WaypointIcons.ContainsKey(CustomArrowIconCode))
        {
            layer.WaypointIcons[CustomArrowIconCode] = CreateArrowIconTexture;
        }

        if (TexturesByIconField?.GetValue(layer) is Dictionary<string, LoadedTexture> texDict
            && !texDict.ContainsKey(CustomArrowIconCode))
        {
            texDict[CustomArrowIconCode] = CreateArrowIconTexture();
        }
    }

    private LoadedTexture CreateArrowIconTexture()
    {
        var tex = new LoadedTexture(_capi);
        int size = Math.Max(24, (int)GuiElement.scaled(28.0));
        SvgTextureHelper.LoadOrReplace(
            _capi,
            new AssetLocation(SeraphQoLLang.Domain, "textures/icons/waypoint-arrow.svg"),
            size,
            ColorUtil.WhiteArgb,
            ref tex);
        return tex;
    }

    private void AddComponentToLayer(WaypointMapLayer layer, Waypoint waypoint)
    {
        if (WayPointComponentsField?.GetValue(layer) is System.Collections.IList components)
        {
            components.Add(new WaypointMapComponent(-1, waypoint, layer, _capi));
        }
    }

    private WaypointMapLayer? ResolveWaypointLayer()
    {
        WorldMapManager? mapManager = _capi.ModLoader.GetModSystem<WorldMapManager>();
        return mapManager?.MapLayers.OfType<WaypointMapLayer>().FirstOrDefault();
    }

    private void RemoveWaypoint(WaypointMapLayer layer, string key)
    {
        if (!_activeWaypoints.Remove(key, out ActiveClientWaypoint? target)) return;

        RemoveMatchingFromList(WayPointComponentsField?.GetValue(layer) as System.Collections.IList, target.Waypoint, disposeRemoved: true);
        RemoveMatchingFromList(TmpWaypointsField?.GetValue(layer) as System.Collections.IList, target.Waypoint, disposeRemoved: false);
    }

    private static void RemoveMatchingFromList(System.Collections.IList? list, Waypoint target, bool disposeRemoved)
    {
        if (list == null) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] is WaypointMapComponent comp
                && ReferenceEquals(ComponentWaypointField?.GetValue(comp), target))
            {
                if (disposeRemoved)
                {
                    comp.Dispose();
                }
                list.RemoveAt(i);
            }
        }
    }
}
