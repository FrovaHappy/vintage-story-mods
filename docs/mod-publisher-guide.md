# Guía de Buenas Prácticas y Publicación de Mods (`publisher/`)

Esta guía define los estándares técnicos, visuales y editoriales para preparar, mantener y publicar los mods del monorepo en **Vintage Story** (Gestor de Mods del juego y [ModDB](https://mods.vintagestory.at)).

---

## 1. Estructura estándar por mod (`mods/<mod>/publisher/`)

Cada mod debe mantener una carpeta `publisher/` fuera del paquete distribuible final (excepto `modicon.png`, que se enlaza a la raíz del `.zip`):

```text
mods/<mod>/
├── modinfo.json
├── <Mod>.csproj                  # Enlaza publisher/images/modicon.png -> modicon.png
└── publisher/
    ├── description.html          # Template HTML (100% inline CSS, TinyMCE-safe) para ModDB
    ├── metadata.md               # Ficha de metadatos, tags, enlaces y checklist de publicación
    ├── images/
    │   ├── modicon.png           # Ícono del gestor de mods in-game (256x256 PNG)
    │   ├── moddb-logo.jpg        # ModDB Logo image (exacto 480x480 px, subir a Screenshots y elegir como ModDB Logo)
    │   ├── external-logo.jpg     # External Logo image (exacto 480x320 px, subir a Screenshots para embeds sociales)
    │   ├── icon.jpg              # Ícono/tarjeta cuadrada fuente (1024x1024 JPG/PNG)
    │   └── cover.jpg             # Portada / banner fuente (1376x768 o 16:9 JPG/PNG)
    └── screenshots/
        ├── README.md             # Lista de capturas recomendadas para ese mod
        └── *.png / *.jpg         # Capturas limpias dentro del juego
```

> [!IMPORTANT]
> **Regla obligatoria por nueva feature:** cada vez que se añada, modifique o elimine una funcionalidad visible para el jugador, atajo de teclado o regla de configuración, se debe actualizar en el mismo ciclo de trabajo:
> 1. `mods/<mod>/publisher/description.html` (sección de módulos/features, controles o configuración).
> 2. `mods/<mod>/publisher/metadata.md` (resumen, tags, changelog o checklist de capturas).
> 3. `mods/<mod>/publisher/screenshots/` (añadir o reemplazar capturas si cambia la UI).

---

## 2. Datos obligatorios a rellenar en cada mod

### 2.1. `modinfo.json` (Gestor de Mods y Runtime)
Campos mínimos requeridos y buenas prácticas:
- `type`: `"code"` para mods compilados en C#.
- `modid`: identificador único en minúsculas sin espacios ni guiones (`seraphui`, `tradersoverhaul`, `seraphqol`).
- `name`: nombre legible público (`Seraph UI`, `Traders Overhaul`, `Seraph QoL`).
- `version`: **SemVer** (`MAJOR.MINOR.PATCH`, p. ej. `0.1.0`), alineado con la release en ModDB.
- `side`: `"Client"`, `"Server"` o `"Universal"` según su alcance real.
- `authors` y `contributors`: atribución explícita de autoría e inspiraciones/referencias.
- `description`: resumen breve y claro en una sola frase para el listado del Gestor de Mods.
- `iconPath`: `"modicon.png"` (junto al archivo `modicon.png` copiado a la raíz del `.zip` compilado).
- `dependencies`: incluir `"game": "1.22.0"`, `"survival": "1.22.0"` (si aplica) y dependencias internas como `"seraphui": "0.1.0"`.

### 2.2. `publisher/metadata.md` (Datos de ficha en ModDB)
Todo mod debe tener completos y revisados los siguientes campos antes de publicar:
- **Mod Name & `modid`**
- **Tagline / Summary** (máx. ~160 caracteres para tarjeta de ModDB)
- **Side** (`Client` / `Server` / `Universal`) & **Supported Game Versions**
- **Category & Tags** (máx. 5–8 tags relevantes, p. ej. `QoL`, `Inventory`, `UI`, `Economy`, `Library`)
- **Dependencies & Compatibility** (incluyendo enlaces reales a ModDB en reemplazo de placeholders como `#SERAPH_UI_MODDB_URL`)
- **Configuration Channels** (`ModConfig/<modid>.json` / `ModConfig/<modid>-server.json` + **ConfigLib**; sin comandos personalizados de chat)
- **Media & Assets Status** (`modicon.png`, `icon.jpg`, `cover.jpg`, `screenshots/`)
- **Release Checklist & Changelog**

---

## 3. Buenas prácticas de desarrollo y publicación en Vintage Story

1. **Ícono en el Gestor de Mods (`modicon.png`)**:
   - Debe residir en la **raíz del `.zip`** (al lado de `modinfo.json`) y referenciarse con `"iconPath": "modicon.png"` en `modinfo.json`.
   - En el `.csproj`, vincular `publisher\images\modicon.png` con `<Link>modicon.png</Link>` y `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` para no incluir el resto de `publisher/` dentro del `.zip`.
   - Formato recomendado: PNG `256x256` px, fondo opaco o silueta legible incluso a tamaño reducido en el menú de mods.
2. **Logos en ModDB (`moddb-logo.jpg` y `external-logo.jpg`)**:
   - ModDB exige que tanto **ModDB Logo image** (tarjeta del mod) como **External Logo image** (embeds en redes sociales/Discord) se seleccionen desde **Screenshots** y midan exactamente **`480x480`** o **`480x320`** px (las imágenes elegidas como logo no se muestran en el slideshow).
   - Si no se elige un External Logo dedicado, ModDB recorta los `480x320` px superiores del ModDB Logo. Por eso cada mod mantiene `moddb-logo.jpg` (`480x480`) y `external-logo.jpg` (`480x320`).
3. **Template HTML de ModDB (`publisher/description.html`)**:
   - Usar **100% CSS inline** compatible con TinyMCE (`mods.vintagestory.at` -> *Source code `<>`*); evitar `<style>`, `<script>` o clases externas.
   - Mantener idioma **inglés** en el HTML público de ModDB, con la paleta retro de Vintage Story (`#4a3b2c`, `#31261c`, `#534231`, bordes biselados).
   - No documentar comandos o atajos inexistentes o retirados; mantener sincronía exacta con `assets/<modid>/lang/*.json` y `configlib-patches.json`.
3. **Capturas de pantalla (`publisher/screenshots/`)**:
   - Formato 16:9 (`1920x1080` o `1376x768`), PNG o JPG de alta calidad, sin overlay de FPS/debug (`F3`) ni marcas de agua.
   - Nombres descriptivos en kebab-case (`01-trade-window.png`, `02-alloy-calculator.png`).
4. **Configuración limpia (ConfigLib + `.json`)**:
   - Persistir preferencias en `ModConfig/<modid>.json` (y `<modid>-server.json` si hay reglas autoritativas de servidor).
   - Proveer `configlib-patches.json` con traducciones completas en los 4 idiomas del monorepo (`en`, `es-es`, `es-419`, `ru`).
   - No implementar comandos personalizados de chat (usar atajos reconfigurables en Controles del juego y ConfigLib / `.json`).
5. **Empaquetado y prueba aislada**:
   - Compilar en `Release` y verificar el contenido del `.zip` con `scripts/package-mods.ps1` (incluyendo prueba aislada con `-IsolateMods` antes de publicar una nueva versión).
