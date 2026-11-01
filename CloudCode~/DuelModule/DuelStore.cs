using System.Net;
using System.Text.Json;
using TTTXO.Core.Duel.Online;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace DuelModule
{
    /// <summary>Raised when another call changed the duel between our read and our write.</summary>
    public sealed class WriteConflictException : Exception
    {
        public WriteConflictException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>A duel as stored, plus the write lock Cloud Save handed us when we read it.</summary>
    public sealed class StoredDuel
    {
        public DuelRecord Record { get; set; }
        public string WriteLock { get; set; }
    }

    /// <summary>
    /// Persists duels in Cloud Save "custom data" (one document per duel, id = "duel-CODE"), read and
    /// written with the service token, i.e. only from Cloud Code. This is the point of the module: the
    /// player-data copy that the client keeps (Cloud Save Player Data, class Default) can be edited by
    /// the player with their own token, so it can never be the source of truth for HP or the winner.
    ///
    /// Both players write the same document in the same round, so every save carries the write lock
    /// from the read. If the lock no longer matches, Cloud Save rejects the write (HTTP 409 Conflict)
    /// and the caller re-reads and re-applies instead of overwriting the other player's sequence.
    /// </summary>
    public static class DuelStore
    {
        private const string StateKey = "state";
        private const string DocumentPrefix = "duel-";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        /// <summary>Only ever built from a validated code, never from raw client input.</summary>
        public static string DocumentId(string code) => DocumentPrefix + code;

        public static async Task<StoredDuel> LoadAsync(IExecutionContext context, IGameApiClient gameApiClient, string code)
        {
            var response = await gameApiClient.CloudSaveData.GetCustomItemsAsync(
                context, context.ServiceToken, context.ProjectId, DocumentId(code), new List<string> { StateKey });

            var item = response.Data.Results.FirstOrDefault(i => i.Key == StateKey);
            if (item == null)
                return null;

            var json = item.Value is JsonElement element ? element.GetString() ?? element.GetRawText() : item.Value?.ToString();
            if (string.IsNullOrEmpty(json))
                return null;

            return new StoredDuel
            {
                Record = JsonSerializer.Deserialize<DuelRecord>(json, JsonOptions),
                WriteLock = item.WriteLock
            };
        }

        /// <summary>Saves the record. <paramref name="writeLock"/> is the lock from <see cref="LoadAsync"/>, or null when creating.</summary>
        public static async Task SaveAsync(IExecutionContext context, IGameApiClient gameApiClient, DuelRecord record, string writeLock)
        {
            var json = JsonSerializer.Serialize(record);
            var body = writeLock == null
                ? new SetItemBody(StateKey, json)
                : new SetItemBody(StateKey, json, writeLock);

            try
            {
                await gameApiClient.CloudSaveData.SetCustomItemAsync(
                    context, context.ServiceToken, context.ProjectId, DocumentId(record.Code), body);
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.Conflict)
            {
                throw new WriteConflictException($"Duel {record.Code} was changed by another call.", e);
            }
        }
    }
}
