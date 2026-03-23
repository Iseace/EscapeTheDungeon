# End Match Setup (Escena unica)

## 1) Scripts base

Ya estan listos estos scripts:

- MatchEndTypes.cs
- MatchEndSnapshotBuilder.cs
- EndMatchSceneDirector.cs
- EndMatchResultsHUD.cs
- EndMatchSkinsPresenter.cs

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
