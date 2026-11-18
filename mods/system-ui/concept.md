# Mod: Seraph UI (`seraphui`) — Concepto

## 1. Visión General
**Seraph UI** (anteriormente *System UI*) es un mod de infraestructura y biblioteca visual para **Vintage Story** enfocado en modernizar y simplificar la creación de interfaces de usuario dentro del juego.

Crear pantallas ricas, cómodas y modulares en el juego base suele volverse rígido y difícil de mantener a medida que los mods crecen en complejidad. Este mod busca ofrecer una base visual unificada, con componentes reutilizables y una experiencia consistente que sirva como cimiento para los demás mods del ecosistema (como **Comerciantes** y **UX Utilities**).

---

## 2. Objetivos de Diseño
- **Separar el diseño visual de la lógica de juego**: Permitir que cada mod defina *qué* quiere mostrar sin quedar atado a los detalles de bajo nivel de cómo se dibuja cada elemento.
- **Catálogo de Componentes Reutilizables**: Proveer bloques visuales coherentes y listos para usar:
  - Botones con estados claros (normal, hover, deshabilitado, con icono).
  - Pestañas (*tabs*), paneles temáticos y ventanas modales.
  - Barras de desplazamiento (*scroll*) y listas paginadas o con filtros.
  - Tooltips enriquecidos, selectores de cantidad y campos de búsqueda/entrada.
- **Adaptabilidad a Distintas Resoluciones**: Garantizar que las interfaces se acomoden limpiamente a diferentes tamaños de pantalla y escalas de UI sin solapamientos.
- **Identidad Visual Acorde al Juego**: Mantener una estética que respete los materiales y el tono artístico de Vintage Story (madera, pergamino, metal, piedra).

---

## 3. Casos de Uso y Relación con Otros Mods
- **Mod de Comerciantes (`mods/comerciantes`)**:
  - Ventana modular de intercambio con pestañas para compra, venta y trueque.
  - Grillas de inventario con filtros, búsqueda y previsualización clara de precios/transacciones.
  - Indicadores visuales de balance económico, moneda o reputación.
- **Mod de UX Utilities (`mods/ux-utilities`)**:
  - Botones integrados en inventarios y cofres (ordenar, compactar, bloquear ranuras).
  - Overlays y asistentes en juego (ej. calculadora de aleaciones).
  - Menús propios de configuración avanzada.
- **Futuras Herramientas**: Pantallas de información, diarios de misiones o guías interactivas.
