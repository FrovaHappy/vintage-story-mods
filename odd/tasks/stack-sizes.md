# Task: Modificación configurable de tamaño de stacks en Seraph QoL

- **Estado:** `completed`
- **Tamaño:** Sustancial / Multi-módulo (`mods/seraph-qol`, `tests/SeraphQoL.Tests`)
- **Archivos clave:**
  - `mods/seraph-qol/src/StackSizes/StackSizeModels.cs`
  - `mods/seraph-qol/src/StackSizes/StackSizeClassifier.cs`
  - `mods/seraph-qol/src/StackSizes/StackSizeCalculator.cs`
  - `mods/seraph-qol/src/StackSizes/StackSizeAssetsService.cs`
  - `mods/seraph-qol/src/Config/SeraphQoLServerConfig.cs`
  - `mods/seraph-qol/src/Config/EffectiveServerPolicy.cs`
  - `mods/seraph-qol/src/Network/SeraphQoLPackets.cs`
  - `mods/seraph-qol/src/SeraphQoLModSystem.cs`
  - `mods/seraph-qol/assets/seraphqol/config/configlib-patches.json`
  - `mods/seraph-qol/assets/seraphqol/lang/{en,es-es,es-419,ru}.json`
  - `mods/seraph-qol/publisher/description.html`
  - `mods/seraph-qol/publisher/metadata.md`
  - `tests/SeraphQoL.Tests/StackSizeTests.cs`
  - `tests/SeraphQoL.Tests/SeraphQoLConfigTests.cs`

## Objetivo y Alcance
Implementar un módulo servidor-autoritativo (con sincronización al cliente en vivo) dentro de **Seraph QoL** (`seraphqol`) para modificar los tamaños máximos de stack (`CollectibleObject.MaxStackSize`), inspirado en el diseño de **P1nks Stack Sizes**, con enfoque híbrido estilo Seraph QoL:
- Presets (`Vanilla`, `P1nksBalanced`, `Double2x`, `Builder4x`, `Custom`)
- Multiplicadores y topes claros por categoría (`BuildingBlocks`, `Resources`, `Food`, `Misc`, más topes configurables para `Containers`, `FurnitureUtility` y `Clothing`)
- Protección de seguridad para herramientas, armas, armaduras, mochilas, ítems con durabilidad, líquidos y colmenas pobladas (`skep-populated-*`)
- Overrides y blacklist por patrón/código en JSON (`StackSizeOverrides`, `StackSizeBlacklist`) para compatibilidad con vanilla y mods de terceros
- Aplicación y restauración en vivo sin requerir reinicio cuando cambia la configuración en ConfigLib.

## Checklist de Ejecución
- [x] 1. Exploración del repositorio, contratos de SeraphQoL y diseño de P1nks Stack Sizes
- [x] 2. Pruebas unitarias en `tests/SeraphQoL.Tests` (TDD: RED → GREEN → REFACTOR)
- [x] 3. Implementación del núcleo puro (`StackSizeClassifier`, `StackSizeCalculator`, modelos), configuración, red y adaptador `StackSizeAssetsService`
- [x] 4. Integración en ConfigLib (`configlib-patches.json`), localización (`en`, `es-es`, `es-419`, `ru`) y documentación de publicación (`publisher/`)
- [x] 5. Verificación (`dotnet test`, `scripts/package-mods.ps1 -BuildOnly`, revisión Zero Dead Code)

## Notas de Continuidad / Decisiones
- Se integra como módulo opcional en `SeraphQoLServerConfig` con preset por defecto `Vanilla` (`EnableStackSizeModifier = false`) para no alterar mundos existentes sin opt-in del jugador/servidor, ofreciendo `P1nksBalanced`, `Double2x`, `Builder4x` y `Custom` a un clic en ConfigLib.
- Los valores base `MaxStackSize` originales se capturan una sola vez en `AssetsFinalize` tanto en servidor como en cliente, permitiendo recalcular y sincronizar cambios en vivo mediante `ServerPolicySyncPacket`.
- Se protegen explícitamente líquidos (`EnumMatterState.Liquid`) para no romper capacidades volumétricas de contenedores (litros/porciones), herramientas/armaduras/ítems con durabilidad, mochilas (`EnumItemStorageFlags.Backpack`) y colmenas pobladas.
