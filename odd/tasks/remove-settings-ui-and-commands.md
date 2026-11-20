# Task: Eliminar interfaz Alt+O, comando /seraphqol y overrides /worldconfig seraphqol:*

- **Estado:** `completed`
- **Tamaño:** Multi-módulo (`mods/seraph-qol`, `tests/SeraphQoL.Tests`, `docs`)
- **Archivos clave:**
  - `mods/seraph-qol/src/SeraphQoLModSystem.cs`
  - `mods/seraph-qol/src/Client/GuiDialogSeraphQoLSettings.cs` (eliminar)
  - `mods/seraph-qol/src/Config/WorldPolicyOverride.cs` -> `EffectiveServerPolicy.cs`
  - `mods/seraph-qol/assets/seraphqol/lang/*.json`
  - `tests/SeraphQoL.Tests/SeraphQoLConfigTests.cs`
  - `mods/seraph-qol/publisher/description.html` (antes `docs/moddb/seraphqol-description.html`)

## Objetivo y Alcance
Eliminar la interfaz de ajustes activada con `Alt + O` (`GuiDialogSeraphQoLSettings`), el comando de chat `/seraphqol` y los overrides por mundo `/worldconfig seraphqol:*` (`WorldPolicyOverride`), manteniendo la configuración del mod reservada exclusivamente a **ConfigLib** y archivos `.json` (`ModConfig/seraphqol.json` y `ModConfig/seraphqol-server.json`), sin dejar código, claves de idioma ni pruebas muertas.

## Checklist de Ejecución
- [x] 1. Exploración y definición de contratos afectados (`EffectiveServerPolicy` sin `WorldPolicyOverride`)
- [x] 2. Actualizar pruebas unitarias en `tests/SeraphQoL.Tests/SeraphQoLConfigTests.cs` (TDD)
- [x] 3. Eliminar `GuiDialogSeraphQoLSettings`, hotkey `seraphqol.settings` (`Alt+O`), comando `/seraphqol` y `WorldPolicyOverride`
- [x] 4. Limpiar claves y referencias en `assets/seraphqol/lang/{en,es-419,es-es,ru}.json` y documentación
- [x] 5. Verificación (`dotnet test`, `scripts/package-mods.ps1 -BuildOnly`, revisión Zero Dead Code)

## Notas de Continuidad / Decisiones
- Se conserva `EffectiveServerPolicy` y `ServerPolicySyncPacket` para sincronizar la política de `SeraphQoLServerConfig` del servidor hacia los clientes en multijugador, extrayendo `EffectiveServerPolicy` a `mods/seraph-qol/src/Config/EffectiveServerPolicy.cs`.
