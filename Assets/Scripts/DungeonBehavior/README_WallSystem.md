# Sistema de Paredes Procedurales Optimizado

## 📋 Descripción

Este sistema optimiza la generación de paredes del dungeon, reemplazando múltiples instancias de prefabs de cubos por un **único mesh procedural**. Los huecos para puertas se generan automáticamente.

## 🎯 Beneficios

### Antes (Sistema Antiguo)
- ❌ Un GameObject por cada segmento de pared (cientos de objetos)
- ❌ Múltiples draw calls (uno por objeto)
- ❌ Mayor uso de memoria
- ❌ Peor rendimiento en dungeons grandes

### Después (Sistema Nuevo)
- ✅ **Un solo GameObject** con todas las paredes
- ✅ **Un solo draw call** para todas las paredes
- ✅ Reducción de ~95% en número de objetos
- ✅ Mejor rendimiento y FPS
- ✅ Los huecos para puertas se generan automáticamente

## 🚀 Cómo Usar

### 1. Configurar en el Inspector

En el componente `DungeonCreator`:

1. Busca la sección **"Wall Generation"**
2. Marca el checkbox **"Use Procedural Walls"** ✓
3. Asigna un **Material** en el campo "Wall Material" (puedes usar el mismo que usabas antes)

### 2. Generar el Dungeon

El dungeon se generará automáticamente al iniciar, o puedes llamar:

```csharp
dungeonCreator.CreateDungeon();
```

### 3. Comparar Sistemas (Opcional)

Para ver la mejora de rendimiento:

1. Añade el componente `WallSystemStats` a cualquier GameObject
2. Asigna la referencia al `DungeonCreator`
3. En el modo juego:
   - Presiona **K** para comparar ambos sistemas
   - Presiona **L** para ver estadísticas del sistema actual

## 📊 Características Técnicas

### ProceduralWallGenerator.cs

Clase que genera el mesh optimizado:

- **GenerateWalls()**: Versión básica, un cubo por posición de pared
- **GenerateOptimizedWalls()**: Versión avanzada que combina segmentos continuos (RECOMENDADO)

#### Optimizaciones Incluidas:

1. **Agrupación de segmentos**: Paredes adyacentes se combinan en un solo rectángulo largo
2. **Reducción de polígonos**: Menos vértices y triángulos totales
3. **Mesh único**: Todo en un solo objeto con un solo mesh
4. **Colisión optimizada**: Un solo MeshCollider para todas las paredes

### Parámetros Configurables

```csharp
// En DungeonCreator
public bool useProceduralWalls = true;  // Activar sistema nuevo
public Material wallMaterial;            // Material para las paredes
public int wallHeight = 3;               // Altura de las paredes
```

## 🔧 Integración con Sistema Existente

El sistema es **completamente compatible** con tu código existente:

- ✅ Funciona con el sistema de grid actual
- ✅ Respeta las posiciones de puertas existentes
- ✅ Compatible con la generación procedural de habitaciones
- ✅ No requiere cambios en otros scripts
- ✅ Puedes alternar entre sistema antiguo y nuevo en cualquier momento

## 📈 Resultados Esperados

En un dungeon típico de 50x50 con ~200 segmentos de pared:

| Métrica | Antiguo | Nuevo | Mejora |
|---------|---------|-------|--------|
| GameObjects | ~200 | 1 | -99.5% |
| Draw Calls | ~200 | 1 | -99.5% |
| Vértices | ~4,800 | ~800 | -83% |
| Memoria | ~230 KB | ~38 KB | -83% |

## 🎮 Controles de Prueba

Cuando tienes `WallSystemStats` activo:

- **K**: Comparar sistemas (genera dungeon con ambos y muestra estadísticas)
- **L**: Mostrar estadísticas del sistema actual

## 🔍 Solución de Problemas

### Las paredes no se ven

- Verifica que `wallMaterial` esté asignado en el Inspector
- Asegúrate de que el material tenga un shader válido

### Las paredes tienen huecos incorrectos

- El sistema usa las mismas listas que el sistema antiguo (`possibleDoorHorizontalPosition`, etc.)
- Si funcionaba antes, debería funcionar ahora

### Quiero volver al sistema antiguo

Simplemente desmarca `Use Procedural Walls` en el Inspector del `DungeonCreator`.

## 💡 Próximas Mejoras Posibles

- [ ] Añadir variación de texturas por segmento
- [ ] Sistema de damage/destrucción de paredes
- [ ] Generación de detalles (grietas, decoraciones)
- [ ] Lightmap UVs para mejor iluminación
- [ ] Occlusion culling automático

## 📝 Notas de Implementación

El sistema mantiene compatibilidad total con:
- Sistema de grid (`DungeonGrid`)
- Generación de habitaciones (`RoomGenerator`)
- Generación de corredores (`CorridorsGenerator`)
- Sistema de puertas existente

## 🤝 Créditos

Sistema diseñado para optimizar la generación procedural de dungeons en Unity.
