# Task: Estructura publisher/ por mod, guía de buenas prácticas e iconos en Mod Manager

- **Estado:** `completed`
- **Tamaño:** Multi-módulo (`AGENT.md`, `docs/`, `mods/system-ui`, `mods/comerciantes`, `mods/seraph-qol`)
- **Archivos clave:**
  - `AGENT.md`
  - `docs/mod-publisher-guide.md`
  - `mods/<mod>/publisher/{description.html,metadata.md,images/,screenshots/}`
  - `mods/<mod>/*.csproj` y `mods/<mod>/modinfo.json`

## Objetivo y Alcance
1. Actualizar `AGENT.md` para exigir que toda nueva feature se documente también en el template de publicación del mod (`mods/<mod>/publisher/description.html`) y sus metadatos (`metadata.md`), enlazando la guía de buenas prácticas.
2. Crear `docs/mod-publisher-guide.md` con buenas prácticas de desarrollo/publicación para mods de Vintage Story y la definición de datos a rellenar por mod.
3. Organizar `mods/<mod>/publisher/` (`system-ui`, `comerciantes`, `seraph-qol`) con templates HTML, metadatos, imágenes existentes (`cover`, `icon`, `modicon.png`) y carpeta `screenshots/`.
4. Configurar `.csproj` y `modinfo.json` de los 3 mods para empaquetar `modicon.png` en la raíz del `.zip` y que cargue en el gestor de mods de Vintage Story.

## Checklist de Ejecución
- [x] 1. Explorar `docs/moddb/`, `.csproj`, `modinfo.json` y carga de `modicon.png` en Vintage Story
- [x] 2. Mover assets existentes de `docs/moddb/` a `mods/<mod>/publisher/`
- [x] 3. Crear `docs/mod-publisher-guide.md`, `mods/<mod>/publisher/metadata.md` y `screenshots/README.md`
- [x] 4. Configurar `.csproj` (`<Link>modicon.png</Link>`) y `modinfo.json` (`"iconPath": "modicon.png"`) en los 3 mods
- [x] 5. Actualizar `AGENT.md` con la regla de documentación en templates y estructura `publisher/`
- [x] 6. Verificar compilación, tests (`dotnet test`), empaquetado (`package-mods.ps1 -BuildOnly`) e inspección de `modicon.png` dentro de los `.zip`

## Notas de Continuidad / Decisiones
- Verificado vía reflexión sobre `VintagestoryAPI.dll` / `VintagestoryLib.dll` y mods instalados que Vintage Story carga `modicon.png` desde la raíz del `.zip` (`ModInfo.IconPath` con fallback `"modicon.png"`).
- Se vincula `publisher\images\modicon.png` con `<Link>modicon.png</Link>` en cada `.csproj` para evitar duplicar binarios y no incluir el resto de `publisher/` en el `.zip` distribuible.
