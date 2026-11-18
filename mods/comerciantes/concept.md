# Mod: Traders Overhaul (`tradersoverhaul`) — Concepto

## 1. Visión General
**Traders Overhaul** es un mod para **Vintage Story** cuyo propósito es reescribir y modernizar integralmente el sistema de comercio del juego.

El comercio nativo de Vintage Story se apoya en vendedores estáticos con inventarios rígidos y una ventana de diálogo limitada. Este mod busca transformar esa mecánica en una experiencia económica viva, intuitiva y profunda, acompañada de una interfaz moderna diseñada sobre **System UI**.

---

## 2. Objetivos de Diseño y Jugabilidad
- **Renovar la Experiencia de Intercambio (UI/UX)**:
  - Reemplazar la ventana nativa de comercio por una interfaz clara con categorías, barra de búsqueda, filtros de mercancía y previsualización detallada de cada transacción.
  - Mostrar de forma transparente lo que el jugador entrega, lo que recibe, el balance disponible y la valoración en tiempo real de cada ítem.
- **Economía Viva, Especializada y Anti-Explotación**:
  - Permitir vender prácticamente cualquier ítem a cualquier comerciante, pero con precios condicionados por la especialidad del vendedor, la demanda temporal y la saturación del mercado.
  - Mantener la rotación periódica por días de los ítems requeridos/mejor valorados por cada comerciante.
  - Prevenir el farmeo infinito de engranajes mediante rendimientos decrecientes cuando se vende repetidamente el mismo recurso.
- **Exploración y Planificación de Rutas**:
  - Proveer un menú global de consulta con todos los comerciantes conocidos y sus listados, manteniendo obligatorio desplazarse físicamente hasta el campamento para comprar o vender.
- **Transición Limpia en el Mundo**:
  - Integrarse con los comerciantes existentes en el mundo del jugador sin romper partidas guardadas ni entrar en conflicto con la generación natural de campamentos.

---

## 3. Mecánicas Principales del Sistema Económico

### 3.1. Venta Universal con Valor Intrínseco
- A diferencia del juego base (donde cada comerciante solo acepta una lista cerrada de ~6 a 10 ítems), el jugador puede **vender cualquier ítem comerciable a cualquier comerciante**, sin importar su tipo.
- Todo ítem posee un **valor intrínseco base** (expresado en engranajes oxidados o fracciones de engranaje según su rareza, costo de materiales y dificultad de fabricación).
- Los ítems dañados (herramientas/armaduras con durabilidad reducida) o perecederos con frescura baja ajustan su valoración proporcionalmente a su estado.

### 3.2. Especialización por Tipo de Comerciante (Afinidad)
- Cada tipo de comerciante (*Supervivencia*, *Agricultura*, *Artesano*, *Construcción*, *Ropa*, *Lujos*, *Cazatesoros*, etc.) evalúa los ítems según su afinidad profesional:
  - **Ejemplo**: Un comerciante de **Supervivencia** valorará mejor un hacha, un cuchillo, pieles, arcos o equipo de campamento que un comerciante de **Ropa** o **Materiales de Construcción**.
  - Un ítem vendido fuera de la especialidad del comerciante sigue siendo aceptado, pero con una tarifa reducida respecto a su valor intrínseco.

### 3.3. Demanda Periódica por Días (Ítems Mejor Valorados)
- Se conserva el sistema nativo de rotación por días de Vintage Story para generar la **lista de ítems requeridos / destacados** de cada comerciante.
- Cada ciertos días de juego (ciclo configurable), el comerciante renueva los artículos que busca activamente:
  - Los ítems que coinciden con la lista requerida del día reciben una **bonificación adicional de precio** y aparecen destacados en la UI.
  - Esto incentiva revisar las rotaciones y planificar cuándo y a quién vender ciertos cargamentos.

### 3.4. Penalización Anti-Farmeo (Saturación de Mercado)
- Para evitar la explotación económica (fabricar o recolectar masivamente un único ítem barato para farmear engranajes), cada venta registra volumen acumulado:
  - A medida que se venden grandes cantidades de un mismo ítem (o familia de ítems) en poco tiempo, **la recompensa disminuye progresivamente** hasta alcanzar un piso mínimo configurable.
  - **Propagación regional (por defecto)**: La saturación afecta principalmente al comerciante receptor y **se propaga parcialmente a comerciantes cercanos** dentro de un radio configurable, evitando simplemente caminar al campamento contiguo para esquivar la penalización.
  - La regla de alcance es **configurable por el usuario/servidor** (solo local, propagación parcial cercana o global, además de ajustar radio y porcentaje).
  - La saturación se recupera de forma gradual con el paso de los días de juego, incentivando diversificar las ventas o cambiar de región.

### 3.5. Valoración de Artículos, Lotes Mínimos y Tendencia de Precio
- **Manejo por lote mínimo**: Como los engranajes oxidados son enteros, los ítems cuyo valor unitario es menor a `1` engranaje (o dejan fracción) se tasan y venden por **lote mínimo entero** (ej. `4x = 1 engranaje`), mostrando claramente cuántas unidades forman un lote válido.
- **Visibilidad contextual**: Por defecto, la valoración y tendencia solo se muestran dentro de la **ventana de comercio** y el **menú global de comerciantes**, con una **opción activable** para visualizar también la valoración estimada en el tooltip del **inventario normal**.
- Al inspeccionar un ítem en esos contextos se muestra:
  - **Precio actual exacto** (ej. `10 engranajes`, o `4x → 1 engranaje`).
  - **Indicador de variación (subida / bajada / neutro)** respecto al valor intrínseco o precio anterior (ej. `10 ⚙ ▲ +25%` en verde cuando hay alta demanda/afinidad, o `6 ⚙ ▼ -40%` en rojo/ámbar cuando hay saturación o baja afinidad).
  - **Desglose claro en tooltip**: Valor base, multiplicador por tipo de comerciante, bono por demanda del día y penalización por saturación.

### 3.6. Menú Global de Comerciantes Conocidos (Solo Consulta)
- El jugador dispone de un **directorio global** accesible desde cualquier lugar (atajo de teclado o botón en **System UI**) con la lista de **todos los comerciantes que ya ha descubierto físicamente** en el mundo.
- En este menú puede:
  - Filtrar y buscar por tipo de comerciante, ítem de interés (venta o compra) o cercanía.
  - Consultar los precios, los ítems requeridos del ciclo, la ubicación/distancia y el tiempo estimado para la próxima actualización por días.
- **Modo de actualización (configurable)**:
  - **Por defecto (Última visita / Snapshot)**: Muestra la información registrada la última vez que el jugador visitó físicamente a ese comerciante (indicando *"Visto el Día X"*), manteniendo el incentivo de explorar y confirmar rotaciones en persona.
  - **Opcional (En vivo)**: Permite configurar que los comerciantes ya descubiertos actualicen automáticamente sus listas y precios en el directorio con el paso de los días.
- **Restricción física obligatoria**: El menú global es **estrictamente de solo lectura**. No permite comprar ni vender a distancia; el jugador debe viajar físicamente al puesto del comerciante para realizar cualquier intercambio.

---

## 4. Flujo de Interacción Esperado
1. **Descubrimiento y Consulta**: Al encontrar e interactuar con un comerciante en el mundo, este queda registrado en el menú global de comerciantes conocidos del jugador con los precios vigentes de esa visita.
2. **Planificación**: Antes de salir de viaje, el jugador abre el menú global para comparar qué comerciante ofrece mejor precio por sus ítems (afinidad + ítems requeridos del día - saturación) y trazar su ruta.
3. **Intercambio Presencial**: El jugador viaja hasta el comerciante e interactúa con él.
4. **Nueva UI Modular**: En lugar del diálogo tradicional, se abre la pantalla modular de comercio provista por **System UI**, mostrando pestañas (Compra, Venta, Ítems Requeridos), valoración exacta por unidad o lote mínimo, indicadores de subida/bajada y el desglose de modificadores.
5. **Confirmación**: El jugador selecciona cantidades exactas (o lotes enteros) y confirma la operación con retroalimentación visual inmediata y actualización en vivo de la saturación del mercado (local y cercanos).

---

## 5. Opciones Configurables por el Usuario
- **Actualización del menú global**: *Última visita (por defecto)* vs. *Sincronización en vivo*.
- **Alcance de la penalización anti-farmeo**: *Comerciante + cercanos parcial (por defecto)*, *Solo comerciante local* o *Global*, junto con radio de influencia y tasa de contagio/recuperación.
- **Tooltip de valoración en inventario normal**: *Desactivado (por defecto)* / *Activado* (opción de cliente).
