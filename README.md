# Vintage Story Mods - Monorepo

Monorepo de desarrollo de mods para **Vintage Story** en C# (.NET).

---

## 📁 Estructura del Repositorio

```text
vintage-story-mods/
├── README.md
├── .gitignore
└── mods/
    ├── system-ui/
    │   ├── concept.md          # Concepto y diseño visual del Mod de System UI
    │   └── technical.md        # Definiciones técnicas y arquitectura de UI
    ├── comerciantes/
    │   ├── concept.md          # Concepto y diseño económico del Mod de Comerciantes
    │   ├── technical.md        # Definiciones técnicas, entidad, red y parches
    │   └── balance.md          # Balance numérico, afinidades, saturación y precios base
    ├── ux-utilities/
    │   ├── concept.md          # Concepto, módulos QoL y créditos originales
    │   └── technical.md        # Definiciones técnicas, ConfigLib y menú de mundo
    └── fauna-salvaje/
        ├── concept.md          # Concepto y balance de caza (vida y carneado)
        └── technical.md        # Definiciones técnicas sobre EntityBehaviorHealth/Harvestable
```

---

## 🧩 Mods en este Monorepo

| Mod | `modid` | Concepto | Definición Técnica | Estado | Descripción |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Seraph UI** | `seraphui` | [`concept.md`](mods/system-ui/concept.md) | [`technical.md`](mods/system-ui/technical.md) | Activo (`0.1.0`) | Framework y biblioteca de componentes de interfaz gráfica (UI) desacoplada para Vintage Story. |
| **Traders Overhaul** | `tradersoverhaul` | [`concept.md`](mods/comerciantes/concept.md) | [`technical.md`](mods/comerciantes/technical.md) / [`balance.md`](mods/comerciantes/balance.md) | Activo (`0.1.0`) | Rediseño integral de la economía, entidades comerciantes, inventario e interacción del jugador con UI reactiva. |
| **Seraph QoL** | `seraphqol` | [`concept.md`](mods/ux-utilities/concept.md) | [`technical.md`](mods/ux-utilities/technical.md) | Conceptual | Suite modular de utilidades QoL y UX (inventario, resaltado de ítems, auto-refill, rastreo de proyectiles, partículas) con soporte para ConfigLib y personalización de mundo. |
| **Fauna QoL** | `faunaqol` | [`concept.md`](mods/fauna-salvaje/concept.md) | [`technical.md`](mods/fauna-salvaje/technical.md) | Conceptual | Rebalanceo de caza: incremento de vida en animales salvajes y mayor obtención de recursos al carnear. |

---

## 🛠️ Requisitos de Entorno de Desarrollo

De acuerdo a la arquitectura técnica de Vintage Story (v1.19+):

1. **.NET SDK**: .NET 7 SDK (o la versión correspondiente a la versión objetivo de Vintage Story).
2. **Variable de Entorno**:
   - `VINTAGE_STORY`: Ruta de instalación del juego (por defecto `%appdata%\Vintagestory`).
   - `VINTAGE_STORY_DATA`: Ruta de datos del juego (por defecto `%appdata%\VintagestoryData`).
3. **Plantillas Oficiales**:
   ```bash
   dotnet new install Vintagestory.Mod.Templates
   ```
4. **Herramientas de Inspección recomendadas**:
   - [ILSpy](https://github.com/icsharpcode/ILSpy) o [JetBrains dotPeek](https://www.jetbrains.com/decompiler/) para analizar `VintagestoryLib.dll` y `VSEssentials.dll`.
