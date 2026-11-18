# AGENT.md — Guía de Ingeniería para Agentes

Este documento define las metodologías y principios obligatorios para diseñar, implementar, probar y mantener mods en este monorepo (`VintageStoryMods.sln`).

---

## 1. Contexto del Monorepo

- **Lenguaje / Runtime**: C# (`.NET 10.0`, `Nullable` habilitado) sobre la API de Vintage Story.
- **Proyectos activos**:
  - `mods/system-ui` (`seraphui`): librería visual reutilizable (cliente).
  - `mods/comerciantes` (`tradersoverhaul`): motor económico, sincronización cliente/servidor, parches Harmony e integración con ConfigLib e i18n (`en`, `es-es`, `es-419`, `ru`).
  - `tests/Comerciantes.Tests`: suite de pruebas unitarias con xUnit.
- **Comandos de verificación estándar**:
  - Pruebas unitarias: `dotnet test VintageStoryMods.sln --no-restore -m:1`
  - Compilación Release y empaquetado: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/package-mods.ps1 -BuildOnly`

---

## 2. ODD — Object-Driven Design (Diseño Dirigido por Objetos)

Toda nueva funcionalidad o refactorización debe partir del modelado de **objetos de dominio cohesivos** antes de escribir parches Harmony, paquetes de red o vistas GUI:

1. **Comportamiento junto al estado**: evitar modelos anémicos y lógica de negocio dispersa en `ModSystem`, `GuiDialog` o parches `[HarmonyPatch]`. Cada objeto de dominio encapsula sus propias invariantes y reglas (p. ej., `ComerciantesConfig`, `WorldSaturationRegistry`, `TraderAffinityMatrix`, `ItemValuationRegistry`, `PriceCalculator`).
2. **Aislamiento de infraestructura**: separar el núcleo del dominio de las dependencias de ejecución del juego (`ICoreAPI`, `ICoreServerAPI`, `ICoreClientAPI`, `GuiComposer`, `Harmony`). Cuando un componente necesite datos de configuración o assets, debe aceptar objetos de datos puros o abstracciones además de su cargador desde `IAssetManager` (ejemplo: `ItemValuationRegistry.ApplyExplicitConfig` y `TraderAffinityMatrix.Load`).
3. **Contratos claros y lenguaje ubicuo**: nombrar clases, métodos y paquetes de red (`TraderPackets.cs`) según el dominio del mod, evitando literales mágicos y textos de interfaz acoplados al servidor (usar claves de traducción vía `ComerciantesLang` / `assets/<modid>/lang/*.json`).
4. **Composición sobre acoplamiento global**: los diálogos y utilidades de UI (`UiDialogBase`, `UiRichTextBuilder`, `UiComposerExtensions`) se componen mediante temas y constructores fluidos sin asumir estado global del mod consumidor.

---

## 3. TDD — Test-Driven Development (Desarrollo Guiado por Pruebas)

Cualquier cambio en reglas de negocio, cálculos, configuración, contratos de datos o localización debe seguir el ciclo **Red → Green → Refactor**:

1. **Red (Prueba que falla)**:
   - Antes de implementar o corregir lógica, escribir una prueba unitaria en `tests/<Mod>.Tests` (actualmente `tests/Comerciantes.Tests`) que describa el comportamiento esperado o reproduzca el bug.
   - Si una clase del dominio aún no es testeable sin levantar Vintage Story, extraer primero la frontera/contrato limpio (ver ODD y DIP) para poder ejercitarla en aislamiento.
2. **Green (Implementación mínima)**:
   - Escribir el código de producción mínimo necesario para que la prueba pase, sin sobre-ingeniería ni duplicación.
3. **Refactor (Limpieza segura)**:
   - Eliminar duplicación, mejorar nombres, extraer helpers compartidos y verificar que **toda** la suite siga pasando (`dotnet test VintageStoryMods.sln --no-restore -m:1`).

### Reglas prácticas de testing en este repo
- **Sin dependencias al juego en ejecución**: usar tipos livianos/stubs en `tests/` (como `TestItem` y `TestBlock` en `TestHelpers.cs`) cuando `VintagestoryAPI` lo permita, evitando inicializar estado gráfico (Cairo) o servidores reales en pruebas unitarias.
- **Cobertura de contratos e i18n**: todo texto nuevo orientado al usuario debe tener su clave en `en.json`, `es-es.json`, `es-419.json` y `ru.json`, y pasar las validaciones de `LocalizationTests` (claves idénticas, valores no vacíos, mismos marcadores `{0}`..`{n}` y coherencia con `configlib-patches.json`).
- **No declarar éxito sin ejecutar**: solo reportar pruebas como pasadas después de ejecutar `dotnet test` tras la última edición.

---

## 4. Principios SOLID

### S — Single Responsibility Principle (Responsabilidad Única)
- Cada clase debe tener una única razón de cambio:
  - El cálculo de precios (`PriceCalculator`), la valoración base (`ItemValuationRegistry`), las afinidades (`TraderAffinityMatrix`), la saturación (`WorldSaturationRegistry`) y la localización (`ComerciantesLang`) deben permanecer separados.
  - Los `ModSystem` (`ComerciantesModSystem`, `SystemUIModSystem`) solo orquestan ciclo de vida, eventos, red y comandos; no deben concentrar fórmulas de negocio ni renderizado complejo.
  - Los parches Harmony (`TraderDialogPatch`) deben delegar inmediatamente en servicios o el `ModSystem` correspondiente.

### O — Open/Closed Principle (Abierto/Cerrado)
- Preferir extensión mediante datos, tablas, mapeos o composición antes que cadenas crecientes de `if/else` o `switch`:
  - Reglas de afinidad y valores explícitos viven en JSON (`assets/tradersoverhaul/config/*.json`), no hardcodeados en la UI.
  - Los idiomas se extienden añadiendo archivos `assets/<modid>/lang/<locale>.json` sin modificar lógica de UI.
  - Los componentes de `SystemUI` se extienden mediante métodos de extensión sobre `GuiComposer` (`UiComposerExtensions`) y herencia controlada de `UiDialogBase`.

### L — Liskov Substitution Principle (Sustitución de Liskov)
- Toda subclase o implementación de interfaz (`UiDialogBase : GuiDialog`, `DisplayOnlyItemSlot : ItemSlot`, `SellOfferItemSlot : ItemSlotSurvival`, `StockOverlaySlotGrid : GuiElementItemSlotGrid`) debe respetar las precondiciones, postcondiciones e invariantes del tipo base sin lanzar excepciones inesperadas ni romper el flujo del invocador.
- En pruebas, los dobles (`TestItem : Item`, `TestBlock : Block`) deben respetar los contratos virtuales de `CollectibleObject`.

### I — Interface Segregation Principle (Segregación de Interfaces)
- Preferir contratos pequeños y específicos frente a dependencias monolíticas.
- Un calculador o componente visual no debe recibir `ICoreServerAPI` ni `ComerciantesModSystem` completo si solo necesita un subconjunto de datos (por ejemplo, `ComerciantesConfig`, un diccionario de precios o un proveedor de tooltips `Func<string>`).

### D — Dependency Inversion Principle (Inversión de Dependencias)
- Los módulos de alto nivel (economía, reglas de negocio, formateo de mensajes) no deben depender de detalles de infraestructura de bajo nivel (archivos en disco, red ProtoBuf, UI nativa o `Lang.CurrentLocale` sin guarda).
- Inyectar dependencias por constructor (`PriceCalculator(valuations, affinities, saturation)`) o mediante parámetros explícitos, permitiendo sustituir o alimentar datos directamente desde pruebas unitarias.

---

## 5. Zero Dead Code (Cero Código Muerto)

El repositorio debe mantenerse libre de código, assets o configuraciones sin uso real:

1. **Sin código huérfano ni comentado**: no dejar bloques de código comentados ("por si acaso"), métodos privados sin llamadas, clases sin referencias ni `using` innecesarios.
2. **Sin flags ni ramas inalcanzables**: eliminar parámetros obsoletos, propiedades que nadie lee, campos de paquetes ProtoBuf sin consumidor y ramas `if`/`switch` muertas tras un refactor.
3. **Limpieza de claves y assets**: toda clave en `assets/<modid>/lang/*.json` o entrada en `configlib-patches.json` debe tener uso activo en código o configuración; si se elimina una funcionalidad, se eliminan también sus claves de idioma, parches y pruebas obsoletas.
4. **Cero advertencias por código sin uso**: resolver advertencias del compilador (`CS0168`, `CS0169`, `CS0219`, `CS0414`, `IDE0051`, etc.) en lugar de silenciarlas con `#pragma warning disable`.

---

## 6. Checklist antes de dar por terminada una tarea

1. ¿El diseño separa objetos de dominio de parches Harmony, red y GUI (**ODD**)?
2. ¿Cada nueva regla, fórmula o corrección tiene pruebas unitarias asociadas y pasan todas (**TDD**)?
3. ¿Las clases mantienen responsabilidades acotadas, contratos limpios y sin lógica duplicada (**SOLID**)?
4. ¿Se eliminaron métodos, campos, `using`, claves `lang` y ramas sin uso (**Zero Dead Code**)?
5. Si hubo textos visibles para el jugador, ¿están localizados en `en`, `es-es`, `es-419` y `ru` y validados por `LocalizationTests`?
6. ¿Pasan `dotnet test VintageStoryMods.sln --no-restore -m:1` y `scripts/package-mods.ps1 -BuildOnly` sin errores?
