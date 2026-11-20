# Vintage Story Mods — Monorepo

Monorepo de desarrollo de mods para **Vintage Story** en C# (`.NET 10.0`, `Nullable` habilitado). Toda la guía de ingeniería, flujo **ODD**, reglas **SOLID / TDD / Zero Dead Code** y convenciones se administran desde [`AGENT.md`](AGENT.md).

---

## 📁 Estructura del Repositorio

```text
vintage-story-mods/
├── AGENT.md                        # Flujo ODD y reglas obligatorias de ingeniería
├── README.md
├── VintageStoryMods.sln
├── docs/
│   └── mod-publisher-guide.md      # Buenas prácticas de publicación y datos por mod
├── odd/
│   └── tasks/                      # Seguimiento persistente de tareas sustanciales
├── scripts/
│   └── package-mods.ps1            # Compilación, empaquetado (.zip) y despliegue aislado
├── mods/
│   ├── system-ui/                  # Seraph UI (seraphui)
│   │   ├── SystemUI.csproj
│   │   ├── modinfo.json
│   │   ├── publisher/              # description.html, metadata.md, images/, screenshots/
│   │   └── src/
│   ├── comerciantes/               # Traders Overhaul (tradersoverhaul)
│   │   ├── Comerciantes.csproj
│   │   ├── modinfo.json
│   │   ├── balance.md              # Referencia numérica de economía y saturación
│   │   ├── assets/tradersoverhaul/ # Config, ConfigLib e i18n (en, es-es, es-419, ru)
│   │   ├── publisher/              # description.html, metadata.md, images/, screenshots/
│   │   └── src/
│   └── seraph-qol/                 # Seraph QoL (seraphqol)
│       ├── SeraphQoL.csproj
│       ├── modinfo.json
│       ├── assets/seraphqol/       # ConfigLib, shaders, iconos SVG e i18n (en, es-es, es-419, ru)
│       ├── publisher/              # description.html, metadata.md, images/, screenshots/
│       └── src/
└── tests/
    ├── SystemUI.Tests/
    ├── Comerciantes.Tests/
    └── SeraphQoL.Tests/
```

---

## 🧩 Mods en este Monorepo

| Mod | `modid` | Lado | Publicación | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **Seraph UI** | `seraphui` | `Client` | [`publisher/`](mods/system-ui/publisher/metadata.md) | Librería de componentes de interfaz gráfica (`UiDialogBase`, `SystemUITheme`) reutilizable para Vintage Story. |
| **Traders Overhaul** | `tradersoverhaul` | `Universal` | [`publisher/`](mods/comerciantes/publisher/metadata.md) / [`balance.md`](mods/comerciantes/balance.md) | Rediseño integral de la economía de comerciantes, afinidades, demanda diaria, saturación y directorio global. |
| **Seraph QoL** | `seraphqol` | `Universal` | [`publisher/`](mods/seraph-qol/publisher/metadata.md) | Suite modular de UX/QoL (inventario, calculadora de aleaciones, menús radiales, indicadores de slots/fauna, refill, rastreo y rebalanceo de fauna). |

---

## 🛠️ Verificación y Entorno de Desarrollo

1. **.NET SDK**: `.NET 10.0 SDK` (`TargetFramework: net10.0`).
2. **Variables de Entorno**:
   - `VINTAGE_STORY`: ruta de instalación del juego (por defecto `%appdata%\Vintagestory`).
   - `VINTAGE_STORY_DATA`: ruta de datos del juego (por defecto `%appdata%\VintagestoryData`).
3. **Comandos estándar**:
   ```pwsh
   dotnet test VintageStoryMods.sln --no-restore -m:1
   pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/package-mods.ps1 -BuildOnly
   ```
4. **Herramientas de Inspección recomendadas**:
   - [ILSpy](https://github.com/icsharpcode/ILSpy) o [JetBrains dotPeek](https://www.jetbrains.com/decompiler/) para analizar `VintagestoryAPI.dll`, `VintagestoryLib.dll` y `VSEssentials.dll`.
