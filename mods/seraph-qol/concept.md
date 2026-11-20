# Mod: Seraph QoL (`seraphqol`) — Concepto

## 1. Visión General
**Seraph QoL** (`seraphqol`) es el mod unificado de **Experiencia de Usuario (UX)**, **Calidad de Vida (QoL)** y **Rebalanceo de Supervivencia / Fauna** para **Vintage Story** dentro de este monorepo (consolidando los antiguos módulos conceptuales *UX Utilities* y *Fauna QoL*).

El proyecto centraliza en un único mod modular un conjunto de mejoras prácticas de inventario, interacción con contenedores y mesas de trabajo, indicadores visuales en slots y entidades, utilidades de exploración y el reequilibrio de caza y carneado de fauna silvestre. Cada módulo funciona de forma independiente y configurable, manteniendo coherencia visual mediante **Seraph UI** (`seraphui`).

> [!IMPORTANT]
> **Compromiso de Créditos y Licencias:** Cada mecánica inspirada o estudiada de proyectos existentes mantendrá de forma explícita y visible el crédito a sus autores originales (en `modinfo.json`, documentación, repositorio y pantallas de configuración/acerca de), respetando estrictamente la licencia de cada mod de referencia (en particular, implementaciones limpias desde cero en C# cuando la referencia use licencias copyleft como AGPL-3).

---

## 2. Objetivos Principales
- **Modularidad Granular**: Cada bloque funcional (UX de inventario, interacción vanilla, indicadores HUD/slot, exploración y rebalanceo de fauna) opera como un submódulo activable o desactivable de forma independiente.
- **Configuración Unificada y Dual (Cliente vs. Servidor)**:
  - Edición en vivo desde el menú de pausa mediante compatibilidad con **ConfigLib** / **ConfigKit**.
  - Persistencia autónoma en `ModConfig/seraphqol.json` (cliente) y `ModConfig/seraphqol-server.json` (servidor) sin requerir ConfigLib como dependencia obligatoria.
- **Consistencia Visual y Bajo Impacto**: Integrarse con la estética nativa de Vintage Story apoyándose en **Seraph UI** (`seraphui`), sin añadir fricción ni desbalancear la progresión vanilla salvo en las reglas explícitamente configuradas.

---

## 3. Módulos Unificados de Seraph QoL

### 3.1. Gestión Avanzada de Inventario y Contenedores
- **Referencias y Créditos Base**:
  - **Storage Tweaks** (Vintage Story) — *Trojan96*: ordenamiento/compactado server-authoritative, favoritos por jugador, force-stack de perecederos, store-nearby.
  - **Inventory Profiles Next** (Minecraft) — *Mirinimi* (`AGPL-3`, solo referencia conceptual/arquitectónica): reglas de ordenamiento declarativas, ranuras bloqueadas (*Locked Slots*), perfiles de equipamiento, scroll rápido y resaltado de ítems iguales.
- **Funcionalidades**:
  - Botones integrados en el inventario del jugador y en contenedores (cofres, barriles, vasijas, cajones, etc.) para **ordenar y compactar** stacks, además de atajos de teclado dedicados.
  - **Descarga rápida (*Quick Store* / *Store Nearby*)**: Enviar ítems coincidentes desde mochila/hotbar hacia el contenedor abierto o hacia todos los contenedores compatibles cercanos dentro de un radio configurable.
  - **Ranuras bloqueadas / Favoritos (*Locked Slots* / *Favorites*)**: Proteger ítems o posiciones concretas frente a ordenamientos y descargas rápidas, con toggle visual en el inventario.
  - **Fusión opcional de perecederos (*Force-Stack Perishables*)**: Permitir combinar stacks de comida con distinta frescura promediando su progreso de deterioro cuando el jugador lo habilite.
  - **Perfiles de equipamiento rápido** y **resaltado de ítems coincidentes** bajo el cursor.

### 3.2. Interacción Vanilla QoL y Herramientas
- **Referencias y Créditos Base**:
  - **Vanilla: Expanded** (Vintage Story) — *Sisco++*: [GitHub](https://github.com/dsisco11/VanillaExpanded)
- **Funcionalidades**:
  - **Auto-Stashing**: Mantener pulsado interactuar sobre contenedores, cajas (*crates*), hornos de lupia (*bloomery*), molinos (*quern*) o bolsas montadas en entidades para depositar automáticamente ítems elegibles, con barra de progreso radial.
  - **Auto-Looting**: Mantener pulsado interactuar sobre cadáveres ya carneados para recoger automáticamente todo el botín que quepa en el inventario.
  - **Asistente / Calculadora de Aleaciones**: Panel adjunto al abrir una fogata con crisol para seleccionar aleación o metal puro, ajustar porcentajes y unidades objetivo sin desperdicio, estimar combustible y precalentamiento, y depositar ingredientes y combustible de forma atómica.
  - **Menú Radial de Modos de Herramienta y Quick-Swap**: Reemplazar la rejilla de modos (`GuiDialogToolMode`) por un menú radial fluido (con estrategias para cincel, martillo de forja, rodillo de bote, etc.) y añadir un menú radial de cambio rápido de herramienta/luz (*Quick Tools*).
  - **Atajo de Fuente de Luz (*Equip Light Hotkey*)**: Equipar/intercambiar rápidamente la fuente de luz más brillante a mano secundaria (*offhand*) o hotbar.
  - **Herramientas de Ignición y Protección de Agua**: Encender fuegos con velas, antorchas, linternas o lámparas de aceite; proteger bloques fuente de agua contra derrames accidentales cuando `noLiquidSourceTransport` está activo.
  - **Recetas QoL opcionales**: Descrafteo de mochilas/sacos de lino, reciclaje de cabezas metálicas con cincel, palos desde tablones/leña y descrafteo de zarzo (*wattle*), activables por configuración.

### 3.3. Indicadores Visuales en Slots y Mundo
- **Referencias y Créditos Base**:
  - **Vanilla: Expanded** (Vintage Story) — *Sisco++* (indicadores en `GuiElementItemSlotGridBase`, decal de spawn y sexo de animales)
  - **Item Pickup Highlighter** (Vintage Story): [ModDB #25352](https://mods.vintagestory.at/show/mod/25352)
  - **Particles Plus** (Vintage Story): [ModDB](https://mods.vintagestory.at/particlesplus)
- **Funcionalidades**:
  - **Indicadores en Slots**: Barras/fondos/bordes configurables para frescura de perecederos, progreso de preparación, estado de ropa/armadura, volumen de líquidos, contenido de crisoles, combustible de visión nocturna y cantidad en contenedores de comida.
  - **Resaltado e Iconos de Ítems en el Suelo**: Localización visual de `EntityItem` tirados en un radio configurable mediante atajo de teclado (`Shift + F`) o modo continuo, y etiquetas de nombre al agacharse.
  - **Indicadores de Partículas de Recursos**: Emisión sutil o destacada sobre recursos clave durante la exploración (resina, *Terra Preta*, hierro meteórico, arcilla) con filtros por categoría.
  - **Decal de Punto de Respawn**: Marcador luminoso en el bloque de reaparición del jugador.

### 3.4. Recarga Automática (*Hotbar Refill*)
- **Referencias y Créditos Base**:
  - **CFY Simple Refill** (Vintage Story) — *CarFy*: [ModDB](https://mods.vintagestory.at/simplerefill)
  - **Inventory Profiles Next** (Minecraft) — *Mirinimi* (umbrales de durabilidad, alertas y configuración por slot)
- **Funcionalidades**:
  - Reemplazo automático en la hotbar de bloques, consumibles o herramientas al agotarse un stack o antes de romperse una herramienta (umbral configurable).
  - Lista negra (*blacklist*) configurable, retardo (*delay*) anti-desincronización y preferencia por tamaño de stack.

### 3.5. Rastreo de Proyectiles
- **Referencias y Créditos Base**:
  - **Projectile Tracker** (Vintage Story) — *MrFastZombie*: [ModDB](https://mods.vintagestory.at/projectiletracker)
- **Funcionalidades**:
  - Creación automática de un *waypoint* temporal en el mapa donde aterriza un proyectil propio (flechas, lanzas).
  - Eliminación automática al recogerlo o cuando desaparece (*despawn*).

### 3.6. Fauna QoL: Caza, Carneado e Indicadores Biológicos
*(Módulo integrado desde el antiguo concepto `faunaqol` + indicadores de cría de `Vanilla Expanded`)*
- **Objetivo de Diseño y Balance**:
  - Evitar que la caza se resuelva de un único golpe trivial, haciendo los encuentros con presas y depredadores más tácticos, y recompensar el esfuerzo con mayor rendimiento al carnear.
- **Categorización de Especies**:
  - **Presas pequeñas** (liebres, gallinas salvajes, zorros, etc.): Incremento moderado de vida; mayor rendimiento de carne pequeña, plumas o huesos.
  - **Presas medianas y grandes** (ciervos, cabras montesas, jabalíes, borregos, alces): Mayor resistencia para fomentar rastreo, emboscadas o armas de rango; incremento notable en carne, grasa y pieles.
  - **Depredadores** (lobos, hienas, osos): Combates exigentes con recompensa abundante en pieles grandes/enormes, grasa y carne.
- **Escalado Independiente por Tipo de Recurso**:
  - Multiplicadores separados para carne cruda, pieles/cueros, grasa y subproductos (huesos, plumas, cornamentas), manteniendo cantidades razonables.
- **Diferenciación Salvaje vs. Domesticado e Indicadores de Cría**:
  - Priorizar el escalado de vida sobre fauna salvaje (`generation == 0`), con interruptor configurable para generaciones domesticadas (`generation > 0`).
  - Listas `whitelist`/`blacklist` por código de entidad para cobertura consistente en fauna vanilla y de terceros.
  - **Indicadores visuales de sexo y preñez** sobre animales vivos domesticados (`generation >= 1`): iconos configurables de macho, hembra y hembra preñada visibles a corta distancia para facilitar la cría selectiva.

---

## 4. Alcance de Personalización (Jugador vs. Mundo/Servidor)
- **Preferencias Personales del Jugador (Cliente)**:
  - Atajos de teclado, estilo/opacidad de menús radiales e indicadores de slots, visibilidad de botones de inventario, iconos de sexo en animales, intensidad de partículas, colores de resaltado y preferencias de ordenamiento.
- **Reglas Autoritativas del Mundo / Servidor**:
  - Multiplicadores de vida y carneado de fauna por categoría y recurso, comportamiento con animales domesticados, permiso de *auto-looting*, *auto-stashing*, *auto-refill*, rastreo de proyectiles, radio de *store-nearby*, listas blancas/negras de contenedores y entidades, y activación de recetas QoL.
