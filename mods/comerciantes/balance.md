# Mod: Comerciantes / Traders Overhaul — Balance Numérico y Economía

Calibrado a partir de los 9 arquetipos y listas reales de `assets/survival/config/tradelists/trader-*.json` en Vintage Story (`money` base entre `20` y `50` engranajes y ciclo de refresco vanilla de `7.0` días en `EntityTradingHumanoid`).

---

## 1. Referencia de Comerciantes Vanilla (`trader-*.json`)

| Arquetipo | Billetera Base (`money`) | Slots Venta / Lista | Slots Compra / Lista | Enfoque Económico |
| :--- | :---: | :---: | :---: | :--- |
| `agriculture` | `20 ± 4 ⚙` | `10 / 25` | `10 / 25` | Cultivos, semillas, pan, herramientas agrícolas, miel/cera, lana |
| `artisan` | `40 ± 8 ⚙` | `14 / 65` | `14 / 23` | Arcilla, cerámica, vasijas, grasa, cera, carbón, turba, gemas |
| `buildmaterials` | `25 ± 5 ⚙` | `14 / 35` | `14 / 21` | Madera, ladrillos, tejas, cal, herramientas de obra, linternas grandes |
| `clothing` | `35 ± 7 ⚙` | `12 / 38` | `12 / 38` | Hilo/tela de lino, cuero, vestimenta común y noble |
| `commodities` | `30 ± 6 ⚙` | `8 / 26` | `8 / 17` | Minerales, pepitas, azufre, pólvora, cajas, vasijas de botín |
| `furniture` | `25 ± 5 ⚙` | `14 / 37` | `14 / 23` | Vidrio, alfombras, candelabros, cuadros, herrajes/clavos |
| `luxuries` | `50 ± 10 ⚙` | `14 / 42` | `14 / 42` | Oro, plata, gemas, cuadros raros, ropa real, libros, omok |
| `survivalgoods` | `25 ± 5 ⚙` | `14 / 31` | `14 / 22` | Armas, arcos, flechas, mochilas, linternas, raciones, pieles/cuero |
| `treasurehunter` | `25 ± 5 ⚙` | `14 / 44` | `14 / 25` | Falx, picos, palas, bombas de mineral, escudos, reliquias, raciones |

> [!NOTE]
> En vanilla, la diferencia entre precio de venta al jugador y precio de compra al jugador ronda **1.8x – 2.5x** (por ejemplo, `survivalgoods` vende `blade-falx-tinbronze` a `10 ⚙` mientras `treasurehunter` lo compra a `4 ⚙`; `agriculture` vende `bread-spelt-perfect` `8@2 ⚙` y `survivalgoods` lo compra `8@1 ⚙`). Este margen sirve como ancla para los valores base y la regla anti-arbitraje.

---

## 2. Tiers de Valor Intrínseco Base (`ValorBase`)

Todo ítem comerciable recibe un valor intrínseco en engranajes (o fracción para calcular lotes mínimos):

| Tier | Categoría / Ejemplos Reales Vanilla | Rango `ValorBase` | Lote Mínimo Típico (`1 ⚙`) |
| :---: | :--- | :---: | :---: |
| **Tier 0 (Blacklist)** | Tierra cruda, grava, arena común, bloques creativos, agua | `0 ⚙` | No comerciable |
| **Tier 1 (Granel)** | Arcilla azul (`64@1`), arcilla refractaria (`32@1`), plumas / cal / soja (`16@1`), carbón / turba (`12@1`), grasa / pan / carne cocida / vidrio plano (`8@1`) | `0.015 – 0.125 ⚙` | `8x – 64x → 1 ⚙` |
| **Tier 2 (Procesado ligero)** | Cordel / cera / cuencos (`6@1`), chatarra metálica (`5@1`), calabazas / vidrio teñido (`3–4@1`), cuero / tela de lino / velas / clavos de cobre (`2@1`) | `0.15 – 0.50 ⚙` | `2x – 6x → 1 ⚙` |
| **Tier 3 (Manufactura / Metal base)** | Lingote de cobre, saco de lino, caja, colmena, kit de costura (`1 ⚙`), lingote de estaño (`2 ⚙`), acero / plata (`3 ⚙`), herramientas de cobre (`2–3 ⚙`) | `1.0 – 3.0 ⚙` | `1x → 1–3 ⚙` |
| **Tier 4 (Bronce y Hierro)** | Herramientas y armas `tinbronze` (`4–5 ⚙`), `bismuthbronze` (`5–6 ⚙`), `blackbronze` (`6–7 ⚙`), hierro (`7–9 ⚙`), arcos largos/recurvos (`5–6 ⚙`), linternas (`5–8 ⚙`) | `4.0 – 9.0 ⚙` | `1x → 4–9 ⚙` |
| **Tier 5 (Lujo y Reliquias)** | Herramientas/armas de acero (`10–14 ⚙`), cofres (`10 ⚙`), escudos (`10 ⚙`), vestimenta noble/real (`7–16 ⚙`), gemas en bruto (`8–16 ⚙`), pinturas (`9–20 ⚙`) | `10.0 – 20.0+ ⚙` | `1x → 10–20+ ⚙` |

### Fallback Heurístico para Ítems sin Entrada Explícita (Mods y Derivados)
Si un ítem no está en `item_values.json` ni en `TradeProps` vanilla:
1. Calcular la suma de `ValorBase` de sus ingredientes de receta más económica × factor de transformación (`0.85` si solo ensambla en grilla para evitar generar valor infinito crafteando; `1.05` si requiere fundición/forja/horneado).
2. Si no tiene receta (ej. drops raros de mods), asignar por categoría/tag del `CollectibleObject` dentro de Tier 1 o Tier 2.

---

## 3. Matriz de Afinidad por Arquetipo ($M_{\text{afinidad}}$)

Niveles de multiplicador según relación entre el tipo de comerciante y la familia del ítem:
- **Especialidad Principal (`1.35x`)**: El ítem pertenece al núcleo comercial del vendedor.
- **Afinidad Secundaria (`1.15x`)**: Insumos o bienes complementarios a su oficio.
- **Neutral (`1.00x`)**: Uso general.
- **Baja Afinidad (`0.70x`)**: Fuera de su rubro principal.
- **Desinterés (`0.50x`)**: Completamente ajeno a su especialidad.

| Categoría de Ítem | `survivalgoods` | `agriculture` | `artisan` | `buildmaterials` | `clothing` | `commodities` | `furniture` | `luxuries` | `treasurehunter` |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Hachas, cuchillos, arcos, mochilas** | **1.35x** | 1.00x | 0.70x | 1.15x | 0.50x | 1.00x | 0.50x | 0.50x | **1.35x** |
| **Armas (falx, escudos) y picos** | 1.15x | 0.50x | 0.70x | 1.00x | 0.50x | 1.00x | 0.50x | 0.70x | **1.35x** |
| **Sierras, martillos, hachas de tala** | 1.15x | 0.70x | 1.15x | **1.35x** | 0.50x | 1.00x | 1.15x | 0.50x | 0.70x |
| **Azadas, guadañas, semillas, cultivos** | 1.00x | **1.35x** | 0.70x | 0.50x | 0.70x | 1.00x | 0.50x | 0.50x | 0.70x |
| **Comida cocida / pan / raciones** | **1.35x** | **1.35x** | 0.70x | 0.70x | 0.70x | 1.00x | 0.70x | 0.70x | 1.15x |
| **Pieles, cuero, cordel, telas básicas** | **1.35x** | 1.00x | 1.00x | 0.70x | **1.35x** | 1.00x | 1.00x | 0.70x | 1.00x |
| **Vestimenta común y de trabajo** | 1.15x | 1.00x | 0.70x | 1.00x | **1.35x** | 0.70x | 0.70x | 1.00x | 1.00x |
| **Vestimenta noble / real y joyas** | 0.50x | 0.50x | 0.70x | 0.50x | **1.35x** | 0.70x | 1.00x | **1.35x** | 1.00x |
| **Arcilla, cerámica, grasa, cera, carbón** | 1.00x | 1.00x | **1.35x** | 1.15x | 0.70x | 1.15x | 1.00x | 0.50x | 0.70x |
| **Piedra, ladrillos, madera, cal, clavos** | 0.70x | 0.70x | 1.00x | **1.35x** | 0.50x | 1.00x | 1.15x | 0.50x | 0.50x |
| **Vidrio, lámparas, alfombras, muebles** | 1.00x | 0.50x | 1.15x | 1.15x | 0.70x | 0.70x | **1.35x** | 1.15x | 0.70x |
| **Lingotes base (cobre, estaño, bronce, hierro)** | 1.15x | 0.70x | 1.15x | 1.15x | 0.70x | **1.35x** | 1.00x | 0.70x | 1.15x |
| **Oro, plata, gemas, cuadros, reliquias** | 0.50x | 0.50x | 1.15x | 0.50x | 0.70x | 1.00x | 1.15x | **1.35x** | **1.35x** |

---

## 4. Demanda Periódica por Días ($M_{\text{demanda}}$)

- **Ciclo de actualización**: Conserva `doubleRefreshIntervalDays = 7.0` (o valor configurado del mundo) de `EntityTradingHumanoid`.
- **Ítems requeridos activos**: Los `BuyingSlots` generados por `RefreshBuyingSellingInventory` para el ciclo actual actúan como la lista de **Ítems Requeridos / Mejor Valorados** de ese comerciante.
- **Multiplicador**:
  - Ítem en lista requerida activa: **`M_demanda = 1.40x`** (`+40%`).
  - Resto de ítems comerciables: **`M_demanda = 1.00x`**.

---

## 5. Curva Anti-Farmeo y Propagación ($M_{\text{saturación}}$)

Para equilibrar por valor económico real y no penalizar de más materiales de Tier 1 frente a herramientas de Tier 4, el volumen acumulado $S$ se mide en **Engranajes Base Equivalentes (GEV)** vendidos de ese ítem o familia (`SaturationGroup`):
$$\Delta S = \text{ValorBase} \times \text{CantidadVendida}$$

### Fórmula de Decaimiento
$$M_{\text{saturación}}(S) = \max\left(M_{\min},\; \frac{1}{1 + \left(\frac{S}{S_{50}}\right)^{p}}\right)$$

Parámetros por defecto recomendados:
- **$S_{50} = 15.0$ GEV** (tras vender ~`15 ⚙` de valor base en el mismo ítem/familia, el multiplicador cae al 50%).
- **$p = 1.6$** (pendiente progresiva, más agresiva).
- **$M_{\min} = 0.10$** (piso del 10%; nunca paga menos de la décima parte, salvo si el lote queda por debajo de 1 engranaje).
- **Evaluación marginal**: En ventas de varias unidades/lotes simultáneos, cada unidad o lote incremental suma su $\Delta S$ antes de evaluar la siguiente, evitando que vender 64 unidades juntas pague todo al 100%.

| Volumen Acumulado $S$ (GEV) | $M_{\text{saturación}}$ | Efecto práctico (ej. hacha de `4 ⚙`) |
| :---: | :---: | :--- |
| `0` (Sin ventas previas) | **100%** (`1.00x`) | Primera venta sin penalización |
| `10 GEV` (~2–3 hachas) | **76%** (`0.76x`) | Desgaste moderado |
| `25 GEV` (~6 hachas) | **50%** (`0.50x`) | Mercado medio saturado |
| `50 GEV` (~12–13 hachas) | **13%** (`0.13x`) | Farmeo fuertemente desincentivado |
| `80+ GEV` (~20+ hachas) | **10%** (`0.10x`, piso) | Piso mínimo alcanzado |

### Propagación a Comerciantes Cercanos (`NearbyPartial` por defecto)
- **Radio**: `1500` bloques (`SaturationSpilloverRadius`).
- **Factor máximo de contagio**: `0.35` (`SaturationSpilloverFactor = 35%`), atenuado linealmente por distancia:
  $$\Delta S_{\text{vecino}} = \Delta S \times 0.35 \times \max\left(0,\; 1 - \frac{\text{distancia}}{1500}\right)$$
- **Recuperación diaria (`RecoveryPerDay`)**: **`8.0` GEV/día** por comerciante (recupera `25 GEV` en ~`3.1` días de juego).

---

## 6. Reglas de Protección y Casos Borde

1. **Modificador por estado ($M_{\text{estado}}$)**:
   - **Durabilidad**: $\text{Restante} / \text{Máxima}$. Ítems por debajo del **80%** de durabilidad se rechazan (o aplican valor `0` para evitar vender herramientas muy desgastadas).
   - **Perecederos**: Escala linealmente según frescura restante; comida podrida (`rot`) = Tier 0 (`0 ⚙`).
2. **Regla Anti-Arbitraje**:
   - Si el comerciante tiene actualmente ese mismo ítem en sus `SellingSlots` a precio $P_{\text{venta}}$, su precio máximo de compra queda acotado a:
     $$P_{\text{compra\_max}} = \lfloor P_{\text{venta}} \times 0.85 \rfloor$$
3. **Billetera Finita del Comerciante (`MoneySlot`)**:
   - Se respeta el fondo de engranajes del comerciante (`20–50 ⚙` por ciclo según `TradeProps.Money`), incrementándose cuando el jugador **compra** mercancía al comerciante y recargándose en el refresco periódico de `7.0` días.
4. **Lote Mínimo Entero Estandarizado**:
   - Si $\text{PrecioUnitario} < 1$, el tamaño de lote requerido se estandariza a **múltiplos de 4** desde **`4`** hasta **`min(64, MaxStackSize)`** (`4, 8, 12, 16, ..., 64`, o tope `16` en ítems limitados a stack de 16), entregando `floor(Lote * PrecioUnitario)` engranajes (mínimo `1`). Si por bajo valor o saturación acumulada el lote requerido supera `min(64, MaxStackSize)`, la venta del ítem queda bloqueada hasta que se recupere la saturación.
   - Si $\text{PrecioUnitario} \ge 1$, se redondea al entero más cercano (`Math.Round(..., AwayFromZero)`, mínimo `1 ⚙` salvo que tras saturación baje de `1.0`, pasando automáticamente a modo lote mínimo estandarizado).

---

## 7. Ejemplos Completos de Valoración en Juego

Supongamos un **`axe-felling-tinbronze`** con **`ValorBase = 4 ⚙`** (100% durabilidad):
- **Caso 1 — Comerciante `survivalgoods` (Sin demanda del día, sin saturación)**:
  - $4 \times 1.35 \times 1.00 \times 1.00 = 5.4 \rightarrow$ **`5 ⚙`** (`▲ +25%`).
- **Caso 2 — Comerciante `survivalgoods` (En lista de Ítems Requeridos del Día, sin saturación)**:
  - $4 \times 1.35 \times 1.40 \times 1.00 = 7.56 \rightarrow$ **`8 ⚙`** (`▲ +100%`).
- **Caso 3 — Comerciante `survivalgoods` (Saturado con $S = 25$ GEV, sin demanda del día)**:
  - $4 	imes 1.35 	imes 1.00 	imes 0.33 = 1.78 \rightarrow$ **`2 ⚙`** (`▼ -25%`).
- **Caso 4 — Comerciante `clothing` (Baja afinidad, sin saturación)**:
  - $4 \times 0.50 \times 1.00 \times 1.00 = 2.0 \rightarrow$ **`2 ⚙`** (`▼ -50%`).
- **Caso 5 — Ítem barato: `feather` (`ValorBase = 0.0625 ⚙`, `16x = 1 ⚙`)**:
  - En `commodities` sin saturación: `16x → 1 ⚙`; al acumular ventas y subir la saturación pasa por múltiplos de 4 (`20x`, `24x`, ..., `64x → 1 ⚙`), y si supera `64x` queda bloqueado.
