# Sistema de Habitaciones Prefabricadas - Guía de Uso

## Descripción General

El sistema de habitaciones prefabricadas te permite crear habitaciones completas en Unity (con decoración, objetos, enemigos, etc.) y hacer que se generen automáticamente en tu dungeon procedural, manteniendo las conexiones con los corredores.

## Cómo Funciona

1. **El generador crea espacios** para habitaciones usando BSP (Binary Space Partitioning)
2. **El sistema evalúa cada espacio** y decide si usar un prefab o generar proceduralmente
3. **Se selecciona un prefab compatible** basado en tamaño y tipo de habitación
4. **El prefab se instancia** en la posición correcta del dungeon
5. **Los corredores se conectan** automáticamente a la habitación

## Configuración Paso a Paso

### 1. Crear una Habitación Prefab

1. En tu escena de Unity, crea una habitación GameObject vacío
2. Añade todos los elementos que quieras (paredes, decoración, enemigos, cofres, etc.)
3. **Importante**: Diseña la habitación en una cuadrícula pensando en el tamaño del grid
   - Por ejemplo, si tu habitación será de 8x8 tiles, diseña objetos dentro de esos límites
4. Guarda el GameObject como Prefab en tu carpeta de Assets

**Ejemplo de estructura de prefab:**
```
PrefabRoom_BossArena
├── Floor (mesh de suelo)
├── Decorations
│   ├── Pillar1
│   ├── Pillar2
│   └── Throne
├── SpawnPoints
│   ├── BossSpawn
│   └── PlayerSpawn
└── Lighting
    └── TorchesGroup
```

### 2. Crear un PrefabRoomData (ScriptableObject)

1. Click derecho en Project → Create → Dungeon → Prefab Room Data
2. Configura los parámetros:

**Prefab Settings:**
- `Room Prefab`: Arrastra tu prefab de habitación aquí
- `Room Name`: Nombre descriptivo (ej: "Boss Arena")

**Size Requirements:**
- `Required Width`: Ancho mínimo en tiles (ej: 8)
- `Required Height`: Alto mínimo en tiles (ej: 8)
- `Exact Size Only`: Si es true, solo se usará si el espacio es exactamente del tamaño especificado

**Connection Points:**
- Define puntos donde los corredores pueden conectarse (coordenadas locales)
- Ejemplo: Para una habitación 8x8, puedes poner puntos en (4, 0), (4, 8), (0, 4), (8, 4)
- Si dejas vacío, el sistema usará las conexiones automáticas del generador

**Spawn Settings:**
- `Spawn Weight`: Probabilidad relativa (mayor = más probable)
- `Allow Multiple`: ¿Puede aparecer más de una vez en el mismo dungeon?
- `Compatible Room Types`: Tipos de habitación compatibles (Start, Boss, Normal, etc.)
  - Vacío = compatible con todos

### 3. Crear una PrefabRoomConfiguration

1. Click derecho en Project → Create → Dungeon → Prefab Room Configuration
2. Añade tus PrefabRoomData a la lista `Prefab Rooms`
3. Ajusta `Use Prefab Chance` (probabilidad de usar prefabs vs procedural)
4. Marca `Prioritize Prefabs` si quieres que se prioricen sobre generación procedural

### 4. Configurar el DungeonCreator

1. Selecciona tu GameObject con el componente `DungeonCreator`
2. En la sección **Prefab Rooms**:
   - Marca `Enable Prefab Rooms` como true
   - Arrastra tu `PrefabRoomConfiguration` al campo `Prefab Room Config`
3. ¡Listo! Genera el dungeon y verás tus habitaciones prefab

## Ejemplos de Uso

### Ejemplo 1: Habitación de Boss Específica

```
PrefabRoomData Settings:
- Room Name: "Dragon Lair"
- Required Width: 12
- Required Height: 12
- Exact Size Only: false
- Spawn Weight: 100
- Allow Multiple: false
- Compatible Room Types: [Boss]
```

Esta habitación solo aparecerá en espacios de al menos 12x12, solo en la habitación Boss, y solo una vez por dungeon.

### Ejemplo 2: Habitaciones de Tienda Variadas

Crea varios PrefabRoomData para tiendas:
```
Shop_Small: 6x6, Weight 40
Shop_Medium: 8x8, Weight 30
Shop_Large: 10x10, Weight 20
```

Todas compatibles con `RoomType.Shop`, el sistema elegirá aleatoriamente basado en peso y tamaño disponible.

### Ejemplo 3: Habitaciones Decorativas

```
PrefabRoomData Settings:
- Compatible Room Types: [Normal]
- Allow Multiple: true
- Spawn Weight: 15
```

Puedes tener varias habitaciones normales decoradas que aparecen aleatoriamente.

## Ventajas y Limitaciones

### ✅ Ventajas:
- Control total sobre el diseño de habitaciones específicas
- Mantiene conexiones automáticas con corredores
- Sistema de pilares funciona correctamente
- Mezcla perfecta con generación procedural
- Reutilización de prefabs

### ⚠️ Consideraciones:
- Los prefabs deben diseñarse considerando el tamaño del grid
- Las conexiones de corredores se calculan automáticamente
- Las habitaciones con prefab no reciben formas variadas (L, T, Cruz)
- El suelo de la habitación se genera normalmente, el prefab se coloca encima

## Flujo del Sistema

```
Generación de Dungeon
    ↓
Crear espacios con BSP
    ↓
Generar habitaciones rectangulares
    ↓
Asignar tipos (Start, Boss, Normal, etc.)
    ↓
¿EnablePrefabRooms?
    ├→ NO: Continuar con generación procedural
    └→ SÍ: Evaluar cada habitación
             ├→ ¿Hay prefab compatible?
             │    ├→ SÍ: Asignar prefab
             │    └→ NO: Usar generación procedural
             ↓
Aplicar formas variadas (solo a habitaciones sin prefab)
    ↓
Generar corredores y conectar
    ↓
Instanciar prefabs en posición
    ↓
Generar paredes y pilares
```

## Consejos Avanzados

1. **Puntos de Spawn**: Incluye GameObjects vacíos en tu prefab para marcar puntos de spawn de enemigos, items, etc.

2. **Iluminación**: Incluye luces locales en tus prefabs para ambiente único por habitación

3. **Variantes**: Crea múltiples variantes de la misma habitación con diferentes decoraciones

4. **Debugging**: Usa el componente `PrefabRoomInstance` que se agrega automáticamente para acceder a datos de la habitación en runtime

5. **Mixing Systems**: Puedes combinar:
   - Habitaciones prefab (diseño manual)
   - Formas variadas (L, T, Cruz)
   - Generación procedural pura
   Simplemente ajusta las probabilidades en las configuraciones.

## Troubleshooting

**Problema**: "El prefab no aparece"
- Verifica que `Enable Prefab Rooms` esté marcado
- Comprueba que el tamaño requerido sea compatible con los espacios generados
- Revisa el `Use Prefab Chance` en la configuración

**Problema**: "Los corredores no conectan bien"
- Define `Connection Points` en tu PrefabRoomData
- Asegúrate de que los puntos estén en los bordes de la habitación

**Problema**: "La habitación está mal posicionada"
- El prefab se centra en el espacio de la habitación automáticamente
- Asegúrate de que tu prefab tenga el pivot en el centro (0,0,0)
