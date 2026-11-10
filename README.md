# Guerrero, Maga y Asesino · Duelo de Gremios

Juego de piedra, papel o tijera con tres combatientes animados:

| Combatiente | Jugada |
| --- | --- |
| Guerrero | Piedra |
| Maga | Papel |
| Asesino | Tijera |

Tiene dos modos:

- **Serie local**: un jugador contra la IA o dos jugadores en el mismo dispositivo (mejor de cinco).
- **Duelo asíncrono**: partidas por turnos de 3 choques, 100 HP y 15 de daño por choque ganado. Se guardan como un documento JSON y se suben a **Unity Cloud Save**; el rival no tiene que estar conectado al mismo tiempo.

## Cómo jugar

1. Abre el proyecto con **Unity 6000.3.20f1**.
2. Abre `Assets/Scenes/Rps.unity` (es la única escena del build).
3. Pulsa **Play**.

### Serie local

1. **Menú RPS** → `Play` → eliges **Single Player** o **Local 2P**.
2. **Character Select**: eliges combatiente. En Local 2P, el jugador 1 elige y luego el jugador 2.
3. **VS Screen**: el combatiente del jugador 1 aparece a la izquierda. La elección del rival (IA o jugador 2) se revela a los 5,5 segundos.
4. El ganador de la ronda reproduce `Attack`; el perdedor, `Hurt`, durante un segundo. Después se vuelve a Character Select.
5. Un empate muestra **EMPATE** durante tres segundos y la serie continúa.
6. La serie es al mejor de cinco (primero en llegar a 3). Al terminar aparecen **Rematch** y **Menu**.

### Duelo asíncrono

1. **Menú RPS** → `Duels` abre el **Hub Central** con todos los duelos activos (rival, ronda, HP y de quién es el turno).
2. `New duel (practice)` crea una partida contra el rival de práctica.
3. En la pantalla de combate eliges **3 ranuras** (Guerrero/Maga/Asesino; se permiten repeticiones) y pulsas `Send moves`.
4. Al enviar, el documento queda en `ESPERANDO_RIVAL` y el rival responde la **próxima vez** que abras el duelo. Volver al Hub y reabrir la tarjeta resuelve la ronda.
5. Cada ronda resuelve los 3 choques: 15 HP al perdedor de cada choque, 0 en empate. Al bajar a 0 HP el duelo termina por KO.
6. Si el rival no juega en **10 minutos**, se habilita `Claim victory` (victoria por abandono). En **Settings → Duels** hay un toggle que baja el límite a **2 minutos** para demos y evaluación.
7. En el Editor (o en un Development Build) el Hub muestra además `Demo: silent rival`, que crea un rival que nunca contesta para poder ver el timeout sin esperar.

## Pantallas

- `RpsMenu`: menú principal, accesos a Duels, Store y Settings.
- `RpsModeSelect`: Single Player / Local 2P.
- `RpsCharacterSelect`: los tres combatientes en Idle; también muestra ronda, turno y marcador.
- `RpsVs`: revelación, animaciones de ataque/daño, empate y fin de serie.
- `DuelHub`: lista de duelos activos y creación de partidas.
- `DuelCombat`: barras de 100 HP, 3 ranuras, selector de clase, cuenta atrás y `Claim victory`.
- `Store`: escaparate local sin catálogo real todavía.
- `Settings`: idioma, volúmenes, timeout de demo y borrado de datos locales.

## Duelo asíncrono: cómo está montado

El documento (`DuelStateDto`) es el contrato JSON exacto que se guarda en Cloud Save:

```json
{
  "match_id": "6f2c...",
  "ronda_actual": 1,
  "estado_partida": "ESPERANDO_RIVAL",
  "turno_de": "rival-practica",
  "timestamp_ultimo_movimiento": "2026-10-27T12:34:56.0000000+00:00",
  "jugador1": { "id": "p1", "nombre": "Invitado", "hp": 100, "secuencia_actual": ["Guerrero", "Maga", "Asesino"] },
  "jugador2": { "id": "rival-practica", "nombre": "Practice rival", "hp": 100, "secuencia_actual": ["", "", ""] },
  "ganador": "",
  "motivo_fin": ""
}
```

- `estado_partida` se guarda una sola vez por documento (`TU_TURNO`, `ESPERANDO_RIVAL`, `FINALIZADO`); cada cliente deriva su perspectiva con `DuelStateDto.StateFor(playerId)`.
- `ganador` y `motivo_fin` son campos añadidos al contrato original: sin ellos no se puede mostrar el resultado ni distinguir KO de abandono.
- El cliente que completa la ronda descarga el documento, resuelve los 3 choques con `RpsRulesEngine`, aplica el daño, limpia las secuencias, suma 1 a `ronda_actual` y vuelve a subir el JSON (`DuelResolver`).
- La regla de abandono vive en `DuelTimeout`, fuera de la UI, para que Cloud Code pueda revalidarla con el reloj del servidor.
- Tope de 30 rondas: si ambos empatan todos los choques, gana quien tenga más HP y con HP igual es empate.

### Persistencia y servicios

- `IDuelStore` abstrae el almacenamiento. `LocalDuelStore` escribe un JSON por partida en `Application.persistentDataPath/duels`.
- `DuelCloudStore` escribe primero en local y después empuja a Cloud Save en segundo plano: **el juego nunca espera a la red**.
- `DuelCloudService` hace login anónimo con **Unity Authentication** y guarda `duelIndex` + `duelMatch_<id>` con **Cloud Save**.
- Si el proyecto no está vinculado a un Unity Project ID o no hay red, todo sigue funcionando en local y solo aparece un warning en la consola.
- Alcance actual: Cloud Save **Player Data** (cada cuenta ve sus documentos). Es una **caché del jugador**, no la fuente de verdad: esa clase de acceso (Default) la puede escribir el propio jugador.

### Duelo online entre dos cuentas (autoridad en Cloud Code)

El PvP real lo decide un módulo de Cloud Code, `CloudCode~/DuelModule/`, que **ejecuta las reglas de este proyecto** (`DuelResolver`, `DuelTimeout`, `DuelMoveCodec`: se compilan dentro del módulo, no se copian). El servidor guarda las secuencias ocultas, resuelve la ronda cuando llegan las dos, mide "reclamar victoria" con su propio reloj y avisa al rival con un **mensaje push de Cloud Code**; el cliente se suscribe y, por si un push se pierde, también sondea. Mensajes, diagrama de una ronda y despliegue están en [`CloudCode~/DuelModule/README.md`](CloudCode~/DuelModule/README.md).

Estado: el módulo compila y tiene pruebas; el servicio de cliente (`IDuelOnlineService`) existe, pero **`DuelHub`/`DuelCombat` todavía no lo usan** y el módulo no está desplegado. Conectar la UI es la siguiente tarea del equipo.

## Estructura

```
Assets/
  Scenes/Rps.unity                 única escena
  Scripts/Core/                    reglas RPS, IA, serie y reglas del duelo
  Scripts/Core/Duel/               contrato JSON, resolver y timeout
  Scripts/Game/Bootstrap/          GameManager local (PlayerPrefs) y arranque UGS
  Scripts/Game/Duel/               Hub, almacenes local/nube y serializador
  Scripts/Game/Services/           Authentication + Cloud Save
  Scripts/Game/UI/                 router, historial y textos
  Scripts/Game/UI/Screens/         controladores de pantalla
  Scripts/Game/Editor/             setup de escena, frames y localización
  Scripts/Tests/                   EditMode y PlayMode
  UI/Screens/Root.uxml             las ocho pantallas
  UI/Styles/main.uss               estilos
  Resources/RpsCharacterDatabase.asset
  Resources/RPSFrames/             frames extraídos por personaje
  Art/SpriteSheet/                 hojas de sprites originales
```

## Animaciones

Los personajes no usan `Animator` ni `SpriteRenderer`. El editor extrae cada frame de las hojas de sprites a `Assets/Resources/RPSFrames/<Personaje>/<Clip>/`, lo pega en un canvas transparente del tamaño máximo del personaje (alineado abajo al centro) y lo guarda como `Texture2D`. `VSAnimationController` intercambia `background-image` en UI Toolkit. La pantalla de duelo reutiliza ese controlador para una pasada Attack/Hurt por ronda; la animación choque a choque está pendiente.

Para regenerar los frames:

- Menú de Unity: **TTTXO → Setup → Create RPS Character Database**.
- O en batchmode:

```
Unity.exe -batchmode -nographics -quit ^
  -projectPath "<ruta del proyecto>" ^
  -executeMethod TTTXO.Game.Editor.RpsCharacterDatabaseSetup.CreateDatabase ^
  -logFile "Logs/RpsSetup.log"
```

## Tests

Las reglas del duelo online (autoridad, secuencias ocultas, reintentos, reclamar victoria) se prueban **sin Unity**, en segundos:

```
cd CloudCode~/DuelModule.Tests
dotnet test
```

EditMode en Unity (reglas, IA, serie, navegación, duelo, serialización y reglas del duelo online):

```
Unity.exe -batchmode -nographics -projectPath "<ruta>" ^
  -runTests -testPlatform EditMode ^
  -testResults "Logs/EditModeTests.xml" ^
  -logFile "Logs/EditModeTests.log"
```

PlayMode (carga `Rps.unity`, revelación de la IA y empate en Local 2P):

```
Unity.exe -batchmode -projectPath "<ruta>" ^
  -runTests -testPlatform PlayMode ^
  -testResults "Logs/PlayModeTests.xml" ^
  -logFile "Logs/PlayModeTests.log"
```

No uses `-quit` con `-runTests`: Unity debe quedarse hasta que el runner termine.

## Notas

- El espacio de nombres interno sigue siendo `TTTXO.*` por historia del proyecto; no afecta al nombre del producto ni al contenido.
- Store y Settings son locales. No hay compras ni matchmaking en tiempo real.
- Para que Authentication/Cloud Save funcionen hay que vincular el proyecto a un Unity Project ID desde **Project Settings → Services** y añadir los paquetes `com.unity.services.core`, `authentication` y `cloudsave` (ya declarados en `Packages/manifest.json`).
- Las claves de localización nuevas se añaden en `LocalizationSetup.cs`; hasta regenerar las tablas usan el texto de respaldo en inglés.
