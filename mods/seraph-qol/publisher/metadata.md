# Publisher Metadata — Seraph QoL (`seraphqol`)

## Ficha ModDB

- **Nombre público:** Seraph QoL
- **`modid`:** `seraphqol`
- **Autor:** `FrovaHappy`
- **Origen / Desarrollo:** Impulsado con IA · Hecho en Argentina
- **Versión actual:** `0.1.0`
- **Estado de desarrollo:** Etapa temprana de desarrollo (puede contener bugs)
- **Lado (`Side`):** `Universal`
- **Versión de Vintage Story:** `1.22.0`
- **Categoría sugerida:** Quality of Life / Utility
- **Tags sugeridos:** `QoL`, `Inventory`, `Radial Menu`, `Fauna`, `ConfigLib`
- **Dependencias obligatorias:** `game` (`1.22.0`), `survival` (`1.22.0`), `seraphui` (`0.1.0`)
- **Idiomas soportados:** `en`, `es-es`, `es-419`, `ru`
- **Canales de configuración:** `ModConfig/seraphqol.json` (cliente), `ModConfig/seraphqol-server.json` (servidor) y **ConfigLib**

## Resumen corto (ModDB Tagline)
>
> Modular Quality-of-Life suite for Vintage Story: inventory sorting & protection, improved handbook recipes, configurable stack sizes, expandable backpacks & saddlebags, alloy calculator, radial menus, slot & livestock badges, entity health bars, hotbar refill, tracking, and fauna rebalance.

## Estado de Media y Templates

- [x] `publisher/description.html` (HTML TinyMCE-safe actualizado sin `Alt+O`, `/seraphqol` ni `/worldconfig`)
- [x] `publisher/images/modicon.png` (`256x256` PNG — enlazado a raíz del `.zip`)
- [x] `publisher/images/moddb-logo.jpg` (`480x480` JPG — ModDB Logo)
- [x] `publisher/images/external-logo.jpg` (`480x320` JPG — External Logo / social embeds)
- [x] `publisher/images/icon.jpg` (`1024x1024` JPG)
- [x] `publisher/images/cover.jpg` (`1376x768` JPG)
- [x] `publisher/screenshots/` (6 capturas añadidas: inventario, calculadora de aleaciones, menú radial, barras de vida e insignias, partículas y ConfigLib)

## URLs y Placeholders pendientes antes de publicar

- **Dependencia Seraph UI (ModDB):** `https://mods.vintagestory.at/seraphui`
- **URL ModDB asignada:** `TBD`
- **Repositorio / Source URL:** `TBD`

## Checklist de Release (`0.1.0`)

- [x] `modinfo.json` con `iconPath: "modicon.png"`
- [x] `SeraphQoL.csproj` empaqueta `modicon.png` en la raíz del `.zip`
- [x] Capturas limpias añadidas en `publisher/screenshots/`
- [ ] Prueba en juego con `-DeployToGame -IsolateMods`
