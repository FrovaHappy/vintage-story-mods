# Informe Técnico de Referencias para Seraph QoL (`seraphqol`)

Este documento analiza en detalle la arquitectura interna, mecanismos de extensión, parches y características de tres mods de referencia proporcionados para el diseño de **Seraph QoL**:

1. **Vanilla Expanded v1.7.0** (`vanillaexpanded_1.7.0.zip`) — Vintage Story (C# / .NET 10.0)
2. **Storage Tweaks v1.4.2** (`storagetweaks_1.4.2.zip`) — Vintage Story (C# / .NET 8.0)
3. **Inventory Profiles Next v2.3.8** (`InventoryProfilesNext-fabric-26.3-2.3.8.jar`) — Minecraft Fabric 26.3 (Kotlin / JVM)

---

## 1. Resumen Comparativo

| Aspecto | Vanilla Expanded (`1.7.0`) | Storage Tweaks (`1.4.2`) | Inventory Profiles Next (`2.3.8`) |
| :--- | :--- | :--- | :--- |
| **Juego / Plataforma** | Vintage Story `1.22.0` (.NET 10.0) | Vintage Story `*` (.NET 8.0) | Minecraft `26.3` / Fabric (`libipn`, Kotlin 2.3.20) |
| **Autor** | Sisco++ | Trojan96 | Mirinimi |
| **Licencia** | No declarada en `modinfo.json` | No declarada en `modinfo.json` | **AGPL-3** (en `fabric.mod.json`) |
| **Lado de ejecución** | `Universal` (`requiredOnClient: true`, `requiredOnServer: false`) | `Universal` | `client` (100% client-side) |
| **Hook / Patching** | Harmony (`PatchClassProcessor` selectivo + ReversePatch + Transpiler) | Harmony (`PatchAll` + parches dinámicos por reflexión) | Mixins (`mixins.ipnext.json`, 21 mixins + plugin OptiFabric) + `accessWidener` |
| **Red / Sincronización** | Canal `vanillaexpanded` + paquetes nativos (`SendEntityPacketWithOffset`, `SendPacketClient`, `Packet_ToolMode`) | Canal `storagetweaks` con 5 paquetes `ProtoContract` | Sin protocolo propio; simula clicks de ventana (`ContainerClicker` / `PContainerClicker`) |
| **UI / Renderizado** | `GuiDialogBlockEntity`, elementos custom, **OpenGL4 + GLSL Shaders**, barra radial | Botones **SVG + Cairo** inyectados en `GuiDialogInventory` y contenedores | Widgets inyectados en `AbstractContainerScreen`, editor visual (`GUIDEEditorScreen`), hints JSON |
| **Configuración** | `VanillaExpanded.json` + ConfigLib v2 + EventBus live reload (`ILiveConfigurable`) | `storagetweaks.json` + `storagetweaks-server.json` + ConfigLib v0 | Múltiples páginas (`Features`, `ModSettings`, `SortSettings`, `GuiSettings`, `AutoRefillSettings`, `LockedSlotsSettings`, `ScrollSettings`, `EditProfiles`, `Hotkeys`, `Tweaks`) + archivos de reglas/perfiles |

---

## 2. Análisis Técnico: Vanilla Expanded (`v1.7.0`)

### 2.1. Cómo funciona internamente
El mod está estructurado en múltiples `ModSystem` especializados coordinados por `VanillaExpandedModSystem`, con soporte de recarga en vivo (`ILiveConfigurable` + `LiveConfigReload`):

1. **Carga selectiva de Harmony (`VanillaExpandedModSystem.ApplySelectivePatches`)**:
   - En lugar de ejecutar `Harmony.PatchAll()` ciegamente, instancia `PatchClassProcessor` solo para los parches habilitados en `VanillaExpandedConfig`: `FirepitGuiPatch`, `ServerPlayerPatches`, `AutoStashPatch`, `IgnitionSourcesPatch`/`ItemCandlePatch`, `ToolModeDialogPatch`, más sistemas independientes como `BucketSourceProtectionModSystem` (`vanillaexpanded.bucketsourceprotection`) e `ItemSlotIndicatorSystem` (`vanillaexpanded.itemslotindicators`).
2. **Integración avanzada con ConfigLib (`ConfigLibIntegrationModSystem` + `ConfigLibSavedSelectionRestore`)**:
   - Escucha el EventBus de Vintage Story para `configlib:vanillaexpanded:config-saved`, `configlib:vanillaexpanded:setting-changed`, `configlib:vanillaexpanded:setting-loaded` y `configlib:config-reload`, deserializa los atributos `TreeAttribute` cambiados, actualiza `VanillaExpandedConfig` en memoria y notifica a todos los `ModSystem` que implementen `ILiveConfigurable.OnConfigReloaded()`.
   - Restaura selecciones guardadas de ConfigLib tras `LevelFinalize` si se cargaron antes de que el cliente estuviera listo.
3. **Inyección dinámica de comportamientos en `AssetsFinalize`**:
   - **Auto-Stash (`AutoStashPatch.AmendContainerBehaviors`)**: recorre `api.World.Blocks` añadiendo `BlockBehaviorAutoStashable` (y `BehaviorCrateEntityEventBridge` en bloques `Crate`), evitando parches JSON por archivo.
   - **Ignición (`IgnitionToolsModSystem`)**: recorre `api.World.Blocks` y `api.World.Items` en `AssetsFinalize`, añadiendo `BlockBehaviorIgnitionTool` / `CollectibleBehaviorIgnitionTool` a `candle`, `lantern` y `oillamp`.
   - **Auto-Loot (`AutoLootSystem_Client.OnBlockTexturesLoaded`)**: recorre `api.World.EntityTypes` en cliente e inyecta `EntityBehaviorAutoLootable` (`VEAutoLootable`).
   - **Recetas configurables (`UpdateRecipesBasedOnConfig`)**: filtra `api.World.GridRecipes` del dominio `vanillaexpanded` y activa o desactiva cada receta según el toggle correspondiente.
4. **Arquitectura de Auto-Stash (`AutoStashing/*`)**:
   - Separa claramente **Evaluación/Planificación** (`AutoStashAssessor`, `AutoStashPlanner`, `AutoStashPolicy` con políticas dedicadas `MatchingContentsPolicy`, `CratePolicy`, `QuernPolicy`, `BloomeryPolicy`), **Destinos** (`ContainerAutoStashTarget`, `BloomeryAutoStashTarget`, `AttachedBagAutoStashTarget`), **Ejecución** (`AutoStashService`, `InventoryTransferExecutor`, `AutoStashMutationState`) y **Auditoría** (`AutoStashAuditContext` con `ILogger.Audit`).
   - En cliente muestra un progreso radial (`ModSystemRadialProgressBar` + shaders `radial_progress.*`) durante `AutoStashDelay`; al completarse envía `Packet_RequestAutoStash` o `Packet_RequestEntityAutoStash` al servidor, donde `AutoStashSystem_Server` verifica permisos (`CachedAccessPerms.IsInteractingPlayerAllowedTo`) y distancia antes de transferir ítems.
5. **Arquitectura de Auto-Loot (`AutoLoot/*`)**:
   - 100% cliente (`AutoLootClient` + `AutoLootTarget` + `AutoLootTransferCoordinator`): al mantener click derecho sobre un cadáver ya carneado (`EntityBehaviorHarvestable.IsHarvested` y `EntityBehaviorContainer.Inventory` no vacío) dentro del alcance, tras `AutoLootDelay` transfiere cada slot con `IPlayerInventoryManager.TryTransferAway` y envía paquetes nativos `SendEntityPacketWithOffset`, cerrando el diálogo de botín al terminar.
6. **Calculadora de Aleaciones (`AlloyCalculator/*` + `GuiDialogAlloyCalculator`)**:
   - `FirepitGuiPatch` intercepta `GuiDialogBlockEntityFirepit.OnGuiOpened` y `OnGuiClosed` con `[HarmonyPostfix]` para abrir/cerrar `GuiDialogAlloyCalculator` acoplado a la ventana de la fogata.
   - `AlloyCalculatorLogic` calcula proporciones (`NormalizePercentages`), objetivos sin desperdicio (`FindAdjacentWasteFreeTarget`, `TryCalculateWasteFreeNuggets`), composición actual del crisol (`CalculateContentComposition`) y combustible + precalentamiento necesarios (`CalculateFuelRequired`, `CalculatePrewarmingFuelRequired`).
   - `AlloyDepositService`, `AlloyDepositStackPlanner` y `AlloyFuelDepositService` planifican y ejecutan el depósito desde mochila/hotbar y contenedores abiertos (`AlloyTransferInventoryPolicy`, `ClientInventorySlotReconciler`).
7. **Indicadores en Slots con Transpiler y Simulación GPU (`ItemSlotIndicators/*`)**:
   - `ItemSlotIndicatorPatch` aplica un `[HarmonyTranspiler]` sobre `GuiElementItemSlotGridBase.RenderInteractiveElements`, interceptando `IRenderAPI.RenderItemstackToGui` (`RenderItemstackWithIndicator`) y `IRenderAPI.PopScissor` (`PopScissorWithOutline`).
   - Los proveedores (`IItemSlotIndicatorProvider`) tienen prioridad y muestreo adaptativo (`AdaptiveSamplingOptions`): `FreshnessIndicatorProvider`, `FoodContainerIndicatorProvider`, `PreparationIndicatorProvider`, `ClothingIndicatorProvider`, `LiquidContainerIndicatorProvider`, `WateringCanIndicatorProvider`, `CrucibleIndicatorProvider` y `NightVisionFuelIndicatorProvider`.
   - Utiliza simulaciones y shaders GLSL propios (`vanillaexpanded_itemslot_liquid_simulation/slosh`, `vanillaexpanded_itemslot_food_grains/simulation`, `vanillaexpanded_itemslot_metal_simulation/chunks`, `vanillaexpanded_itemslot_crucible_solid/molten`) impulsados por movimiento de cámara (`ItemSlotIndicatorFrameUpdater`).
8. **Renderizado 3D en Mundo y Menús Radiales**:
   - **Indicadores de sexo animal (`AnimalSexIndicators/*`)**: `IRenderer` en cliente con shader `animal_sex_indicator.*` que lee `WatchedAttributes` (`generation`, `gender`, `multiply.isPregnant`) y dibuja texturas `male.png`, `female.png`, `female-pregnant.png` con tintes y desvanecimiento por distancia.
   - **Decal de respawn (`SpawnDecal/*`)**: `ServerPlayerPatches` intercepta `SetSpawnPosition`, `ClearSpawnPosition` y `GetSpawnPosition` para enviar `Packet_TemporalSpawn`; `SpawnDecalRenderer` dibuja un quad pulsante (`textures/respawnpoint.png`) sobre el bloque de respawn.
   - **Menú radial (`RadialMenu/*` + `ToolModeRadialMenu/*` + `QuickTools/*`)**: `ToolModeDialogPatch` intercepta `GuiDialogToolMode.OnKeyCombinationToggle` (`[HarmonyPrefix]`) para mostrar un menú radial configurable con estrategias específicas (`ChiselToolModeMenuLayoutStrategy`, `SmithingHammerToolModeMenuLayoutStrategy`, `BoatRollerToolModeMenuLayoutStrategy`, `GenericToolModeMenuLayoutStrategy`) y selector paginado de materiales de cincel (`ChiselMaterialMenu`). `QuickToolSystem` ofrece un menú radial al mantener una tecla para intercambiar herramientas o fuentes de luz en mano activa/secundaria con restauración de sesión.

### 2.2. Lista completa de características de Vanilla Expanded
- **Auto-Stash en contenedores y entidades**: Contenedores estándar, *crates*, *quern*, *bloomery* y bolsas acopladas a entidades/monturas, con barra radial configurable y ayuda contextual.
- **Auto-Loot de cadáveres**: Recogida automática al mantener click derecho tras carnear.
- **Calculadora de aleaciones y combustible**: Selección de aleación o metal puro, sliders de porcentaje, botones +/- de objetivo sin desperdicio, detección automática del contenido actual, cálculo de combustible/precalentamiento y depósito con un clic.
- **Menú radial de modos de herramienta**: Reemplazo del selector vanilla con estrategias para cincel (incluyendo cambio de material), martillo de forja y rodillo de bote; tamaño, grosor de anillo, centro, iconos y opacidad configurables.
- **Menú radial Quick-Swap (`ve.quickTool`)**: Cambio rápido de herramientas por categoría y fuentes de luz al mantener pulsado el atajo (con opción de seleccionar al soltar).
- **Atajos de fuente de luz**: `ve.equipLightSourceToOffhand` y `ve.equipLightSourceToHotbar` que buscan y equipan la fuente de luz más brillante (con toggle reversible).
- **Indicadores en slots de inventario**:
  - Frescura de ítems perecederos (fondo, borde o barra) y cantidad en contenedores de comida con simulación de partículas.
  - Volumen líquido con efecto *slosh* animado en recipientes y regaderas.
  - Progreso de preparación y condición de ropa/armadura.
  - Cantidad y estado (sólido vs. fundido brillante) en crisoles.
  - Combustible de visión nocturna.
- **Indicadores de sexo y preñez en animales**: Símbolos 3D configurables (tamaño, rango hasta 8 bloques, opacidad, colores RGB) sobre animales vivos de generación 1+.
- **Presentación mejorada de cabezas de herramienta (`ToolHeadPresentationSystem`)**: Parches JSON en 23 categorías de herramientas para centrar/rotar cabezas en iconos GUI.
- **Decal brillante en punto de respawn**: Marcador visual sincronizado con el spawn del servidor.
- **Herramientas de ignición**: Encender fuegos con velas, linternas y lámparas de aceite (`ItemCandlePatch` + `IgnitionSourcesPatch` + behaviors).
- **Protección de fuentes de agua (`BucketSpillPatch`)**: Evita destruir bloques fuente de agua al derramar un balde cuando `noLiquidSourceTransport` está activo.
- **Fix de sonido de pesca (`FishingBiteSoundPatch`)**: Transpiler en `EntityBobber.playCatchEffects` para corregir el sonido/volumen al picar un pez.
- **Recetas QoL activables por config**: Descrafteo de mochila y saco de lino, reciclaje de cabezas metálicas a pepitas/bits con cincel, fabricación de palos desde tablones/leña y descrafteo de *wattle*.
- **Parche de tags**: `is-fuel-source`, `tool-healingitem`, `tool-shield`.

---

## 3. Análisis Técnico: Storage Tweaks (`v1.4.2`)

### 3.1. Cómo funciona internamente
**Storage Tweaks** apuesta por una arquitectura **ligera y estrictamente autoritativa en servidor** para evitar desincronizaciones y duplicaciones de ítems:

1. **Red `ProtoBuf` dedicada (`StorageTweaksModSystem`)**:
   - Registra el canal `"storagetweaks"` con 5 mensajes `ProtoContract`:
     - `SortInventoryPacket` (`InventoryId`, `SkipFavoritesWhenSorting`, `SortHotbarWithBackpack`, `StackPerishables`)
     - `UnloadInventoryPacket` (`InventoryId`, `StackPerishables`)
     - `QuickStoreNearbyContainersPacket` (`StackPerishables`)
     - `UpdateFavoritesPacket` (`Code`, `IsFavorite`)
     - `RemeshContainersPacket` (`Positions` para forzar `InitMesh` en cliente tras descargas remotas en contenedores visuales como estanterías/crates).
2. **Motor de Ordenamiento con Rollback (`SortSystem`)**:
   - Se ejecuta en el servidor (`HandleSortInventory`): toma una **instantánea (*snapshot*)** de los `ItemStack` de todos los slots antes de ordenar; si `SortInventoryInternal` falla o lanza excepción, restaura el inventario completo, marca los slots *dirty* y envía un error al jugador (`storagetweaks:rollback`).
   - Primero compacta stacks (`CompactStacks`) combinando ítems compatibles (y opcionalmente perecederos con distinta frescura si `StackPerishables == true`), luego ordena la lista y la reubica respetando `GetBestSuitedSlot` y `ItemSlotExt.CanSortInto`.
3. **Protección de Slots de Mods de Terceros (`ItemSlotExt`, `InventoryExt`, `FoodShelvesSegmentGuard`, `InventoryBasePatch`)**:
   - Lista blanca explícita de tipos de slot ordenables (`ItemSlotSurvival`, `ItemSlotBagContent`, `ItemSlotBagContentWithWildcardMatch`, `BackpackSlot`, `VanillaBagContentSlot`, `ItemSlotDynamic`, `StandardSlot`, `ItemSlotToolBagContent`).
   - Detecta slots wildcard restringidos (`IsRestrictedWildcardSlot`) y slots legacy de `Overhaullib` / `quiversandsheaths` para no mover ítems a slots incompatibles (`InventoryBasePatch` en `InventoryBase.GetSuitability`).
   - `FoodShelvesSegmentGuard` inspecciona por reflexión `FoodShelves.IFoodShelvesContainer` (`ItemsPerSegment`, `GetSegmentLimit`, `CountItemsInSegment`, `ItemSlotFSUniversal`) para respetar límites por segmento sin acoplamiento en compilación.
4. **Favoritos sincronizados en entidad del jugador (`FavoritesManager`)**:
   - Almacena un `TreeAttribute` bajo `"storageTweaksFavorites"` dentro de `player.Entity.WatchedAttributes`.
   - En `OnPlayerJoin` se asegura su inicialización y marcado *dirty*. El cliente consulta `IsFavorite` localmente para dibujar el icono en la esquina del slot (`GuiElementItemSlotGridPatch` + `FavoritedSlot`) y envía `UpdateFavoritesPacket` al hacer clic en modo favorito (`PrefixSlotClick` intercepta `SlotClick`).
   - `UnloadInventory` excluye automáticamente los ítems marcados como favoritos.
5. **Descarga rápida y Store Nearby (`UnloadInventory` + `QuickStoreNearbyContainerSystem` + `ContainerWhitelist`)**:
   - `ContainerWhitelist` mantiene una lista blanca compilada de contenedores permitidos (`chest-*`, `crate`, `labeledchest-*`, `stationarybasket-*`, `storagevessel-*`, `trunk-*`, `bettercrates:*`, etc.) filtrada en servidor por `ContainerBlacklist` y `AdditionalContainerWhitelist`.
   - `QuickStoreNearbyContainerSystem` usa `BlockAccessor.WalkBlocks` alrededor del jugador dentro de `QuickStoreNearbySearchRadius`, descarga los ítems coincidentes de mochila/hotbar (`UnloadInventory`) en cada contenedor permitido y envía `RemeshContainersPacket` si alguno requiere `InitMesh`.
   - `ProcessInventorySlots` protege además herramientas y comida (`ToolAndFoodCodes` construidos en `PopulateToolAndFoodCodes`) cuando corresponde y respeta `CanHold` / `CanSortInto`.
6. **Inyección de UI SVG + Compatibilidad con *More Inventorys***:
   - `GuiDialogInventoryPatch` y `GuiDialogBlockEntityInventoryPatch` hacen postfix sobre `AddDialogTitleBar`, `ComposeSurvivalInvDialog` y `OnGuiOpened` (`GuiDialog`, `GuiDialogBlockEntityInventory`, `GuiDialogBlockEntity`) para insertar `InventoryActionButtons` y `ContainerActionButtons`.
   - Los botones usan `SvgButton` y `SvgToggleButton` renderizando los `.svg` del mod mediante **Cairo** (`IGuiAPI.DrawSvg` -> `LoadedTexture`).
   - `MoreInventorysPatch` parchea dinámicamente por reflexión `GuiDialogCrateClosed` y `GuiDialogDynamic` (`SetupDialog` y `OnInventorySlotModified`) con *debouncing* de reconstrucción GUI durante actualizaciones masivas.
   - `CreativeInventoryContainerTabPatch` añade la pestaña `"storage-containers"` al inventario creativo agrupando todos los `BlockEntityContainer`.

### 3.2. Lista completa de características de Storage Tweaks
- **Compactar y ordenar (`Compact & sort`)**: En inventario del jugador (mochila + opcionalmente hotbar) y en contenedores abiertos, mediante botón SVG o atajos (`storagetweaks.sort`, `storagetweaks.sortcontainer`).
- **Descarga rápida al contenedor abierto (`Quick store`)**: Transfiere desde inventario/hotbar al contenedor abierto todos los ítems cuyos códigos ya existan en el contenedor.
- **Descarga a contenedores cercanos (`Store nearby`)**: Botón y atajo (`storagetweaks.storenearby`) que busca contenedores permitidos alrededor del jugador y descarga ítems coincidentes, actualizando el mesh en clientes si hace falta.
- **Modo Favoritos y bloqueo ante descarga/ordenamiento**: Botón toggle en inventario para marcar/desmarcar ítems favoritos con clic, icono SVG en esquina del slot (con opción `HideFavorites`), exclusión de descarga rápida y opción `SkipFavoritesWhenSorting`.
- **Force-stack de perecederos (`StackPerishables`)**: Botón toggle que permite fusionar comida con distinto deterioro promediando su progreso de caducidad.
- **Configuración cliente y servidor + ConfigLib**: Ocultar botones individualmente, incluir hotbar al ordenar mochila, radio de búsqueda y whitelist/blacklist de contenedores en `storagetweaks-server.json`.
- **Pestaña "Containers" en creativo**: Agrupación automática de bloques contenedor en el modo creativo.
- **Localización completa**: `en`, `es-419`, `es-es`, `ru`.

---

## 4. Análisis Técnico: Inventory Profiles Next (`v2.3.8`)

### 4.1. Cómo funciona internamente
**Inventory Profiles Next (IPN)** es un mod **100% client-side** para Minecraft Fabric (escrito en Kotlin sobre `libipn` y bajo licencia **AGPL-3**). Como en Minecraft un mod cliente no puede modificar el inventario en el servidor directamente, toda su arquitectura está diseñada para **planificar en un sandbox local y traducir el plan a una secuencia mínima de clics virtuales**:

1. **Abstracción de Contenedor y Planificación en Sandbox (`AdvancedContainer`, `ItemTracker`, `ContainerSandbox`, `ItemPlanner`)**:
   - Convierte los slots de `AbstractContainerMenu` en una representación en memoria (`MutableItemTracker` con `cursor`, `slots` y `thrownItems`).
   - Separa áreas lógicas (`AreaTypes`: `playerStorage`, `playerHotbar`, `playerMainhand`, `playerOffhand`, `itemStorage`, `sortableItemStorage`, `lockedSlots`, `craftingIngredient`).
   - Las acciones (`GeneralInventoryActions`, `SubTrackerActionsKt`, `PostActions`) calculan el estado objetivo (*goal tracker*) primero en memoria: agrupan, ordenan, aplican post-acciones (`GROUP_IN_ROWS`, `GROUP_IN_COLUMNS`, `DISTRIBUTE_EVENLY`, `SHUFFLE`, `FILL_ONE`, `REVERSE`) y generan el objetivo final.
2. **Motor de Diferencias (`DiffCalculator` + `DiffCalculatorInstance`)**:
   - Toma el estado actual y el estado objetivo (`goalTracker`) y calcula en `ContainerSandbox` la secuencia exacta de clics (`SandboxClick`: left/right/outside) necesaria para transformar el inventario real en el estado objetivo.
   - Dispone de 3 algoritmos configurables (`DiffCalculatorType`): `SIMPLE`, `SCORE_BASED_SINGLE` y `SCORE_BASED_DUAL`, con detector de bucles infinitos (`> 100000` iteraciones) y verificación de espacio libre (`NoRoomException`).
   - Finalmente `ContainerClicker` / `PContainerClicker` ejecuta los clics contra `MinecraftClient` respetando `ADD_INTERVAL_BETWEEN_CLICKS` (`INTERVAL_BETWEEN_CLICKS_MS`, útil para anti-cheat / ping en servidores) y opcionalmente resaltando el slot que está siendo clicado.
3. **Motor de Reglas Declarativas con ANTLR4 (`RulesLexer`/`RulesParser` + `item/rule/*`)**:
   - Incluye un DSL completo compilado con **ANTLR4** (`rules.txt` y archivos `rules-v2.*.txt`) para definir órdenes jerárquicos (`@default`, `@creative_menu_order`, `@item_name`, `@item_id`, `@raw_id`, `@accumulated_count_descending/ascending`) y sub-reglas componibles (`::custom_name`, `::search_tab_index`, `::enchantments_tooltip_order`, `::damage`, `::potion_effect`, `::by_nbt`, `::match_nbt`, grupos de ítems e IDs/tags).
4. **Perfiles de Equipamiento con ANTLR4 (`ProfilesLexer`/`ProfilesParser` + `profiles/config/*` + `ProfileSwitchHandler`)**:
   - Otro DSL propio (`profiles.txt` / `profiles.default.txt`) para definir conjuntos de armadura, offhand, hotbar y mano activa con listas de prioridad y componentes/encantamientos (ej. `diamond_sword iron_sword`), rotación (`Next`/`Prev`), 10 *Quick Profiles* y guardado rápido (`save_as_profile`) como perfil `"Saved"`.
5. **Sistema de Slots Bloqueados (`LockSlotsHandler` + `LockedSlotKeeper` + Mixins)**:
   - Protege slots elegidos por el jugador (por perfil/servidor si `ENABLE_LOCK_SLOTS_PER_SERVER` está activo) contra ordenamiento, `Move All`, limpieza de cursor, recarga y crafteo continuo.
   - `LockedSlotKeeper` monitorea los slots vacíos bloqueados en cada tick para evitar que ítems recogidos del suelo entren en slots bloqueados vacíos (o los reubica automáticamente mediante `moveItem` usando una lista blanca de hotbar `LOCKED_SLOTS_EMPTY_HOTBAR_BLACKLIST`).
   - Reforzado con Mixins (`MixinPlayerInteractionManagerForLockedSlotsMovePrevention`, `MixinPlayerInventory`, `MixinClientPlayerInteractionManager`) para bloquear `Shift+Click`, `Q`/`Ctrl+Q`, swipe o toda interacción manual si se configura.
6. **Auto-Refill Avanzado (`AutoRefillHandler` + `SpecificItemSlotMonitor`)**:
   - Monitorea la hotbar y armadura (`REFILL_ARMOR`) por tick, detectando agotamiento de stacks (`STACKABLE_THRESHOLD`) o desgaste de herramientas antes de romperse (`REFILL_BEFORE_TOOL_BREAK`, `TOOL_DAMAGE_THRESHOLD` en valor absoluto o `%`, excluyendo encantamientos *Loyalty* o herramientas de baja durabilidad máxima si se permite romperlas).
   - Soporta coincidencia por nombre personalizado, NBT (`EXACT` vs `CAN_HAVE_EXTRA`), cualquier comida ordenada por saturación, preferencia por stacks pequeños, retardo en ticks (`AUTO_REFILL_WAIT_TICK`), toggle por slot, y alertas visuales/sonoras previas a la rotura (`tool_refill_step_ping.ogg`).
7. **Inyección de UI y Sistema de Hints (`gui/inject/*`, `integration/HintsManagerNG`, `GUIDEEditorScreen`)**:
   - Usa Mixins (`MixinContainerScreen`, `MixinScreen`, `MixinMerchantScreen`, etc.) y `InsertWidgetHandler` para inyectar botones (`SortButtonWidget`, `CheckBoxWidget`, `ProfileButtonWidget`, `SettingsWidget`, `EditorWidget`, `VillagerOverlayWidget`).
   - Resuelve compatibilidad con cientos de pantallas de mods mediante anotaciones Java API (`@IPNGuiHint`, `@IPNIgnore`, `@IPNPlayerSideOnly`, `@IPNStorageDimensionsHint`) + archivos JSON integrados (`ModIntegrationHintsNG.json`, `SlotIntegrationHints.json`) y un editor visual en tiempo real (`GUIDEEditorScreen`) para reposicionar botones o marcar pantallas ignoradas.
8. **Automatizaciones Contextuales (Crafting, Cortapiedras, Yunque, Aldeanos, Scroll)**:
   - **Continuous / Auto Crafting (`ContinuousCraftingHandler` + `MixinCraftingResultSlot`)**: rellena automáticamente la grilla de crafteo tras fabricar; manteniendo `Shift` entra en modo *Auto Crafting*.
   - **Cutter Crafting (`StoneCutterCraftingHandler`)**: repite automáticamente recetas en el cortapiedras.
   - **Bulk Rename en Yunque (`AnvilHandler` + `MixinAnvilScreenHandler`)**: preserva el texto de renombrado tras retirar el resultado para renombrar múltiples stacks en serie.
   - **Villager Trading Bookmarks (`VillagerTradeManager` + `VillagerDataManager` + `MixinMerchantScreen`)**: 3 grupos de marcadores locales (por aldeano) y globales (por profesión / *Wandering Trader*) con ejecución en lote de todos los intercambios posibles.
   - **Item Scrolling (`ScrollingUtils` + `MouseTracer`)**: mover de 1 en 1 o stacks completos con la rueda del ratón entre inventarios, con modificadores para dejar 1 (*leave one*), repartir (*spread*), soltar (*throw*) o auto-recargar cursor.
   - **Slot Highlight (`SlotHighlightHandler`)**: resalta parpadeando o estáticamente todos los slots con el mismo ítem que el ítem bajo el cursor.

### 4.2. Lista completa de características de Inventory Profiles Next
- **Ordenamiento multi-estrategia**: Por menú creativo (búsqueda o prioridad de categorías), nombre, item ID, ID numérico, cantidad acumulada ascendente/descendente o reglas personalizadas DSL (`rules-v2`).
- **Modos de disposición (Post-Actions)**: Regular, columnas, filas, distribuir equitativamente, mezclar, rellenar uno, invertir.
- **Movimiento y descarte rápido**: `Move All` / `Move Matching` / `Only focused type` / `Just refill stacks` (con modificador de hotbar), `Throw All` / `Throw Matching`, y limpieza automática del cursor al cerrar (`PREVENT_CLOSE_GUI_DROP_ITEM`).
- **Swipe Moving**: Arrastrar con `Shift + Click` sobre múltiples slots (incluyendo resultado de crafteo).
- **Item Scrolling con rueda del ratón**: Transferencia unidad a unidad o por stack, con modificadores *Leave One*, *Spread*, *Throw* y auto-recarga de cursor.
- **Locked Slots (Slots Bloqueados)**: Protección contra sort/move/throw/pickup, redirección automática de ítems recogidos, configuración por servidor/mundo, bloqueo de interacción manual y personalización visual.
- **Auto-Refill completo**: Reposición de bloques, comida y herramientas/armadura antes de romperse; comparación NBT/nombre, blacklist, configuración por slot y alertas escalonadas de durabilidad.
- **Equipment Profiles**: Hasta 10 accesos rápidos + rotación siguiente/anterior, guardado instantáneo (`Saved`) y reglas de respaldo por prioridad de equipamiento.
- **Resaltado de ítems coincidentes**: Iluminación de slots iguales al ítem bajo el cursor.
- **Crafteo continuo / automático y Cortapiedras automático**: Checkbox integrado en mesa de crafteo/inventario.
- **Renombrado masivo en Yunque**: Checkbox de *Bulk Rename*.
- **Marcadores y comercio automático con Aldeanos**: 3 grupos de marcadores locales y globales.
- **Editor visual de overlay (`GUIDEEditorScreen`) y API pública (`org.anti_ad.mc.ipn.api`)**: Reposicionamiento de botones y soporte de integración para modpacks.

---

## 5. Conclusiones y Recomendaciones de Arquitectura para Seraph QoL

1. **Licencias y Clean-Room**:
   - `Inventory Profiles Next` está bajo **AGPL-3**; por tanto, **Seraph QoL** no debe copiar ni portar código fuente de IPN. Su análisis sirve únicamente como referencia conceptual de UX y de diseño de planificadores.
2. **Modelo de Red (Vintage Story vs. Minecraft)**:
   - A diferencia de IPN (limitado a simular clics en el cliente), Vintage Story permite mods `Universal` con canales `INetworkChannel`. Para acciones de inventario (ordenar, descargar, *store-nearby*, *auto-stash*), el patrón **Storage Tweaks / Vanilla Expanded** (planificación + ejecución autoritativa en servidor con validación de permisos, rollback por snapshot y protección de slots de mods terceros) es muy superior en fiabilidad y seguridad multijugador.
3. **Adopción de lo mejor de cada referencia en `SeraphQoL`**:
   - **De Storage Tweaks**: Rollback por snapshot en ordenamientos, whitelist/blacklist de contenedores, detección de slots especiales (`ItemSlotExt`, `FoodShelvesSegmentGuard`), favoritos persistidos en `WatchedAttributes` del jugador y botones SVG limpios.
   - **De Vanilla Expanded**: Inyección dinámica de `BlockBehavior` / `EntityBehavior` en `AssetsFinalize` (evitando parches JSON frágiles), activación selectiva de parches Harmony por módulo, recarga en vivo vía ConfigLib (`ILiveConfigurable`), barra radial / menús radiales, indicadores de slots con muestreo adaptativo, indicadores de sexo/preñez animal y calculadora de aleaciones.
   - **De Inventory Profiles Next (conceptual)**: Separación estricta entre *planificador puro* (`SortPlanner`) y *ejecutor*, reglas de ordenamiento componibles (nombre, código, cantidad, frescura/durabilidad), bloqueo de slots por índice además de por favorito, resaltado de ítems coincidentes bajo el cursor, y umbrales/alertas de durabilidad en *Auto-Refill*.
4. **Sinergia con Fauna QoL dentro de Seraph QoL**:
   - Unificar la inspección de `EntityTypes` en `AssetsFinalize` permite que **SeraphQoL** aplique en un mismo ciclo limpio el rebalanceo de vida/carneado salvaje (`Fauna QoL`), la lógica salvaje vs. domesticada (`generation > 0`), el `Auto-Loot` de cadáveres y los indicadores visuales de sexo/preñez sobre animales domésticos.
