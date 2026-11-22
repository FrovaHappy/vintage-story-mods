# Task: Improved Handbook Recipes en Seraph QoL

- **Estado:** `completed`
- **Tamaño:** Sustancial / Multi-módulo (`mods/seraph-qol`, `tests/SeraphQoL.Tests`)
- **Archivos clave:**
  - `mods/seraph-qol/src/Handbook/HandbookVariantIndexCalculator.cs`
  - `mods/seraph-qol/src/Handbook/CraftingFillPlanner.cs`
  - `mods/seraph-qol/src/Handbook/HandbookFillButtonComponent.cs`
  - `mods/seraph-qol/src/Handbook/HandbookRecipeService.cs`
  - `mods/seraph-qol/src/Patches/SeraphQoLHandbookPatches.cs`
  - `mods/seraph-qol/src/Config/SeraphQoLClientConfig.cs`
  - `mods/seraph-qol/src/SeraphQoLModSystem.cs`
  - `mods/seraph-qol/assets/seraphqol/textures/icons/wrench.svg`
  - `mods/seraph-qol/assets/seraphqol/config/configlib-patches.json`
  - `mods/seraph-qol/assets/seraphqol/lang/{en,es-es,es-419,ru}.json`
  - `mods/seraph-qol/modinfo.json`
  - `mods/seraph-qol/publisher/description.html`
  - `mods/seraph-qol/publisher/metadata.md`
  - `mods/seraph-qol/changelogs/unreleased.md`
  - `tests/SeraphQoL.Tests/HandbookRecipeTests.cs`
  - `tests/SeraphQoL.Tests/SeraphQoLConfigTests.cs`

## Objetivo y Alcance
Integrar las funcionalidades de **Improved Handbook Recipes** (referencia de Kathanon, v1.1.3) de forma limpia, modular y compatible con **Vintage Story 1.22.0** dentro de **Seraph QoL** (`seraphqol`):
1. **Scroll libre de variantes en el Handbook**: rueda del mouse sobre `SlideshowGridRecipeTextComponent` y `SlideshowItemstackTextComponent` (adaptado a los campos de VS 1.22: `currentItemIndex` / `curItemIndex`, `GridRecipesAndUnnamedIngredients`, `UnnamedIngredients`), con cálculo de MCM para retroceder limpiamente sobre ingredientes alternativos.
2. **Botones `+` y `*` de auto-relleno en grilla 3x3**: inyectados junto a recetas de grilla en el Handbook cuando el inventario de supervivencia está abierto; soportan shaped (alineando `< 3x3` mediante `RecipeGridBounds`), `3x3` y shapeless; toman ingredientes de `backpack` (excluyendo `ItemSlotBackpack`), `hotbar` y cofres abiertos (`OpenedInventories` de tipo `InventoryGeneric` salvo si se mantiene `Shift`); respetan `LockedSlots` (configurable) y sueltan al suelo (`DropItem`) ítems incompatibles de la grilla si el inventario está lleno.
3. **Indicador de herramienta no consumible y costo de durabilidad**: renderizado de ícono `wrench.svg` sobre ingredientes `IsTool` y texto de costo de durabilidad en el tooltip.
4. **Max stack size en tooltips**: añadir `\nMax stack size: {0}\n` en `CollectibleObject.GetHeldItemInfo` para ítems no líquidos (`ShowItemMaxStackTooltip = true` por defecto).

## Checklist de Ejecución
- [x] 1. Exploración de `ImprovedHandbookRecipes_1.1.3.zip`, APIs de VS 1.22 y puntos de integración en `seraph-qol`
- [x] 2. Pruebas unitarias en `tests/SeraphQoL.Tests` (TDD: RED → GREEN → REFACTOR)
- [x] 3. Implementación de dominio puro (`HandbookVariantIndexCalculator`, `CraftingFillPlanner`), UI (`HandbookFillButtonComponent`), servicio y parches Harmony para VS 1.22
- [x] 4. Configuración (`SeraphQoLClientConfig`, `configlib-patches.json`), localización (`en`, `es-es`, `es-419`, `ru`), `wrench.svg`, créditos y documentación (`publisher/`, `changelogs/unreleased.md`)
- [x] 5. Verificación (`dotnet test VintageStoryMods.sln --no-restore -m:1`, `scripts/package-mods.ps1 -BuildOnly`, revisión Zero Dead Code)

## Notas de Continuidad / Decisiones
- VS 1.22 renombró en `SlideshowGridRecipeTextComponent`: `curItemIndex` -> `currentItemIndex`, `GridRecipesAndUnIn` -> `GridRecipesAndUnnamedIngredients`, y `GridRecipeAndUnnamedIngredients.unnamedIngredients` -> `UnnamedIngredients`. `SlideshowItemstackTextComponent` conserva `curItemIndex`.
- `ShowItemMaxStackTooltip` queda en `true` por defecto y el fallback al vaciar slots incompatibles de la grilla con inventario lleno usa `DropItem` (soltar en el suelo), según preferencia confirmada por el usuario.
- Se corrigen bugs del mod original: `GlobalBounds` (`absFixedY`), doble `GuiElement.scaled` en `AdjustOffsets`, verificación de `IsOpened()` en `GuiDialogInventory` y respeto a `LockedSlots` de Seraph QoL.
