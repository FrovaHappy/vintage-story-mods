# Mod: Fauna Salvaje — Definiciones Técnicas

## 1. Enfoque Técnico: Code Mod Dinámico vs. Parches JSON
Aunque Vintage Story permite alterar entidades mediante parches JSON sobre `entities/land/*` (y `water/*` / `air/*`), los parches estáticos por índice en el arreglo `behaviors` son frágiles ante actualizaciones del juego o mods que insertan comportamientos y además no cubren automáticamente entidades de mods de terceros.

Por ello, se implementará como un **Code Mod en C#** con lógica dinámica ejecutada en el servidor (`ICoreServerAPI`).

---

## 2. Arquitectura en Dos Capas

### 2.1. Capa 1: Ajuste de Definiciones en `AssetsFinalize` (`ICoreServerAPI`)
Al finalizar la carga de assets del mundo:
1. Recorrer la colección `api.World.EntityTypes`.
2. **Filtrado de fauna salvaje**:
   - Identificar entidades animales por código/clase/tags/comportamientos (`EntityAgent`, presencia de `health` y `harvestable`).
   - Excluir explícitamente jugadores, comerciantes (`EntityTrader`), NPCs, entidades inanimadas y criaturas temporales (*drifters*, *locusts*, *shivers*, *bowtorn*), además de respetar la `blacklist` configurada.
3. **Escalado de vida (`EntityBehaviorHealth`)**:
   - Localizar en `Server.Behaviors` el comportamiento con `"code": "health"`.
   - Leer y multiplicar `maxhealth` (y `currenthealth` si está definido) según la categoría de la entidad y el multiplicador configurado.
4. **Escalado de botín (`EntityBehaviorHarvestable`)**:
   - Localizar en `Server.Behaviors` el comportamiento con `"code": "harvestable"`.
   - Iterar sobre su arreglo de `drops` e identificar el código de cada ítem (`bushmeat-raw`, `redmeat-raw`, `poultry-raw`, `hide-raw-*`, `fat`, huesos, plumas, etc.) para multiplicar sus propiedades `avg` y `var` manteniendo compatibilidad con `GuiDialogCreatureContents`.

### 2.2. Capa 2: Ajuste en Tiempo de Ejecución (`OnEntitySpawn` / Comportamientos / Harmony)
- **Diferenciación Salvaje vs. Domesticado**:
  - En `sapi.Event.OnEntitySpawn`, comprobar si el animal pertenece a una generación domesticada (por ejemplo, evaluando `entity.WatchedAttributes.GetInt("generation") > 0`) para decidir si conserva la vida estándar o mantiene el escalado salvaje según la configuración del mod.
- **Control fino en `EntityBehaviorHarvestable`**:
  - Evaluar en `VSEssentials.dll` cómo interactúan el peso del animal y las penalizaciones nativas (caída/aplastamiento a la mitad, muerte no provocada por jugador al 40%) para asegurar que el escalado no rompa esas mecánicas o para exponer opciones de ajuste si se desea.

---

## 3. Configuración y Sincronización
- **Persistencia local (`ModConfig`)**: Archivo JSON en `ModConfig/` con multiplicadores de vida por categoría, multiplicadores de botín por tipo de recurso, `whitelist`/`blacklist` de códigos de entidad y bandera de domesticados.
- **Compatibilidad con ConfigLib / ConfigKit**: Exponer los parámetros en menú (mediante `ConfigLibModSystem` / `ConfigKitModSystem`) para edición cómoda.
- **Reglas de Servidor / `WorldConfiguration`**: Asegurar que los valores autoritativos se apliquen desde el servidor para mantener consistencia en multijugador (sincronizando la vida máxima esperada al cliente mediante los atributos propios de `EntityBehaviorHealth`).

---

## 4. Referencias de Inspección y Próximos Pasos Técnicos
1. Inspeccionar en los assets base (`assets/survival/entities/land/`) las definiciones JSON de lobos, osos, ciervos, jabalíes y liebres para construir una tabla de referencia de `maxhealth` y `drops`.
2. Descompilar `EntityBehaviorHealth` y `EntityBehaviorHarvestable` en `VSEssentials.dll` (con ILSpy / dotPeek) para verificar exactamente cómo se leen `Properties` (`JsonObject`) en `Initialize(...)` y cuándo conviene modificar los `EntityProperties` antes o durante `AssetsFinalize`.
3. Definir las reglas heurísticas o explícitas para clasificar automáticamente entidades por categoría (presa pequeña, mediana/grande, depredador) incluso en mods externos.
