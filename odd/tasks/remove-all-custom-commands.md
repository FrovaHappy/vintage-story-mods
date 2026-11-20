# Task: Eliminar todos los comandos personalizados en los mods

- **Estado:** `completed`
- **Tamaño:** Multi-módulo (`AGENT.md`, `docs/mod-publisher-guide.md`, `mods/comerciantes`, `tests/Comerciantes.Tests`)
- **Archivos clave:**
  - `mods/comerciantes/src/ComerciantesModSystem.cs`
  - `mods/comerciantes/src/Config/ComerciantesConfig.cs`
  - `mods/comerciantes/assets/tradersoverhaul/config/configlib-patches.json`
  - `mods/comerciantes/assets/tradersoverhaul/lang/{en,es-419,es-es,ru}.json`
  - `mods/comerciantes/publisher/{description.html,metadata.md,screenshots/README.md}`
  - `tests/Comerciantes.Tests/ComerciantesConfigTests.cs`
  - `AGENT.md`, `docs/mod-publisher-guide.md`

## Objetivo y Alcance
Eliminar por completo el registro y configuración de comandos personalizados (`ChatCommands`) en `mods/comerciantes` (único mod restante con comando personalizado, ya que `seraph-qol` y `system-ui` no registran ninguno), dejando la apertura del directorio exclusivamente mediante el atajo `K` (reconfigurable en Controles) y el botón en la ventana de comercio, además de actualizar `AGENT.md`, `docs/mod-publisher-guide.md` y templates `publisher/`.

## Checklist de Ejecución
- [x] 1. Remover `EnableDirectoryCommand`, `DirectoryCommandName`, `Normalize*` y `EnsureDirectoryCommandRegistered` de `ComerciantesConfig` y `ComerciantesModSystem`
- [x] 2. Limpiar `configlib-patches.json`, los 4 `lang/*.json` y `ComerciantesConfigTests.cs` (Zero Dead Code)
- [x] 3. Actualizar `mods/comerciantes/publisher/{description.html,metadata.md,screenshots/README.md}`, `AGENT.md` y `docs/mod-publisher-guide.md`
- [x] 4. Verificar (`dotnet test`, `scripts/package-mods.ps1 -BuildOnly`, búsqueda cero `ChatCommands`)
