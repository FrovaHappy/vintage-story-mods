# Mod: System UI — Definiciones Técnicas

## 1. Entorno de Ejecución y Naturaleza del Mod
- **Tipo**: Code Mod en C# (biblioteca compartida / mod de dependencia).
- **Lado de ejecución**: Exclusivamente en el cliente (`ICoreClientAPI`, `EnumAppSide.Client`).
- **Naturaleza de la UI en Vintage Story**: El motor no utiliza lenguajes declarativos como HTML, XML o XAML; todas las interfaces se construyen y orquestan imperativamente desde C#.

---

## 2. Base Nativa de la API
El sistema se construirá extendiendo y envolviendo las clases principales de UI de Vintage Story:
- **`GuiDialog`**: Control del ciclo de vida de la ventana (`TryOpen`, `TryClose`, captura de eventos de teclado/ratón y foco).
- **`GuiComposer`**: Constructor fluido nativo (`capi.Gui.CreateCompo(...)`) encargado de encadenar primitivas gráficas, fondos, títulos, grillas de ítems (`AddItemSlotGrid`) y compilar la vista (`.Compose()`).
- **`ElementBounds`**: Sistema jerárquico de posicionamiento y dimensionamiento relativo/fijo de elementos.

---

## 3. Arquitectura y Abstracciones Propuestas
1. **Builders y Helpers para `GuiComposer`**:
   - Envoltorios fluidos y declarativos para componer contenedores anidados (filas, columnas, grillas responsivas) calculando automáticamente los `ElementBounds` y reduciendo coordenadas fijas manuales.
2. **Sistema de Temas y Paletas**:
   - Centralización de colores, tipografías, texturas y bordes reutilizables para evitar duplicación de estilos entre mods dependientes.
3. **Gestión de Estado y Reactividad**:
   - Mecanismo ligero de enlace de datos (*data-binding* / callbacks de actualización) para refrescar subsecciones de una ventana ante cambios de inventario o datos de red sin necesidad de destruir y reconstruir todo el `GuiDialog`.
4. **API Pública para Mods del Monorepo**:
   - Exposición de un `ModSystem` cliente que otros mods puedan resolver mediante `api.ModLoader.GetModSystem<...>()` o mediante referencia directa al ensamblado `.dll`.

---

## 4. Próximos Pasos Técnicos
1. Descompilar e inspeccionar `GuiDialog`, `GuiComposer` y `ElementBounds` en `VintagestoryLib.dll` (usando ILSpy / dotPeek) para mapear el ciclo de renderizado, eventos de entrada y recomposición parcial.
2. Definir el catálogo mínimo viable (MVP) de componentes: botón con icono, panel con scroll, grilla de slots filtrable y diálogo modal.
3. Estructurar la solución `.csproj` como librería reutilizable compatible con los demás proyectos del monorepo.
