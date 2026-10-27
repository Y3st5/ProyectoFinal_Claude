using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TTTXO.Core.Duel;
using TTTXO.Game.Duel;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

namespace TTTXO.Game.Services
{
    /// <summary>
    /// Cloud Save payload for one match. The duel itself stays the exact JSON contract from
    /// <see cref="DuelStateDto"/>; wrapping it in a one-field object lets us read it back with the
    /// SDK's proven <c>GetAs&lt;T&gt;()</c> path instead of depending on a raw-string accessor.
    /// </summary>
    [Serializable]
    public sealed class DuelCloudPayload
    {
        public string json;
    }

    /// <summary>Cloud Save payload for the player's list of match ids (one key instead of a query).</summary>
    [Serializable]
    public sealed class DuelIndexPayload
    {
        public List<string> matchIds = new();
    }

    /// <summary>
    /// Phase 4 of the flight plan: anonymous sign-in plus Cloud Save persistence for duel documents.
    ///
    /// The project is offline-first on purpose. <see cref="InitializeAsync"/> is best effort - if the
    /// project is not linked to a Unity project id, or the device is offline, every call returns
    /// false and the game keeps running against the local mirror. Nothing in the Hub blocks on the
    /// network.
    ///
    /// Scope note: this uses Cloud Save <b>Player Data</b>, so each account stores its own copy of
    /// its matches. Real player-vs-player duels need the document to live in Cloud Save game data
    /// (or be written by a Cloud Code module) so both players read the same record and the round
    /// resolution is authoritative; that is the next phase, not this one.
    /// </summary>
    public static class DuelCloudService
    {
        private const string IndexKey = "duelIndex";
        private const string MatchKeyPrefix = "duelMatch_";

        private static readonly System.Threading.SemaphoreSlim IndexLock = new(1, 1);

        /// <summary>
        /// Serializes document uploads. Two saves of the same match (submit, then resolve) are fired
        /// in order but their network round trips can finish out of order; the lock keeps the cloud
        /// copy from ending up on the older document.
        /// </summary>
        private static readonly System.Threading.SemaphoreSlim WriteLock = new(1, 1);

        public static bool IsReady { get; private set; }

        /// <summary>Unity Authentication player id, or empty while signed out.</summary>
        public static string PlayerId { get; private set; } = string.Empty;

        public static string LastError { get; private set; } = string.Empty;

        public static bool IsSignedIn => IsReady && AuthenticationService.Instance.IsSignedIn;

        /// <summary>Initializes UGS and signs in anonymously. Safe to call more than once.</summary>
        public static async Task<bool> InitializeAsync()
        {
            if (IsReady)
            {
                return true;
            }

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                PlayerId = AuthenticationService.Instance.PlayerId;
                IsReady = true;
                LastError = string.Empty;
                Debug.Log($"DuelCloudService: signed in anonymously as '{PlayerId}'.");
                return true;
            }
            catch (Exception ex)
            {
                IsReady = false;
                LastError = ex.Message;
                Debug.LogWarning($"DuelCloudService: online session unavailable, duels stay local - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Downloads every match the account has in the cloud and writes it into the local mirror.
        /// Returns how many documents were restored. Local documents that are not in the cloud are
        /// left untouched (they will be pushed on their next save).
        /// </summary>
        public static async Task<int> PullAsync(LocalDuelStore mirror)
        {
            if (!IsReady || mirror == null)
            {
                return 0;
            }

            await WriteLock.WaitAsync();
            try
            {
                var index = await LoadIndexAsync();
                if (index.Count == 0)
                {
                    return 0;
                }

                var keys = new HashSet<string>();
                foreach (var matchId in index)
                {
                    keys.Add(KeyFor(matchId));
                }

                var documents = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);
                int restored = 0;

                foreach (var matchId in index)
                {
                    if (!documents.TryGetValue(KeyFor(matchId), out var item))
                    {
                        continue;
                    }

                    var payload = item.Value.GetAs<DuelCloudPayload>();
                    if (payload == null || string.IsNullOrEmpty(payload.json))
                    {
                        continue;
                    }

                    if (!DuelStateSerializer.TryFromJson(payload.json, out var state, out var error))
                    {
                        Debug.LogWarning($"DuelCloudService: skipped cloud match '{matchId}' - {error}");
                        continue;
                    }

                    mirror.Save(state);
                    restored++;
                }

                return restored;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DuelCloudService: pull failed - {ex.Message}");
                return 0;
            }
            finally
            {
                WriteLock.Release();
            }
        }

        /// <summary>Uploads one document and makes sure its id is in the cloud index.</summary>
        public static async Task<bool> PushAsync(DuelStateDto state)
        {
            if (!IsReady || state == null)
            {
                return false;
            }

            // Serialize the document before awaiting so the caller can keep mutating its copy.
            var json = DuelStateSerializer.ToJson(state);
            var matchId = state.match_id;

            await WriteLock.WaitAsync();
            try
            {
                var data = new Dictionary<string, object>
                {
                    { KeyFor(matchId), new DuelCloudPayload { json = json } }
                };

                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                await AddToIndexAsync(matchId);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DuelCloudService: could not upload match '{matchId}' - {ex.Message}");
                return false;
            }
            finally
            {
                WriteLock.Release();
            }
        }

        public static async Task<bool> DeleteAsync(string matchId)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(matchId))
            {
                return false;
            }

            await WriteLock.WaitAsync();
            try
            {
                await CloudSaveService.Instance.Data.Player.DeleteAsync(
                    KeyFor(matchId), new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions());
                await RemoveFromIndexAsync(matchId);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DuelCloudService: could not delete match '{matchId}' - {ex.Message}");
                return false;
            }
            finally
            {
                WriteLock.Release();
            }
        }

        private static async Task<List<string>> LoadIndexAsync()
        {
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { IndexKey });
            if (!result.TryGetValue(IndexKey, out var item))
            {
                return new List<string>();
            }

            var index = item.Value.GetAs<DuelIndexPayload>();
            return index?.matchIds ?? new List<string>();
        }

        private static async Task AddToIndexAsync(string matchId)
        {
            await IndexLock.WaitAsync();
            try
            {
                var ids = await LoadIndexAsync();
                if (ids.Contains(matchId))
                {
                    return;
                }

                ids.Add(matchId);
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
                {
                    { IndexKey, new DuelIndexPayload { matchIds = ids } }
                });
            }
            finally
            {
                IndexLock.Release();
            }
        }

        private static async Task RemoveFromIndexAsync(string matchId)
        {
            await IndexLock.WaitAsync();
            try
            {
                var ids = await LoadIndexAsync();
                if (!ids.Remove(matchId))
                {
                    return;
                }

                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
                {
                    { IndexKey, new DuelIndexPayload { matchIds = ids } }
                });
            }
            finally
            {
                IndexLock.Release();
            }
        }

        private static string KeyFor(string matchId)
        {
            return MatchKeyPrefix + matchId;
        }
    }
}
