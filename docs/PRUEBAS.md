# Pruebas y tareas — autoridad del duelo en Cloud Code

Este documento acompaña al PR que añade `CloudCode~/DuelModule/` (la autoridad del duelo entre dos cuentas). La idea es que **ustedes prueben, nos cuenten qué pasó y sigan desarrollando**. Cada prueba dice qué hacer, qué debería pasar y qué reportar si falla. Lo que el PR no pudo verificar está marcado con 🔴.

> Antes de empezar, lean `CloudCode~/DuelModule/README.md`: explica los mensajes, el diagrama de una ronda y por qué el estado ya no puede vivir en Cloud Save Player Data.

## Qué quedó verificado y qué no

| Qué | Estado |
|---|---|
| Reglas del duelo online sin Unity (`dotnet test`) | ✅ 64 pruebas en verde al preparar el PR |
| El módulo compila y se publica con el perfil de Deployment | ✅ |
| Unity compila el cliente y pasan las pruebas de EditMode/PlayMode | ver la descripción del PR (se anota ahí el resultado exacto) |
| El módulo desplegado en un entorno real de UGS | 🔴 no se pudo (hace falta su proyecto de UGS) |
| Dos cuentas jugando una ronda completa | 🔴 no se pudo (y la UI del duelo todavía no usa el servicio) |
| Que la ventana de Deployment liste `DuelModule.ccmr` | 🔴 no se pudo |

## A. Pruebas que pueden hacer ya (sin UGS)

### A1. Reglas con `dotnet` (2 minutos)

1. Instalen el SDK de .NET 9 o superior.
2. En una terminal: `cd CloudCode~/DuelModule.Tests` y `dotnet test`.
3. **Esperado:** todas las pruebas en verde (`Superado: 64, Con error: 0`).
4. **Si falla:** copien el nombre de la prueba y el mensaje, y la salida de `dotnet --version`.

### A2. Pruebas en Unity (EditMode)

1. Abran el proyecto con Unity 6000.3.20f1. La primera vez descarga los paquetes (incluido `com.unity.services.cloudcode` 2.10.4).
2. `Window → General → Test Runner → EditMode → Run All`.
3. **Esperado:** todo en verde, incluida la clase `DuelOnlineEngineTests`.
4. **Si falla:** capturen la ventana del Test Runner y el error de la consola. Si el error es de compilación, copien la primera línea roja entera.

### A3. Una prueba "tramposa" que escriben ustedes (ejercicio)

Quiero que **rompan** el motor a propósito y comprueben que las pruebas lo detectan:

1. En `DuelOnlineEngine.SubmitSequence`, comenten la comprobación de idempotencia (`record.ProcessedRequestIds.Contains(requestId)`).
2. Corran `dotnet test`.
3. **Esperado:** falla `Submit_ReplayOfAnAppliedRequest_IsReplayedEvenAfterTheRoundMovedOn`.
4. Reporten: ¿qué pasaría en el juego si ese cambio llegara a producción? (pista: un reintento por mala red contaría dos veces). Deshagan el cambio.

## B. Pruebas contra UGS (necesitan su proyecto)

### B1. Desplegar el módulo

1. Vinculen el proyecto a **su** proyecto de UGS (`Edit → Project Settings → Services`) y elijan un entorno.
2. En el Dashboard de UGS habiliten **Authentication**, **Cloud Code** y **Cloud Save** de ese entorno.
3. `Window → Deployment`: busquen `Assets/CloudCode/DuelModule.ccmr`, tilden y *Deploy Selected*. Si no aparece, instalen `com.unity.services.deployment`.
4. **Esperado:** el despliegue termina sin errores y, en el Dashboard → Cloud Code, aparece el módulo `DuelModule` con 5 funciones: `CreateDuel`, `JoinDuel`, `SubmitSequence`, `GetDuel`, `ClaimVictory`.
5. **Si falla:** capturen el error de la ventana de Deployment y la lista de módulos del Dashboard. Anoten si el paso 3 no mostró el archivo.

### B2. Guion de dos jugadores

Necesitan dos sesiones distintas (dos Editores, un Editor y un build, o el ejercicio de la tarea 3). Para cada paso anoten **lo que obtuvieron** en la última columna.

| # | Quién | Acción | Resultado esperado |
|---|---|---|---|
| 1 | A | `CreateDuel` | `Result = applied`, `MatchCode` de 6 caracteres, `Phase = waiting_for_rival` |
| 2 | A | Repetir `CreateDuel` con **el mismo** `requestId` | `Result = replayed` y **el mismo** `MatchCode` |
| 3 | B | `JoinDuel` con el código | `applied`, `Phase = in_progress`, `YourHp = RivalHp = 100` |
| 4 | A | `SubmitSequence` `[Guerrero, Guerrero, Asesino]` en la ronda 1 | `applied`, `YouHaveSubmitted = true`, `RivalHasSubmitted = false` |
| 5 | B | `GetDuel` | `RivalHasSubmitted = true` y `YourPendingSequence` **vacía** (B no debe ver qué envió A) |
| 6 | A | `SubmitSequence` otra vez con otras jugadas | `already_submitted`; la secuencia de A no cambia |
| 7 | B | `SubmitSequence` `[Maga, Maga, Asesino]` | `applied`; la ronda se resuelve: `Round = 2` y `LastRound` con 3 choques |
| 8 | A y B | `GetDuel` | Los dos ven los mismos 3 choques, cada uno desde su lado (`YourMove`/`RivalMove` invertidos) |
| 9 | A | Repetir el paso 4 con su `requestId` original | `replayed`; el HP no cambia |
| 10 | A | `ClaimVictory` justo después de enviar | `claim_not_available` (no pasaron 10 minutos) |
| 11 | Un tercero | `GetDuel` con el código de A y B | `not_a_participant` y **ningún dato** de la partida |

**Si algo no coincide:** reporten el número de paso, lo esperado, lo obtenido y el `Result` completo del `DuelView`. En Dashboard → Cloud Code → Logs aparecen las trazas del módulo (con el `playerId`).

### B3. Reclamar victoria (prueba larga)

1. Repitan los pasos 1 a 4 y **no** envíen nada con B durante 10 minutos.
2. Pasado ese tiempo, A llama `ClaimVictory`.
3. **Esperado:** `applied`, `Phase = finished`, `Winner = you`, `EndReason = ABANDONO`. Para B, `Winner = rival`.
4. **Pregunta para el informe:** ¿qué pasa si el cliente de A **adelanta su reloj** y llama a `ClaimVictory` antes? (debería responder `claim_not_available`: el reloj que cuenta es el del servidor).

### B4. Qué reportar siempre

La versión de Unity, el entorno de UGS (nombre, no el ID), la fecha y hora aproximada, el paso, lo esperado y lo obtenido, y una captura o el texto de la consola. **No** peguen tokens ni claves de cuentas de servicio.

## C. Tareas para seguir desarrollando (repartidas)

El PR deja la **autoridad** lista; falta que el juego la use. Las tareas están pensadas para que cada integrante aporte código propio con su rama y su PR. Es una sugerencia de reparto, ajústenla entre ustedes.

### Tarea 1 — Piero: crear y unirse a un duelo desde el Hub

**Objetivo:** que `DuelHub` pueda crear un duelo online y unirse a otro con un código, usando `IDuelOnlineService`.

- Añade al Hub un botón **Crear duelo online** que llame a `CreateDuelAsync` y muestre el código (`MatchCode`) para compartirlo.
- Añade un campo de texto y un botón **Unirse** que llame a `JoinDuelAsync`.
- Si falla (sin red o sin proyecto vinculado), muestra el mensaje de `DuelOnlineException` y deja el duelo local contra el bot funcionando, como ya hace el proyecto.
- Guarda el código de la partida en `PlayerPrefs` para poder volver a ella al reabrir la app (reconexión): con el código basta llamar a `GetDuel`.
- **Criterio de hecho:** dos instancias del juego pasan de "crear" y "unirse" a la pantalla de combate con el mismo duelo. Documenta cómo lo probaste en `docs/PRUEBAS.md`.
- **Pista:** mira cómo el servicio custodia el `requestId` y por qué `CreateDuelAsync` no genera uno nuevo en cada reintento.

### Tarea 2 — James: combate contra el servidor, con animación

**Objetivo:** que `DuelCombat` envíe las tres jugadas al servidor y anime lo que el servidor resolvió.

- Al fijar las tres ranuras, llama a `SubmitSequenceAsync` y pasa a "esperando al rival".
- Reacciona al push (`ConsumeRefreshRequest()`) y sondea `RefreshAsync` cada pocos segundos como respaldo.
- Cuando `DuelView.LastRound` cambie, **anima los tres choques con esos datos** (`Clashes`) sin recalcular nada en el cliente. Aquí encaja la tarea pendiente de la animación choque a choque.
- **Criterio de hecho:** los dos jugadores ven la misma secuencia de choques y el mismo HP final.
- **Pregunta para el informe:** ¿qué parte de `DuelCombat` hoy decide cosas que ahora debe decidir el servidor? Lista cada una.

### Tarea 3 — Juan: despliegue, limpieza y la prueba de dos cuentas

**Objetivo:** que el módulo funcione de verdad en un entorno de UGS y quede robusto.

- Despliega el módulo (B1) y completa el guion B2 con dos cuentas. Para no depender de dos máquinas puede servir un script de Editor que use perfiles distintos de Authentication.
- Añade un *trigger* programado que borre los duelos terminados hace más de N días (hoy quedan para siempre en Cloud Save).
- Mueve la ventana de "reclamar victoria" a una configuración del servidor (Remote Config): el cliente **no** debe poder decidirla, ni siquiera en el modo demo de Settings.
- Revisa los PRs de Piero y James.
- **Criterio de hecho:** el guion B2 entero con los resultados esperados, y un informe de lo que no coincidió.

### Para los tres

- Una **rama y un PR por integrante**, con mensajes de commit descriptivos y en inglés (como pide el `CLAUDE.md` del repo).
- Cada PR debe actualizar este archivo con la prueba que permite verificarlo.
- Antes de pedir revisión, corran `dotnet test` y el Test Runner de Unity.

## D. Preguntas para discutir en el equipo

1. ¿Por qué el estado del duelo no puede vivir en Cloud Save Player Data? (pista: clase de acceso Default).
2. El push no lleva jugadas. ¿Qué información filtraría si las llevara?
3. ¿Por qué el servidor comprueba el `requestId` **antes** que la ronda?
4. ¿Qué pasa si dos jugadores envían su secuencia en el mismo segundo? ¿Quién evita que una pise a la otra?
5. ¿Por qué "reclamar victoria" no se resuelve solo y requiere una llamada explícita?
