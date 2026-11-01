using System.Collections.Generic;

namespace TTTXO.Core.Duel.Online
{
    /// <summary>
    /// Constants of the online duel protocol. The Unity client and the Cloud Code module compile this
    /// same file (and the rest of <c>TTTXO.Core</c>), so a message name or a result code cannot drift.
    /// </summary>
    public static class DuelOnlineProtocol
    {
        /// <summary>Name of the Cloud Code module (must match the deployed .ccm file name).</summary>
        public const string ModuleName = "DuelModule";

        /// <summary>
        /// <c>messageType</c> of the Cloud Code push message that tells a player "your duel changed,
        /// fetch it". The payload is a <see cref="DuelPushPayload"/> and never carries a move.
        /// </summary>
        public const string PushMessageType = "duel_updated";

        /// <summary>
        /// Version of the message contract (same idea as TTTXO's <c>GameProtocol.Version</c>). Bump it
        /// when a function's parameters or <see cref="DuelView"/> change in a way an old client cannot read.
        /// </summary>
        public const int Version = 1;

        public const int MatchCodeLength = 6;

        /// <summary>No 0/O/1/I: a code read aloud or typed from a screenshot must not be ambiguous.</summary>
        public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>How long a created duel waits for a rival before it expires.</summary>
        public const int WaitingForRivalSeconds = 600;

        public const int MaxRememberedRequestIds = 64;

        public static class Function
        {
            public const string CreateDuel = "CreateDuel";
            public const string JoinDuel = "JoinDuel";
            public const string SubmitSequence = "SubmitSequence";
            public const string GetDuel = "GetDuel";
            public const string ClaimVictory = "ClaimVictory";
        }

        public static class Phase
        {
            public const string WaitingForRival = "waiting_for_rival";
            public const string InProgress = "in_progress";
            public const string Finished = "finished";
        }

        /// <summary>Outcome of a call, reported in the view (a rejected move is not an exception).</summary>
        public static class Result
        {
            public const string Applied = "applied";
            /// <summary>The same requestId was already processed: nothing changed, here is the current state.</summary>
            public const string Replayed = "replayed";
            public const string StaleRound = "stale_round";
            public const string AlreadySubmitted = "already_submitted";
            public const string WrongState = "wrong_state";
            public const string InvalidArgument = "invalid_argument";
            public const string NotAParticipant = "not_a_participant";
            public const string NotFound = "not_found";
            /// <summary>"Reclamar victoria" before the rival was silent for the whole window, or without having played.</summary>
            public const string ClaimNotAvailable = "claim_not_available";
            /// <summary>Two writes raced and this one lost every retry: fetch the duel again.</summary>
            public const string Conflict = "conflict";
        }

        public static class FinishReason
        {
            public const string Cancelled = "cancelled";
            public const string Expired = "expired";
        }

        public static class ClashOutcome
        {
            public const string You = "you";
            public const string Rival = "rival";
            public const string Draw = "draw";
        }

        public static class PushEvent
        {
            public const string RivalJoined = "rival_joined";
            public const string RivalSubmitted = "rival_submitted";
            public const string RoundResolved = "round_resolved";
            public const string Finished = "finished";
        }
    }

    /// <summary>Lightweight notification pushed to a player. Fetch the duel to see what changed.</summary>
    public sealed class DuelPushPayload
    {
        public string MatchCode { get; set; }
        public string Event { get; set; }
        public int Round { get; set; }
        public string Phase { get; set; }
    }

    /// <summary>One of the three clashes of a closed round, from the viewer's side. Moves are "Guerrero"/"Maga"/"Asesino".</summary>
    public sealed class DuelClashView
    {
        public int Slot { get; set; }
        public string YourMove { get; set; }
        public string RivalMove { get; set; }
        public string Outcome { get; set; }
        public int Damage { get; set; }
    }

    /// <summary>The last round that closed. Both sequences are public once the round is over.</summary>
    public sealed class DuelRoundView
    {
        public int Round { get; set; }
        public List<DuelClashView> Clashes { get; set; } = new List<DuelClashView>();
        public int DamageToYou { get; set; }
        public int DamageToRival { get; set; }
    }

    /// <summary>
    /// What a player is allowed to see of a duel. Built per viewer: the rival's pending sequence is
    /// never in it, and a round's moves only appear once the round is closed.
    /// </summary>
    public sealed class DuelView
    {
        /// <remarks>Defaults to 0 on purpose: an answer from an older module that does not send the field must read as a mismatch.</remarks>
        public int ProtocolVersion { get; set; }

        public string Result { get; set; }
        public string MatchCode { get; set; }
        public string Phase { get; set; }

        /// <summary>The document's per-player state: TU_TURNO, ESPERANDO_RIVAL or FINALIZADO (see <see cref="DuelRules"/>).</summary>
        public string EstadoPartida { get; set; }

        public int Round { get; set; }
        public int YourSeat { get; set; }
        public string YourName { get; set; }
        public string RivalName { get; set; }
        public int YourHp { get; set; }
        public int RivalHp { get; set; }
        public bool RivalJoined { get; set; }
        public bool YouHaveSubmitted { get; set; }
        public bool RivalHasSubmitted { get; set; }

        /// <summary>The viewer's own locked-in sequence for the open round (so a reconnecting client can show it).</summary>
        public List<string> YourPendingSequence { get; set; } = new List<string>();

        /// <summary>Server time (unix seconds) when "reclamar victoria" unlocks, or 0 when it does not apply.</summary>
        public long ClaimAvailableAtUnix { get; set; }

        public long ServerTimeUnix { get; set; }

        public DuelRoundView LastRound { get; set; }

        /// <summary>"you", "rival" or null while open or ended without a winner.</summary>
        public string Winner { get; set; }

        /// <summary>KO, ABANDONO, EMPATE, LIMITE (see <see cref="DuelRules"/>) or a <see cref="DuelOnlineProtocol.FinishReason"/>.</summary>
        public string EndReason { get; set; }

        public bool IsFinished => Phase == DuelOnlineProtocol.Phase.Finished;
    }
}
