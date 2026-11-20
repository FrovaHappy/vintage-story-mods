# AGENT.md — Guía de Ingeniería para Agentes

Este documento define el flujo de trabajo y los principios obligatorios para diseñar, implementar, probar y mantener mods en este monorepo (`VintageStoryMods.sln`).

---

## 1. Contexto del Monorepo

- **Lenguaje / Runtime**: C# (`.NET 10.0`, `Nullable` habilitado) sobre la API de Vintage Story (`VintagestoryAPI`, `VSEssentials`, `VSSurvivalMod`, `0Harmony`, `protobuf-net`, `Newtonsoft.Json`, `cairo-sharp`).
- **Proyectos activos**:
  - `mods/system-ui` (`seraphui`): librería visual reutilizable (cliente) basada en `UiDialogBase`, `UiRichTextBuilder`, `UiComposerExtensions` y `SystemUITheme`.
  - `mods/comerciantes` (`tradersoverhaul`): motor económico, sincronización cliente/servidor, parches Harmony e integración con ConfigLib e i18n (`en`, `es-es`, `es-419`, `ru`).
  - `mods/seraph-qol` (`seraphqol`): suite modular de calidad de vida (inventario, menús radiales, calculadora de aleaciones, indicadores visuales, hotbar refill, rastreo de proyectiles y rebalanceo de fauna) con integración ConfigLib e i18n (`en`, `es-es`, `es-419`, `ru`).
- **Suites de pruebas (`xUnit`)**:
  - `tests/SystemUI.Tests`
  - `tests/Comerciantes.Tests`
  - `tests/SeraphQoL.Tests`
- **Configuración y Publicación de mods**:
  - Las preferencias y políticas se gestionan exclusivamente mediante archivos `.json` en `ModConfig/` (`LoadModConfig` / `StoreModConfig`) y su integración con **ConfigLib** (`assets/<modid>/config/configlib-patches.json`), sin comandos de chat propios para ajustes ni overrides por `/worldconfig`.
  - Cada mod mantiene sus materiales de publicación en `mods/<mod>/publisher/` (`description.html`, `metadata.md`, `images/`, `screenshots/`), siguiendo las convenciones y campos obligatorios definidos en [`docs/mod-publisher-guide.md`](docs/mod-publisher-guide.md).
  - Todo mod debe enlazar `publisher\images\modicon.png` hacia `<Link>modicon.png</Link>` en su `.csproj` y declarar `"iconPath": "modicon.png"` en `modinfo.json` para que cargue correctamente en el Gestor de Mods de Vintage Story.
- **Comandos de verificación estándar**:
  - Pruebas unitarias: `dotnet test VintageStoryMods.sln --no-restore -m:1`
  - Compilación Release y empaquetado: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/package-mods.ps1 -BuildOnly`

---

## 2. ODD — Organic Driven Development (Flujo de Trabajo)

El proceso de desarrollo crece de forma **proporcional y orgánica** según la complejidad, el alcance y el riesgo de la tarea (**Task Size**):

- **Cambios pequeños o locales (Inline)**:
  - Correcciones puntuales, ajustes de UI aislados, renombres o cambios de pocas líneas con alcance claro.
  - Se ejecutan de forma directa e inline sin burocracia ni artefactos pesados.
- **Cambios sustanciales o complejos (Tracked)**:
  - Nuevas features de extremo a extremo, refactorizaciones multi-módulo, cambios de red/configuración con impacto transversal o tareas que abarquen múltiples sesiones.
  - Generan documentación persistente de seguimiento en `odd/tasks/{feature-slug}.md` **antes de realizar la primera escritura de código** (*Track Before First Write*).

### Ciclo de Vida ODD (4 Fases)

1. **Fase 1 — Explore (Exploración en Solo Lectura)**:
   - **Lectura primero**: analizar código existente, dependencias, paquetes ProtoBuf, claves `lang` y pruebas antes de mutar cualquier archivo.
   - **Preguntas dirigidas**: preguntar al usuario **únicamente** cuando exista una decisión de producto o técnica genuinamente ambigua que no pueda deducirse del código.
   - **Task Sizing**: si el cambio es pequeño/local, pasar directo a ejecución; si es sustancial/multi-módulo, crear o actualizar `odd/tasks/{feature-slug}.md` con alcance, decisiones y checklist.
2. **Fase 2 — Plan (Planificación Proporcional)**:
   - Definir una secuencia concreta y pragmática basada en hallazgos reales del repositorio (archivos afectados, contratos de red/config y cobertura de pruebas), evitando especificaciones especulativas extensas.
   - **Continuidad (Durable Resume)**: si ya existe `odd/tasks/{feature-slug}.md`, reconciliar el estado del repositorio con su checklist para retomar exactamente donde quedó la sesión previa.
3. **Fase 3 — Implement (Implementación Incremental y TDD)**:
   - Ejecutar en bloques manejables (~400 líneas modificadas como referencia máxima por bloque lógico), actualizando `odd/tasks/{feature-slug}.md` cuando aplique.
   - Aplicar **TDD por defecto (`RED → GREEN → REFACTOR`)** en toda lógica de dominio, cálculo, planificación o configuración testeable. Si aplica una excepción (por ejemplo, renderizado OpenGL/Cairo o interoperabilidad directa con APIs visuales del juego), dejar constancia explícita y validar mediante compilación y verificación funcional.
   - **Documentación en template al añadir features**: toda nueva feature (o cambio/retiro visible para el jugador) debe quedar documentada en `mods/<mod>/publisher/description.html` y `mods/<mod>/publisher/metadata.md` (y en `publisher/screenshots/` si modifica la UI), conforme a [`docs/mod-publisher-guide.md`](docs/mod-publisher-guide.md).
4. **Fase 4 — Verify (Verificación y Cierre)**:
   - Ejecutar `dotnet test VintageStoryMods.sln --no-restore -m:1` y `scripts/package-mods.ps1 -BuildOnly` tras la última edición.
   - Confirmar cumplimiento de **Zero Dead Code**, marcar las tareas completadas en `odd/tasks/{feature-slug}.md` (si aplica) y reportar resultados exactos.
   - **Control del usuario**: acciones como `git commit`, `git push`, creación de PRs o merges quedan siempre bajo decisión explícita del usuario.

### Plantilla mínima para `odd/tasks/{feature-slug}.md`

```markdown
# Task: {Título de la tarea}

- **Estado:** `in-progress` | `completed` | `blocked`
- **Tamaño:** Sustancial / Multi-módulo
- **Archivos clave:**
  - `mods/<mod>/src/...`
  - `mods/<mod>/assets/...`
  - `tests/<Mod>.Tests/...`

## Objetivo y Alcance
Breve descripción del problema y del resultado esperado.

## Checklist de Ejecución
- [ ] 1. Exploración y definición de contratos / tipos
- [ ] 2. Pruebas unitarias en `tests/<Mod>.Tests` (TDD)
- [ ] 3. Implementación de dominio, red / config / UI y localización (`en`, `es-es`, `es-419`, `ru`)
- [ ] 4. Verificación (`dotnet test`, `package-mods.ps1 -BuildOnly`, Zero Dead Code)

## Notas de Continuidad / Decisiones
- Registro breve de decisiones técnicas tomadas o bloqueos encontrados.
```

---

## 3. Arquitectura de Dominio y Principios SOLID

### Aislamiento del Dominio vs. Infraestructura
- **Comportamiento junto al estado**: evitar modelos anémicos y lógica de negocio dispersa en `ModSystem`, `GuiDialog` o parches `[HarmonyPatch]`. Cada objeto de dominio encapsula sus propias invariantes y reglas (p. ej., `ComerciantesConfig`, `PriceCalculator`, `InventorySortPlanner`, `FaunaBalanceCalculator`, `AlloyCalculatorLogic`).
- **Aislamiento de infraestructura**: separar el núcleo puro del dominio de las dependencias del motor del juego (`ICoreAPI`, `ICoreServerAPI`, `ICoreClientAPI`, `GuiComposer`, `Harmony`). Cuando un componente necesite datos de configuración o assets, debe aceptar modelos de datos puros o abstracciones además de su cargador desde `IAssetManager` (p. ej., `ItemValuationRegistry.ApplyExplicitConfig`, `TraderAffinityMatrix.Load`).
- **Contratos claros y composición**: nombrar clases, métodos y paquetes `[ProtoContract]` según el dominio del mod; componer UI mediante `SeraphUI` (`UiDialogBase`, `UiRichTextBuilder`, `UiComposerExtensions`) sin acoplar estado global del mod consumidor.

### Principios SOLID
- **S — Single Responsibility**: cada clase tiene una única razón de cambio. Los `ModSystem` (`ComerciantesModSystem`, `SeraphQoLModSystem`, `SystemUIModSystem`) solo orquestan ciclo de vida, eventos, red y ConfigLib; los parches Harmony (`TraderDialogPatch`, `SeraphQoLClientPatches`) delegan inmediatamente en servicios o en su `ModSystem`.
- **O — Open/Closed**: extender mediante datos, tablas, mapeos o composición antes que cadenas crecientes de `if/else` o `switch` (p. ej., reglas en JSON bajo `assets/<modid>/config/`, idiomas en `assets/<modid>/lang/*.json`, extensiones sobre `GuiComposer`).
- **L — Liskov Substitution**: toda subclase (`UiDialogBase : GuiDialog`, `DisplayOnlyItemSlot : ItemSlot`, `SellOfferItemSlot : ItemSlotSurvival`, `StockOverlaySlotGrid : GuiElementItemSlotGrid`) y doble de prueba (`TestItem : Item`, `TestBlock : Block`) debe respetar precondiciones, postcondiciones e invariantes del tipo base.
- **I — Interface Segregation**: preferir contratos pequeños (`Func<EffectiveServerPolicy>`, `ITraderClientGateway`, registros de solo lectura) en lugar de pasar `ICoreServerAPI` o el `ModSystem` completo a componentes que solo necesitan un subconjunto de datos.
- **D — Dependency Inversion**: inyectar dependencias por constructor o parámetros explícitos (p. ej., `PriceCalculator(valuations, affinities, saturation)`) para ejercitar los módulos de negocio directamente en pruebas unitarias sin depender de disco, red o `Lang.CurrentLocale`.

---

## 4. TDD, Localización y Testing

Cualquier cambio en reglas de negocio, cálculos, configuración, contratos de red o localización sigue el ciclo **Red → Green → Refactor**:

1. **Red (Prueba que falla)**: escribir o ajustar primero la prueba en `tests/<Mod>.Tests` que especifique el comportamiento esperado o reproduzca el fallo. Si una clase aún no es testeable sin levantar Vintage Story, extraer primero la frontera limpia.
2. **Green (Implementación mínima)**: escribir el código de producción mínimo necesario para que la prueba pase, sin sobre-ingeniería ni duplicación.
3. **Refactor (Limpieza segura)**: eliminar duplicación, mejorar cohesión/nombres y verificar que toda la solución siga en verde.

### Reglas prácticas del monorepo
- **Sin dependencias al juego en ejecución**: usar tipos livianos/stubs en `tests/` (como `TestItem` y `TestBlock` en `TestHelpers.cs`) cuando `VintagestoryAPI` lo permita, evitando inicializar estado gráfico (Cairo) o servidores reales en pruebas unitarias.
- **Cobertura de contratos e i18n**: todo texto nuevo orientado al jugador debe existir en los 4 idiomas obligatorios (`en.json`, `es-es.json`, `es-419.json`, `ru.json`) y pasar `LocalizationTests` (claves idénticas, valores no vacíos, mismos marcadores `{0}`..`{n}` y coherencia con `configlib-patches.json`).
- **Configuración consistente**: toda opción configurable vive en su clase `*Config` (`.json`) y, cuando sea editable en UI, en `configlib-patches.json` con su handler correspondiente en `ApplySetting`.
- **No declarar éxito sin ejecutar**: solo reportar pruebas o compilación como exitosas después de ejecutarlas tras la última edición de código.

---

## 5. Zero Dead Code (Cero Código Muerto)

El repositorio debe mantenerse libre de código, assets o configuraciones sin uso real:

1. **Sin código huérfano ni comentado**: no dejar bloques comentados ("por si acaso"), métodos privados sin llamadas, clases sin referencias ni `using` innecesarios.
2. **Sin flags ni ramas inalcanzables**: eliminar parámetros obsoletos, propiedades que nadie lee, campos `[ProtoMember]` sin consumidor y ramas `if`/`switch` muertas tras un refactor.
3. **Limpieza de claves, config y pruebas**: toda clave en `assets/<modid>/lang/*.json` o entrada en `configlib-patches.json` debe tener uso activo; al eliminar una funcionalidad se eliminan también sus archivos, claves `lang`, configuraciones, documentación obsoleta y pruebas asociadas.
4. **Cero advertencias por código sin uso**: resolver advertencias del compilador (`CS0168`, `CS0169`, `CS0219`, `CS0414`, `IDE0051`, etc.) en lugar de silenciarlas con `#pragma warning disable`.

---

## 6. Checklist antes de dar por terminada una tarea

1. **ODD**: si la tarea fue sustancial/multi-módulo, ¿quedó registrada y actualizada en `odd/tasks/{feature-slug}.md` antes de escribir código?
2. **Arquitectura y SOLID**: ¿las reglas de dominio están aisladas de parches Harmony, red y GUI, con responsabilidades claras y sin duplicación?
3. **TDD**: ¿las nuevas reglas, fórmulas o correcciones tienen pruebas unitarias asociadas?
4. **Zero Dead Code**: ¿se eliminaron archivos, métodos, campos, `using`, claves `lang`, entradas de ConfigLib y pruebas obsoletas?
5. **Localización**: si hubo textos visibles para el jugador, ¿están en `en`, `es-es`, `es-419` y `ru` y validados por `LocalizationTests`?
6. **Publicación y Templates (`publisher/`)**: si se añadió o modificó una feature visible para el jugador, ¿se actualizó `mods/<mod>/publisher/description.html` y `metadata.md` según [`docs/mod-publisher-guide.md`](docs/mod-publisher-guide.md), y el mod conserva `modicon.png` + `iconPath` en `modinfo.json`?
7. **Verificación**: ¿pasan `dotnet test VintageStoryMods.sln --no-restore -m:1` y `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/package-mods.ps1 -BuildOnly` sin errores ni advertencias?
