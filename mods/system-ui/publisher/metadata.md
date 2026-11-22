# Publisher Metadata — Seraph UI (`seraphui`)

## Ficha ModDB
- **Nombre público:** Seraph UI
- **`modid`:** `seraphui`
- **Autor:** `FrovaHappy`
- **Origen / Desarrollo:** Impulsado con IA · Hecho en Argentina
- **Versión actual:** `0.1.1`
- **Estado de desarrollo:** Etapa temprana de desarrollo (puede contener bugs)
- **Lado (`Side`):** `Universal`
- **Versión de Vintage Story:** `1.22.0`
- **Categoría sugerida:** Library / UI
- **Tags sugeridos:** `Library`, `UI`, `Framework`, `Universal`, `Theme`
- **Dependencias obligatorias:** `game` (`1.22.0`)
- **Requerido por:** `Traders Overhaul (tradersoverhaul)`, `Seraph QoL (seraphqol)`
- **Canales de configuración:** Sin configuración de usuario requerida

## Resumen corto (ModDB Tagline)
> Shared UI framework and reusable component library built to match Vintage Story's native interface aesthetic.

## Estado de Media y Templates
- [x] `publisher/description.html` (HTML TinyMCE-safe actualizado)
- [x] `publisher/images/modicon.png` (`256x256` PNG — enlazado a raíz del `.zip`)
- [x] `publisher/images/moddb-logo.jpg` (`480x480` JPG — ModDB Logo)
- [x] `publisher/images/external-logo.jpg` (`480x320` JPG — External Logo / social embeds)
- [x] `publisher/images/icon.jpg` (`1024x1024` JPG)
- [x] `publisher/images/cover.jpg` (`1376x768` JPG)
- [ ] `publisher/screenshots/` (capturas de diálogos usando `SeraphUI` en juego)

## URLs y Placeholders pendientes antes de publicar
- **URL ModDB asignada:** `https://mods.vintagestory.at/seraphui`
- **Repositorio / Source URL:** `TBD`

## Checklist de Release (`0.1.0`)
- [x] `modinfo.json` con `iconPath: "modicon.png"`
- [x] `SystemUI.csproj` empaqueta `modicon.png` en la raíz del `.zip`
- [ ] Capturas limpias añadidas en `publisher/screenshots/`
- [ ] Prueba en juego con `-DeployToGame -IsolateMods`
