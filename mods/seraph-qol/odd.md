# Seraph QoL (`seraphqol`) — Object-Driven Design (ODD)

Este documento define el modelo orientado a objetos (ODD) del mod **Seraph QoL** siguiendo los principios de [`AGENT.md`](../../AGENT.md): **comportamiento junto al estado**, **aislamiento de infraestructura**, **contratos claros** y **cero código muerto**.

---

## 1. Fronteras del Dominio vs. Infraestructura

```text
+-------------------------------------------------------------------+
|  Infraestructura (Vintage Story API / Harmony / Red / SeraphUI)   |
|  - SeraphQoLModSystem, FaunaAssetsService, ServerInventoryExecutor |
|  - Patches Harmony (Inventory, Container, SlotGrid, Suitability)  |
|  - GuiDialogAlloyCalculator (SeraphUI), SvgButton / Toggle        |
+-------------------------------+-----------------------------------+
                                | adapta entradas/salidas
                                v
+-------------------------------------------------------------------+
|  Núcleo de Dominio Puro (100% testeable sin motor del juego)      |
|  - Config: SeraphQoLClientConfig, SeraphQoLServerConfig,          |
|            EffectiveServerPolicy                                  |
|  - Fauna:  FaunaEntityDescriptor, FaunaClassifier,                |
|            FaunaDropClassifier, FaunaBalanceCalculator            |
|  - Inv.:   InventorySortPlanner, QuickStorePlanner,               |
|            ContainerWhitelistPolicy, SlotProtectionPolicy         |
+-------------------------------------------------------------------+
```

---

## 2. Objetos de Dominio por Bounded Context

### 2.1. Configuración y Políticas (`SeraphQoL.Config`)
- **`SeraphQoLClientConfig`**:
  - Estado: `SortOrder` (`Category`, `Name`, `Code`, `StackCount`), `SortHotbarWithBackpack` (`false`), `StackPerishables` (`false`), `SkipFavoritesWhenSorting` (`true`), flags de visibilidad de botones/overlays y `LockedSlots` por clase de inventario (`backpack`, `hotbar`).
  - Comportamiento: `ApplySetting(code, IAttribute)` para recarga en vivo con ConfigLib, `LockSlot` / `UnlockSlot` / `ToggleLockedSlot` / `GetLockedSlots`.
- **`SeraphQoLServerConfig`**:
  - Estado: multiplicadores de vida (`1.5` / `2.0` / `2.5`) y carneado (`1.75` / `1.75` / `1.5` / `1.5`), banderas de domesticados (`ScaleDomesticatedHealth = false`, `ScaleDomesticatedDrops = true`), `IncludeAquaticFauna = false`, `FaunaWhitelist`, `FaunaBlacklist`, reglas de inventario (`EnableInventoryManagement`, `EnableStoreNearby`, `StoreNearbyRadius = 6`, `RequireLandClaimPermission = true`, `AdditionalContainerWhitelist`, `ContainerBlacklist`).
  - Comportamiento: `Normalize()` garantiza cotas válidas (multiplicadores `>= 0.1`, radio `1..16`), `ApplySetting(code, IAttribute)`.
- **`EffectiveServerPolicy`**:
  - Política inmutable autoritativa derivada de `SeraphQoLServerConfig` (ConfigLib / `seraphqol-server.json`) que el servidor aplica y sincroniza al cliente mediante `ServerPolicySyncPacket`.
- **`SeraphQoLLang`**:
  - Proveedor de localización bajo el dominio `seraphqol` con fallback seguro para ejecución en pruebas unitarias sin `Lang` inicializado.

### 2.2. Fauna QoL (`SeraphQoL.Fauna`)
- **`FaunaEntityDescriptor`** *(record inmutable)*:
  - Encapsula `Code`, `ClassName`, `MaxHealth`, `Tags`, `BehaviorCodes`, `DropCodes`, `IsPlayer`, `IsTraderOrNpc`, `IsTemporal`, `IsAquatic`.
- **`FaunaClassifier`**:
  - Invariantes: nunca clasifica jugadores, comerciantes (`EntityTrader`), NPCs ni criaturas temporales (*drifter*, *locust*, *shiver*, *bowtorn*) aunque estén mal etiquetadas; exige presencia de `health` y `harvestable` salvo inclusión explícita en `Whitelist`; respeta `Blacklist` por coincidencia exacta o comodín `*`.
  - Clasifica por categoría (`SmallPrey`, `MediumLargePrey`, `Predator`) usando reglas explícitas de especie (con soporte vanilla y de mods de terceros mediante palabras clave y fallback por `MaxHealth` base).
- **`FaunaDropClassifier`**:
  - Clasifica códigos de drop en `FaunaResourceKind` (`Meat`, `Fat`, `Hide`, `Byproduct`, `None`) para aplicar escalado independiente sin tocar drops especiales/quest que no correspondan a carneado.
- **`FaunaBalanceCalculator`**:
  - `CalculateTargetMaxHealth(baseHealth, category, isDomesticated, policy)`: devuelve `baseHealth` si `isDomesticated && !policy.ScaleDomesticatedHealth`, o aplica el multiplicador de la categoría salvaje.
  - `CalculateScaledDrop(baseAvg, baseVar, kind, isDomesticated, policy)`: aplica el multiplicador del recurso si corresponde (`!isDomesticated || policy.ScaleDomesticatedDrops`), manteniendo `avg >= 0` y `var >= 0`.

### 2.3. Gestión de Inventario y Contenedores (`SeraphQoL.Inventory`)
- **`SortSlotSnapshot`** *(modelo de slot puro)*:
  - Representa un slot con `SlotIndex`, `InventoryClass`, `IsSortable`, `IsLocked`, `IsFavorite`, `ItemCode`, `DisplayName`, `Category`, `StackSize`, `MaxStackSize`, `IsPerishable`, `FreshnessRatio`, `OriginalOrder`.
- **`InventorySortPlanner`**:
  - Invariantes:
    1. Los slots `!IsSortable`, `IsLocked` o (`SkipFavorites && IsFavorite`) **nunca se mueven ni se fusionan**.
    2. La suma total de ítems por `ItemCode` antes y después del plan es estrictamente conservada (cero duplicación o pérdida).
    3. Los ítems perecederos con distinta `FreshnessRatio` solo se fusionan si `StackPerishables == true` (en cuyo caso se calcula la frescura promedio ponderada por `StackSize`).
  - Soporta 4 criterios deterministas (`Category`, `Name`, `Code`, `StackCount`) con desempate estable.
  - Produce `SortPlan` (`IReadOnlyList<SortSlotAssignment>`).
- **`QuickStorePlanner`**:
  - Dado el inventario origen (`backpack` + opcionalmente `hotbar`) y la huella del contenedor destino (códigos ya presentes + slots libres/parciales), calcula las transferencias elegibles excluyendo `IsLocked`, `IsFavorite` (por código de ítem) y slots no transferibles.
- **`ContainerWhitelistPolicy`**:
  - Evalúa códigos de bloque contra la lista blanca base (`game:chest-*`, `game:crate`, `game:labeledchest-*`, `game:stationarybasket-*`, `game:storagevessel-*`, `game:trunk-*`, `bettercrates:*`) + `AdditionalContainerWhitelist` - `ContainerBlacklist`, soportando patrones con `*`.
- **`SlotProtectionPolicy`**:
  - Determina qué tipos de `ItemSlot` son ordenables y qué slots wildcard/especiales (`IsRestrictedWildcardSlot`, `ItemSlotToolBagContent`, `quiversandsheaths`) deben quedar intactos.
- **`HotbarRefillPlanner`**:
  - Planifica la recarga de consumibles/bloques al agotarse un stack en la barra rápida y el reemplazo preventivo de herramientas cuando la durabilidad cae al umbral configurado (`ToolDurabilityThreshold`), respetando `LockedSlots` y `RefillBlacklist`.
- **`EquipLightPlanner`**:
  - Selecciona la fuente de luz más brillante (`LightLevel`) fuera de slots bloqueados y mantiene estado de intercambio reversible (`EquipLightSwapState`).

### 2.4. Menú Radial y Exploración (`SeraphQoL.Interaction`, `SeraphQoL.Exploration`)
- **`ToolModeRadialPlanner`**:
  - Calcula la distribución angular uniforme, paginación (`MaxSectorsPerPage = 10`) y hit-testing con zona muerta central para el menú radial de modos de herramienta (`GuiDialogRadialToolMode`, tecla `F`).
- **`MatchingItemHighlightPolicy`** & **`GroundItemHighlightPlanner`**:
  - Determinan qué slots coinciden con el ítem bajo el cursor y proyectan indicadores/etiquetas con atenuación por distancia para `EntityItem` cercanos.
- **`ProjectileTrackerState`** & **`ProjectileWaypointFormatter`**:
  - Gestionan el ciclo de vida de proyectiles disparados por jugadores (registro, aterrizaje único, tolerancia a descarga de chunk y expiración/limpieza al recoger o cumplirse el tiempo límite).

### 2.5. Herramientas Avanzadas y Renderizado Visual (`SeraphQoL.Alloy`, `SeraphQoL.Visuals`)
- **`AlloyCalculatorLogic`**:
  - Calcula porcentajes normalizados dentro de `[MinRatio, MaxRatio]`, cantidades por ingrediente descontando lo ya presente en el crisol de la fogata, pasos sin desperdicio (`FindNextWasteFreeUnits`) y combustible + precalentamiento requerido.
- **`QuickSwapPlanner`**:
  - Filtra y agrupa herramientas y fuentes de luz únicas de hotbar/mochila para alimentar `GuiDialogRadialQuickSwap`.
- **`SlotIndicatorPlanner`**:
  - Prioriza y evalúa métricas puras de frescura, durabilidad/condición, preparación, volumen líquido, contenido/estado del crisol, raciones de comida y combustible de visión nocturna.
- **`AnimalSexClassifier`** & **`AnimalIndicatorGeometry`**:
  - Clasifica ganado vivo domesticado (`generation >= 1`) en `Male`, `Female` o `PregnantFemale` y calcula el desvanecimiento por distancia.
- **`ResourceBlockClassifier`**:
  - Clasifica bloques clave (resina sin recolectar, Terra Preta, hierro meteórico) por categoría para adjuntarles `AdvancedParticleProperties` nativos.

---

## 3. Contratos de Red (`SeraphQoL.Network`)
Todos los paquetes usan `[ProtoContract]` sobre el canal `"seraphqol"`:
- `ServerPolicySyncPacket`: sincroniza reglas efectivas del servidor al cliente.
- `SortInventoryPacket`: solicita ordenamiento autoritativo de un inventario (`InventoryId`, `SortOrder`, `SortHotbarWithBackpack`, `StackPerishables`, `SkipFavoritesWhenSorting`).
- `QuickStorePacket`: solicita descarga rápida de ítems coincidentes al contenedor abierto (`InventoryId`, `StackPerishables`, `IncludeHotbar`).
- `StoreNearbyPacket`: solicita descarga a contenedores cercanos permitidos (`StackPerishables`, `IncludeHotbar`).
- `UpdateFavoritePacket`: sincroniza toggle de favorito por código de ítem (`ItemCode`, `IsFavorite`).
- `UpdateLockedSlotsPacket`: sincroniza los índices bloqueados del jugador (`InventoryClass`, `SlotIndices`).
- `RemeshContainersPacket`: notifica al cliente posiciones de contenedores modificados que requieren refrescar su mesh visual.
- `HotbarRefillPacket`: solicita al servidor la recarga o reemplazo preventivo de un slot de hotbar desde mochila.
- `SwapSlotsPacket`: solicita intercambio atómico entre dos ranuras del jugador (usado por `Equip Light` y `Quick-Swap`).
- `ProjectileWaypointPacket`: notifica al cliente la creación o eliminación de un waypoint temporal de proyectil.
