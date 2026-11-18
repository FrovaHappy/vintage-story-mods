# Mod: Comerciantes / Traders Overhaul — Definiciones Técnicas

## 1. Arquitectura Vanilla Verificada (`VSSurvivalMod.dll` y Assets)
Tras inspeccionar `VSSurvivalMod.dll` y `assets/survival/` en la instalación local (`.NET 10.0` / `net10.0`), la arquitectura real de comerciantes en Vintage Story se compone de:

### 1.1. Jerarquía de Entidades y Diálogo
- **Clases en `Vintagestory.GameContent` (`VSSurvivalMod.dll`)**:
  - `EntityTrader` hereda de **`EntityTradingHumanoid`** (base: `EntityDressedHumanoid`).
  - `EntityTradingHumanoid` mantiene `InventoryTrader Inventory`, `TradeProperties TradeProps`, `double doubleRefreshIntervalDays` (por defecto `7.0` días en el constructor) y `NextRefreshTotalDays()`.
- **Flujo de apertura del diálogo**:
  - Los archivos `entities/humanoid/trader-male.json` y `trader-female.json` usan `EntityBehaviorConversable` con `config/dialogue/trader.json` (y `treasurehunter.json`).
  - Al elegir comerciar en el diálogo, `EntityTradingHumanoid.Dialog_DialogTriggers(...)` invoca **`TryOpenTradeDialog(EntityAgent forEntity)`**, que abre `GuiDialogTrader`.
  - **Punto de intercepción recomendado**: En lugar de bloquear `OnInteract` (lo que rompería el diálogo de presentación y `triggeredNameReveal`), aplicar un `[HarmonyPrefix]` sobre `EntityTradingHumanoid.TryOpenTradeDialog` (o sobrescribirlo en `EntityCustomTrader`), manteniendo intacta la conversación previa.

### 1.2. Arquetipos, Inventario y Refresco (`TradeProperties` / `InventoryTrader`)
- **9 arquetipos oficiales** definidos en `variantgroups` (`type`) y `assets/survival/config/tradelists/trader-{type}.json`:
  - `agriculture`, `artisan`, `buildmaterials`, `clothing`, `commodities`, `furniture`, `luxuries`, `survivalgoods`, `treasurehunter`.
- **Estructuras clave**:
  - `TradeProperties`: `NatFloat Money` (20 a 50 engranajes base según arquetipo), `TradeList Buying` (`MaxItems` 8–14), `TradeList Selling` (`MaxItems` 8–14).
  - `TradeItem` / `ResolvedTradeItem`: incluye `Price`, `Stock`, `RestockOpts` (`Quantity`, `HourDelay`) y `SupplyDemandOpts` (`PriceChangePerPurchase`, `PriceChangePerDay`).
  - `InventoryTrader`: expone `SellingSlots`, `BuyingSlots`, `MoneySlot`, `IsTraderInterestedIn(ItemStack)`, `CurrencyValuePerItem(ItemSlot)`, `GetTotalCost()`, `GetTotalGain()`, `TryBuySell(IPlayer)` y `GiveToTrader(int)` / `DeductFromTrader(int)`.
  - `RefreshBuyingSellingInventory(float refreshChance)` renueva las listas activas de compra/venta cada ciclo.

### 1.3. Estrategia de Integración del Mod
- **Harmony Patching sobre `EntityTradingHumanoid` e `InventoryTrader`**:
  - Interceptar `TryOpenTradeDialog` para abrir `GuiDialogCustomTrader` (**System UI**) y disparar la sincronización de red servidor → cliente.
  - Extender `IsTraderInterestedIn` y el flujo de venta en `InventoryTrader` / canal propio para aceptar cualquier ítem comerciable manteniendo `MoneySlot` del comerciante y la rotación de `BuyingSlots` como lista de ítems requeridos del ciclo.

---

## 2. Motor Económico Dinámico (Lado Servidor Autoritativo)

### 2.1. Valor Intrínseco de Ítems (`ItemValuationRegistry`)
Para permitir la **venta universal de cualquier ítem** a cualquier comerciante sin depender únicamente de las listas cerradas de `TradeProps`:
- **Resolución por capas**:
  1. **Tabla explícita (`assets/<modid>/config/item_values.json`)**: Precios base definidos manualmente por código de ítem/bloque (ej. herramientas, lingotes, gemas, comida elaborada) y una lista negra (`blacklist`) para ítems no comerciables (tierra cruda, bloques creativos, etc.).
  2. **Inferencia desde `TradeProps` nativos**: Extraer los valores promedio de compra/venta que los distintos arquetipos nativos asignan en los JSON del juego base para calibrar automáticamente el valor intrínseco.
  3. **Fallback heurístico por receta/categoría**: Para ítems de otros mods sin entrada explícita, estimar un valor base a partir de sus ingredientes de crafteo o atributos/tags del `CollectibleObject`.
- **Modificador por estado físico**:
  - **Durabilidad**: Si el ítem tiene durabilidad (`GetRemainingDurability` / `GetMaxDurability`), escalar el valor proporcionalmente (con umbral mínimo configurable para evitar vender herramientas casi rotas por valor completo).
  - **Perecederos (`TransitionableProperties`)**: Ajustar el valor según la frescura restante (`TransitionState`).

### 2.2. Afinidad por Tipo de Comerciante (`TraderAffinityMatrix`)
- Cada arquetipo de comerciante posee una matriz de multiplicadores por categoría/tag de ítem (`assets/<modid>/config/trader_affinities.json`):
  - **Especialidad directa** (ej. `survival` comprando hachas, cuchillos, pieles, arcos, linternas): multiplicador alto (ej. `1.25x – 1.50x`).
  - **Neutral**: `1.00x`.
  - **Fuera de especialidad** (ej. `clothing` comprando un pico o mineral en bruto): multiplicador reducido (ej. `0.50x – 0.75x`).

### 2.3. Rotación por Días e Ítems Requeridos (`DailyDemandSystem`)
- **Reutilización del sistema nativo de refresco**:
  - `EntityTrader` / `InventoryTrader` ya controla la renovación periódica mediante los días transcurridos del mundo (`sapi.World.Calendar.TotalDays` y `RefreshDays` / `NextRefreshTotalDays`).
  - En cada ciclo de actualización nativa del comerciante, se lee o regenera su lista de **ítems requeridos / mejor valorados** (`WantedItems` derivados de las tablas de compra de `TradeProps` de ese tipo de comerciante).
  - Cualquier ítem presente en esa lista activa recibe el **Bono de Demanda del Ciclo** (ej. `+30%` a `+60%`) y se marca como *Destacado / Alta Demanda*.

### 2.4. Penalización Anti-Farmeo (`TraderSaturationLedger`)
Para evitar la explotación repetitiva de un único recurso:
- **Registro por comerciante e ítem (o familia de ítems)**:
  - Cada entidad comerciante mantiene un diccionario persistente en sus atributos/datos del servidor (`WatchedAttributes` o `ISaveGame` indexado por `EntityId`) con el volumen vendido acumulado `S` para cada código o grupo económico (`SaturationGroup`).
- **Alcance y propagación regional (configurable)**:
  - **Modo por defecto (`NearbyPartial`)**: Además de incrementar `S` al 100% en el comerciante receptor, propaga una fracción configurable (`SpilloverFactor`, ej. `0.35`, opcionalmente atenuada por distancia) hacia otros comerciantes conocidos/cargados o registrados en el índice espacial dentro de `SpilloverRadiusBlocks` (ej. `1500` bloques).
  - Para alcanzar también comerciantes en chunks descargados sin forzar carga de chunks, el índice espacial y los contadores de saturación pueden centralizarse en un `WorldSaturationRegistry` persistido en `SaveGame` y consultado por `EntityId`.
  - **Modos alternativos configurables**: `LocalOnly` (solo el comerciante actual) y `Global` (mercado compartido).
- **Curva de rendimientos decrecientes**:
  - Factor de saturación `M_sat(S)` decreciente conforme aumenta `S` (por ejemplo, decaimiento por tramos o curva suave `max(MinFloor, 1 / (1 + k * S))`, con un piso configurable como `0.15` o `0.20`).
  - Al confirmar una venta de `N` unidades, la saturación se calcula de forma **progresiva por unidad o tramo** (de modo que vender 64 unidades de golpe no burle el escalado pagando las 64 al precio inicial).
- **Recuperación temporal**:
  - Al avanzar los días del mundo (evaluado al interactuar o en cada ciclo de `RefreshDays`), `S` decae gradualmente (`S = max(0, S - RecoveryPerDay * ElapsedDays)`), restaurando la demanda con el tiempo.

### 2.5. Fórmula de Valoración, Lote Mínimo y Tendencia
$$\text{PrecioUnitario} = \text{ValorBase} \times M_{\text{afinidad}} \times M_{\text{demanda}} \times M_{\text{saturación}} \times M_{\text{estado}}$$
- **Manejo por lote mínimo entero (`MinBatchQuote`)**:
  - Los *Rusty Gears* en Vintage Story son ítems enteros (`1` unidad mínima) y no se utilizan saldos fraccionarios ocultos.
  - Si `PrecioUnitario < 1`, se calcula el tamaño mínimo de lote `LotSize = ceil(1 / PrecioUnitario)` para obtener `1` engranaje (o el menor múltiplo entero limpio `LotSize` → `LotGears`).
  - La UI y la validación de servidor trabajan por **lotes enteros**: solo se habilita la venta cuando el jugador entrega una cantidad válida que produzca al menos `1` engranaje entero, indicando explícitamente en pantalla cuántas unidades componen el lote (ej. `4x → 1 ⚙`) y cuántas sobran/faltan.
- **Cálculo de Tendencia (`PriceTrend`)**:
  - Comparación contra el valor de referencia (`ValorBase`, o precio del ciclo anterior):
    - `DeltaPct = ((PrecioActual - ValorReferencia) / ValorReferencia) * 100`
    - Estado visual: `Up` (subió, verde), `Down` (bajó, rojo/ámbar), `Neutral` (estable).
  - El servidor calcula y envía el desglose completo (`Base`, `AffinityMod`, `DemandMod`, `SaturationMod`, `ConditionMod`, `FinalUnitPrice`, `MinLotSize`, `LotGearPayout`, `DeltaPct`) para evitar discrepancias en cliente.

---

## 3. Intercepción de la Interacción (`TryOpenTradeDialog`)
Sobre `EntityTradingHumanoid` (clase base de `EntityTrader`) se intercepta mediante Harmony:
```csharp
public void TryOpenTradeDialog(EntityAgent forEntity)
```
- Se anula la apertura del diálogo nativo (`GuiDialogTrader`), preservando el sistema de conversación (`EntityBehaviorConversable` y `config/dialogue/trader.json`).
- **División por lado de ejecución (`EnumAppSide`)**:
  - **Lado Servidor (`ICoreServerAPI`)**:
    1. Actualiza la recuperación de saturación pendiente y verifica si corresponde rotación de ciclo (`TotalDays` / `NextRefreshTotalDays()`).
    2. Registra/actualiza al comerciante en el **Directorio de Comerciantes Conocidos** del jugador que interactuó.
    3. Envía al cliente el estado completo del comerciante (`TraderStateSyncPacket`).
  - **Lado Cliente (`ICoreClientAPI`)**: Instancia y abre `GuiDialogCustomTrader` (`TryOpen()`), construida con los componentes de [`mods/system-ui`](../system-ui/technical.md).

---

## 4. Arquitectura de Red Cliente-Servidor
Vintage Story separa estrictamente la presentación gráfica (cliente) de la simulación y validación autoritativa (servidor):
- **Canal dedicado**: Registro de un canal mediante `api.Network.RegisterChannel("comerciantes")` (`INetworkChannel`).
- **Paquetes principales**:
  1. `TraderStateSyncPacket` (Servidor → Cliente): Stock de venta, lista de ítems requeridos del día, matriz de afinidad del arquetipo, libro de saturación actual del comerciante y temporizador al próximo refresco.
  2. `PriceQuoteRequestPacket` / `PriceQuoteResponsePacket` (opcional o calculable con datos sincronizados y revalidado en servidor): Previsualización del lote seleccionado con desglose de tendencia.
  3. `TradeRequestPacket` (Cliente → Servidor): ID de entidad del comerciante, tipo de operación (compra/venta), slots e ítems ofrecidos/solicitados y cantidades.
  4. `TradeResponsePacket` (Servidor → Cliente): Resultado atómico tras validar **distancia física máxima al comerciante**, inventario, engranajes, saturación progresiva y durabilidad; devuelve inventario actualizado y nueva saturación.
  5. `KnownTradersSyncPacket` (Servidor → Cliente): Lista de comerciantes conocidos por el jugador para alimentar el menú global.

---

## 5. Directorio Global de Comerciantes Conocidos (Solo Consulta)
- **Registro por descubrimiento físico**:
  - Un comerciante pasa a ser *conocido* para un jugador (`PlayerUID`) cuando este interactúa con él (o opcionalmente al entrar en un radio cercano configurable).
  - La persistencia se almacena por jugador en el servidor (`sapi.WorldManager.SaveGame.StoreData("comerciantes_known_traders", ...)`), guardando: `EntityId`, nombre, arquetipo, última posición conocida (`BlockPos`), día de última visita (`LastSeenTotalDays`), lista de ítems requeridos/ofertas conocidas y estado de saturación.
- **Modelo de actualización de información (`DirectorySyncMode`)**:
  - **`LastVisitedSnapshot` (por defecto)**: Muestra los precios e ítems requeridos registrados en la última visita física junto con la marca de tiempo (*"Visto el Día X — próxima rotación estimada en Y días"*). No consulta estado remoto hasta que el jugador vuelve a visitar al comerciante.
  - **`LiveKnownTraders` (opcional por configuración)**: El servidor sincroniza automáticamente con el cliente los cambios diarios de inventario/demanda y saturación de los comerciantes ya descubiertos.
- **Garantía de presencia física**:
  - El menú global (`GuiDialogTraderDirectory`) es exclusivamente una vista de solo lectura en cliente; no expone botones de compra/venta y el servidor rechaza cualquier `TradeRequestPacket` si el jugador no se encuentra dentro del rango de interacción física del `EntityTrader`.

---

## 6. Integración con `mods/system-ui`
- Consumirá los layouts, grillas filtrables, selectores de cantidad, pestañas y diálogos modales expuestos por **System UI**:
  - **Ventana de Comercio (`GuiDialogCustomTrader`)**: Pestañas (Compra / Venta Universal / Ítems Requeridos del Día), previsualización de lote mínimo en tiempo real, insignia de tendencia (`▲ +X%` / `▼ -X%`) y barra/indicador de saturación por ítem.
  - **Tooltip de Valoración**:
    - **Activo siempre dentro de comercio y directorio global**: Muestra precio en engranajes (o tamaño de lote mínimo), flecha de tendencia y desglose de modificadores.
    - **Inventario normal (opcional, desactivado por defecto)**: Mediante un hook en `CollectibleObject.GetHeldItemInfo` (o `HarmonyPostfix`), si el cliente activa `ShowValuationInInventoryTooltip`, muestra el valor intrínseco base (o mejor referencia conocida en el directorio) sin interferir con el flujo vanilla.
  - **Menú Global de Comerciantes (`GuiDialogTraderDirectory`)**: Panel buscable/ordenable por distancia, tipo de comerciante o mejor comprador para un ítem específico, con enlace/botón para marcar o copiar coordenadas de waypoint en el mapa sin permitir transacciones remotas.

---

## 7. Configuración y Personalización (`ModConfig` / `ConfigLib` / `WorldConfiguration`)
Separación explícita entre reglas autoritativas de mundo/servidor y preferencias visuales de cliente:

| Parámetro | Lado | Valor por Defecto | Opciones |
| :--- | :--- | :--- | :--- |
| `DirectorySyncMode` | Servidor / Mundo | `LastVisitedSnapshot` | `LastVisitedSnapshot`, `LiveKnownTraders` |
| `SaturationScope` | Servidor / Mundo | `NearbyPartial` | `LocalOnly`, `NearbyPartial`, `Global` |
| `SaturationSpilloverRadius` | Servidor / Mundo | `1500` bloques | Numérico configurable |
| `SaturationSpilloverFactor` | Servidor / Mundo | `0.35` (35%) | `0.0 – 1.0` |
| `SaturationMinFloor` | Servidor / Mundo | `0.20` (20%) | `0.05 – 1.0` |
| `SaturationRecoveryPerDay` | Servidor / Mundo | Configurable | Unidades/día |
| `ShowValuationInInventoryTooltip` | Cliente | `false` | `true` / `false` |

- Persistencia estándar vía `ModConfig/comerciantes.json`, con integración opcional en **ConfigLib / ConfigKit** y respaldo en `WorldConfiguration` para partidas multijugador.

---

## 8. Referencias de Balance y Próximos Pasos
- **Balance numérico completo**: Ver [`balance.md`](balance.md) para las tablas de afinidad de los 9 arquetipos, la curva exacta de saturación anti-farmeo, los tiers de valor intrínseco calibrados contra `tradelists/trader-*.json` y la regla anti-arbitraje.
- **Ensamblados de referencia**: `VSSurvivalMod.dll` (`Vintagestory.GameContent`) y `VintagestoryAPI.dll` (runtime objetivo `net10.0`).

### Próximos Pasos Técnicos
1. Estructurar el proyecto C# del mod (`net10.0`) con referencia a `VintagestoryAPI.dll`, `VSEssentials.dll`, `VSSurvivalMod.dll` y `0Harmony.dll`.
2. Generar el archivo inicial `item_values.json` y `trader_affinities.json` a partir de las reglas definidas en [`balance.md`](balance.md).
3. Implementar el núcleo de servidor (`ItemValuationRegistry`, `WorldSaturationRegistry`, `KnownTradersRegistry`) y los paquetes de red.
4. Conectar el parche de `TryOpenTradeDialog` con la UI en [`mods/system-ui`](../system-ui/technical.md).
