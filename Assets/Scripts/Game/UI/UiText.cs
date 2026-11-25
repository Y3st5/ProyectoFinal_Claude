using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TTTXO.Game.UI
{
    /// <summary>
    /// Centralized source of every player-visible string across all screens. No UXML file and no
    /// controller should contain a hardcoded literal string meant to be shown to the player - it
    /// must be added here instead.
    ///
    /// Milestone 3 (see Docs/02-GDD-TicTacToe.md#8 Localización and
    /// Docs/03-Arquitectura-UGS-TicTacToe.md#Localización — implicancias técnicas): every property
    /// below that carries real language content resolves through Unity Localization's "UiStrings"
    /// string table collection (see Assets/Scripts/Game/Editor/LocalizationSetup.cs, menu
    /// "TTTXO/Setup/Create Localization Assets") via <see cref="Localize"/>. Glyph/emoji icons and
    /// pure numeric-only format strings (nothing to translate) are intentionally left as plain
    /// constants - see the comment on each nested class.
    ///
    /// <see cref="Localize"/> is defensive like <c>GameAnalytics</c>: if the localization system has
    /// no settings configured yet, has not finished initializing, or is missing a key/table (e.g. the
    /// Editor setup menu item was never run), it logs a warning and returns the English fallback
    /// passed in - it never throws and never returns an empty string, so the game stays playable
    /// even before the localization tables are generated.
    /// </summary>
    public static class UiText
    {
        /// <summary>Name of the Unity Localization String Table Collection created by LocalizationSetup.</summary>
        public const string TableName = "UiStrings";

        public static class Common
        {
            /// <summary>Directional glyph, not language content - GDD's 10 target locales are all LTR (see Docs/02-GDD-TicTacToe.md#8), so this never needs to flip or translate.</summary>
            public const string BackIcon = "‹";
            public static string Cancel => Localize("Common/Cancel", "Cancel");
            public static string Confirm => Localize("Common/Confirm", "Confirm");
            public static string Resume => Localize("Common/Resume", "Resume");
            public static string Abandon => Localize("Common/Abandon", "Abandon");
            /// <summary>Directional glyph, see <see cref="BackIcon"/>.</summary>
            public const string Chevron = "›";
            public static string PlayerNamePlaceholder => Localize("Common/PlayerNamePlaceholder", "Player");
            public static string ComingSoon => Localize("Common/ComingSoon", "Coming soon");
        }

        /// <summary>
        /// Glyph/emoji icons used in place of real art assets in Milestone 1. These are symbols, not
        /// language content - there is nothing to translate, so unlike the rest of this file they are
        /// NOT routed through Unity Localization.
        /// Note: Using simple characters that render in Unity's default font to avoid missing-glyph boxes.
        /// </summary>
        public static class Icons
        {
            public const string Profile = "👤";
            public const string Store = "S";
            public const string Settings = "⚙";
            public const string SinglePlayer = "1P";
            public const string LocalMultiplayer = "2P";
            public const string Online = "🌐";
            public const string Streak = "🔥";
            public const string EditLook = "✎";
            public const string Rps = "✋";
            public const string RpsRock = "✊";
            public const string RpsPaper = "✋";
            public const string RpsScissors = "✌";
            /// <summary>Crossed swords for the async duel hub entry.</summary>
            public const string Duel = "⚔";
        }

        public static class Splash
        {
            public static string AppTitle => Localize("Splash/AppTitle", "TIC TAC XO");
            public static string Loading => Localize("Splash/Loading", "Loading...");

            // Generic, non-technical error state (Docs/06-Wireframes-UI.md screen 1: "Sin UI
            // interactiva ni texto tecnico de error. En fallo de init: pantalla de error generica
            // con boton Retry"). Never show the underlying exception message here.
            public static string ErrorMessage => Localize(
                "Splash/ErrorMessage", "Something went wrong. Please check your connection and try again.");
            public static string RetryButton => Localize("Splash/RetryButton", "Retry");
        }

        public static class Home
        {
            public static string Title => Localize("Home/Title", "TIC TAC XO");
            public static string PlayButton => Localize("Home/PlayButton", "Play");
            /// <summary>Pure numeric formatter, nothing to translate - not routed through Localization.</summary>
            public const string CurrencyFormat = "{0}";
            public static string ProfileLabel => Localize("Home/ProfileLabel", "Profile");
            public static string StoreLabel => Localize("Home/StoreLabel", "Store");
            public static string SettingsLabel => Localize("Home/SettingsLabel", "Settings");
            /// <summary>Short name of the Rock-Paper-Scissors mode on the Home secondary row.</summary>
            public static string RpsLabel => Localize("Home/RpsLabel", "RPS");
        }

        /// <summary>
        /// Rock-Paper-Scissors start menu (the RPS scene's home) - see RpsMenuScreenController and
        /// Assets/Scenes/Rps.unity. "RPS" is kept as a short code in every locale, same treatment as
        /// the Home secondary-row label.
        /// </summary>
        public static class RpsMenu
        {
            public static string Title => Localize("RpsMenu/Title", "Guerrero, Maga y Asesino");
            public static string Subtitle => Localize("RpsMenu/Subtitle", "Rock, paper, scissors");
            public static string PlayButton => Localize("RpsMenu/PlayButton", "Play");
            public static string StoreLabel => Localize("RpsMenu/StoreLabel", "Store");
            public static string SettingsLabel => Localize("RpsMenu/SettingsLabel", "Settings");
            public static string DuelsLabel => Localize("RpsMenu/DuelsLabel", "Duels");
        }

        public static class RpsModeSelect
        {
            public static string Title => Localize("RpsModeSelect/Title", "Choose a mode");
            public static string SinglePlayerTitle => Localize("RpsModeSelect/SinglePlayerTitle", "Single Player");
            public static string SinglePlayerDescription => Localize("RpsModeSelect/SinglePlayerDescription", "Best of five vs AI");
            public static string LocalMultiplayerTitle => Localize("RpsModeSelect/LocalMultiplayerTitle", "Local 2P");
            public static string LocalMultiplayerDescription => Localize("RpsModeSelect/LocalMultiplayerDescription", "Pass the device, round by round");
            public static string OnlineTitle => Localize("RpsModeSelect/OnlineTitle", "Online");
            public static string OnlineDescription => Localize("RpsModeSelect/OnlineDescription", "Versus a rival over UGS");
            /// <summary>Toast shown until online RPS ships (Milestone contract pending, see Docs/05-UI-Pantallas-TicTacToe.md).</summary>
            public static string OnlineComingSoonMessage => Localize("RpsModeSelect/OnlineComingSoonMessage", "Online RPS is coming soon!");
        }

        public static class RpsCharacterSelect
        {
            public static string Title => Localize("RpsCharacterSelect/Title", "Choose your fighter");
            public static string Subtitle => Localize("RpsCharacterSelect/Subtitle", "Each fighter has unique animations");
            public static string Guerrero => Localize("RpsCharacterSelect/Guerrero", "Guerrero");
            public static string Maga => Localize("RpsCharacterSelect/Maga", "Maga");
            public static string Asesino => Localize("RpsCharacterSelect/Asesino", "Asesino");
            public static string SingleTurn => Localize("RpsCharacterSelect/SingleTurn", "Choose your fighter");
            public static string PlayerOneTurn => Localize("RpsCharacterSelect/PlayerOneTurn", "Player 1, choose your fighter");
            public static string PlayerTwoTurn => Localize("RpsCharacterSelect/PlayerTwoTurn", "Player 2, choose your fighter");
            public static string RoundFormat => Localize("RpsCharacterSelect/RoundFormat", "Round {0} · Best of five");
            public static string ScoreFormat => Localize("RpsCharacterSelect/ScoreFormat", "{0} {1}   ·   {2} {3}");
        }

        public static class RpsGame
        {
            public static string RoundFormat => Localize("RpsGame/RoundFormat", "Round {0}");
            public static string Rock => Localize("RpsGame/Rock", "Rock");
            public static string Paper => Localize("RpsGame/Paper", "Paper");
            public static string Scissors => Localize("RpsGame/Scissors", "Scissors");
            /// <summary>Round label shown while a side still has to play - e.g. "Round 2 - your move".</summary>
            public static string RoundYourMove => Localize("RpsGame/RoundYourMove", "Round {0} - your move");
            /// <summary>Round label shown while waiting for the opponent in local 2P handoff - e.g. "Round 2 - waiting for Player 2".</summary>
            public static string RoundWaiting => Localize("RpsGame/RoundWaiting", "Round {0} - waiting for {1}");
            /// <summary>Round label shown while the AI picks (single player) between the human move and the reveal.</summary>
            public static string AiThinking => Localize("RpsGame/AiThinking", "AI is thinking...");
            public static string YouLabel => Localize("RpsGame/YouLabel", "You");
            public static string AiLabel => Localize("RpsGame/AiLabel", "AI");
            public static string Player1Label => Localize("RpsGame/Player1Label", "Player 1");
            public static string Player2Label => Localize("RpsGame/Player2Label", "Player 2");
            /// <summary>Handoff banner in local 2P: Player N submits hidden, then the device switches hands.</summary>
            public static string HandoffLabel => Localize("RpsGame/HandoffLabel", "Pass the device to {0}");
            /// <summary>Score chip value: side name + running score + series win target, e.g. "You 2 / 3".</summary>
            public const string ScoreChipFormat = "{0} {1}";
            /// <summary>Win target used inside <see cref="ScoreChipFormat"/>'s numbers, e.g. "2 / 3".</summary>
            public const string WinPointsFormat = "{0} / {1}";
            public static string YouWon => Localize("RpsGame/YouWon", "You won the series!");
            public static string YouLost => Localize("RpsGame/YouLost", "AI won the series");
            public static string Player1Won => Localize("RpsGame/Player1Won", "Player 1 won the series!");
            public static string Player2Won => Localize("RpsGame/Player2Won", "Player 2 won the series!");
            public static string SeriesDraw => Localize("RpsGame/SeriesDraw", "Series tied");
            /// <summary>Draw label shown on VS Screen when both players pick the same move.</summary>
            public static string DrawLabel => Localize("RpsGame/DrawLabel", "EMPATE");
            public static string RematchButton => Localize("RpsGame/RematchButton", "Rematch");
            public static string MenuButton => Localize("RpsGame/MenuButton", "Menu");
        }

        /// <summary>
        /// Async duel screens (Hub Central + combat). The duel itself is not best-of-five: it is a
        /// sequence of rounds of three clashes each, with 100 HP and 15 damage per clash won.
        /// </summary>
        public static class Duel
        {
            public static string HubTitle => Localize("Duel/HubTitle", "Active duels");
            public static string HubEmpty => Localize(
                "Duel/HubEmpty", "You have no active duels. Start a new one and come back whenever you want.");
            public static string NewDuelButton => Localize("Duel/NewDuelButton", "New duel (practice)");
            public static string IdleDemoButton => Localize("Duel/IdleDemoButton", "Demo: silent rival (timeout)");

            public static string CardYourTurn => Localize("Duel/CardYourTurn", "Your turn");
            public static string CardWaitingRival => Localize("Duel/CardWaitingRival", "Waiting for rival");
            public static string CardFinished => Localize("Duel/CardFinished", "Finished");
            /// <summary>Hub card line, e.g. "Round 3 · You 100 HP · Rival 85 HP".</summary>
            public static string CardSummaryFormat => Localize(
                "Duel/CardSummaryFormat", "Round {0} · You {1} HP · Rival {2} HP");

            /// <summary>Name stored in the document for the practice bot (and shown in the Hub card).</summary>
            public static string PracticeRivalName => Localize("Duel/PracticeRivalName", "Practice rival");
            /// <summary>Name stored in the document for the editor-only silent rival.</summary>
            public static string IdleRivalName => Localize("Duel/IdleRivalName", "Silent rival");

            public static string Title => Localize("Duel/Title", "Duel");
            public static string RoundFormat => Localize("Duel/RoundFormat", "Round {0}");
            public static string YourTurn => Localize("Duel/YourTurn", "Your turn: fill the three slots");
            public static string WaitingRival => Localize(
                "Duel/WaitingRival", "Moves sent. Your rival will answer the next time you open this duel.");
            public static string WaitingCountdownFormat => Localize(
                "Duel/WaitingCountdownFormat", "Claim unlocks in {0}:{1:00} · inactivity window {2} min");
            public static string ClaimButton => Localize("Duel/ClaimButton", "Claim victory");
            public static string ConfirmButton => Localize("Duel/ConfirmButton", "Send moves");
            public static string ConfirmIncomplete => Localize("Duel/ConfirmIncomplete", "Fill the three slots");
            /// <summary>Empty action slot placeholder - a glyph, not language content.</summary>
            public const string SlotEmpty = "—";
            /// <summary>Compact HP readout, e.g. "85 / 100". Nothing to translate.</summary>
            public const string HpFormat = "{0} / {1}";

            public static string ResultRoundTitleFormat => Localize("Duel/ResultRoundTitleFormat", "Clashes of round {0}");
            public static string ResultWin => Localize("Duel/ResultWin", "Victory!");
            public static string ResultLoss => Localize("Duel/ResultLoss", "Defeat");
            public static string ResultDraw => Localize("Duel/ResultDraw", "Draw");
            public static string ResultContinue => Localize("Duel/ResultContinue", "Continue");
            public static string ResultBackToHub => Localize("Duel/ResultBackToHub", "Back to Hub");

            /// <summary>One clash row: "Guerrero vs Asesino".</summary>
            public static string ClashFormat => Localize("Duel/ClashFormat", "{0} vs {1}");
            public static string ClashDraw => Localize("Duel/ClashDraw", "Same class · no damage");
            /// <summary>Clash winner + damage, e.g. "Guerrero wins · 15 damage".</summary>
            public static string ClashDamageFormat => Localize("Duel/ClashDamageFormat", "{0} wins · {1} damage");
            public static string WinByKo => Localize("Duel/WinByKo", "Victory by KO");
            public static string WinByAbandon => Localize("Duel/WinByAbandon", "Victory by abandonment");
            public static string DrawByLimit => Localize("Duel/DrawByLimit", "Draw: round limit reached");
            public static string LossReasonFormat => Localize("Duel/LossReasonFormat", "Your rival wins · {0}");
            /// <summary>Short reason used inside <see cref="LossReasonFormat"/>.</summary>
            public static string ReasonKo => Localize("Duel/ReasonKo", "KO");
            public static string ReasonAbandon => Localize("Duel/ReasonAbandon", "abandonment");
        }

        public static class ModeSelect
        {
            public static string Title => Localize("ModeSelect/Title", "Choose a mode");
            public static string SinglePlayerTitle => Localize("ModeSelect/SinglePlayerTitle", "Single Player");
            public static string SinglePlayerDescription => Localize("ModeSelect/SinglePlayerDescription", "Play vs AI · pick difficulty");
            public static string LocalMultiplayerTitle => Localize("ModeSelect/LocalMultiplayerTitle", "Local 2P");
            public static string LocalMultiplayerDescription => Localize("ModeSelect/LocalMultiplayerDescription", "Same device");
            public static string OnlineTitle => Localize("ModeSelect/OnlineTitle", "Online");
            public static string OnlineDescription => Localize("ModeSelect/OnlineDescription", "Quickmatch or Ranked");

            /// <summary>No longer shown anywhere (Online and Ranked are both enabled as of Milestone 5) - kept only so a stale reference never throws; unused by any controller.</summary>
            public static string OnlineSoonTag => Localize("ModeSelect/OnlineSoonTag", "Soon");

            /// <summary>No longer shown (Online is enabled, Milestone 4) - kept only so a stale reference never throws; unused by any controller.</summary>
            public static string OnlineComingSoonMessage => Localize("ModeSelect/OnlineComingSoonMessage", "Online play is coming soon!");

            public static string OnlineQuickmatchTitle => Localize("ModeSelect/OnlineQuickmatchTitle", "Quickmatch");
            public static string OnlineQuickmatchDescription => Localize("ModeSelect/OnlineQuickmatchDescription", "Play a rival online now");
            public static string OnlineRankedTitle => Localize("ModeSelect/OnlineRankedTitle", "Ranked");
            public static string OnlineRankedDescription => Localize("ModeSelect/OnlineRankedDescription", "Competitive ladder");

            /// <summary>No longer shown (Ranked is enabled, Milestone 5) - kept only so a stale reference never throws; unused by any controller.</summary>
            public static string OnlineRankedComingSoonMessage => Localize("ModeSelect/OnlineRankedComingSoonMessage", "Ranked is coming soon!");
        }

        public static class Matchmaking
        {
            public static string SearchingLabel => Localize("Matchmaking/SearchingLabel", "Searching for a rival...");
            public static string ConnectingLabel => Localize("Matchmaking/ConnectingLabel", "Connecting to match...");
            public static string FallbackTitle => Localize("Matchmaking/FallbackTitle", "Taking too long?");
            public static string PlayVsAiButton => Localize("Matchmaking/PlayVsAiButton", "Play vs AI");
            /// <summary>Pure numeric mm:ss formatter, nothing to translate - not routed through Localization.</summary>
            public const string ElapsedFormat = "{0}:{1:00}";
        }

        public static class BoardSelect
        {
            public static string BoardSizeSectionLabel => Localize("BoardSelect/BoardSizeSectionLabel", "Board size");
            /// <summary>Pure numeric formatter ("3x3"), nothing to translate - not routed through Localization.</summary>
            public const string BoardSizeFormat = "{0}x{0}";
            public static string BoardAlignFormat => Localize("BoardSelect/BoardAlignFormat", "align {0}");
            public static string DifficultyLabel => Localize("BoardSelect/DifficultyLabel", "Difficulty");
            public static string DifficultyEasy => Localize("BoardSelect/DifficultyEasy", "Easy");
            public static string DifficultyMedium => Localize("BoardSelect/DifficultyMedium", "Medium");
            public static string DifficultyHard => Localize("BoardSelect/DifficultyHard", "Hard");
            public static string DifficultyAdaptive => Localize("BoardSelect/DifficultyAdaptive", "Adaptive");
            public static string ConfirmButton => Localize("BoardSelect/ConfirmButton", "Start Game");
        }

        public static class Game
        {
            public static string YouLabel => Localize("Game/YouLabel", "You");
            public static string RivalLabel => Localize("Game/RivalLabel", "Rival");
            public static string AiThinking => Localize("Game/AiThinking", "AI is thinking...");
            public static string Player1Label => Localize("Game/Player1Label", "Player 1");
            public static string Player2Label => Localize("Game/Player2Label", "Player 2");
            /// <summary>Glyph substitute for a pause icon, nothing to translate - not routed through Localization.</summary>
            public const string PauseButtonIcon = "II";
            public static string PauseTitle => Localize("Game/PauseTitle", "Paused");

            // Wire status chip (Milestone 4, online-only - Docs/06-Wireframes-UI.md screen 6: "estado
            // de conexion Wire como chip fantasma"). Glyph prefixes are decorative, not translated.
            public static string WireSubscribed => Localize("Game/WireSubscribed", "◉ Subscribed");
            public static string WireConnecting => Localize("Game/WireConnecting", "◌ Connecting...");
            public static string WireOffline => Localize("Game/WireOffline", "◌ Offline");

            // Online-only abandon confirmation (Docs/06-Wireframes-UI.md screen 6: "Abandono desde
            // pausa -> confirma -> el rival gana por abandono al detectarlo el server").
            public static string AbandonConfirmTitle => Localize("Game/AbandonConfirmTitle", "Abandon match?");
            public static string AbandonConfirmMessage => Localize(
                "Game/AbandonConfirmMessage", "Your rival will win if you leave now. This cannot be undone.");
        }

        public static class Result
        {
            public static string YouWin => Localize("Result/YouWin", "You Win! \U0001F389");
            public static string YouLose => Localize("Result/YouLose", "You Lose");
            public static string Draw => Localize("Result/Draw", "Draw");
            public static string Player1Wins => Localize("Result/Player1Wins", "Player 1 Wins!");
            public static string Player2Wins => Localize("Result/Player2Wins", "Player 2 Wins!");
            public static string RewardCardTitle => Localize("Result/RewardCardTitle", "Reward");
            /// <summary>Pure numeric formatter, nothing to translate - not routed through Localization.</summary>
            public const string RewardFormat = "+{0}";

            /// <summary>Milestone 5 - wireframe 7's Ranked-only MMR card title. "Ranked" is kept as an English loanword in most locales, same treatment as ModeSelect/OnlineRankedTitle.</summary>
            public static string RankedCardTitle => Localize("Result/RankedCardTitle", "Ranked");

            /// <summary>"MMR {value} {arrow}{signed delta}" (wireframe 7: "MMR 1240 ▲ +18") - "MMR" is universal terminology (kept as-is across every locale, same as design-doc.md itself does in Spanish) and the arrow/sign are glyphs/numbers, nothing to translate - not routed through Localization.</summary>
            public const string RankedMmrCardFormat = "MMR {0} {1}{2}";

            public static string RematchButton => Localize("Result/RematchButton", "Rematch");
            public static string HomeButton => Localize("Result/HomeButton", "Home");

            /// <summary>Shown under the status label for an online match ended by risk #4's abandonment resolution (Docs/03-Arquitectura-UGS-TicTacToe.md#4).</summary>
            public static string OpponentLeftNote => Localize("Result/OpponentLeftNote", "Your rival left the match.");
        }

        public static class Profile
        {
            public static string Title => Localize("Profile/Title", "Profile");
            /// <summary>Top link of the profile card - opens the equip panel, does not navigate.</summary>
            public static string EditLookLinkFormat => Localize("Profile/EditLookLinkFormat", "{0} Edit look {1}");

            /// <summary>Link at the bottom of the equip panel - leaves for <see cref="Store"/>.</summary>
            public static string StoreLinkFormat => Localize("Profile/StoreLinkFormat", "{0} Go to Store {1}");
            public static string WinRateCaption => Localize("Profile/WinRateCaption", "Win rate");
            public static string StreakCaption => Localize("Profile/StreakCaption", "Streak");
            /// <summary>Pure numeric formatter, nothing to translate - not routed through Localization.</summary>
            public const string WinRateValueFormat = "{0}%";
            /// <summary>Icon + number, nothing to translate - not routed through Localization.</summary>
            public const string StreakValueFormat = "{0} {1}";
            public static string MatchHistoryButton => Localize("Profile/MatchHistoryButton", "Match history");

            /// <summary>Milestone 5 - wireframe 8's "Leaderboard (Ranked) ›" list button, navigates to <see cref="Leaderboard"/>.</summary>
            public static string LeaderboardButton => Localize("Profile/LeaderboardButton", "Leaderboard (Ranked)");

            // ---- Milestone 5 Ranked section (design-doc.md section 6, wireframe 8's stats row MMR) ----
            // See ProfileScreenController.RefreshRankedSectionAsync: one row per Ranked-enabled board
            // size (3x3/6x6), each showing either the placement progress or the real MMR/tier once
            // placement is done (design-doc.md section 6.6: "un jugador aparece en el leaderboard solo
            // tras completar sus 10 partidas de colocacion" - the same gate applies to this display).

            public static string RankedSectionTitle => Localize("Profile/RankedSectionTitle", "Ranked");

            // Equip panel section headings. Without them the panel is five unlabelled rows of art and the
            // player has to infer from the shapes what each one changes.
            public static string EquipAvatarsTitle => Localize("Profile/EquipAvatarsTitle", "Avatar");

            public static string EquipFramesTitle => Localize("Profile/EquipFramesTitle", "Frame");

            public static string EquipBannersTitle => Localize("Profile/EquipBannersTitle", "Banner");

            public static string EquipPieceSkinsTitle => Localize("Profile/EquipPieceSkinsTitle", "Pieces");

            public static string EquipBoardSkinsTitle => Localize("Profile/EquipBoardSkinsTitle", "Board");

            /// <summary>"MMR {0} · {1}" (e.g. "MMR 1180 · Gold") - "MMR" is universal terminology and "·" is a glyph separator, same treatment as Result/RankedMmrCardFormat; {1} (the tier name) is the part that is actually translated, via RankedTierBronze..RankedTierDiamond below - not routed through Localization itself.</summary>
            public const string RankedValueFormat = "MMR {0} · {1}";

            /// <summary>Shown instead of <see cref="RankedValueFormat"/> while the player has not finished the season's placement matches on this board yet (wireframe 8: "Colocación: 3 / 5"). Both numbers come from the server (<c>PlacementsPlayed</c>/<c>PlacementMatchesRequired</c>), so the requirement can be retuned in Remote Config without a client build.</summary>
            public static string RankedPlacementFormat => Localize("Profile/RankedPlacementFormat", "Placement {0}/{1}");

            // design-doc.md section 6.5 tier table display names - the server only ever sends the
            // English internal code ("bronze".."diamond", see RankedBoardProfileDto.Tier), this file
            // maps it to the localized name the player actually sees.
            public static string RankedTierBronze => Localize("Profile/RankedTierBronze", "Bronze");
            public static string RankedTierSilver => Localize("Profile/RankedTierSilver", "Silver");
            public static string RankedTierGold => Localize("Profile/RankedTierGold", "Gold");
            public static string RankedTierPlatinum => Localize("Profile/RankedTierPlatinum", "Platinum");
            public static string RankedTierDiamond => Localize("Profile/RankedTierDiamond", "Diamond");

            public static string DeleteDataButton => Localize("Profile/DeleteDataButton", "Delete my data");
            public static string DeleteConfirmTitle => Localize("Profile/DeleteConfirmTitle", "Delete my data?");
            public static string DeleteConfirmMessage => Localize(
                "Profile/DeleteConfirmMessage",
                "This permanently deletes your local progress, currency and match history on this device. This cannot be undone.");
            public static string DeleteConfirmButton => Localize("Profile/DeleteConfirmButton", "Delete");
        }

        public static class MatchHistory
        {
            public static string Title => Localize("MatchHistory/Title", "Match History");
            public static string FilterAll => Localize("MatchHistory/FilterAll", "All");
            public static string FilterSingle => Localize("MatchHistory/FilterSingle", "Single");
            public static string FilterLocal => Localize("MatchHistory/FilterLocal", "Local");
            public static string EmptyState => Localize("MatchHistory/EmptyState", "No matches yet. Play a game to see it here!");
            public static string ResultWin => Localize("MatchHistory/ResultWin", "Win");
            public static string ResultLoss => Localize("MatchHistory/ResultLoss", "Loss");
            public static string ResultDraw => Localize("MatchHistory/ResultDraw", "Draw");
            public static string LocalPlayer1Win => Localize("MatchHistory/LocalPlayer1Win", "Player 1 won");
            public static string LocalPlayer2Win => Localize("MatchHistory/LocalPlayer2Win", "Player 2 won");
            /// <summary>Pure separators/numbers, nothing to translate - not routed through Localization.</summary>
            public const string BoardSizeAndModeFormat = "{0}×{0} · {1} · {2}";
            public static string DurationMinutesFormat => Localize("MatchHistory/DurationMinutesFormat", "{0}m {1}s");
            public static string DurationSecondsFormat => Localize("MatchHistory/DurationSecondsFormat", "{0}s");
            public static string TimeAgoJustNow => Localize("MatchHistory/TimeAgoJustNow", "Just now");
            public static string TimeAgoMinutesFormat => Localize("MatchHistory/TimeAgoMinutesFormat", "{0}m ago");
            public static string TimeAgoHoursFormat => Localize("MatchHistory/TimeAgoHoursFormat", "{0}h ago");
            public static string TimeAgoDaysFormat => Localize("MatchHistory/TimeAgoDaysFormat", "{0}d ago");
        }

        /// <summary>
        /// Leaderboard screen (Docs/06-Wireframes-UI.md screen 10, Milestone 5): board-size filter
        /// chips (3x3/6x6) over <c>GetRankedLeaderboard</c>'s top N + the caller's own row (see
        /// LeaderboardScreenController).
        /// </summary>
        public static class Leaderboard
        {
            public static string Title => Localize("Leaderboard/Title", "Leaderboard");

            public static string LoadingMessage => Localize("Leaderboard/LoadingMessage", "Loading leaderboard...");

            /// <summary>Shown on a failed <c>GetRankedLeaderboard</c> call (network/server error) - never fabricated data, per Docs/01-Directrices-Proyecto.md.</summary>
            public static string UnavailableMessage => Localize(
                "Leaderboard/UnavailableMessage", "The Ranked leaderboard isn't available here yet. Check back soon.");

            /// <summary>Shown when the call succeeded but this board has no ranked players yet (top empty and no own entry).</summary>
            public static string EmptyMessage => Localize("Leaderboard/EmptyMessage", "No ranked players yet on this board.");

            /// <summary>Shown below the list when the top N loaded fine but the caller has not finished placement on this board yet (design-doc.md section 6.6 elegibilidad - no own row to highlight).</summary>
            public static string PlacementRequiredMessage => Localize(
                "Leaderboard/PlacementRequiredMessage", "Finish your placement matches to join the ranking.");

            /// <summary>Rank prefix ("#3") - pure numeric formatter, nothing to translate - not routed through Localization.</summary>
            public const string RankFormat = "#{0}";

            /// <summary>Separator between the top list and a non-top own row (wireframe 10: "Top N en cards + separador ⋮ + fila propia destacada") - a glyph, nothing to translate - not routed through Localization.</summary>
            public const string OwnEntrySeparator = "⋮";
        }

        public static class Store
        {
            public static string Title => Localize("Store/Title", "Store");
            public static string TabSkins => Localize("Store/TabSkins", "Skins");
            public static string TabArenas => Localize("Store/TabArenas", "Arenas");
            public static string TabFx => Localize("Store/TabFx", "FX");
            public static string ComingSoonCardTitle => Localize("Store/ComingSoonCardTitle", "Coming soon");
            public static string ComingSoonCardDescription => Localize("Store/ComingSoonCardDescription", "New cosmetics are on their way.");
            public static string BuyCurrencyToast => Localize("Store/BuyCurrencyToast", "Currency purchases are coming soon!");
            /// <summary>Glyph, nothing to translate - not routed through Localization.</summary>
            public const string AddCurrencyButtonIcon = "+";
        }

        public static class Settings
        {
            public static string Title => Localize("Settings/Title", "Settings");
            public static string LanguageLabel => Localize("Settings/LanguageLabel", "Language");
            public static string SoundLabel => Localize("Settings/SoundLabel", "Sound");
            public static string MusicLabel => Localize("Settings/MusicLabel", "Music");
            public static string DeleteAccountButton => Localize("Settings/DeleteAccountButton", "Delete account & data");
            public static string DeleteAccountConfirmTitle => Localize("Settings/DeleteAccountConfirmTitle", "Delete account & data?");
            public static string DuelSectionLabel => Localize("Settings/DuelSectionLabel", "Duels");
            public static string DuelDemoTimeoutLabel => Localize(
                "Settings/DuelDemoTimeoutLabel", "Short timeout (2 min) for demos and evaluation");
        }

        /// <summary>
        /// Resolves <paramref name="key"/> from the "UiStrings" table for the currently selected
        /// locale. Defensive by design (see class remarks): any failure to resolve - no
        /// LocalizationSettings asset, system still initializing, missing table/key/translation -
        /// degrades to a warning and returns <paramref name="fallback"/> (the original Milestone 1/2
        /// English literal), so a screen never shows blank text and the game never throws because of
        /// localization.
        /// </summary>
        private static string Localize(string key, string fallback)
        {
            try
            {
                if (!LocalizationSettings.HasSettings)
                {
                    // No LocalizationSettings asset in the project yet (setup menu item never run) -
                    // this is expected until Milestone 3 setup runs, not an error.
                    return fallback;
                }

                if (!LocalizationSettings.InitializationOperation.IsDone)
                {
                    // Still starting up (loading locales/tables). Do not block a frame waiting for it -
                    // the fallback covers this call, and the real value appears on the next OnShow/
                    // RefreshLocalizedText pass once initialization settles (Splash already awaits
                    // UgsInitializer before navigating to Home, so in practice this window is tiny).
                    return fallback;
                }

                string localized = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key);
                if (string.IsNullOrEmpty(localized))
                {
                    Debug.LogWarning($"UiText: key '{key}' resolved to an empty/missing entry in table '{TableName}' - using English fallback.");
                    return fallback;
                }

                return localized;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"UiText: localization lookup failed for key '{key}' - {ex.Message}. Using English fallback.");
                return fallback;
            }
        }
    }
}
