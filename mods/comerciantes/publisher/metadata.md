# Publisher Metadata — Traders Overhaul (`tradersoverhaul`)

## Ficha ModDB
- **Nombre público:** Traders Overhaul
- **`modid`:** `tradersoverhaul`
- **Autor:** `FrovaHappy`
- **Origen / Desarrollo:** Impulsado con IA · Hecho en Argentina
- **Versión actual:** `0.1.0`
- **Estado de desarrollo:** Etapa temprana de desarrollo (puede contener bugs)
- **Lado (`Side`):** `Universal`
- **Versión de Vintage Story:** `1.22.0`
- **Categoría sugerida:** Economy / Gameplay
- **Tags sugeridos:** `Traders`, `Economy`, `QoL`, `UI`, `ConfigLib`
- **Dependencias obligatorias:** `game` (`1.22.0`), `survival` (`1.22.0`), `seraphui` (`0.1.0`)
- **Idiomas soportados:** `en`, `es-es`, `es-419`, `ru`
- **Canales de configuración:** `ModConfig/tradersoverhaul.json` + **ConfigLib** (apertura de directorio vía atajo `K` o botón en la ventana de comercio)

## Resumen corto (ModDB Tagline)
> A living trader economy for Vintage Story: universal selling, archetype affinities, daily wanted bonuses, anti-farming saturation, modern trade UI, and Global Trader Directory.

## Estado de Media y Templates
- [x] `publisher/description.html` (HTML TinyMCE-safe actualizado)
- [x] `publisher/images/modicon.png` (`256x256` PNG — enlazado a raíz del `.zip`)
- [x] `publisher/images/moddb-logo.jpg` (`480x480` JPG — ModDB Logo)
- [x] `publisher/images/external-logo.jpg` (`480x320` JPG — External Logo / social embeds)
- [ ] `publisher/images/icon.jpg` (`1024x1024` — pendiente)
- [ ] `publisher/images/cover.jpg` (`1376x768` — pendiente)
- [x] `publisher/screenshots/` (4 capturas añadidas: ventana de comercio, tasación detallada, directorio global y ConfigLib)

## URLs y Placeholders pendientes antes de publicar
- **Dependencia Seraph UI (ModDB):** `https://mods.vintagestory.at/seraphui`
- **URL ModDB asignada:** `TBD`
- **Repositorio / Source URL:** `TBD`

## Checklist de Release (`0.1.0`)
- [x] `modinfo.json` con `iconPath: "modicon.png"`
- [x] `Comerciantes.csproj` empaqueta `modicon.png` en la raíz del `.zip`
- [ ] Generar `publisher/images/icon.jpg` y `publisher/images/cover.jpg`
- [x] Capturas limpias añadidas en `publisher/screenshots/`
- [ ] Prueba en juego con `-DeployToGame -IsolateMods`
