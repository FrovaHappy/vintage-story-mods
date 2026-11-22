# Task: Resaltado rojo de ingredientes faltantes y búsqueda en cofres cercanos (Handbook Recipes)

- **Estado:** `completed`
- **Tamaño:** Sustancial / Multi-módulo (`mods/seraph-qol`, `tests/SeraphQoL.Tests`)
- **Archivos clave:**
  - `mods/seraph-qol/src/Handbook/CraftingFillPlanner.cs`
  - `mods/seraph-qol/src/Handbook/HandbookRecipeService.cs`
  - `mods/seraph-qol/src/Handbook/HandbookFillButtonComponent.cs`
  - `mods/seraph-qol/src/Patches/SeraphQoLHandbookPatches.cs`
  - `mods/seraph-qol/src/Inventory/ServerInventoryExecutor.cs`
  - `mods/seraph-qol/src/Network/SeraphQoLPackets.cs`
  - `mods/seraph-qol/src/Config/SeraphQoLClientConfig.cs`
  - `mods/seraph-qol/src/SeraphQoLModSystem.cs`
  - `mods/seraph-qol/assets/seraphqol/config/configlib-patches.json`
  - `mods/seraph-qol/assets/seraphqol/lang/{en,es-es,es-419,ru}.json`
  - `mods/seraph-qol/publisher/description.html`
  - `mods/seraph-qol/changelogs/unreleased.md`
  - `tests/SeraphQoL.Tests/HandbookRecipeTests.cs`
  - `tests/SeraphQoL.Tests/SeraphQoLConfigTests.cs`

## Objetivo y Alcance
Extender el módulo **Improved Handbook Recipes** de `seraphqol` con:
1. **Resaltado rojo en tiempo real de ingredientes faltantes**:
   - Evaluar los ingredientes de la receta actualmente visible en `SlideshowGridRecipeTextComponent` contra la disponibilidad combinada (`craftinggrid` + `backpack` desbloqueado + `hotbar` desbloqueado + cofres abiertos + snapshot de cofres cercanos).
   - Pintar un overlay rojo semitransparente (textura Cairo 1x1 generada proceduralmente) sobre los ingredientes faltantes o con durabilidad insuficiente en `OnHandbookRecipeRender`, aislado por `GridRecipe` para no contaminar otras recetas de la misma página.
   - Teñir en rojo el texto/aviso de los botones `+` / `*` y añadir indicador en tooltip cuando faltan materiales para craftear.
2. **Búsqueda y extracción desde cofres cercanos cerrados**:
   - Escaneo en servidor (`RequestHandbookScanPacket` / `HandbookScanResultPacket`) reutilizando `FindNearbyAllowedContainers`, `ContainerWhitelistPolicy` y `RequireLandClaimPermission`.
   - Caché en cliente con invalidación por cambio de posición de bloque, tiempo o crafteo.
   - Ejecución en servidor (`HandbookFillGridPacket` / `HandbookFillResultPacket`) cuando se necesiten materiales de cofres cercanos (si no se mantiene `Shift`), reutilizando `CraftingFillPlanner`, respetando `LockedSlots`, devolviendo/soltando slots incompatibles si se limpian, marcando contenedores sucios y enviando `RemeshContainersPacket`.

## Checklist de Ejecución
- [x] 1. Exploración de `ImprovedHandbookRecipesContinued_1.3.0.zip` e infraestructura de cofres cercanos en `seraph-qol`
- [x] 2. Pruebas unitarias en `tests/SeraphQoL.Tests` (TDD: `FindMissingIngredients`, durabilidad/cantidad parcial, serialización ProtoBuf, config)
- [x] 3. Implementación de dominio (`CraftingFillPlanner.FindMissingIngredients`), paquetes ProtoBuf, ejecutor servidor y servicio/UI cliente
- [x] 4. Configuración (`SeraphQoLClientConfig`, `configlib-patches.json`), localización (`en`, `es-es`, `es-419`, `ru`), `publisher/description.html` y `changelogs/unreleased.md`
- [x] 5. Verificación (`dotnet test VintageStoryMods.sln --no-restore -m:1`, `scripts/package-mods.ps1 -BuildOnly`, revisión Zero Dead Code)

## Notas de Continuidad / Decisiones
- Se evalúa la receta visible en el slideshow en tiempo real (cacheado por tick/inventario) además de al hacer clic, evitando el bug de IHRC 1.3.0 donde el estado rojo era global y contaminaba otras recetas.
- Los cofres cercanos cerrados se leen/consumen autoritativamente en el servidor con el mismo patrón de `AlloyDepositService` y `ServerInventoryExecutor`.
- `ShowHandbookMissingHighlight` (`HighlightMissingHandbookIngredients = true`), `HandbookSearchNearbyContainers = true` y `HandbookSearchRadius = 6` (`4..10`).
