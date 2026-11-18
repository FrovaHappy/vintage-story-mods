# Mod: Seraph QoL (`seraphqol`) — Concepto

## 1. Visión General
**Seraph QoL** (anteriormente *UX Utilities*) es un mod enfocado en mejorar integralmente la **Experiencia de Usuario (UX)** y la calidad de vida (*Quality of Life*, QoL) en **Vintage Story**.

El proyecto busca consolidar en un único mod modular un conjunto de utilidades prácticas probadas en la comunidad (de Vintage Story y Minecraft), evitando depender de decenas de mods dispersos, manteniendo coherencia visual mediante **System UI** y permitiendo activar, desactivar o ajustar cada función por separado.

> [!IMPORTANT]
> **Compromiso de Créditos y Licencias:** Cada utilidad inspirada o adaptada de un mod existente mantendrá de forma explícita y visible el crédito a sus autores originales (en `modinfo.json`, documentación, repositorio y pantallas de configuración/acerca de), respetando estrictamente las licencias de cada proyecto original antes de adaptar código o mecánicas.

---

## 2. Objetivos Principales
- **Modularidad Granular**: Cada utilidad funcionará como un módulo independiente que el usuario o administrador del mundo puede activar o desactivar.
- **Configuración Unificada y Accesible**:
  - Edición en tiempo real desde el menú de pausa (mediante compatibilidad con **ConfigLib**).
  - Reglas compartidas integradas en el **Menú de Personalización del Mundo** al crear o configurar una partida.
- **Consistencia Visual**: Integrarse limpiamente con la estética del juego apoyándose en **System UI**.

---

## 3. Módulos Planificados y Créditos de Referencia

### 3.1. Gestión Avanzada de Inventario y Contenedores
- **Inspiración y Créditos Base**:
  - **Inventory Profiles Next** (Minecraft) — *mirinimi*: [CurseForge](https://www.curseforge.com/minecraft/mc-mods/inventory-profiles-next)
  - **Storage Tweaks** (Vintage Story) — *trojan96g*: [GitHub](https://github.com/trojan96g/vintagestory-storage-tweaks)
- **Funcionalidades planeadas**:
  - Botones integrados en el inventario del jugador y en contenedores (cofres, barriles, vasijas, etc.) para **ordenar** y **compactar** stacks.
  - Transferencia inteligente de ítems coincidentes (*Move Matching* / descarga rápida hacia contenedores que ya contienen ese recurso).
  - **Ranuras bloqueadas (*Locked Slots*)**: Marcar slots específicos para protegerlos de ordenamientos o transferencias rápidas.
  - Perfiles/conjuntos de equipamiento rápido (armadura, herramientas, barra rápida).

### 3.2. Resaltado e Iconos de Ítems en el Suelo
- **Inspiración y Créditos Base**:
  - **Item Pickup Highlighter** (Vintage Story): [ModDB #25352](https://mods.vintagestory.at/show/mod/25352)
- **Funcionalidades planeadas**:
  - Localización visual de objetos tirados en el suelo dentro de un radio configurable (ideal para objetos perdidos al morir, flechas, lanzas, bayas o semillas en vegetación densa).
  - Indicadores visuales / iconos o partículas distintivas activables por atajo de teclado (ej. `Shift + F`) o modo continuo.
  - Opción de mostrar etiquetas de nombre al agacharse (*sneaking*).

### 3.3. Recarga Automática de Barra Rápida (*Hotbar Refill*)
- **Inspiración y Créditos Base**:
  - **CFY Simple Refill** (Vintage Story) — *CarFy*: [ModDB](https://mods.vintagestory.at/simplerefill)
- **Funcionalidades planeadas**:
  - Reemplazo automático en la hotbar de bloques, consumibles o herramientas desde el inventario al agotarse un stack o romperse una herramienta.
  - Lista negra (*blacklist*) configurable (ej. antorchas o ítems específicos).
  - Retardo configurable y preferencia opcional por consumir primero stacks más grandes para reducir desorden.

### 3.4. Mejoras Vanilla QoL (Interacción, Luz y Aleaciones)
- **Inspiración y Créditos Base**:
  - **Vanilla: Expanded** (Vintage Story): [ModDB](https://mods.vintagestory.at/vanillaexpanded)
- **Funcionalidades planeadas**:
  - *Auto-Stashing*: Transferir rápidamente ítems coincidentes a un contenedor manteniendo pulsado el botón de interacción.
  - *Auto-Looting*: Recoger automáticamente el botín de criaturas cosechadas que quepa en el inventario.
  - Cambio rápido de fuente de luz a la mano secundaria (*offhand*) o ranura activa mediante atajo de teclado.
  - Asistente / calculadora visual de aleaciones al abrir una fogata con crisol (proporciones exactas y depósito rápido).

### 3.5. Rastreo de Proyectiles
- **Inspiración y Créditos Base**:
  - **Projectile Tracker** (Vintage Story) — *MrFastZombie*: [ModDB](https://mods.vintagestory.at/projectiletracker)
- **Funcionalidades planeadas**:
  - Creación automática de un *waypoint* en el mapa en la posición donde aterriza un proyectil disparado o lanzado (flechas, jabalinas).
  - Eliminación automática del marcador cuando el jugador recoge el proyectil o cuando este desaparece (*despawn*).

### 3.6. Indicadores de Partículas para Recursos
- **Inspiración y Créditos Base**:
  - **Particles Plus** (Vintage Story): [ModDB](https://mods.vintagestory.at/particlesplus)
- **Funcionalidades planeadas**:
  - Emisión de partículas configurables sobre recursos difíciles de distinguir durante la exploración (ej. resina, *Terra Preta*, hierro meteórico, arcilla).
  - Modos de intensidad (desde indicadores claros hasta modo *Subtle* integrado con el entorno) y filtros por tipo de recurso.

---

## 4. Alcance de Personalización (Jugador vs. Mundo)
- **Preferencias Personales del Jugador**: Atajos de teclado, estilo e intensidad de partículas, colores de resaltado, perfiles de ordenamiento y bloqueo de ranuras.
- **Reglas del Mundo / Servidor**: Qué utilidades con impacto en jugabilidad están permitidas en la partida (ej. rastreo de proyectiles, *auto-looting*, *auto-refill* o radios máximos de detección).
