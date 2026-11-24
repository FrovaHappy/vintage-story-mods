# Task: Per-Item Price Overrides in ConfigLib + Tighter Saturation Defaults

- **Estado:** `completed`
- **Tamaño:** Sustancial / Multi-módulo
- **Archivos clave:**
  - `mods/comerciantes/assets/tradersoverhaul/config/item_values.json`
  - `mods/comerciantes/assets/tradersoverhaul/config/configlib-patches.json`
  - `mods/comerciantes/src/Config/ComerciantesConfig.cs`
  - `mods/comerciantes/src/Economy/ItemValuationRegistry.cs`
  - `mods/comerciantes/src/Economy/PriceCalculator.cs`
  - `mods/comerciantes/src/Economy/WorldSaturationRegistry.cs`
  - `mods/comerciantes/src/ComerciantesModSystem.cs`
  - `mods/comerciantes/assets/tradersoverhaul/lang/en.json`
  - `mods/comerciantes/assets/tradersoverhaul/lang/es-es.json`
  - `mods/comerciantes/assets/tradersoverhaul/lang/es-419.json`
  - `mods/comerciantes/assets/tradersoverhaul/lang/ru.json`
  - `tests/Comerciantes.Tests/ComerciantesConfigTests.cs`
  - `tests/Comerciantes.Tests/PriceCalculatorTests.cs`
  - `tests/Comerciantes.Tests/WorldSaturationRegistryTests.cs`
  - `mods/comerciantes/changelogs/unreleased.md`
  - `mods/comerciantes/publisher/description.html`
  - `mods/comerciantes/publisher/metadata.md`

---

## Objetivo y Alcance

### Problema A — Casos "siempre 1 engranaje"
Con los defaults actuales (`SaturationMinFloor = 0.20`, `SaturationS50 = 25.0`, `SaturationCurvePower = 1.25`), ítems de Tier 3–4 con afinidad alta (p. ej. hacha tinbronze en `survivalgoods`: `4 × 1.35 × 0.20 = 1.08 ⚙`) siguen siendo comerciables a lote 1 incluso con saturación máxima. El jugador percibe que "la config no hace efecto" porque el ítem nunca se bloquea.

**Solución:** Ajustar los defaults en `ComerciantesConfig` para que la curva sea más agresiva desde la release:

| Parámetro | Valor Actual | Nuevo Default |
|---|---|---|
| `SaturationMinFloor` | `0.20` | `0.10` |
| `SaturationS50` | `25.0` | `15.0` |
| `SaturationCurvePower` | `1.25` | `1.6` |
| `SaturationRecoveryPerDay` | `6.0` | `8.0` (recupera más rápido para compensar) |

> ⚠️ Este cambio requiere **reset de los datos de saturación guardados** en las partidas existentes (clave `tradersoverhaul_world_saturation` en el save). Se notificará a los usuarios.

### Problema B — Sin forma de editar precios in-config
El usuario no puede sobreescribir precios individuales desde ConfigLib; solo puede editar `item_values.json` manualmente. Se añadirá soporte para un bloque `itemValueOverrides` en `tradersoverhaul.json` (el mismo archivo que maneja ConfigLib), que permita sobreescribir el `ValorBase` de ítems específicos sin tocar el asset JSON.

**Solución:** Añadir `Dictionary<string, double> ItemValueOverrides` a `ComerciantesConfig` y aplicarlo como capa de overrides sobre `ItemValuationRegistry` al cargar.

---

## Checklist de Ejecución

### Bloque 1 — Nuevos defaults de saturación
- [x] 1a. Ajustar valores default en `ComerciantesConfig` (`SaturationMinFloor`, `SaturationS50`, `SaturationCurvePower`, `SaturationRecoveryPerDay`).
- [x] 1b. Actualizar rangos/defaults en `configlib-patches.json` para reflejar los nuevos valores.
- [x] 1c. Actualizar tabla de ejemplos en `balance.md`.
- [x] 1d. Actualizar o añadir casos en `PriceCalculatorTests` y `WorldSaturationRegistryTests` que validen que ítems Tier 3–4 con saturación máxima caen por debajo de 1 ⚙ y quedan bloqueados.

### Bloque 2 — `ItemValueOverrides` en config
- [x] 2a. Añadir `Dictionary<string, double> ItemValueOverrides` a `ComerciantesConfig` con valor default vacío.
- [x] 2b. Añadir `ApplySetting` handler para leer la clave `ItemValueOverrides` desde ConfigLib (o desde el JSON directo).
- [x] 2c. En `ComerciantesModSystem` (o en el punto de inicialización de `ItemValuationRegistry`), tras `LoadFromAssets`, aplicar los overrides del config: iterar `ItemValueOverrides` y actualizar `_explicitValues` del registry.
- [x] 2d. Exponer un método público en `ItemValuationRegistry` (p. ej. `ApplyConfigOverrides(IReadOnlyDictionary<string, double>)`) para aplicar los overrides de forma testeable y aislada. Las claves que terminan en `*` se tratan como wildcards de prefijo; en `GetBaseValue` se resuelve primero coincidencia exacta, luego la wildcard de prefijo más largo.
- [x] 2e. Escribir tests en `ComerciantesConfigTests` que validen que los overrides se aplican y tienen prioridad sobre `item_values.json` y sobre los valores inferidos.
- [x] 2f. Añadir entradas en los 4 archivos `lang/*.json` para la descripción del campo en ConfigLib (si se expone en UI) — N/A (no se expone en UI de ConfigLib según decisión 2g).
- [x] 2g. Decidir si `ItemValueOverrides` aparece en la UI de ConfigLib (un mapa no es un tipo directo de ConfigLib — evaluarlo; alternativa: solo editable en el JSON del archivo `tradersoverhaul.json`).

### Bloque 3 — Localización y documentación
- [x] 3a. Actualizar `en.json`, `es-es.json`, `es-419.json`, `ru.json` si se añaden textos visibles.
- [x] 3b. Actualizar `mods/comerciantes/publisher/description.html` y `metadata.md`.
- [x] 3c. Actualizar `mods/comerciantes/changelogs/unreleased.md`.

### Bloque 4 — Verificación
- [x] 4a. `dotnet test VintageStoryMods.sln --no-restore -m:1` sin errores (244 tests passing).
- [x] 4b. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/package-mods.ps1 -BuildOnly` sin errores (0 advertencias, 0 errores, zips generados).
- [x] 4c. Zero Dead Code: sin parámetros, campos o claves `lang` huérfanas, sin advertencias del compilador.

---

## Notas de Continuidad / Decisiones

- **Reset de save obligatorio para Problema A**: el cambio de defaults no migra automáticamente los datos `tradersoverhaul_world_saturation` existentes. Los usuarios deberán resetear la saturación manualmente o se aplicará solo a partir del próximo ciclo.
- **Problema B — UI de ConfigLib para diccionarios**: ConfigLib no soporta campos `Dictionary` como control de UI nativo. `ItemValueOverrides` es editable únicamente en el JSON de `tradersoverhaul.json` directamente, sin control de UI en ConfigLib.
- **Prioridad de overrides**: `ItemValueOverrides` (config) > `item_values.json` (asset) > heurísticas de familia > valores inferidos por receta.
- **Wildcards**: el campo `ItemValueOverrides` acepta claves con `*` como sufijo (p. ej. `"game:axe-felling-*": 3.0`). La resolución sigue el orden: coincidencia exacta en overrides > coincidencia por wildcard más larga en overrides > cadena normal de valuación. Wildcards no soportan posición media (`*` solo al final). Sin soporte de UI en ConfigLib (campo `Dictionary` no soportado nativamente); edición solo vía JSON.
- **Aplicación de overrides**: `ApplyConfigOverrides` se llama en `AssetsFinalize` y en `ReloadConfig` para persistir cambios al editar el JSON.
