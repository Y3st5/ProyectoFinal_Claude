using System;
using System.Collections.Generic;
using Proto = TTTXO.Core.Duel.Online.DuelOnlineProtocol;

namespace TTTXO.Core.Duel.Online
{
    /// <summary>One clash of the last closed round as stored on the server (seat 0 = creator, seat 1 = joiner).</summary>
    public sealed class DuelClashRecord
    {
        public int Slot { get; set; }
        public string MoveSeat0 { get; set; }
        public string MoveSeat1 { get; set; }
        /// <summary>Seat that won the clash, or -1 for a draw.</summary>
        public int WinnerSeat { get; set; } = -1;
        public int Damage { get; set; }
    }

    public sealed class DuelLastRoundRecord
    {
        public int Round { get; set; }
        public List<DuelClashRecord> Clashes { get; set; } = new List<DuelClashRecord>();
        public int DamageToSeat0 { get; set; }
        public int DamageToSeat1 { get; set; }
    }

    /// <summary>
    /// A duel as the module persists it. It carries hidden information (<see cref="Sequence0"/> and
    /// <see cref="Sequence1"/> are pending moves) and must never be sent to a client as is: use
    /// <see cref="DuelOnlineEngine.BuildView"/>.
    ///
    /// It is a plain properties-only record on purpose (System.Text.Json ignores public fields, which
    /// is what the shared <see cref="DuelStateDto"/> uses); the engine converts to a
    /// <see cref="DuelStateDto"/> to run the rules and copies the result back.
    /// </summary>
    public sealed class DuelRecord
    {
        public string Code { get; set; }
        public string Phase { get; set; } = Proto.Phase.WaitingForRival;
        public List<string> PlayerIds { get; set; } = new List<string>();
        public List<string> PlayerNames { get; set; } = new List<string>();
        public int Round { get; set; } = 1;
        public int[] Hp { get; set; } = { DuelRules.MaxHp, DuelRules.MaxHp };
        public string[] Sequence0 { get; set; } = EmptySequence();
        public string[] Sequence1 { get; set; } = EmptySequence();
        public string LastMoveTimestamp { get; set; } = string.Empty;
        public string WinnerId { get; set; } = string.Empty;
        public string EndReason { get; set; } = string.Empty;
        public long CreatedAtUnix { get; set; }
        public DuelLastRoundRecord LastRound { get; set; }
        public List<string> ProcessedRequestIds { get; set; } = new List<string>();

        public static string[] EmptySequence() => new[] { string.Empty, string.Empty, string.Empty };
    }

    /// <summary>Tells the module whom to push a notification to once the record is saved.</summary>
    public sealed class DuelNotice
    {
        public DuelNotice(int seat, string pushEvent)
        {
            Seat = seat;
            Event = pushEvent;
        }

        public int Seat { get; }
        public string Event { get; }
    }

    public sealed class DuelEngineResult
    {
        public string Result { get; set; }
        /// <summary>True when the record changed and must be saved.</summary>
        public bool Changed { get; set; }
        public List<DuelNotice> Notices { get; } = new List<DuelNotice>();
    }

    /// <summary>
    /// The authoritative online rules of the duel, as a pure state machine with no I/O, no clock and
    /// no Unity: every call receives "now". The rules themselves are the ones the project already
    /// has - <see cref="DuelResolver"/> (3 clashes, 15 damage, KO / round limit), <see cref="DuelTimeout"/>
    /// ("reclamar victoria") and <see cref="DuelMoveCodec"/> - this class only adds who is allowed to
    /// do what, in which order, and what each player may see.
    ///
    /// Order of checks in every player action: arguments -> membership -> idempotency (requestId) ->
    /// phase -> round -> rules. A retried request is answered as "replayed" before anything can reject
    /// it for being "stale".
    /// </summary>
    public static class DuelOnlineEngine
    {
        private const int MaxNameLength = 24;

        public static DuelRecord Create(string code, string playerId, string displayName, string requestId, DateTimeOffset now)
        {
            var record = new DuelRecord
            {
                Code = code,
                Phase = Proto.Phase.WaitingForRival,
                CreatedAtUnix = now.ToUnixTimeSeconds(),
                LastMoveTimestamp = DuelTimestamp.ToIso(now)
            };
            record.PlayerIds.Add(playerId);
            record.PlayerNames.Add(CleanName(displayName));
            Remember(record, requestId);
            return record;
        }

        /// <summary>Uppercases and validates a match code. Returns null when it cannot be a code.</summary>
        public static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var normalized = code.Trim().ToUpperInvariant();
            if (normalized.Length != Proto.MatchCodeLength)
                return null;

            foreach (var c in normalized)
            {
                if (Proto.CodeAlphabet.IndexOf(c) < 0)
                    return null;
            }

            return normalized;
        }

        public static int SeatOf(DuelRecord record, string playerId)
        {
            return string.IsNullOrEmpty(playerId) ? -1 : record.PlayerIds.IndexOf(playerId);
        }

        /// <summary>
        /// Applies what the clock decides on its own: a duel nobody joined expires. (An absent rival is
        /// not resolved here: "reclamar victoria" is an explicit claim by the player who is waiting.)
        /// Cloud Code has no timers, so every call runs this first.
        /// </summary>
        public static DuelEngineResult Tick(DuelRecord record, DateTimeOffset now)
        {
            var result = new DuelEngineResult { Result = Proto.Result.Applied };

            if (record.Phase == Proto.Phase.WaitingForRival
                && now.ToUnixTimeSeconds() - record.CreatedAtUnix > Proto.WaitingForRivalSeconds)
            {
                record.Phase = Proto.Phase.Finished;
                record.EndReason = Proto.FinishReason.Expired;
                record.LastMoveTimestamp = DuelTimestamp.ToIso(now);
                result.Changed = true;
            }

            return result;
        }

        public static DuelEngineResult Join(DuelRecord record, string playerId, string displayName, string requestId, DateTimeOffset now)
        {
            var result = new DuelEngineResult();

            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(requestId))
                return Reject(result, Proto.Result.InvalidArgument);

            var seat = SeatOf(record, playerId);
            if (seat >= 0)
            {
                // The creator "joining" their own duel is a mistake; the joiner repeating the request is a retry.
                if (seat == 1 && record.ProcessedRequestIds.Contains(requestId))
                    return Reject(result, Proto.Result.Replayed);

                return Reject(result, Proto.Result.WrongState);
            }

            if (record.Phase != Proto.Phase.WaitingForRival || record.PlayerIds.Count >= 2)
                return Reject(result, Proto.Result.WrongState);

            record.PlayerIds.Add(playerId);
            record.PlayerNames.Add(CleanName(displayName));
            record.Phase = Proto.Phase.InProgress;
            record.Round = 1;
            record.LastMoveTimestamp = DuelTimestamp.ToIso(now);
            Remember(record, requestId);

            result.Result = Proto.Result.Applied;
            result.Changed = true;
            result.Notices.Add(new DuelNotice(0, Proto.PushEvent.RivalJoined));
            return result;
        }

        /// <summary>
        /// Locks in the player's three moves for the open round, hidden. When the second sequence
        /// arrives the round is resolved with <see cref="DuelResolver"/>.
        /// </summary>
        public static DuelEngineResult SubmitSequence(
            DuelRecord record, string playerId, IReadOnlyList<string> moves, int expectedRound, string requestId, DateTimeOffset now)
        {
            var result = new DuelEngineResult();

            if (string.IsNullOrEmpty(requestId) || !TryParseSequence(moves, out var parsed))
                return Reject(result, Proto.Result.InvalidArgument);

            var seat = SeatOf(record, playerId);
            if (seat < 0)
                return Reject(result, Proto.Result.NotAParticipant);

            if (record.ProcessedRequestIds.Contains(requestId))
                return Reject(result, Proto.Result.Replayed);

            if (record.Phase != Proto.Phase.InProgress)
                return Reject(result, Proto.Result.WrongState);

            if (expectedRound != record.Round)
                return Reject(result, Proto.Result.StaleRound);

            var dto = ToDto(record);
            if (!dto.CanSubmit(playerId))
                return Reject(result, Proto.Result.AlreadySubmitted);

            if (!dto.PlayerById(playerId).TrySetSequence(parsed, out _))
                return Reject(result, Proto.Result.InvalidArgument);

            dto.timestamp_ultimo_movimiento = DuelTimestamp.ToIso(now);
            Remember(record, requestId);
            result.Result = Proto.Result.Applied;
            result.Changed = true;

            var rivalSeat = 1 - seat;
            if (!DuelResolver.CanResolve(dto))
            {
                CopyBack(dto, record);
                result.Notices.Add(new DuelNotice(rivalSeat, Proto.PushEvent.RivalSubmitted));
                return result;
            }

            var round = DuelResolver.Resolve(dto, now);
            CopyBack(dto, record);
            record.LastRound = ToRecord(round);
            result.Notices.Add(new DuelNotice(rivalSeat, dto.IsFinished ? Proto.PushEvent.Finished : Proto.PushEvent.RoundResolved));
            return result;
        }

        /// <summary>
        /// "Reclamar victoria": legal only for the player who already played this round, when the rival
        /// has been silent for the whole <paramref name="timeout"/>, measured with the server clock.
        /// </summary>
        public static DuelEngineResult ClaimVictory(DuelRecord record, string playerId, string requestId, DateTimeOffset now, TimeSpan timeout)
        {
            var result = new DuelEngineResult();

            if (string.IsNullOrEmpty(requestId))
                return Reject(result, Proto.Result.InvalidArgument);

            var seat = SeatOf(record, playerId);
            if (seat < 0)
                return Reject(result, Proto.Result.NotAParticipant);

            if (record.ProcessedRequestIds.Contains(requestId))
                return Reject(result, Proto.Result.Replayed);

            if (record.Phase != Proto.Phase.InProgress)
                return Reject(result, Proto.Result.WrongState);

            var dto = ToDto(record);
            if (!DuelTimeout.TryClaimVictory(dto, playerId, now, timeout))
                return Reject(result, Proto.Result.ClaimNotAvailable);

            Remember(record, requestId);
            CopyBack(dto, record);
            result.Result = Proto.Result.Applied;
            result.Changed = true;
            result.Notices.Add(new DuelNotice(1 - seat, Proto.PushEvent.Finished));
            return result;
        }

        /// <summary>Builds what <paramref name="playerId"/> may see. Never includes the rival's pending sequence.</summary>
        public static DuelView BuildView(DuelRecord record, string playerId, DateTimeOffset now, string result, TimeSpan claimTimeout)
        {
            var seat = SeatOf(record, playerId);
            var view = new DuelView
            {
                ProtocolVersion = Proto.Version,
                Result = result,
                MatchCode = record.Code,
                ServerTimeUnix = now.ToUnixTimeSeconds()
            };

            if (seat < 0)
            {
                // Not a participant: confirm nothing about the duel.
                view.Phase = Proto.Phase.Finished;
                view.YourSeat = -1;
                return view;
            }

            var rivalSeat = 1 - seat;
            var mine = seat == 0 ? record.Sequence0 : record.Sequence1;
            var theirs = seat == 0 ? record.Sequence1 : record.Sequence0;

            view.Phase = record.Phase;
            view.Round = record.Round;
            view.YourSeat = seat;
            view.YourName = record.PlayerNames[seat];
            view.RivalJoined = record.PlayerIds.Count >= 2;
            view.RivalName = view.RivalJoined ? record.PlayerNames[rivalSeat] : string.Empty;
            view.YourHp = record.Hp[seat];
            view.RivalHp = record.Hp[rivalSeat];
            view.EndReason = record.EndReason;

            if (record.Phase == Proto.Phase.InProgress)
            {
                view.YouHaveSubmitted = IsFull(mine);
                view.RivalHasSubmitted = IsFull(theirs);
                if (view.YouHaveSubmitted)
                    view.YourPendingSequence = new List<string>(mine);

                var dto = ToDto(record);
                view.EstadoPartida = dto.StateFor(playerId);

                if (view.YouHaveSubmitted && !view.RivalHasSubmitted
                    && DuelTimestamp.TryParse(record.LastMoveTimestamp, out var last))
                {
                    view.ClaimAvailableAtUnix = (last + claimTimeout).ToUnixTimeSeconds();
                }
            }
            else
            {
                view.EstadoPartida = record.Phase == Proto.Phase.Finished
                    ? DuelRules.StateFinished
                    : DuelRules.StateWaitingRival;
            }

            if (record.LastRound != null)
                view.LastRound = ToRoundView(record.LastRound, seat);

            if (record.Phase == Proto.Phase.Finished && !string.IsNullOrEmpty(record.WinnerId))
                view.Winner = record.WinnerId == playerId ? Proto.ClashOutcome.You : Proto.ClashOutcome.Rival;

            return view;
        }

        // ---- conversions ---------------------------------------------------------------------------

        /// <summary>The record as the shared document the rules work on.</summary>
        public static DuelStateDto ToDto(DuelRecord record)
        {
            var dto = new DuelStateDto
            {
                match_id = record.Code,
                ronda_actual = record.Round,
                timestamp_ultimo_movimiento = record.LastMoveTimestamp ?? string.Empty,
                ganador = record.WinnerId ?? string.Empty,
                motivo_fin = record.EndReason ?? string.Empty,
                estado_partida = record.Phase == Proto.Phase.Finished ? DuelRules.StateFinished : DuelRules.StateYourTurn,
                jugador1 = new DuelPlayerDto
                {
                    id = record.PlayerIds[0],
                    nombre = record.PlayerNames[0],
                    hp = record.Hp[0],
                    secuencia_actual = (string[])record.Sequence0.Clone()
                },
                jugador2 = new DuelPlayerDto
                {
                    id = record.PlayerIds.Count > 1 ? record.PlayerIds[1] : string.Empty,
                    nombre = record.PlayerNames.Count > 1 ? record.PlayerNames[1] : string.Empty,
                    hp = record.Hp[1],
                    secuencia_actual = (string[])record.Sequence1.Clone()
                }
            };

            dto.Normalize();
            return dto;
        }

        private static void CopyBack(DuelStateDto dto, DuelRecord record)
        {
            record.Round = dto.ronda_actual;
            record.Hp = new[] { dto.jugador1.hp, dto.jugador2.hp };
            record.Sequence0 = (string[])dto.jugador1.secuencia_actual.Clone();
            record.Sequence1 = (string[])dto.jugador2.secuencia_actual.Clone();
            record.LastMoveTimestamp = dto.timestamp_ultimo_movimiento;
            record.WinnerId = dto.ganador;
            record.EndReason = dto.motivo_fin;
            if (dto.IsFinished)
                record.Phase = Proto.Phase.Finished;
        }

        private static DuelLastRoundRecord ToRecord(DuelRoundResult round)
        {
            var last = new DuelLastRoundRecord
            {
                Round = round.RoundNumber,
                DamageToSeat0 = round.DamageToPlayerOne,
                DamageToSeat1 = round.DamageToPlayerTwo
            };

            foreach (var clash in round.Clashes)
            {
                last.Clashes.Add(new DuelClashRecord
                {
                    Slot = clash.Slot,
                    MoveSeat0 = DuelMoveCodec.ToCode(clash.PlayerOneMove),
                    MoveSeat1 = DuelMoveCodec.ToCode(clash.PlayerTwoMove),
                    WinnerSeat = clash.Winner == null ? -1 : (clash.Winner == RpsPlayer.One ? 0 : 1),
                    Damage = clash.Damage
                });
            }

            return last;
        }

        private static DuelRoundView ToRoundView(DuelLastRoundRecord last, int seat)
        {
            var view = new DuelRoundView
            {
                Round = last.Round,
                DamageToYou = seat == 0 ? last.DamageToSeat0 : last.DamageToSeat1,
                DamageToRival = seat == 0 ? last.DamageToSeat1 : last.DamageToSeat0
            };

            foreach (var clash in last.Clashes)
            {
                view.Clashes.Add(new DuelClashView
                {
                    Slot = clash.Slot,
                    YourMove = seat == 0 ? clash.MoveSeat0 : clash.MoveSeat1,
                    RivalMove = seat == 0 ? clash.MoveSeat1 : clash.MoveSeat0,
                    Damage = clash.Damage,
                    Outcome = clash.WinnerSeat < 0
                        ? Proto.ClashOutcome.Draw
                        : (clash.WinnerSeat == seat ? Proto.ClashOutcome.You : Proto.ClashOutcome.Rival)
                });
            }

            return view;
        }

        /// <summary>Exactly three moves, each one a canonical code (Guerrero, Maga, Asesino).</summary>
        private static bool TryParseSequence(IReadOnlyList<string> moves, out List<RpsMove> parsed)
        {
            parsed = null;
            if (moves == null || moves.Count != DuelRules.SlotsPerRound)
                return false;

            var list = new List<RpsMove>(DuelRules.SlotsPerRound);
            foreach (var code in moves)
            {
                if (!DuelMoveCodec.TryParse(code, out var move))
                    return false;

                list.Add(move);
            }

            parsed = list;
            return true;
        }

        private static bool IsFull(string[] sequence)
        {
            if (sequence == null || sequence.Length != DuelRules.SlotsPerRound)
                return false;

            foreach (var code in sequence)
            {
                if (!DuelMoveCodec.TryParse(code, out _))
                    return false;
            }

            return true;
        }

        /// <summary>A name the rival will see: trimmed, no control characters, at most 24 characters.</summary>
        private static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Player";

            var chars = new List<char>();
            foreach (var c in name.Trim())
            {
                if (!char.IsControl(c))
                    chars.Add(c);
            }

            var cleaned = new string(chars.ToArray());
            if (cleaned.Length > MaxNameLength)
                cleaned = cleaned.Substring(0, MaxNameLength);

            return string.IsNullOrWhiteSpace(cleaned) ? "Player" : cleaned;
        }

        private static void Remember(DuelRecord record, string requestId)
        {
            record.ProcessedRequestIds.Add(requestId);
            while (record.ProcessedRequestIds.Count > Proto.MaxRememberedRequestIds)
                record.ProcessedRequestIds.RemoveAt(0);
        }

        private static DuelEngineResult Reject(DuelEngineResult result, string code)
        {
            result.Result = code;
            result.Changed = false;
            return result;
        }
    }
}
