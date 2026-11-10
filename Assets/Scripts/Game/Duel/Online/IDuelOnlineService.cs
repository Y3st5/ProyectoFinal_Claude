using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TTTXO.Core;
using TTTXO.Core.Duel.Online;

namespace TTTXO.Game.Duel.Online
{
    /// <summary>
    /// What the duel screens need from the online duel, without knowing about Cloud Code. The screens
    /// send intentions and draw the <see cref="DuelView"/> that comes back; they never decide
    /// anything about the duel (HP, damage, winner, abandonment): that is the server's job.
    ///
    /// The interesting job of an implementation is not calling the server, it is guarding the
    /// <c>requestId</c>: a submission has one id and that id must survive every retry of that
    /// submission. A new id on retry would make the server think it is a second submission.
    /// </summary>
    public interface IDuelOnlineService
    {
        /// <summary>Latest view the server gave us, or null before any duel.</summary>
        DuelView Current { get; }

        /// <summary>True while a call is in flight (the UI uses it to ignore double taps).</summary>
        bool IsBusy { get; }

        /// <summary>
        /// True once per push message received. The push only says "something changed": the caller
        /// reacts by calling <see cref="RefreshAsync"/>. Thread-safe on purpose, push callbacks do
        /// not have to arrive on the main thread.
        /// </summary>
        bool ConsumeRefreshRequest();

        /// <summary>Seconds until "reclamar victoria" unlocks by the server's clock; 0 when it is available or does not apply.</summary>
        double SecondsUntilClaim { get; }

        /// <summary>Initializes the services, signs in anonymously and subscribes to push messages.</summary>
        Task EnsureReadyAsync();

        Task<DuelView> CreateDuelAsync(string displayName);

        Task<DuelView> JoinDuelAsync(string matchCode, string displayName);

        /// <summary>Locks in the three moves of the open round. A retry of the same sequence reuses the same requestId.</summary>
        Task<DuelView> SubmitSequenceAsync(IReadOnlyList<RpsMove> moves);

        /// <summary>Fetches the duel without changing it. This is also the polling fallback for lost pushes.</summary>
        Task<DuelView> RefreshAsync();

        /// <summary>Asks the server for the victory by abandonment. The server decides if the window really passed.</summary>
        Task<DuelView> ClaimVictoryAsync();

        /// <summary>Forgets the current duel locally without telling the server.</summary>
        void Forget();
    }

    /// <summary>A call to the server failed (no network, service down, module error). The UI shows <see cref="Exception.Message"/>.</summary>
    public sealed class DuelOnlineException : Exception
    {
        public DuelOnlineException(string message, Exception inner = null) : base(message, inner) { }
    }
}
