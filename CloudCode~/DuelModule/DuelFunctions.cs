using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TTTXO.Core.Duel;
using TTTXO.Core.Duel.Online;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Proto = TTTXO.Core.Duel.Online.DuelOnlineProtocol;

namespace DuelModule
{
    /// <summary>
    /// Cloud Code endpoints of the online duel. The client only sends intentions ("create", "join",
    /// "these are my three moves", "claim victory"); everything else - who is in the duel, when a round
    /// closes, the damage, the winner, whether the rival was silent long enough - is decided here by
    /// <see cref="DuelOnlineEngine"/>, which runs the project's own <c>DuelResolver</c> and <c>DuelTimeout</c>.
    ///
    /// Messaging, as Cloud Code offers it: the caller always gets the new state as the function result,
    /// and the rival gets a push message (<see cref="IPushClient"/>, type <see cref="Proto.PushMessageType"/>)
    /// that only says "fetch your duel". A push can be lost, so the client also polls <c>GetDuel</c>.
    /// </summary>
    public class DuelFunctions
    {
        private const int MaxSaveAttempts = 4;
        private const int MaxRequestIdLength = 64;
        private const int MaxMovesLength = 8;

        private readonly ILogger<DuelFunctions> _logger;

        public DuelFunctions(ILogger<DuelFunctions> logger)
        {
            _logger = logger;
        }

        [CloudCodeFunction(Proto.Function.CreateDuel)]
        public async Task<DuelView> CreateDuel(IExecutionContext context, IGameApiClient gameApiClient, string requestId, string displayName)
        {
            var playerId = context.PlayerId;
            if (string.IsNullOrEmpty(playerId) || !IsValidRequestId(requestId))
                return Rejected(null, Proto.Result.InvalidArgument);

            // The code is derived from (player, requestId): a retried create finds the same duel
            // instead of opening a second one.
            var code = DeriveCode(playerId, requestId);
            var existing = await DuelStore.LoadAsync(context, gameApiClient, code);
            var now = DateTimeOffset.UtcNow;

            if (existing != null)
            {
                var isRetry = existing.Record.PlayerIds.Count > 0
                    && existing.Record.PlayerIds[0] == playerId
                    && existing.Record.ProcessedRequestIds.Contains(requestId);

                // Anything else is a code collision with somebody else's duel: never overwrite it.
                return isRetry
                    ? DuelOnlineEngine.BuildView(existing.Record, playerId, now, Proto.Result.Replayed, DuelTimeout.Default)
                    : Rejected(code, Proto.Result.Conflict);
            }

            var record = DuelOnlineEngine.Create(code, playerId, displayName, requestId, now);
            try
            {
                await DuelStore.SaveAsync(context, gameApiClient, record, writeLock: null);
            }
            catch (WriteConflictException)
            {
                return Rejected(code, Proto.Result.Conflict);
            }

            _logger.LogInformation("Duel {Code} created.", code);
            return DuelOnlineEngine.BuildView(record, playerId, now, Proto.Result.Applied, DuelTimeout.Default);
        }

        [CloudCodeFunction(Proto.Function.JoinDuel)]
        public Task<DuelView> JoinDuel(
            IExecutionContext context, IGameApiClient gameApiClient, IPushClient pushClient,
            string matchCode, string requestId, string displayName)
        {
            if (!IsValidRequestId(requestId))
                return Task.FromResult(Rejected(null, Proto.Result.InvalidArgument));

            return ApplyAsync(context, gameApiClient, pushClient, matchCode, allowOutsider: true,
                (record, playerId, now) => DuelOnlineEngine.Join(record, playerId, displayName, requestId, now));
        }

        /// <param name="moves">The player's three moves for the round, in order: "Guerrero", "Maga" or "Asesino".</param>
        [CloudCodeFunction(Proto.Function.SubmitSequence)]
        public Task<DuelView> SubmitSequence(
            IExecutionContext context, IGameApiClient gameApiClient, IPushClient pushClient,
            string matchCode, List<string> moves, int expectedRound, string requestId)
        {
            if (!IsValidRequestId(requestId) || moves == null || moves.Count > MaxMovesLength)
                return Task.FromResult(Rejected(null, Proto.Result.InvalidArgument));

            return ApplyAsync(context, gameApiClient, pushClient, matchCode, allowOutsider: false,
                (record, playerId, now) => DuelOnlineEngine.SubmitSequence(record, playerId, moves, expectedRound, requestId, now));
        }

        /// <summary>Read-only for the caller, but it still applies the clock first.</summary>
        [CloudCodeFunction(Proto.Function.GetDuel)]
        public Task<DuelView> GetDuel(
            IExecutionContext context, IGameApiClient gameApiClient, IPushClient pushClient, string matchCode)
        {
            return ApplyAsync(context, gameApiClient, pushClient, matchCode, allowOutsider: false,
                (record, playerId, now) => new DuelEngineResult { Result = Proto.Result.Applied });
        }

        /// <summary>"Reclamar victoria": the 10-minute window is measured here, with the server clock.</summary>
        [CloudCodeFunction(Proto.Function.ClaimVictory)]
        public Task<DuelView> ClaimVictory(
            IExecutionContext context, IGameApiClient gameApiClient, IPushClient pushClient, string matchCode, string requestId)
        {
            if (!IsValidRequestId(requestId))
                return Task.FromResult(Rejected(null, Proto.Result.InvalidArgument));

            return ApplyAsync(context, gameApiClient, pushClient, matchCode, allowOutsider: false,
                (record, playerId, now) => DuelOnlineEngine.ClaimVictory(record, playerId, requestId, now, DuelTimeout.Default));
        }

        /// <summary>
        /// Read -> apply the clock -> apply the action -> save with the write lock, retrying from the
        /// read when another call won the race. Safe to repeat because every action is idempotent by requestId.
        /// </summary>
        private async Task<DuelView> ApplyAsync(
            IExecutionContext context, IGameApiClient gameApiClient, IPushClient pushClient,
            string matchCode, bool allowOutsider,
            Func<DuelRecord, string, DateTimeOffset, DuelEngineResult> action)
        {
            var playerId = context.PlayerId;
            var code = DuelOnlineEngine.NormalizeCode(matchCode);
            if (string.IsNullOrEmpty(playerId) || code == null)
                return Rejected(code, Proto.Result.InvalidArgument);

            DuelView lastView = null;

            for (var attempt = 0; attempt < MaxSaveAttempts; attempt++)
            {
                var stored = await DuelStore.LoadAsync(context, gameApiClient, code);
                if (stored == null)
                    return Rejected(code, Proto.Result.NotFound);

                var record = stored.Record;
                var now = DateTimeOffset.UtcNow;

                if (DuelOnlineEngine.SeatOf(record, playerId) < 0 && !allowOutsider)
                {
                    return DuelOnlineEngine.BuildView(record, playerId, now, Proto.Result.NotAParticipant, DuelTimeout.Default);
                }

                var tick = DuelOnlineEngine.Tick(record, now);
                var result = action(record, playerId, now);

                var notices = new List<DuelNotice>(tick.Notices);
                notices.AddRange(result.Notices);

                if (tick.Changed || result.Changed)
                {
                    try
                    {
                        await DuelStore.SaveAsync(context, gameApiClient, record, stored.WriteLock);
                    }
                    catch (WriteConflictException)
                    {
                        _logger.LogInformation("Duel {Code}: write conflict on attempt {Attempt}, retrying.", code, attempt + 1);
                        lastView = DuelOnlineEngine.BuildView(record, playerId, now, Proto.Result.Conflict, DuelTimeout.Default);
                        continue;
                    }

                    await NotifyAsync(context, pushClient, record, playerId, notices);
                }

                return DuelOnlineEngine.BuildView(record, playerId, now, result.Result, DuelTimeout.Default);
            }

            // Every attempt lost the race: tell the client to fetch the duel again.
            return lastView ?? Rejected(code, Proto.Result.Conflict);
        }

        /// <summary>
        /// Pushes a lightweight "duel updated" message to the other player. The payload never carries
        /// a move: whoever receives it fetches their own view. Failing to push never fails the call -
        /// the rival's polling covers it.
        /// </summary>
        private async Task NotifyAsync(
            IExecutionContext context, IPushClient pushClient, DuelRecord record, string callerPlayerId, List<DuelNotice> notices)
        {
            foreach (var notice in notices.GroupBy(n => n.Seat).Select(g => g.Last()))
            {
                if (notice.Seat < 0 || notice.Seat >= record.PlayerIds.Count)
                    continue;

                var targetPlayerId = record.PlayerIds[notice.Seat];
                if (targetPlayerId == callerPlayerId)
                    continue;

                var payload = new DuelPushPayload
                {
                    MatchCode = record.Code,
                    Event = notice.Event,
                    Round = record.Round,
                    Phase = record.Phase
                };

                try
                {
                    await pushClient.SendPlayerMessageAsync(
                        context, JsonSerializer.Serialize(payload), Proto.PushMessageType, targetPlayerId);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Duel {Code}: push to the rival failed, polling will cover it.", record.Code);
                }
            }
        }

        private static bool IsValidRequestId(string requestId)
        {
            return !string.IsNullOrWhiteSpace(requestId) && requestId.Length <= MaxRequestIdLength;
        }

        private static DuelView Rejected(string code, string result)
        {
            return new DuelView
            {
                ProtocolVersion = Proto.Version,
                Result = result,
                MatchCode = code,
                Phase = Proto.Phase.Finished,
                YourSeat = -1,
                ServerTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        /// <summary>6 characters of the code alphabet from SHA-256(playerId|requestId). 32 symbols: no modulo bias.</summary>
        internal static string DeriveCode(string playerId, string requestId)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(playerId + "|" + requestId));
            var chars = new char[Proto.MatchCodeLength];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = Proto.CodeAlphabet[hash[i] & 31];

            return new string(chars);
        }
    }
}
