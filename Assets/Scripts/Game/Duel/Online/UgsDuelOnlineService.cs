using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TTTXO.Core;
using TTTXO.Core.Duel;
using TTTXO.Core.Duel.Online;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.Core;
using UnityEngine;

namespace TTTXO.Game.Duel.Online
{
    /// <summary>
    /// <see cref="IDuelOnlineService"/> over Unity Cloud Code. Two channels, both Cloud Code's own:
    /// <list type="bullet">
    /// <item>Calls: <c>CallModuleEndpointAsync</c> on the <c>DuelModule</c> module. Every call returns
    /// the new <see cref="DuelView"/>.</item>
    /// <item>Push messages: <c>SubscribeToPlayerMessagesAsync</c>. The server pushes a tiny
    /// notification to the rival after every change; here it only raises a flag that makes the screen
    /// fetch the duel. The documentation does not promise delivery, so the screens also poll
    /// <see cref="RefreshAsync"/> every few seconds.</item>
    /// </list>
    /// The Cloud Save Player Data copy that <c>DuelCloudService</c> keeps stays a local cache: it is
    /// class Default, which the player can write with their own token, so it can never decide HP or
    /// the winner. The authoritative duel lives in the module.
    /// </summary>
    public sealed class UgsDuelOnlineService : IDuelOnlineService
    {
        private DuelView _current;
        private double _currentReceivedAt;
        private int _refreshRequested;
        private int _busy;
        private bool _ready;
        private bool _subscribed;

        // requestId custody: each id lives until the server answered that very request.
        private string _createRequestId;
        private PendingRequest _pendingJoin;
        private PendingRequest _pendingSubmit;
        private PendingRequest _pendingClaim;

        private sealed class PendingRequest
        {
            public string RequestId;
            public string Key;
        }

        public DuelView Current => _current;

        public bool IsBusy => Volatile.Read(ref _busy) != 0;

        public bool ConsumeRefreshRequest() => Interlocked.Exchange(ref _refreshRequested, 0) != 0;

        public double SecondsUntilClaim
        {
            get
            {
                if (_current == null || _current.ClaimAvailableAtUnix <= 0)
                {
                    return 0;
                }

                var elapsed = Time.realtimeSinceStartupAsDouble - _currentReceivedAt;
                return Math.Max(0, _current.ClaimAvailableAtUnix - _current.ServerTimeUnix - elapsed);
            }
        }

        public async Task EnsureReadyAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
            }
            catch (Exception e)
            {
                throw new DuelOnlineException("Could not connect to the online services.", e);
            }

            _ready = true;
            await SubscribeAsync();
        }

        private async Task SubscribeAsync()
        {
            if (_subscribed)
            {
                return;
            }

            var callbacks = new SubscriptionEventCallbacks();
            callbacks.MessageReceived += message =>
            {
                if (message.MessageType == DuelOnlineProtocol.PushMessageType)
                {
                    Interlocked.Exchange(ref _refreshRequested, 1);
                }
            };
            callbacks.Kicked += () =>
            {
                _subscribed = false;
                Debug.LogWarning("DuelOnline: the server unsubscribed this player from push messages. Falling back to polling.");
            };
            callbacks.Error += error => Debug.LogWarning($"DuelOnline: push subscription error - {error}");

            try
            {
                await CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks);
                _subscribed = true;
            }
            catch (Exception e)
            {
                // Not fatal: without pushes the game still works, only a few seconds slower.
                Debug.LogWarning($"DuelOnline: could not subscribe to push messages, polling only - {e.Message}");
            }
        }

        public async Task<DuelView> CreateDuelAsync(string displayName)
        {
            _createRequestId ??= NewRequestId();
            var view = await CallAsync(DuelOnlineProtocol.Function.CreateDuel, new Dictionary<string, object>
            {
                { "requestId", _createRequestId },
                { "displayName", displayName ?? string.Empty }
            });

            // The server answered this request (even if it refused): the id is spent.
            _createRequestId = null;
            return view;
        }

        public async Task<DuelView> JoinDuelAsync(string matchCode, string displayName)
        {
            var code = DuelOnlineEngine.NormalizeCode(matchCode);
            if (code == null)
            {
                throw new DuelOnlineException("That is not a valid match code.");
            }

            if (_pendingJoin == null || _pendingJoin.Key != code)
            {
                _pendingJoin = new PendingRequest { RequestId = NewRequestId(), Key = code };
            }

            var view = await CallAsync(DuelOnlineProtocol.Function.JoinDuel, new Dictionary<string, object>
            {
                { "matchCode", code },
                { "requestId", _pendingJoin.RequestId },
                { "displayName", displayName ?? string.Empty }
            });

            _pendingJoin = null;
            return view;
        }

        public async Task<DuelView> SubmitSequenceAsync(IReadOnlyList<RpsMove> moves)
        {
            var duel = _current;
            if (duel == null || duel.Phase != DuelOnlineProtocol.Phase.InProgress)
            {
                throw new DuelOnlineException("There is no duel in progress.");
            }

            if (moves == null || moves.Count != DuelRules.SlotsPerRound)
            {
                throw new DuelOnlineException($"A round needs exactly {DuelRules.SlotsPerRound} moves.");
            }

            var codes = new List<string>(moves.Count);
            foreach (var move in moves)
            {
                codes.Add(DuelMoveCodec.ToCode(move));
            }

            // Same round and same sequence as a call whose outcome we never learned: it is a retry, keep the id.
            var key = $"{duel.MatchCode}:{duel.Round}:{string.Join(",", codes)}";
            if (_pendingSubmit == null || _pendingSubmit.Key != key)
            {
                _pendingSubmit = new PendingRequest { RequestId = NewRequestId(), Key = key };
            }

            var view = await CallAsync(DuelOnlineProtocol.Function.SubmitSequence, new Dictionary<string, object>
            {
                { "matchCode", duel.MatchCode },
                { "moves", codes },
                { "expectedRound", duel.Round },
                { "requestId", _pendingSubmit.RequestId }
            });

            _pendingSubmit = null;
            return view;
        }

        public Task<DuelView> RefreshAsync()
        {
            if (_current == null || string.IsNullOrEmpty(_current.MatchCode))
            {
                throw new DuelOnlineException("There is no duel to refresh.");
            }

            return CallAsync(DuelOnlineProtocol.Function.GetDuel, new Dictionary<string, object>
            {
                { "matchCode", _current.MatchCode }
            });
        }

        public async Task<DuelView> ClaimVictoryAsync()
        {
            var duel = _current;
            if (duel == null || duel.Phase != DuelOnlineProtocol.Phase.InProgress)
            {
                throw new DuelOnlineException("There is no duel in progress.");
            }

            var key = $"{duel.MatchCode}:{duel.Round}";
            if (_pendingClaim == null || _pendingClaim.Key != key)
            {
                _pendingClaim = new PendingRequest { RequestId = NewRequestId(), Key = key };
            }

            var view = await CallAsync(DuelOnlineProtocol.Function.ClaimVictory, new Dictionary<string, object>
            {
                { "matchCode", duel.MatchCode },
                { "requestId", _pendingClaim.RequestId }
            });

            _pendingClaim = null;
            return view;
        }

        public void Forget()
        {
            _current = null;
            _pendingSubmit = null;
            _pendingJoin = null;
            _pendingClaim = null;
            _createRequestId = null;
            Interlocked.Exchange(ref _refreshRequested, 0);
        }

        private async Task<DuelView> CallAsync(string function, Dictionary<string, object> args)
        {
            if (!_ready)
            {
                await EnsureReadyAsync();
            }

            Interlocked.Exchange(ref _busy, 1);
            try
            {
                var view = await CloudCodeService.Instance.CallModuleEndpointAsync<DuelView>(
                    DuelOnlineProtocol.ModuleName, function, args);

                if (view == null)
                {
                    throw new DuelOnlineException("The server returned an empty answer.");
                }

                // A module deployed with a different contract than this build: do not try to play with it.
                if (view.ProtocolVersion != DuelOnlineProtocol.Version)
                {
                    throw new DuelOnlineException(
                        $"This version of the game does not match the online service (client {DuelOnlineProtocol.Version}, server {view.ProtocolVersion}). Update the game.");
                }

                // Only a view of a duel we belong to replaces the current one; a refusal ("not found",
                // "invalid") carries no duel and must not wipe what we are showing.
                if (view.YourSeat >= 0)
                {
                    _current = view;
                    _currentReceivedAt = Time.realtimeSinceStartupAsDouble;
                }

                return view;
            }
            catch (DuelOnlineException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new DuelOnlineException("The online service did not answer. Check your connection and try again.", e);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private static string NewRequestId() => Guid.NewGuid().ToString("N");
    }
}
