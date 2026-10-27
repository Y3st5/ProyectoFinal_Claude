# DuelModule — autoridad del Duelo de Gremios entre dos cuentas (Cloud Code)

Módulo de Unity Cloud Code (C#, .NET 9) que es la **única autoridad** de un duelo entre dos jugadores. El cliente envía intenciones ("crear", "unirme", "estas son mis tres jugadas", "reclamar victoria") y dibuja lo que el servidor responde. Todo lo demás lo decide este módulo: quién está en el duelo, cuándo se cierra una ronda, el daño, el HP, el ganador y si el rival estuvo callado el tiempo suficiente.

No reescribe las reglas del equipo: ejecuta las suyas. `DuelResolver` (3 choques, 15 de daño, KO y tope de rondas), `DuelTimeout` ("reclamar victoria"), `DuelMoveCodec` y `DuelStateDto` de `Assets/Scripts/Core/Duel` se **compilan dentro del módulo** con `<Compile Include>`. Lo que añade `DuelOnlineEngine` es quién puede hacer qué, en qué orden y qué ve cada jugador.

> Documentación de Cloud Code usada: https://docs.unity.com/en-us/cloud-code/3.0.0 (módulos C#, *push messages*, errores y límites).
>
> **Aviso de versión:** esa documentación es la de Cloud Code **3.0.0**, pero el paquete de cliente `com.unity.services.cloudcode` 3.0.0 exige Unity 6000.7. Este proyecto usa Unity 6000.3, así que el cliente usa la **2.10.4**, que ya incluye las suscripciones a mensajes (desde la 2.4.0). El módulo de servidor no depende de esa versión.

## 1. Por qué hace falta (el problema del documento compartido)

Hoy `DuelCloudService` guarda cada duelo en **Cloud Save Player Data**. Hay dos problemas para un PvP entre dos cuentas:

1. **Cada cuenta ve su propia copia.** Dos jugadores no comparten un documento, así que no hay partida común.
2. **Player Data es de la clase Default: el propio jugador puede escribirla con su token** desde la API REST, sin pasar por el juego (lección `ERR-KB-006` de TTTXO). Un cliente modificado podría poner `hp: 100`, `ganador: yo` o cambiar su secuencia después de ver la del rival. Y si el documento compartido contuviera ambas `secuencia_actual`, cada jugador **descargaría la secuencia pendiente del rival** antes de enviar la suya (información oculta expuesta).

Por eso el estado autoritativo vive en este módulo, en Cloud Save *custom data* que solo se lee y escribe con el *service token*, y el cliente solo recibe una **vista proyectada por jugador** (`DuelView`). La copia de Player Data pasa a ser una caché local.

## 2. El sistema de mensajes

Cloud Code ofrece dos canales y el juego usa los dos:

| Canal | Quién → quién | Para qué | API |
|---|---|---|---|
| **Llamada a función** | cliente → módulo | Enviar una intención. La respuesta es siempre la vista actualizada del duelo. | cliente: `CloudCodeService.Instance.CallModuleEndpointAsync<T>`; servidor: `[CloudCodeFunction]` |
| **Mensaje push** | módulo → cliente | Avisar al rival de que algo cambió ("ve a consultar tu duelo"). | servidor: `IPushClient.SendPlayerMessageAsync`; cliente: `CloudCodeService.Instance.SubscribeToPlayerMessagesAsync` |

El mensaje push **nunca lleva una jugada**: es un aviso liviano (`DuelPushPayload`: código, evento, ronda, fase) con `messageType = "duel_updated"`. Quien lo recibe llama a `GetDuel` y obtiene *su* vista. La documentación no garantiza la entrega de los push (solo llegan a clientes ya suscritos), por eso el cliente **también sondea `GetDuel`**; el push hace que se sienta inmediato y el sondeo lo hace correcto si se pierde.

### Funciones (contrato en `Assets/Scripts/Core/Duel/Online/DuelOnlineContract.cs`)

| Función | Quién la llama | Parámetros | Qué valida el servidor |
|---|---|---|---|
| `CreateDuel` | Jugador A | `requestId`, `displayName` | El código se deriva de `(playerId, requestId)`: un reintento encuentra el mismo duelo y nunca crea dos. Si el código existe y no es un reintento tuyo, `conflict` y **no se sobrescribe**. |
| `JoinDuel` | Jugador B | `matchCode`, `requestId`, `displayName` | El duelo existe, espera rival, no es el tuyo y no está lleno. Abre la ronda 1 y avisa al creador (push `rival_joined`). |
| `SubmitSequence` | Cada jugador, una vez por ronda | `matchCode`, `moves` (3 de `Guerrero`/`Maga`/`Asesino`), `expectedRound`, `requestId` | Ver el orden de comprobaciones más abajo. La secuencia se guarda **oculta**; al llegar la segunda, resuelve la ronda con `DuelResolver`. |
| `GetDuel` | Ambos (push + sondeo) | `matchCode` | Eres participante. Aplica primero el reloj. Nunca devuelve la secuencia pendiente del rival. |
| `ClaimVictory` | Quien está esperando | `matchCode`, `requestId` | `DuelTimeout.TryClaimVictory` con el **reloj del servidor**: ya jugaste esta ronda, el rival no, y pasaron los 10 minutos. Si no, `claim_not_available`. |

La identidad del jugador sale **siempre** de `context.PlayerId`, nunca de un parámetro.

### Qué ve cada jugador (`DuelView`)

La vista se construye por jugador (`DuelOnlineEngine.BuildView`): su HP y el del rival, si cada uno ya envió (`YouHaveSubmitted`, `RivalHasSubmitted`), su **propia** secuencia pendiente (para reconectar), el estado por jugador en el vocabulario del equipo (`TU_TURNO` / `ESPERANDO_RIVAL` / `FINALIZADO`), la hora (del servidor) en que se desbloquea "reclamar victoria" y la **última ronda cerrada** con los tres choques (ahí sí aparecen las dos secuencias, juntas). `Result` informa del desenlace sin lanzar excepciones: `applied`, `replayed`, `stale_round`, `already_submitted`, `wrong_state`, `invalid_argument`, `not_a_participant`, `not_found`, `claim_not_available`, `conflict`.

## 3. Diagrama de una ronda

```
Cliente A                 DuelModule (Cloud Code + Cloud Save)               Cliente B
   | SubmitSequence([G,G,A], r=2) ──►  guarda A=[G,G,A] (oculta)                 |
   | ◄── vista: YouHaveSubmitted, RivalHasSubmitted=false                        |
   |                           ── push "rival_submitted" ───────────────────────►|
   |                                                       GetDuel ◄─────────────|
   |                                                       vista: RivalHasSubmitted=true (sin la secuencia)
   |                           ◄──── SubmitSequence([M,A,A], r=2) ───────────────|
   |                           resuelve con DuelResolver: 3 choques, daño, HP    |
   |                           ronda=3; push "round_resolved" ──────────────────►|
   | (push) → GetDuel ───────► vista: LastRound con los 3 choques de los dos     |
   |  animar choques                                          animar choques     |
```

## 4. Reglas de autoridad (en `DuelOnlineEngine`, código puro y probado)

Orden de comprobaciones en cada acción de jugador (importa):

1. **Argumentos** (exactamente 3 jugadas, cada una un código válido).
2. **Pertenencia** al duelo.
3. **Idempotencia**: si el `requestId` ya se procesó, responde `replayed` sin tocar nada, **antes** de mirar la ronda. Si no, un reintento tras perder la respuesta parecería una secuencia "vieja".
4. **Fase** (en curso).
5. **Ronda** (`expectedRound == ronda actual`, si no `stale_round`).
6. **Una secuencia por ronda**: no se puede cambiar una secuencia oculta (`already_submitted`).

Además:

- **Información oculta:** las secuencias pendientes viven solo en el registro del servidor; ninguna vista ni push las incluye.
- **Reloj:** Cloud Code no tiene temporizadores. Cada llamada aplica primero el reloj (`Tick`: un duelo sin rival expira a los 10 min). La ausencia del rival **no** se resuelve sola: es una reclamación explícita y validada (`ClaimVictory`), como ya diseñó el equipo en `DuelTimeout`.
- **Concurrencia:** los dos jugadores escriben el mismo documento en la misma ronda. Se guarda con **write lock**: si otra llamada cambió el duelo entre mi lectura y mi escritura, Cloud Save rechaza con 409 y `DuelFunctions.ApplyAsync` vuelve a leer y a aplicar (hasta 4 intentos). Es seguro repetir porque toda acción es idempotente por `requestId`.
- **Versión del protocolo:** `DuelOnlineProtocol.Version` viaja en cada `DuelView` (vale 0 si el módulo no envía el campo, así que un módulo antiguo también se detecta). Si no coincide con la del cliente, el cliente se niega a jugar y pide actualizar.
- **Entrada no confiable:** el código se valida contra el alfabeto antes de construir el id del documento (`duel-CODE`) y los nombres se limpian (sin caracteres de control, máximo 24).
- **Datos en Cloud Save:** el duelo va en *custom data* (solo servidor). **Confirmar en el Dashboard que su clase de acceso no es pública**, porque contiene las secuencias ocultas.

## 5. Estructura

```
CloudCode~/                      (el ~ hace que Unity ignore la carpeta)
  DuelModule/
    DuelModule.csproj / .sln     net9.0; incluye Assets/Scripts/Core/**/*.cs por <Compile Include>
    ModuleConfig.cs              ICloudCodeSetup: registra IGameApiClient (IPushClient lo inyecta Cloud Code)
    DuelFunctions.cs             las 5 funciones [CloudCodeFunction] + push al rival
    DuelStore.cs                 lectura/escritura en Cloud Save con write lock
    Properties/PublishProfiles/FolderProfile.pubxml   exigido por la ventana de Deployment
  DuelModule.Tests/              pruebas con dotnet (mismos fuentes que Unity)
Assets/Scripts/Core/Duel/Online/
  DuelOnlineContract.cs          constantes, mensajes y vistas (compartido cliente ↔ servidor)
  DuelOnlineEngine.cs            máquina de estados pura del duelo online (sin E/S, sin reloj, sin Unity)
Assets/Scripts/Game/Duel/Online/
  IDuelOnlineService.cs          lo que necesitan las pantallas
  UgsDuelOnlineService.cs        llamadas + suscripción push + sondeo + custodia del requestId
Assets/CloudCode/DuelModule.ccmr referencia del módulo para la ventana de Deployment
```

El registro que guarda el módulo (`DuelRecord`) es un objeto de propiedades planas a propósito: `System.Text.Json` ignora los campos públicos que usa `DuelStateDto`. El motor lo convierte a `DuelStateDto` para correr las reglas del equipo y copia el resultado de vuelta.

## 6. Compilar y probar

```
cd CloudCode~/DuelModule.Tests
dotnet test
```

Corre en segundos y sin Unity las pruebas de `Assets/Scripts/Tests/Core/DuelOnlineEngineTests.cs` (el mismo archivo que ejecuta el Test Runner de Unity), junto con las del resolutor y las reglas de RPS. Incluyen las trampas típicas: enviar dos veces, repetir una petición, leer la secuencia del rival, reclamar una victoria antes de tiempo o sin haber jugado, secuencias inválidas.

Para comprobar que el módulo compila y se publica como lo hace la ventana de Deployment:

```
cd CloudCode~/DuelModule
dotnet publish DuelModule.csproj -c Release -p:PublishProfile=FolderProfile
```

## 7. Despliegue

1. Vincular el proyecto de Unity a un proyecto de UGS (*Edit → Project Settings → Services*) y elegir el entorno. Cada integrante usa el suyo.
2. En el Dashboard de UGS, habilitar **Authentication**, **Cloud Code** y **Cloud Save** del entorno.
3. `Window → Deployment`: tildar `Assets/CloudCode/DuelModule.ccmr` (apunta a `DuelModule.sln` con una ruta relativa) y *Deploy Selected*, o con el UGS CLI: `ugs deploy Assets/CloudCode/DuelModule.ccmr`. **No se pudo comprobar aquí que la ventana lo liste** (hace falta el proyecto de UGS vinculado); si no aparece, instalar el paquete `com.unity.services.deployment`.

> **Estado:** el módulo compila con las reglas del equipo, se publica con el perfil de Deployment y su lógica pasa las pruebas. **No está desplegado ni probado contra el servicio real de UGS**, y **las pantallas `DuelHub`/`DuelCombat` todavía no lo usan** (siguen jugando contra el bot local).

## 8. Siguiente paso para el equipo: conectar la UI

`DuelHub` y `DuelCombat` hoy leen y escriben un `DuelStateDto` local. Conectarlos al online es trabajo de los alumnos, y es una buena práctica:

- **Crear / unirse:** en el Hub, "crear duelo" llama a `IDuelOnlineService.CreateDuelAsync` y muestra el código; "unirse" pide el código. Con el rival unido, abrir `DuelCombat`.
- **Enviar:** al fijar las tres ranuras, `SubmitSequenceAsync`. La pantalla pasa a "esperando al rival" y **dibuja el `DuelView`**, no su propio documento.
- **Esperar:** reaccionar a `ConsumeRefreshRequest()` (push) y sondear `RefreshAsync` cada unos segundos como respaldo.
- **Revelar:** cuando `LastRound` cambia, animar los tres choques con lo que informa el servidor (`DuelView.LastRound`), sin recalcular nada en el cliente.
- **Reclamar:** habilitar "Reclamar Victoria" cuando `SecondsUntilClaim` llegue a 0 y llamar a `ClaimVictoryAsync`; el servidor decide si de verdad procede.
- **Degradar:** si no hay red, mostrar el error y seguir jugando contra el bot local, como ya hace el resto del proyecto.

## 9. Límites y trabajo pendiente

- **UI sin conectar** (ver arriba) y **módulo sin desplegar ni probado con dos cuentas reales**.
- **Limpieza:** los duelos terminados quedan en Cloud Save (no hay TTL). Falta un *trigger* programado que borre los viejos.
- **Sin reconexión automática** al abrir la app: el código del duelo debería guardarse en el dispositivo (por ejemplo en `PlayerPrefs`).
- **Ventana de "reclamar victoria":** el servidor usa 10 minutos (`DuelTimeout.Default`). El modo demo de 2 minutos que tiene el cliente en Settings no cambia al servidor; si se quiere en online, debe ser una configuración del servidor (Remote Config), nunca una decisión del cliente.
- **Límites de Cloud Code a vigilar:** 600 peticiones/min por jugador y 15 s de ejecución por función.
- **Textos:** las claves nuevas de localización usarán el texto de respaldo en inglés hasta regenerar las tablas.
