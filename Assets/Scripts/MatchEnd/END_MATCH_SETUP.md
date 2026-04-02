# End Match Setup (Escena unica)

## 1) Scripts base

Ya estan listos estos scripts:

- MatchEndTypes.cs
- MatchEndSnapshotBuilder.cs
- EndMatchSceneDirector.cs
- EndMatchResultsHUD.cs
- EndMatchSkinsPresenter.cs
- EndMatchTimelineRouter.cs
- EndMatchReturnToLobbyButton.cs
- EndMatchRoomSafetyTimer.cs

## 2) Configuracion en escena Game

En el objeto que tiene DungeonNetworkRunner:

- endMatchSceneName = nombre real de la escena final (ejemplo: EndMatch)
- endMatchSceneIndex = fallback opcional
- endMatchLoadDelaySeconds = 0.5 (recomendado)

## 3) Estructura recomendada de la escena final

Crea esta jerarquia:

- EndMatchDirector (GameObject vacio)
  - EndMatchSceneDirector
  - (opcional) PlayableDirector global
- BossWithKills_Root
  - Cam_BossWithKills
  - Boss actor
  - Survivors derrotados
  - Props y efectos
- BossWithoutKills_Root
  - Cam_BossWithoutKills
  - Boss actor
  - Portal cerrandose
  - Props y efectos
- SurvivorsEscaped_Root
  - Cam_SurvivorsEscaped
  - Survivors corriendo
  - Fondo castillo / skybox

Todos los roots pueden estar activos en editor. El script activa solo 1 en runtime.

### Que significa "PlayableDirector global"

Es un PlayableDirector colocado en EndMatchDirector (arriba de los roots), usado para controlar
el flujo comun a todas las variantes, por ejemplo:

- Fade in / fade out global
- Audio de entrada y salida
- Duracion total de cinematic
- Señales para habilitar boton Skip

La animacion especifica de cada variante puede vivir en directores separados dentro de cada root,
pero el director global orquesta el inicio/fin comun para que el flujo no se duplique.

## 4) Vincular EndMatchSceneDirector

En EndMatchDirector, asigna:

- bossWithKillsRoot -> BossWithKills_Root
- bossWithoutKillsRoot -> BossWithoutKills_Root
- survivorsEscapedRoot -> SurvivorsEscaped_Root

Opcional:

- fallbackVariant: variante por defecto si no hay snapshot
- clearSnapshotAfterApply: true solo si no necesitas usar snapshot despues

## 4.1) Preparar timelines por root (recomendado)

Objetivo: que cada variante tenga su timeline propia y se reproduzca automaticamente.

1. En cada root agrega un PlayableDirector:
  - BossWithKills_Root -> director BossWithKills
  - BossWithoutKills_Root -> director BossWithoutKills
  - SurvivorsEscaped_Root -> director SurvivorsEscaped

2. En EndMatchDirector agrega EndMatchTimelineRouter.

3. En EndMatchTimelineRouter asigna:
  - sceneDirector -> EndMatchSceneDirector del mismo objeto
  - globalDirector -> opcional (solo intro/outro/audio comun)
  - bossWithKillsDirector -> director del root BossWithKills
  - bossWithoutKillsDirector -> director del root BossWithoutKills
  - survivorsEscapedDirector -> director del root SurvivorsEscaped

4. UI resultados:
  - resultsPanelRoot -> panel/canvas de resultados (se oculta durante cinematic)
  - resultsHud -> EndMatchResultsHUD

5. Recomendacion importante:
  - desactiva Play On Awake en los directores de variante
  - deja que EndMatchTimelineRouter los reproduzca

6. Timing:
  - variantStartDelaySeconds: delay antes de iniciar timeline de variante
  - showResultsAfterSeconds:
    - >= 0: tiempo fijo
    - < 0: usa duracion de timeline de variante automaticamente

Con esto, al entrar a EndMatch:

1. EndMatchSceneDirector decide variante y activa root correcto
2. EndMatchTimelineRouter reproduce timeline de esa variante
3. Al terminar, muestra el panel de resultados

## 5) UI de resultados

Crea un Canvas y tres TMP_Text:

- TitleText
- SummaryText
- PlayersText

En un GameObject UI agrega EndMatchResultsHUD y asigna esos campos.

El HUD muestra:

- motivo de fin
- variante seleccionada
- listado de jugadores con estado

### Tabla de resultados (recomendado)

Para pasar de texto corrido a formato tabla:

1. Crea un contenedor con Vertical Layout Group para filas (ejemplo: ResultsTableRows).
2. Crea un prefab de fila UI (RowResult) con 5 columnas TMP:
  - Jugador
  - Rol
  - Resultado
  - Detalle
  - Skin
3. Agrega EndMatchResultsRowUI al prefab de fila y asigna sus TMP_Text.
4. En EndMatchResultsHUD asigna:
  - tableRowsContainer -> ResultsTableRows
  - tableRowPrefab -> RowResult
5. Opcional: llena skinNamesByIndex para mostrar nombres en vez de Skin #index.

El HUD mantiene playersText como fallback automatico si no hay tabla conectada.

### Boton para volver a LobbyList

1. Crea un boton en el Canvas de resultados (ejemplo: Btn_ReturnLobby).
2. Agrega el script EndMatchReturnToLobbyButton al mismo GameObject del boton.
3. Configura:
  - lobbyListSceneName = LobbyList
  - shutdownRunnerBeforeLoad = true
4. Si no asignas returnButton manualmente, el script usa el Button del mismo objeto.

Flujo del boton:

1. Desactiva el boton para evitar doble click.
2. Cierra runners de Fusion activos.
3. Carga la escena LobbyList.

### Mantener resultados visibles pero cerrar room por seguridad

Si quieres que jugadores se queden viendo resultados aunque el host salga, pero evitar que la room quede viva indefinidamente:

1. En NetworkRunnerHandler activa stayInEndMatchAfterDisconnect.
2. En EndMatchDirector (o cualquier objeto de EndMatch) agrega EndMatchRoomSafetyTimer.
3. Configura safetyShutdownDelaySeconds (por ejemplo 45-90 segundos).

Comportamiento:

1. Los jugadores pueden quedarse en EndMatch y presionar Return cuando quieran.
2. El host cierra la room automaticamente al vencer el timer de seguridad.

## 5.1) Mostrar skins reales de jugadores en la cinematic

El snapshot ahora guarda SelectedCharacterIndex por jugador (la skin elegida en partida).

Para usarlo:

1. Crea un GameObject (ejemplo: EndMatchSkins) dentro de la escena final.
2. Agrega EndMatchSkinsPresenter.
3. Asigna survivorSkinPrefabs en el mismo orden que el selector de personajes (index 0..N).
4. Crea slots vacios (Transforms) para colocar personajes:
  - defeatedSlots (para BossWithKills)
  - escapedSlots (para SurvivorsEscaped)
5. Arrastra esos slots al componente.

Comportamiento:

- En BossWithKills instancia las skins de survivors que no escaparon.
- En SurvivorsEscaped instancia las skins de survivors que escaparon.
- En BossWithoutKills normalmente no instancia survivors derrotados.

Nota: ademas de IsDead, ahora puedes controlar animacion de slots con estos modos:

- ForceState: fuerza un estado del Animator por nombre (ejemplo: Dead, Locomotion, Run).
- SetMoveParameters: setea parametros float de movimiento (ejemplo: Speed=1).

Configuracion recomendada para survivors corriendo en slots:

1. escapedAnimationMode = SetMoveParameters
2. moveFloatParameters = Speed, MoveSpeed, Velocity (deja los que tu Animator use)
3. escapedMoveValue = 1.0

Si tu controller no depende de Speed y usa estados directos:

1. escapedAnimationMode = ForceState
2. escapedStateName = Run o Locomotion (segun tu Animator)

Para survivors derrotados:

1. defeatedAnimationMode = ForceState
2. defeatedStateName = Dead o Knockdown

## 6) Flujo final actual

- Si todos los survivors escapan -> cierre anticipado de partida
- Si todos los survivors son derrotados -> cierre anticipado de partida
- Si se agota el timer global -> cierre por tiempo

Al cerrar:

1. se construye snapshot por jugador
2. se replica snapshot por RPC
3. se carga escena final
4. EndMatchSceneDirector activa variante local

## 7) Iteracion de animaciones recomendada

Fase 1 (mock):

- Solo camaras y movimiento simple
- Duracion de 6-8 segundos por variante

Fase 2 (animaciones reales):

- BossWithKills: pose victoria + paneo
- BossWithoutKills: boss mirando portal cerrar
- SurvivorsEscaped: corrida hero y plano abierto

Sugerencia de setup para correr / caer sin mover logica de juego:

- Usa prefabs de solo visual (sin NetworkObject ni scripts de gameplay).
- Agrega Animator con clips de "derrotado" y "run".
- Usa Timeline por variante para mover camara y marcar beats.

Fase 3 (pulido):

- audio por variante
- VFX por variante
- boton skip y retorno a lobby

## 8) Pruebas minimas

Probar estos escenarios:

1. Todos escapan
2. Nadie escapa
3. Mixto (al menos 1 escapa y 1 no)
4. Timeout con players vivos

En caso mixto, cada cliente debe ver su variante local segun su rol/resultado.
