# Instrucciones del proyecto

Este repositorio es **Guerrero, Maga y Asesino**, un juego de piedra, papel o tijera. No es TTTXO/TicTacToe y no debe volver a incluir las pantallas antiguas de TTTXO, leaderboards ni matchmaking en tiempo real.

Desde la rama `feature/duelo-asincrono` el proyecto es además **Duelo de Gremios**: un duelo asíncrono en la nube sobre el mismo RPS. Solo se admiten los paquetes UGS **Authentication** (login anónimo), **Cloud Save** (persistencia) y **Cloud Code** (la autoridad del duelo entre dos cuentas y sus mensajes push). Nada de servidores en tiempo real, Relay, Lobby, Netcode ni Matchmaker.

## Idioma y estilo

- Documentación y textos de jugador: español.
- Código, nombres de clases, métodos, variables, comentarios, logs y commits: inglés.
- Los textos de UI pasan por `UiText`/Unity Localization; no hardcodear texto visible en los controladores.
- Los campos del documento JSON (`match_id`, `nombre`, `hp`, `secuencia_actual`…) son el contrato y van en snake_case/español a propósito. El código usa los alias en inglés de `DuelStateDto`/`DuelPlayerDto`.
- Mantén el estilo existente: controladores de pantalla pequeños, lógica de reglas en `TTTXO.Core`, estado de sesión en `RpsSeries` (local) y `DuelHub` (asíncrono).

## Arquitectura

- `Assets/Scenes/Rps.unity` es la **única** escena y la única entrada en Build Settings.
- `AppRoot` contiene `UIDocument` + `GameManager` + `ScreenRouter` + `AppBootstrap` + `SceneUiBootstrap`.
- `ScreenRouter` es el host de navegación: ocho pantallas, un solo `UIDocument`, `ScreenHistory` para el botón atrás.
- `RpsSeries` es el estado de la serie (mejor de cinco). La IA puede empatar: elige entre las tres jugadas, sin excluir la del jugador.
- Single Player resuelve la ronda al elegir el jugador. Local 2P espera a que el jugador 2 elija.
- El VS Screen no resuelve nada: lee `RpsSeries.LastRound`, revela al rival a los 5,5 s y anima Attack/Hurt durante 1 s.
- Un empate muestra `EMPATE` y vuelve a Character Select; nunca sale al menú.
- La serie termina al llegar a 3. Rematch reinicia; Menu abandona la serie.

## Duelo asíncrono

- Pantallas: `DuelHub` (`screen-duel-hub`, lista de duelos activos) y `DuelCombat` (`screen-duel`, 3 ranuras + barras de 100 HP). Se entra desde el botón **Duels** del menú.
- Reglas en `TTTXO.Core/Duel`: 100 HP, **15 de daño por choque ganado** y 0 por empate, 3 choques por ronda, timeout de 10 min (2 min con el toggle de demo de Settings) y tope de 30 rondas.
- La resolución **debe** usar `RpsRulesEngine` (Guerrero > Asesino > Maga > Guerrero). No dupliques la tabla de victorias.
- `DuelResolver` es una función pura sobre `DuelStateDto`: aplica el daño, limpia las secuencias, suma ronda y sella `timestamp_ultimo_movimiento`. No toca Unity, para que Cloud Code pueda ejecutar el mismo código como autoridad.
- `DuelTimeout` concentra la regla de abandono. Solo puede reclamar quien ya jugó y cuyo rival sigue sin jugar.
- `IDuelStore` abstrae la persistencia. `LocalDuelStore` guarda un JSON por partida en `persistentDataPath/duels`; `DuelCloudStore` espeja en local y empuja a Cloud Save en segundo plano.
- `DuelCloudService` usa **Cloud Save Player Data** (`duelIndex` + `duelMatch_<id>`), así que cada cuenta ve sus propios documentos. **Es una caché del jugador, nunca la fuente de verdad**: Player Data está en la clase Default, que el propio jugador puede escribir con su token (lección `ERR-KB-006` de TTTXO), así que no sirve para decidir HP ni ganador. El PvP real vive en el módulo de Cloud Code (ver abajo); no lo simules con Player Data.
- Sin proyecto UGS vinculado o sin red, todo degrada a local con un warning: el juego nunca debe bloquearse esperando la nube.
- El rival de práctica contesta en la **siguiente** apertura del duelo (así `ESPERANDO_RIVAL` es un estado real). El botón de rival silencioso solo existe en Editor/Development Build para demostrar el timeout.

## Duelo online entre dos cuentas (autoridad en Cloud Code)

El PvP real lo decide `CloudCode~/DuelModule/` (ver su `README.md`: mensajes, diagrama de una ronda y despliegue). Reglas no negociables:

- **El cliente pide, Cloud Code decide.** El cliente solo envía intenciones (`CreateDuel`, `JoinDuel`, `SubmitSequence`, `GetDuel`, `ClaimVictory`) y dibuja el `DuelView` que recibe. Nunca decide HP, daño, ganador ni si el rival abandonó.
- **Las secuencias viajan ocultas.** Aunque `DuelStateDto` guarde las dos `secuencia_actual` en el mismo documento, el servidor **nunca** manda ese documento a un cliente: manda un `DuelView` proyectado por jugador, sin la secuencia pendiente del rival. Si añades campos a `DuelView` o `DuelPushPayload`, comprueba que no filtran la secuencia del rival.
- **El push solo avisa.** `IPushClient.SendPlayerMessageAsync` envía un `DuelPushPayload` sin jugadas; quien lo recibe llama a `GetDuel`. Los push pueden perderse: el cliente también sondea.
- **Identidad y reloj del servidor:** `context.PlayerId` nunca sale de un parámetro; "reclamar victoria" (`DuelTimeout`) se mide con el reloj del servidor, no con el del cliente.
- **Idempotencia primero:** cada envío lleva un `requestId` que se reutiliza en los reintentos; el servidor lo comprueba antes que la ronda. `UgsDuelOnlineService` custodia ese id.
- **Una sola fuente de reglas:** el módulo compila `Assets/Scripts/Core` por `<Compile Include>` y usa `DuelResolver`, `DuelTimeout` y `DuelMoveCodec` tal cual. No dupliques reglas.
- Toda regla nueva del online se escribe **primero** en `DuelOnlineEngine` (C# puro) con su prueba, incluida la "tramposa".
- El estado autoritativo vive en Cloud Save *custom data* (solo accesible desde Cloud Code). El módulo se despliega desde el Editor con la sesión de cada quien: nunca subas credenciales, claves de cuenta de servicio ni tokens.
- El `.gitignore` excluye `*.csproj` y `*.sln` **con excepción de `CloudCode~/`**: ahí sí se versionan, porque sin ellos el módulo no se puede compilar ni desplegar. No subas `bin/` ni `obj/`.
- La pantalla de `DuelHub`/`DuelCombat` aún **no** usa `IDuelOnlineService`: conectarla es la siguiente tarea (ver la propuesta del seguimiento).

## Arte y animaciones

- No reintroduzcas `Animator`, `SpriteRenderer` ni los objetos de preview en `Rps.unity`.
- Los frames viven en `Assets/Resources/RPSFrames` y se referencian desde `Assets/Resources/RpsCharacterDatabase.asset`.
- Para regenerarlos: menú **TTTXO → Setup → Create RPS Character Database** o `-executeMethod TTTXO.Game.Editor.RpsCharacterDatabaseSetup.CreateDatabase`.
- Cada personaje usa un canvas común por personaje, alineado abajo al centro; no cambies pivotes a mano.

## Escena y build

- Para reparar referencias de la escena: **TTTXO → Setup → Fix RPS Scene**. Elimina previews viejos y pone `_startScreen` en `RpsMenu`.
- No vuelvas a añadir `Main.unity` ni escenas de TTTXO.
- `ProjectSettings` debe mantener `Assets/Scenes/Rps.unity` como `templateDefaultScene`.
- El proyecto debe estar vinculado a un **Unity Project ID** (Project Settings → Services) para que Authentication/Cloud Save funcionen. Ese vínculo se hace desde el Editor; no inventes IDs a mano ni los metas en el código.

## Tests

- EditMode: reglas, IA, `RpsSeries`, historial de pantallas, `DuelResolver`/`DuelTimeout`, serialización del contrato JSON, el bucle de `DuelHub` y `DuelOnlineEngineTests` (la autoridad del duelo online).
- Sin Unity, en segundos: `cd CloudCode~/DuelModule.Tests && dotnet test` (compila los mismos fuentes de `Assets/Scripts/Core`).
- PlayMode: `Assets/Scripts/Tests/PlayMode/RpsSceneSmokeTests.cs` carga la escena real y comprueba revelación y empate.
- Comandos en `README.md`.
- No uses `-quit` junto con `-runTests`.

## Deuda conocida

- Los namespaces internos siguen siendo `TTTXO.*`; es histórico y no se debe usar como excusa para reintroducir contenido TTTXO.
- Store/Settings son locales y la tienda no tiene catálogo.
- Las claves nuevas de localización usan fallback en inglés hasta regenerar las tablas.
- La animación completa choque a choque del duelo está pendiente: hoy se revelan las filas en secuencia con una sola pasada Attack/Hurt.
- La autoridad del PvP ya existe en `CloudCode~/DuelModule/` (compila y tiene pruebas), pero la UI del duelo sigue jugando contra el bot local: falta conectar `IDuelOnlineService` a `DuelHub`/`DuelCombat`, desplegar el módulo y probarlo con dos cuentas reales.
- Faltan la limpieza de duelos viejos en Cloud Save y la reconexión automática al abrir la app.
