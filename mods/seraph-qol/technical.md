# Mod: Seraph QoL (`seraphqol`) — Definiciones Técnicas

## 1. Arquitectura Modular y Principios del Monorepo
**Seraph QoL** (`seraphqol`) se implementa como un **Code Mod en C# (.NET 10.0, `Nullable` habilitado)** sobre `VintagestoryAPI`, `VSEssentials` y `VSSurvivalMod`, siguiendo estrictamente las metodologías de [`AGENT.md`](../../AGENT.md):
- **ODD (Object-Driven Design)**: Objetos de dominio cohesivos e independientes de `ICoreAPI`/UI/Harmony (por ejemplo: `SeraphQoLConfig`, `SortPlanner`, `FaunaRebalancePolicy`, `AlloyCalculatorLogic`, `AutoStashPlanner`).
- **TDD + SOLID**: Toda regla de clasificación de fauna, cálculo de botín, algoritmo de ordenamiento, filtrado de slots y regla de aleaciones se prueba de forma unitaria en `tests/SeraphQoL.Tests` sin levantar el cliente gráfico ni servidor real.
- **Separación Cliente / Servidor**: Cada submódulo registra explícitamente su alcance (`Client`, `Server` o `Universal`) y sincroniza acciones con impacto de estado mediante `INetworkChannel` (`ProtoContract`) con validación autoritativa en el servidor.

### 1.1. Matriz de Submódulos

| Submódulo | Lado | Mecanismos Técnicos Principales |
| :--- | :--- | :--- |
| **Inventario y Contenedores** | Cliente + Servidor | Inyección de botones SVG en `GuiDialogInventory` / `GuiDialogBlockEntityInventory` (`SeraphUI` + Harmony); ordenamiento y compactado autoritativos en servidor con rollback transaccional por snapshot; favoritos persistidos en `WatchedAttributes` (`TreeAttribute` sincronizado); búsqueda espacial de contenedores vía `BlockAccessor.WalkBlocks` + `ContainerWhitelist`. |
| **Auto-Stashing y Auto-Looting** | Cliente + Servidor | `BlockBehaviorAutoStashable` / `EntityBehaviorAutoLootable` inyectados dinámicamente en `AssetsFinalize`; barra de progreso radial en cliente; validación de permisos (`IClaimAPI` / `CachedAccessPerms`) y transferencias en servidor. |
| **Calculadora de Aleaciones** | Cliente | Harmony `[HarmonyPostfix]` sobre `GuiDialogBlockEntityFirepit.OnGuiOpened`/`OnGuiClosed`; cálculo de proporciones exactas, objetivo sin desperdicio y combustible/precalentamiento; ejecución de depósito mediante movimientos nativos de inventario cliente-servidor. |
| **Menús Radiales y Quick-Swap** | Cliente | Reemplazo opcional de `GuiDialogToolMode` mediante `[HarmonyPrefix]` con estrategias por herramienta; menú radial de cambio rápido de herramienta/luz; `EquipLightSource` con intercambio reversible y selección del ítem más brillante. |
| **Indicadores en Slots y Mundo** | Cliente | Transpiler/Postfix sobre `GuiElementItemSlotGridBase.RenderInteractiveElements` con proveedores priorizados (`IItemSlotIndicatorProvider`) y muestreo adaptativo; renderizadores OpenGL (`IRenderer`) con shaders propios para sexo/preñez animal y decal de respawn; escaneo de `EntityItem` y partículas locales. |
| **Hotbar Refill** | Cliente (+ red nativa) | Monitoreo de slots activos y durabilidad restante; lista negra por código/tag; retardo configurable (`refillDelayMs`) y envío de paquetes nativos de intercambio de inventario. |
| **Rastreo de Proyectiles** | Cliente + Servidor | Seguimiento de entidades proyectil en servidor (`ICoreServerAPI`) al impactar/detenerse y al recogerse/despawnear, sincronizado vía `INetworkChannel` para crear/eliminar *waypoints* en cliente. |
| **Fauna QoL (Caza y Carneado)** | Servidor (+ sync nativo) | Ajuste dinámico en `AssetsFinalize` sobre `EntityProperties` (`EntityBehaviorHealth` y `EntityBehaviorHarvestable`) + ajuste en `OnEntitySpawn` para diferenciar salvaje vs. domesticado (`generation > 0`) e indicadores visuales en cliente. |

---

## 2. Detalle Técnico de Fauna QoL (Caza y Carneado)

### 2.1. Enfoque Dinámico en C# vs. Parches JSON
Los parches JSON estáticos por índice sobre `behaviors` son frágiles ante actualizaciones del juego o mods de terceros que alteran el arreglo de comportamientos, y no cubren fauna añadida por otros mods sin parches manuales por especie. Por ello se implementa en **C# en el servidor (`ICoreServerAPI`)** mediante una arquitectura en dos capas:

### 2.2. Capa 1: Ajuste de Definiciones en `AssetsFinalize` (`ICoreServerAPI`)
1. Recorrer `sapi.World.EntityTypes`.
2. **Filtrado de fauna**:
   - Identificar entidades animales por clase/tags/comportamientos (`EntityAgent`, presencia de `health` y `harvestable`).
   - Excluir explícitamente jugadores, comerciantes (`EntityTrader`), NPCs, entidades inanimadas y criaturas temporales (*drifters*, *locusts*, *shivers*, *bowtorn*), respetando `whitelist` y `blacklist` configuradas.
3. **Clasificación automática por categoría**:
   - Evaluar reglas explícitas y heurísticas (código de entidad, `maxhealth` base, comportamiento agresivo/depredador, drops característicos) para clasificar en: **Presas pequeñas**, **Presas medianas/grandes** y **Depredadores**.
4. **Escalado de vida (`EntityBehaviorHealth`)**:
   - Localizar en `Server.Behaviors` el comportamiento con `"code": "health"`.
   - Multiplicar `maxhealth` (y `currenthealth` si está definido) según categoría y configuración.
5. **Escalado de botín (`EntityBehaviorHarvestable`)**:
   - Localizar en `Server.Behaviors` el comportamiento con `"code": "harvestable"`.
   - Iterar sobre `drops` clasificando el código del ítem por familia (carne cruda, pieles/cueros, grasa, subproductos como huesos/plumas/cornamentas) para multiplicar `avg` y `var` de forma independiente, manteniendo compatibilidad con `GuiDialogCreatureContents`.

### 2.3. Capa 2: Ajuste en Tiempo de Ejecución (`OnEntitySpawn`)
- En `sapi.Event.OnEntitySpawn`, evaluar `entity.WatchedAttributes.GetInt("generation") > 0` para distinguir fauna salvaje de animales domesticados y aplicar o revertir el multiplicador de vida según la regla configurada.
- Inspeccionar `VSEssentials.dll` (`EntityBehaviorHealth` y `EntityBehaviorHarvestable`) para validar cómo interactúan el peso del animal y las penalizaciones nativas (caída/aplastamiento a la mitad, muerte no provocada por jugador al 40%).
- En el lado cliente, reutilizar `Entity.WatchedAttributes` (`generation`, `gender`, subárbol `multiply.isPregnant`) para renderizar los indicadores de sexo y preñez sobre animales domésticos vivos.

---

## 3. Detalle Técnico de Inventario, Contenedores y UX

### 3.1. Lecciones Adoptadas de Referencias Analizadas
Del análisis técnico de **Storage Tweaks**, **Vanilla: Expanded** e **Inventory Profiles Next** (ver [`informe-tecnico.md`](informe-tecnico.md)) se adoptan los siguientes patrones arquitectónicos:
- **Autoridad en Servidor para Inventarios (patrón *Storage Tweaks*)**: En Vintage Story el cliente no debe simular clicks rápidos como en Minecraft; el servidor ejecuta el ordenamiento, compactado y descarga, tomando un **snapshot previo** para hacer rollback si ocurre un error y protegiendo slots especiales (`CanSort`, `CanSortInto`, `IsRestrictedWildcardSlot`) y contenedores de mods (ej. *FoodShelves*, *MoreInventorys*, *BetterCrates*).
- **Separación de Planificación y Ejecución + Reglas Declarativas (patrón *IPN*, implementación clean-room)**: Modelar un planificador puro (`SortPlanner`) que ordene sobre una vista inmutable de ítems mediante comparadores componibles (nombre, código/ID, cantidad acumulada, frescura/durabilidad, categoría creativa) antes de aplicar movimientos reales, y soportar bloqueo de slots (`Locked Slots`) tanto por posición de slot como por ítem favorito.
- **Inyección Dinámica de Comportamientos y ConfigLib Live Reload (patrón *Vanilla Expanded*)**: Añadir `BlockBehaviorAutoStashable` y `EntityBehaviorAutoLootable` en `AssetsFinalize` (en lugar de parches JSON frágiles), usar `PatchClassProcessor` selectivo para activar/desactivar parches Harmony por módulo y escuchar el bus de eventos `configlib:<modid>:*` para recargar ajustes en vivo (`ILiveConfigurable`).

### 3.2. Protección de Slots y Favoritos
- **Favoritos por ítem** (`TreeAttribute` en `player.Entity.WatchedAttributes`, sincronizado por el servidor): evitan que *Quick Store* o *Store Nearby* descarguen ítems esenciales.
- **Slots bloqueados por índice** (`Locked Slots`): excluidos de ordenamientos y descargas masivas; opcionalmente renderizados con overlay discreto en la grilla.
- **Fusión de perecederos (*Stack Perishables*)**: opción explícita que permite promediar `TransitionStates` al compactar o descargar comida con distinta frescura (desactivada por defecto para evitar pérdida accidental de frescura máxima).

---

## 4. Sistema de Configuración, Mundo y `SeraphUI`

### 4.1. Persistencia y ConfigLib / ConfigKit
- Archivo de cliente `ModConfig/seraphqol.json` y archivo de servidor `ModConfig/seraphqol-server.json` mediante `LoadModConfig` / `StoreModConfig`.
- Compatibilidad opcional (*soft dependency*) con **ConfigLib** / **ConfigKit**:
  - `assets/seraphqol/config/configlib-patches.json` con secciones organizadas por módulo (Inventario, Interacción QoL, Indicadores, Hotbar Refill, Proyectiles, Fauna QoL).
  - Suscripción tanto a `ConfigLibModSystem.SettingChanged` / `ConfigsLoaded` como a los eventos de bus `configlib:seraphqol:setting-changed` para recarga en vivo sin reiniciar.

### 4.2. Sincronización de Política de Servidor
- Sincronizar la política efectiva del servidor (`SeraphQoLServerConfig`) al cliente en el handshake (`PlayerNowPlaying` / canal propio) y al guardar cambios en ConfigLib para que las restricciones del servidor prevalezcan sobre preferencias locales.

### 4.3. Integración con `mods/system-ui` (`seraphui`)
- Uso de [`mods/system-ui`](../system-ui/technical.md) (`UiDialogBase`, `UiComposerExtensions`, `SystemUITheme`) para:
  - Calculadora de aleaciones (`GuiDialogAlloyCalculator`) y componentes visuales integrados con el tema del juego.

---

## 5. Localización e Idiomas
Todo texto visible al jugador (UI, tooltips, ayudas de interacción, mensajes de error, ConfigLib) debe residir en `assets/seraphqol/lang/` en los 4 idiomas obligatorios del monorepo (`en.json`, `es-es.json`, `es-419.json`, `ru.json`) con paridad de claves y marcadores verificada por pruebas automatizadas.

---

## 6. Roadmap y Decisiones de Diseño Acordadas

### Fase 1 — MVP Funcional
1. **Núcleo (`SeraphQoL.csproj` + `SeraphQoL.Tests.csproj`)**:
   - Configuración dual (`ModConfig/seraphqol.json` y `seraphqol-server.json`) con recarga en vivo mediante **ConfigLib** / **ConfigKit**.
   - Canal `ProtoBuf` (`seraphqol`) y paquete base de localización (`en`, `es-es`, `es-419`, `ru`).
2. **Fauna QoL**:
   - Alcance automático sobre fauna terrestre y voladora cazable (excluyendo NPCs, comerciantes, criaturas temporales y peces salvo whitelist explícita).
   - **Valores por defecto (Balanceado Táctico)**:
     - Vida salvaje: `x1.5` presas pequeñas, `x2.0` presas medianas/grandes, `x2.5` depredadores.
     - Carneado: `x1.75` carne cruda y grasa, `x1.5` pieles y subproductos (huesos, plumas, cornamentas, etc.).
   - Animales domesticados (`generation > 0`): conservan vida vanilla por defecto y sí reciben el bonus de carneado configurable.
3. **Inventario y Contenedores**:
   - Ordenamiento y compactado server-autoritativo con snapshot + rollback.
   - Múltiples criterios de ordenamiento (`Categoría/Tipo`, `Nombre`, `Código/ID`, `Cantidad acumulada`) conmutables con rueda del ratón sobre el botón Sort o desde configuración.
   - Hotbar excluida por defecto al ordenar mochila (`SortHotbarWithBackpack = false`) y `StackPerishables = false` por defecto con botón toggle.
   - Protección híbrida: **Favoritos por ítem** (`WatchedAttributes`, protegen en descargas rápidas y opcionalmente en sort) + **Locked Slots por índice** en mochila/hotbar.
   - *Quick Store* al contenedor abierto y *Store Nearby* con radio por defecto de `6` bloques (máximo `16`), validación obligatoria de permisos vía `ILandClaimAPI`, whitelist por defecto de contenedores estándar (`chest-*`, `crate`, `labeledchest-*`, `stationarybasket-*`, `storagevessel-*`, `trunk-*`) + whitelist/blacklist en servidor y `RemeshContainersPacket` en clientes.

### Fase 2 — Interacción, Refill y Exploración
- `Hotbar Refill` (consumibles, bloques y reemplazo preventivo de herramientas antes de romperse).
- `Auto-Loot` de cadáveres y `Auto-Stash` (mantener pulsado interactuar con barra radial).
- Atajo `Equip Light` (fuente de luz más brillante en offhand/hotbar).
- Resaltado de ítems iguales bajo el cursor, resaltado de ítems en el suelo (`EntityItem`) y `Projectile Tracker` (*waypoints* automáticos).

### Fase 3 — Herramientas Avanzadas y Renderizado Visual
- Calculadora de aleaciones y combustible en fogatas con crisol.
- Indicadores visuales de sexo y preñez en animales domésticos (`generation >= 1`).
- Indicadores de estado en slots (`GuiElementItemSlotGridBase`), partículas de recursos (`Particles Plus`), decal de respawn y menús radiales (`GuiDialogToolMode` + *Quick-Swap*).
