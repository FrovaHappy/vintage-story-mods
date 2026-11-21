# Task: Expansión configurable de capacidad de mochilas y alforjas en Seraph QoL

- **Estado:** `completed`
- **Tamaño:** Sustancial / Multi-módulo (`mods/seraph-qol`, `tests/SeraphQoL.Tests`)
- **Archivos clave:**
  - `mods/seraph-qol/src/Backpacks/BackpackModels.cs`
  - `mods/seraph-qol/src/Backpacks/BackpackClassifier.cs`
  - `mods/seraph-qol/src/Backpacks/BackpackCapacityCalculator.cs`
  - `mods/seraph-qol/src/Backpacks/BackpackAssetsService.cs`
  - `mods/seraph-qol/src/Config/SeraphQoLServerConfig.cs`
  - `mods/seraph-qol/src/Config/EffectiveServerPolicy.cs`
  - `mods/seraph-qol/src/Network/SeraphQoLPackets.cs`
  - `mods/seraph-qol/src/Patches/SeraphQoLInventoryPatches.cs`
  - `mods/seraph-qol/src/SeraphQoLModSystem.cs`
  - `mods/seraph-qol/assets/seraphqol/config/configlib-patches.json`
  - `mods/seraph-qol/assets/seraphqol/lang/{en,es-es,es-419,ru}.json`
  - `mods/seraph-qol/publisher/description.html`
  - `mods/seraph-qol/publisher/metadata.md`
  - `tests/SeraphQoL.Tests/BackpackCapacityTests.cs`
  - `tests/SeraphQoL.Tests/SeraphQoLConfigTests.cs`

## Objetivo y Alcance
Implementar un módulo servidor-autoritativo (con sincronización en vivo cliente/servidor) dentro de **Seraph QoL** (`seraphqol`) para escalar la capacidad (`quantitySlots`) de mochilas, cestas, carcajs, bolsas especializadas y alforjas de montura tanto vanilla como de mods terceros, inspirado en **Bigger Backpacks** (`1.2.2`) y superando sus limitaciones:
- Detección dinámica sobre `CollectibleObject.Attributes["backpack"]["quantitySlots"]` sin parches JSON frágiles ni dependencias manuales por mod (resuelve el bug de ruta de `saddlebags2` y soporta automáticamente mods como *Butchering*, *Ithania Backpacks*, *Immersive Backpacks*, *Lumber Sling*, *Wildcraft*, *The Remnants*, etc.).
- Presets en ConfigLib (`Vanilla` por defecto opt-in, `Balanced15x`, `BiggerBackpacks2x`, `Explorer3x`, `Custom`) y multiplicadores separados por categoría (`StandardBackpacks`, `SpecializedBags`, `Saddlebags`, `ModdedBackpacks`) + multiplicador global + tope máximo configurable (`MaxBackpackSlotsCap`).
- Al reducir capacidad (p. ej. volver a `Vanilla`), `ExtractOverflowAndSyncSlots` extrae los ítems excedentes (`slot-N` con `N >= targetSlots`), elimina las claves sobrantes para evitar huecos dispersos o duplicación, suelta los ítems al suelo junto al jugador/contenedor y envía un aviso en chat (`warning-backpack-shrink-dropped`) además de la advertencia en ConfigLib.
- Blacklist y overrides exactos/con comodines `*` en JSON (`BackpackBlacklist`, `BackpackOverrides`) reutilizando `CodePatternMatcher`.

## Checklist de Ejecución
- [x] 1. Exploración del mod `BiggerBackpacks_1.2.2`, API real de `CollectibleBehaviorHeldBag`/`InventoryPlayerBackpacks`/`GuiDialogInventory` y puntos de integración en `seraph-qol`
- [x] 2. Pruebas unitarias en `tests/SeraphQoL.Tests` (TDD: RED → GREEN → REFACTOR)
- [x] 3. Implementación de dominio (`Backpacks/`), configuración, red ProtoBuf, servicio de assets, parche anti-pérdida y recarga en vivo
- [x] 4. Integración en ConfigLib (`configlib-patches.json`), localización (`en`, `es-es`, `es-419`, `ru`) y documentación en `publisher/`
- [x] 5. Verificación (`dotnet test VintageStoryMods.sln --no-restore -m:1`, `scripts/package-mods.ps1 -BuildOnly`, revisión Zero Dead Code)

## Notas de Continuidad / Decisiones
- Verificado en `VSEssentials.dll` / `VintagestoryLib.dll`: `CollectibleBehaviorHeldBag.GetQuantitySlots(ItemStack)` lee dinámicamente `bagstack.Collectible.Attributes["backpack"]["quantitySlots"].AsInt(...)` en tiempo de ejecución y `GuiDialogInventory.ComposeSurvivalInvDialog()` ya incluye `scrollbar` para la grilla de mochilas.
- Se excluye por defecto `ItemGlider` / códigos `*glider*` para evitar expandir planeadores salvo que el usuario lo configure explícitamente en `BackpackOverrides`.
- Verificado en `VSEssentials.dll` (`CollectibleBehaviorHeldBag.GetOrCreateSlots`): cuando una mochila ya fue equipada/guardada previamente, su `ItemStack.Attributes["backpack"]["slots"]` ya existe y Vintage Story solo recrea las claves `slot-N` presentes en ese árbol ignorando `quantitySlots`. Se añade un prefix en `GetOrCreateSlots` (`BackpackAssetsService.SyncHeldBagSlotsTree`) para añadir los `slot-N` faltantes (y podar los vacíos sobrantes al reducir) también en mochilas ya crafteadas.
- Por defecto el preset inicial es `Vanilla` (`EnableBackpackExpansion = false`) para mantener compatibilidad con mundos existentes hasta que el jugador/servidor active el escalado en ConfigLib o `.json`.
