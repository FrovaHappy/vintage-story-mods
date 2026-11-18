# Mod: UX Utilities — Definiciones Técnicas

## 1. Arquitectura Modular y Separación Cliente / Servidor
Para maximizar la compatibilidad en servidores multijugador y mantener el código limpio, cada utilidad se implementará como un submódulo registrable con su alcance bien delimitado:

| Módulo | Lado Principal | Requerimientos Técnicos |
| :--- | :--- | :--- |
| **Inventario y Contenedores** | Cliente + Servidor | Inyección de UI en diálogos de inventario (`ICoreClientAPI`); las operaciones de ordenamiento/transferencia deben respetar las transacciones de inventario de Vintage Story (`ItemSlot`, paquetes nativos de movimiento de slots o canal propio si hay acciones masivas). |
| **Resaltado en el Suelo** | 100% Cliente | Escaneo espacial de `EntityItem` dentro de radio configurado en `ICoreClientAPI`, emisión de partículas locales o renderizado de overlays/etiquetas y manejo de `HotKey` (`Shift + F`). |
| **Hotbar Refill** | Cliente (+ eventos de inventario) | Monitoreo de cambios en el slot activo (`OnSlotModified` / consumo de stack / rotura) e intercambio con el inventario respetando `refillDelayMs` y `blacklist`. |
| **Vanilla QoL** | Cliente + Servidor | *Auto-Stashing* / *Auto-Looting* requieren sincronización limpia con el servidor al interactuar con contenedores o `EntityBehaviorHarvestable`; calculadora de aleaciones y cambio de luz operan principalmente en cliente. |
| **Projectile Tracker** | Cliente + Servidor | Requiere servidor (`ICoreServerAPI`) para detectar con precisión el impacto/detención y recogida/despawn de proyectiles y sincronizar vía `INetworkChannel` la creación/eliminación de *waypoints* en el cliente. |
| **Partículas de Recursos** | 100% Cliente | Inspección periódica por chunks cercanos cargados en cliente para emitir partículas locales sobre bloques objetivo según filtros configurados. |

---

## 2. Sistema de Configuración e Integración

### 2.1. Compatibilidad con ConfigLib (y ConfigKit)
- **Referencia**: [ConfigLib](https://mods.vintagestory.at/configlib)
- **Implementación técnica**:
  - Declarar `configlib` como dependencia opcional (*soft dependency*) o detectar su presencia en tiempo de carga mediante `api.ModLoader.IsModEnabled("configlib")` (y contemplar compatibilidad *drop-in* con **ConfigKit** / `configkit`).
  - Obtener `ConfigLibModSystem` (`api.ModLoader.GetModSystem<ConfigLibModSystem>()`) y suscribirse al evento `SettingChanged` para aplicar cambios en memoria en vivo sin reiniciar el juego.
  - Mantener persistencia estándar en `ModConfig/<modid>.json` mediante `api.LoadModConfig` / `api.StoreModConfig` para garantizar funcionamiento autónomo si ConfigLib no está instalado.

### 2.2. Integración con el Menú de Personalización del Mundo
- **Mecanismo nativo de Vintage Story**:
  - Almacenar reglas globales por partida en `WorldConfiguration` (accesible vía `serverconfig.json`, `/worldconfig` y `sapi.World.Config`).
- **Inyección en la interfaz de creación/personalización de mundo**:
  - Investigar en `VintagestoryLib.dll` las clases de pantalla de creación/personalización (`GuiScreenCustomizeWorld` / `GuiDialogCreateWorld`).
  - Evaluar **Harmony Patching** (`[HarmonyPostfix]`) sobre la composición de dicha pantalla para añadir una pestaña o sección dedicada a **UX Utilities** que lea y escriba atributos tipados directamente en `WorldConfiguration` antes de generar o cargar el mundo.
  - Sincronizar la configuración del servidor al cliente en el handshake/conexión para que las restricciones del mundo prevalezcan sobre las opciones locales cuando aplique.

---

## 3. Integración con `mods/system-ui`
- Uso de componentes de [`mods/system-ui`](../system-ui/technical.md) para:
  - Inyectar botones compactos en las ventanas de inventario y cofres existentes.
  - Renderizar la interfaz de la calculadora de aleaciones y overlays de etiquetas en el suelo.
  - Proveer un diálogo de configuración propio (gestión de *Locked Slots*, perfiles de equipamiento y fallback visual si no se usa ConfigLib).

---

## 4. Próximos Pasos Técnicos
1. **Auditoría de Licencias**: Verificar la licencia de cada repositorio/ModDB listado en `concept.md` (MIT, GPL, CC0, All Rights Reserved, etc.) para definir qué módulos se reimplementan limpiamente desde cero y cuáles pueden adaptar código compatible.
2. **Inspección de `VintagestoryLib.dll`**:
   - Analizar `GuiScreenCustomizeWorld` / `GuiDialogCreateWorld` con ILSpy / dotPeek para confirmar el punto de extensión exacto de la UI de personalización de mundo.
   - Revisar la API exacta de `ConfigLib` / `ConfigKit` para registro programático de esquemas de configuración.
3. **Definición de MVP Técnico**: Implementar el esqueleto del `ModSystem` modular con carga de configuración dual (`ModConfig` + detección de `ConfigLib`) y los primeros 2 módulos puramente *Client-Side* (ej. *Resaltado en el Suelo* y *Ordenamiento base*).
