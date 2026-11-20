# Task: Renombrar comando /comerciantes a tradersoverhaul configurable y limpiar temporales

- **Estado:** `completed`
- **Tamaño:** Multi-módulo (`mods/comerciantes`, `tests/Comerciantes.Tests`, `dist/`)
- **Archivos clave:**
  - `mods/comerciantes/src/Config/ComerciantesConfig.cs`
  - `mods/comerciantes/src/ComerciantesModSystem.cs`
  - `mods/comerciantes/assets/tradersoverhaul/config/configlib-patches.json`
  - `mods/comerciantes/assets/tradersoverhaul/lang/{en,es-419,es-es,ru}.json`
  - `mods/comerciantes/publisher/{description.html,metadata.md}`
  - `tests/Comerciantes.Tests/ComerciantesConfigTests.cs`

## Objetivo y Alcance
1. Renombrar el comando de directorio de `/comerciantes` a `/tradersoverhaul` para mantener coherencia con el `modid` (`tradersoverhaul`).
2. Añadir opciones en `ComerciantesConfig` (`.json` + ConfigLib) para activar/desactivar el comando (`EnableDirectoryCommand`) y personalizar su nombre (`DirectoryCommandName`, por defecto `"tradersoverhaul"`).
3. Actualizar `mods/comerciantes/publisher/description.html` y `metadata.md` según la regla de `AGENT.md`.
4. Eliminar los archivos temporales residuales en `dist/test-data/` y `dist/test-mods/`.

## Checklist de Ejecución
- [x] 1. Pruebas unitarias en `tests/Comerciantes.Tests/ComerciantesConfigTests.cs` (TDD)
- [x] 2. Implementar `EnableDirectoryCommand`, `DirectoryCommandName` y normalización en `ComerciantesConfig`
- [x] 3. Actualizar registro y recarga en vivo del comando en `ComerciantesModSystem`
- [x] 4. Actualizar `configlib-patches.json`, los 4 archivos `lang/*.json` y `mods/comerciantes/publisher/`
- [x] 5. Eliminar `dist/test-data/` y `dist/test-mods/`
- [x] 6. Verificación (`dotnet test`, `scripts/package-mods.ps1 -BuildOnly`, Zero Dead Code)

## Notas de Continuidad / Decisiones
- Confirmado con el usuario: comando por defecto `tradersoverhaul` (coincide con `modid`), configurable para activar/desactivar y cambiar su nombre vía ConfigLib y `tradersoverhaul.json`.
