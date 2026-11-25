using System.Collections.Generic;
using System.IO;
using System.Linq;
using TTTXO.Game.UI;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace TTTXO.Game.Editor
{
    /// <summary>
    /// One-shot Editor tooling (Milestone 3, Docs/02-GDD-TicTacToe.md#8 Localización) that builds the
    /// Unity Localization assets for the project's 10 target locales (<see cref="SupportedLocales"/>):
    /// a <see cref="LocalizationSettings"/> asset wired with the startup locale selector chain
    /// (persisted PlayerPrefs choice -&gt; device locale -&gt; English), one <see cref="Locale"/> asset
    /// per language, and a single "UiStrings" <see cref="StringTableCollection"/> populated from the
    /// <see cref="Translations"/> dictionary below with every translatable key currently in
    /// <c>UiText</c> (see that class for which entries are glyphs/pure format strings and therefore
    /// intentionally NOT localized).
    ///
    /// Re-running the menu item is safe and idempotent: existing locales/tables/entries are reused and
    /// updated in place instead of duplicated, so it can be re-run any time <see cref="Translations"/>
    /// changes (e.g. after a copy edit) without leaving stale duplicate keys behind.
    /// </summary>
    public static class LocalizationSetup
    {
        private const string RootFolder = "Assets/Localization";
        private const string LocalesFolder = RootFolder + "/Locales";
        private const string TablesFolder = RootFolder + "/Tables";
        private const string SettingsAssetPath = RootFolder + "/Localization Settings.asset";
        private const string TableCollectionName = "UiStrings";

        /// <summary>Must match <c>PlayerPrefLocaleSelector.PlayerPreferenceKey</c>'s default and <c>SettingsScreenController.LocalePlayerPrefKey</c> - the same key is used to read/write the persisted choice from both the startup selector and the Settings screen dropdown.</summary>
        private const string LocalePlayerPrefKey = "selected-locale";

        /// <summary>
        /// Translation source: one entry per localizable <c>UiText</c> key, values in
        /// <see cref="SupportedLocales.All"/> order (en, es, fr, de, pt, it, id, vi, tr, pl). Keep this
        /// in sync with UiText.cs's English fallback literals - the "en" column here should always
        /// match the fallback string passed to <c>Localize(...)</c> for the same key.
        /// </summary>
        private static readonly Dictionary<string, string[]> Translations = new()
        {
            // --- Common ---
            ["Common/Cancel"] = new[] { "Cancel", "Cancelar", "Annuler", "Abbrechen", "Cancelar", "Annulla", "Batal", "Hủy", "İptal", "Anuluj" },
            ["Common/Confirm"] = new[] { "Confirm", "Confirmar", "Confirmer", "Bestätigen", "Confirmar", "Conferma", "Konfirmasi", "Xác nhận", "Onayla", "Potwierdź" },
            ["Common/Resume"] = new[] { "Resume", "Reanudar", "Reprendre", "Fortsetzen", "Retomar", "Riprendi", "Lanjutkan", "Tiếp tục", "Devam et", "Wznów" },
            ["Common/Abandon"] = new[] { "Abandon", "Abandonar", "Abandonner", "Aufgeben", "Abandonar", "Abbandona", "Menyerah", "Từ bỏ", "Vazgeç", "Poddaj się" },
            ["Common/PlayerNamePlaceholder"] = new[] { "Player", "Jugador", "Joueur", "Spieler", "Jogador", "Giocatore", "Pemain", "Người chơi", "Oyuncu", "Gracz" },
            ["Common/ComingSoon"] = new[] { "Coming soon", "Próximamente", "Bientôt disponible", "Demnächst", "Em breve", "In arrivo", "Segera hadir", "Sắp ra mắt", "Yakında", "Wkrótce" },

            // --- Splash ---
            ["Splash/AppTitle"] = new[] { "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO" },
            ["Splash/Loading"] = new[] { "Loading...", "Cargando...", "Chargement...", "Wird geladen...", "Carregando...", "Caricamento...", "Memuat...", "Đang tải...", "Yükleniyor...", "Ładowanie..." },
            ["Splash/ErrorMessage"] = new[]
            {
                "Something went wrong. Please check your connection and try again.",
                "Algo salió mal. Revisa tu conexión e inténtalo de nuevo.",
                "Une erreur s'est produite. Vérifiez votre connexion et réessayez.",
                "Etwas ist schiefgelaufen. Bitte überprüfe deine Verbindung und versuche es erneut.",
                "Algo deu errado. Verifique sua conexão e tente novamente.",
                "Qualcosa è andato storto. Controlla la connessione e riprova.",
                "Terjadi kesalahan. Periksa koneksimu dan coba lagi.",
                "Đã xảy ra lỗi. Vui lòng kiểm tra kết nối và thử lại.",
                "Bir şeyler ters gitti. Bağlantını kontrol edip tekrar dene.",
                "Coś poszło nie tak. Sprawdź połączenie i spróbuj ponownie.",
            },
            ["Splash/RetryButton"] = new[] { "Retry", "Reintentar", "Réessayer", "Wiederholen", "Tentar novamente", "Riprova", "Coba lagi", "Thử lại", "Tekrar dene", "Ponów" },

            // --- Home ---
            ["Home/Title"] = new[] { "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO", "TIC TAC XO" },
            ["Home/PlayButton"] = new[] { "Play", "Jugar", "Jouer", "Spielen", "Jogar", "Gioca", "Main", "Chơi", "Oyna", "Graj" },
            ["Home/ProfileLabel"] = new[] { "Profile", "Perfil", "Profil", "Profil", "Perfil", "Profilo", "Profil", "Hồ sơ", "Profil", "Profil" },
            ["Home/StoreLabel"] = new[] { "Store", "Tienda", "Boutique", "Shop", "Loja", "Negozio", "Toko", "Cửa hàng", "Mağaza", "Sklep" },
            ["Home/SettingsLabel"] = new[] { "Settings", "Ajustes", "Réglages", "Einstellungen", "Ajustes", "Impostazioni", "Pengaturan", "Cài đặt", "Ayarlar", "Ustawienia" },

            // --- ModeSelect ---
            ["ModeSelect/Title"] = new[] { "Choose a mode", "Elige un modo", "Choisissez un mode", "Modus wählen", "Escolha um modo", "Scegli una modalità", "Pilih mode", "Chọn chế độ", "Bir mod seç", "Wybierz tryb" },
            ["ModeSelect/SinglePlayerTitle"] = new[] { "Single Player", "Un jugador", "Un joueur", "Einzelspieler", "Um jogador", "Giocatore singolo", "Satu pemain", "Một người chơi", "Tek oyunculu", "Jeden gracz" },
            ["ModeSelect/SinglePlayerDescription"] = new[]
            {
                "Play vs AI · pick difficulty", "Juega contra la IA · elige la dificultad", "Jouez contre l'IA · choisissez la difficulté",
                "Gegen die KI spielen · Schwierigkeit wählen", "Jogue contra a IA · escolha a dificuldade", "Gioca contro l'IA · scegli la difficoltà",
                "Main lawan AI · pilih tingkat kesulitan", "Đấu với AI · chọn độ khó", "Yapay zekaya karşı oyna · zorluk seç", "Graj z AI · wybierz poziom trudności",
            },
            ["ModeSelect/LocalMultiplayerTitle"] = new[] { "Local 2P", "2 jugadores local", "2 joueurs local", "Lokal 2 Spieler", "2 jogadores local", "2 giocatori locale", "2 pemain lokal", "2 người local", "Yerel 2 kişilik", "Lokalnie 2 os." },
            ["ModeSelect/LocalMultiplayerDescription"] = new[] { "Same device", "Mismo dispositivo", "Même appareil", "Gleiches Gerät", "Mesmo dispositivo", "Stesso dispositivo", "Perangkat yang sama", "Cùng thiết bị", "Aynı cihaz", "To samo urządzenie" },
            ["ModeSelect/OnlineTitle"] = new[] { "Online", "Online", "En ligne", "Online", "Online", "Online", "Online", "Trực tuyến", "Çevrimiçi", "Online" },
            // "Ranked" is kept as an English loanword in de/id/vi/tr/pl - common gamer terminology in
            // those markets - and translated in the Romance languages (es/fr/pt/it).
            ["ModeSelect/OnlineDescription"] = new[]
            {
                "Quickmatch or Ranked", "Partida rápida o Clasificatoria", "Match rapide ou Classé", "Schnellspiel oder Ranked",
                "Partida rápida ou Ranqueada", "Partita rapida o Classificata", "Quickmatch atau Ranked", "Nhanh hoặc Ranked",
                "Hızlı Eşleşme veya Ranked", "Szybka gra lub Ranked",
            },
            ["ModeSelect/OnlineSoonTag"] = new[] { "Soon", "Pronto", "Bientôt", "Bald", "Em breve", "Presto", "Segera", "Sắp có", "Yakında", "Wkrótce" },
            ["ModeSelect/OnlineComingSoonMessage"] = new[]
            {
                "Online play is coming soon!", "¡El modo online llega pronto!", "Le mode en ligne arrive bientôt !", "Online-Modus kommt bald!",
                "O modo online chega em breve!", "La modalità online arriva presto!", "Mode online segera hadir!", "Chế độ trực tuyến sắp ra mắt!",
                "Çevrimiçi mod yakında geliyor!", "Tryb online wkrótce dostępny!",
            },
            ["ModeSelect/OnlineQuickmatchTitle"] = new[] { "Quickmatch", "Partida rápida", "Match rapide", "Schnellspiel", "Partida rápida", "Partita rapida", "Quickmatch", "Trận nhanh", "Hızlı Eşleşme", "Szybka gra" },
            ["ModeSelect/OnlineQuickmatchDescription"] = new[]
            {
                "Play a rival online now", "Juega contra un rival online ahora", "Affrontez un adversaire en ligne maintenant", "Jetzt gegen einen Online-Gegner spielen",
                "Jogue contra um rival online agora", "Sfida subito un rivale online", "Main lawan rival online sekarang", "Đấu ngay với đối thủ trực tuyến",
                "Şimdi çevrimiçi bir rakiple oyna", "Zagraj teraz online z rywalem",
            },
            ["ModeSelect/OnlineRankedTitle"] = new[] { "Ranked", "Clasificatoria", "Classé", "Ranked", "Ranqueada", "Classificata", "Ranked", "Ranked", "Ranked", "Ranked" },
            ["ModeSelect/OnlineRankedDescription"] = new[]
            {
                "Competitive ladder", "Ladder competitivo", "Classement compétitif", "Kompetitive Rangliste",
                "Ranking competitivo", "Classifica competitiva", "Papan peringkat kompetitif", "Bảng xếp hạng thi đấu",
                "Rekabetçi sıralama", "Ranking rywalizacyjny",
            },
            ["ModeSelect/OnlineRankedComingSoonMessage"] = new[]
            {
                "Ranked is coming soon!", "¡El modo Clasificatorio llega pronto!", "Le mode Classé arrive bientôt !", "Ranked kommt bald!",
                "O modo Ranqueado chega em breve!", "La modalità Classificata arriva presto!", "Ranked segera hadir!", "Ranked sắp ra mắt!",
                "Ranked yakında geliyor!", "Ranked wkrótce dostępny!",
            },

            // --- Matchmaking ---
            ["Matchmaking/SearchingLabel"] = new[]
            {
                "Searching for a rival...", "Buscando un rival...", "Recherche d'un adversaire...", "Suche nach einem Gegner...",
                "Procurando um rival...", "Ricerca di un rivale...", "Mencari rival...", "Đang tìm đối thủ...",
                "Rakip aranıyor...", "Szukanie rywala...",
            },
            ["Matchmaking/ConnectingLabel"] = new[]
            {
                "Connecting to match...", "Conectando a la partida...", "Connexion à la partie...", "Verbindung zum Spiel wird hergestellt...",
                "Conectando à partida...", "Connessione alla partita...", "Menghubungkan ke pertandingan...", "Đang kết nối vào trận đấu...",
                "Maça bağlanılıyor...", "Łączenie z meczem...",
            },
            ["Matchmaking/FallbackTitle"] = new[] { "Taking too long?", "¿Tarda demasiado?", "Ça prend trop de temps ?", "Dauert es zu lange?", "Demorando muito?", "Ci vuole troppo?", "Terlalu lama?", "Mất quá nhiều thời gian?", "Çok mu uzun sürüyor?", "Trwa zbyt długo?" },
            ["Matchmaking/PlayVsAiButton"] = new[] { "Play vs AI", "Jugar contra la IA", "Jouer contre l'IA", "Gegen die KI spielen", "Jogar contra a IA", "Gioca contro l'IA", "Main lawan AI", "Đấu với AI", "Yapay zekaya karşı oyna", "Graj z AI" },

            // --- BoardSelect ---
            ["BoardSelect/BoardSizeSectionLabel"] = new[] { "Board size", "Tamaño del tablero", "Taille du plateau", "Spielfeldgröße", "Tamanho do tabuleiro", "Dimensione del tabellone", "Ukuran papan", "Kích thước bàn cờ", "Tahta boyutu", "Rozmiar planszy" },
            ["BoardSelect/BoardAlignFormat"] = new[] { "align {0}", "alinea {0}", "aligne {0}", "{0} in Reihe", "alinhe {0}", "allinea {0}", "sejajarkan {0}", "xếp {0} hàng", "{0} hizala", "ułóż {0}" },
            ["BoardSelect/DifficultyLabel"] = new[] { "Difficulty", "Dificultad", "Difficulté", "Schwierigkeit", "Dificuldade", "Difficoltà", "Kesulitan", "Độ khó", "Zorluk", "Trudność" },
            ["BoardSelect/DifficultyEasy"] = new[] { "Easy", "Fácil", "Facile", "Leicht", "Fácil", "Facile", "Mudah", "Dễ", "Kolay", "Łatwy" },
            ["BoardSelect/DifficultyMedium"] = new[] { "Medium", "Medio", "Moyen", "Mittel", "Médio", "Medio", "Sedang", "Trung bình", "Orta", "Średni" },
            ["BoardSelect/DifficultyHard"] = new[] { "Hard", "Difícil", "Difficile", "Schwer", "Difícil", "Difficile", "Sulit", "Khó", "Zor", "Trudny" },
            ["BoardSelect/DifficultyAdaptive"] = new[] { "Adaptive", "Adaptativa", "Adaptative", "Adaptiv", "Adaptativa", "Adattiva", "Adaptif", "Thích ứng", "Uyarlamalı", "Adaptacyjny" },
            ["BoardSelect/ConfirmButton"] = new[] { "Start Game", "Iniciar partida", "Démarrer la partie", "Spiel starten", "Iniciar partida", "Inizia partita", "Mulai permainan", "Bắt đầu", "Oyunu başlat", "Rozpocznij grę" },

            // --- Game ---
            ["Game/YouLabel"] = new[] { "You", "Tú", "Toi", "Du", "Você", "Tu", "Kamu", "Bạn", "Sen", "Ty" },
            ["Game/RivalLabel"] = new[] { "Rival", "Rival", "Adversaire", "Gegner", "Rival", "Rivale", "Lawan", "Đối thủ", "Rakip", "Rywal" },
            ["Game/AiThinking"] = new[] { "AI is thinking...", "La IA está pensando...", "L'IA réfléchit...", "KI denkt nach...", "A IA está pensando...", "L'IA sta pensando...", "AI sedang berpikir...", "AI đang suy nghĩ...", "YZ düşünüyor...", "SI myśli..." },
            ["Game/Player1Label"] = new[] { "Player 1", "Jugador 1", "Joueur 1", "Spieler 1", "Jogador 1", "Giocatore 1", "Pemain 1", "Người chơi 1", "Oyuncu 1", "Gracz 1" },
            ["Game/Player2Label"] = new[] { "Player 2", "Jugador 2", "Joueur 2", "Spieler 2", "Jogador 2", "Giocatore 2", "Pemain 2", "Người chơi 2", "Oyuncu 2", "Gracz 2" },
            ["Game/PauseTitle"] = new[] { "Paused", "Pausa", "Pause", "Pausiert", "Pausado", "In pausa", "Dijeda", "Tạm dừng", "Duraklatıldı", "Wstrzymano" },
            ["Game/WireSubscribed"] = new[] { "◉ Subscribed", "◉ Conectado", "◉ Connecté", "◉ Verbunden", "◉ Conectado", "◉ Connesso", "◉ Terhubung", "◉ Đã kết nối", "◉ Bağlandı", "◉ Połączono" },
            ["Game/WireConnecting"] = new[] { "◌ Connecting...", "◌ Conectando...", "◌ Connexion...", "◌ Verbindung wird hergestellt...", "◌ Conectando...", "◌ Connessione...", "◌ Menghubungkan...", "◌ Đang kết nối...", "◌ Bağlanılıyor...", "◌ Łączenie..." },
            ["Game/WireOffline"] = new[] { "◌ Offline", "◌ Sin conexión", "◌ Hors ligne", "◌ Offline", "◌ Offline", "◌ Offline", "◌ Offline", "◌ Ngoại tuyến", "◌ Çevrimdışı", "◌ Offline" },
            ["Game/AbandonConfirmTitle"] = new[] { "Abandon match?", "¿Abandonar la partida?", "Abandonner la partie ?", "Spiel aufgeben?", "Abandonar a partida?", "Abbandonare la partita?", "Tinggalkan pertandingan?", "Từ bỏ trận đấu?", "Maçtan vazgeçilsin mi?", "Poddać mecz?" },
            ["Game/AbandonConfirmMessage"] = new[]
            {
                "Your rival will win if you leave now. This cannot be undone.", "Tu rival ganará si te vas ahora. Esta acción no se puede deshacer.",
                "Votre adversaire gagnera si vous partez maintenant. Cette action est irréversible.", "Dein Gegner gewinnt, wenn du jetzt gehst. Das kann nicht rückgängig gemacht werden.",
                "Seu rival vencerá se você sair agora. Essa ação não pode ser desfeita.", "Il tuo rivale vincerà se esci ora. Non può essere annullato.",
                "Rivalmu akan menang jika kamu keluar sekarang. Tindakan ini tidak dapat dibatalkan.", "Đối thủ sẽ thắng nếu bạn rời đi ngay bây giờ. Không thể hoàn tác.",
                "Şimdi ayrılırsan rakibin kazanır. Bu işlem geri alınamaz.", "Jeśli teraz wyjdziesz, wygra Twój rywal. Tej operacji nie można cofnąć.",
            },

            // --- Result ---
            ["Result/YouWin"] = new[] { "You Win! \U0001F389", "¡Ganaste! \U0001F389", "Tu gagnes ! \U0001F389", "Du gewinnst! \U0001F389", "Você venceu! \U0001F389", "Hai vinto! \U0001F389", "Kamu Menang! \U0001F389", "Bạn thắng rồi! \U0001F389", "Kazandın! \U0001F389", "Wygrałeś! \U0001F389" },
            ["Result/YouLose"] = new[] { "You Lose", "Perdiste", "Tu perds", "Du verlierst", "Você perdeu", "Hai perso", "Kamu Kalah", "Bạn thua rồi", "Kaybettin", "Przegrałeś" },
            ["Result/Draw"] = new[] { "Draw", "Empate", "Match nul", "Unentschieden", "Empate", "Pareggio", "Seri", "Hòa", "Berabere", "Remis" },
            ["Result/Player1Wins"] = new[] { "Player 1 Wins!", "¡Gana el Jugador 1!", "Le Joueur 1 gagne !", "Spieler 1 gewinnt!", "Jogador 1 venceu!", "Il Giocatore 1 vince!", "Pemain 1 Menang!", "Người chơi 1 thắng!", "Oyuncu 1 kazandı!", "Gracz 1 wygrywa!" },
            ["Result/Player2Wins"] = new[] { "Player 2 Wins!", "¡Gana el Jugador 2!", "Le Joueur 2 gagne !", "Spieler 2 gewinnt!", "Jogador 2 venceu!", "Il Giocatore 2 vince!", "Pemain 2 Menang!", "Người chơi 2 thắng!", "Oyuncu 2 kazandı!", "Gracz 2 wygrywa!" },
            ["Result/RewardCardTitle"] = new[] { "Reward", "Recompensa", "Récompense", "Belohnung", "Recompensa", "Ricompensa", "Hadiah", "Phần thưởng", "Ödül", "Nagroda" },
            ["Result/RematchButton"] = new[] { "Rematch", "Revancha", "Revanche", "Revanche", "Revanche", "Rivincita", "Main lagi", "Đấu lại", "Rövanş", "Rewanż" },
            ["Result/HomeButton"] = new[] { "Home", "Inicio", "Accueil", "Start", "Início", "Home", "Beranda", "Trang chủ", "Ana sayfa", "Menu" },
            ["Result/OpponentLeftNote"] = new[]
            {
                "Your rival left the match.", "Tu rival abandonó la partida.", "Votre adversaire a quitté la partie.", "Dein Gegner hat das Spiel verlassen.",
                "Seu rival abandonou a partida.", "Il tuo rivale ha abbandonato la partita.", "Rivalmu meninggalkan pertandingan.", "Đối thủ của bạn đã rời trận đấu.",
                "Rakibin maçı terk etti.", "Twój rywal opuścił mecz.",
            },
            // "Ranked"/"MMR" kept as English loanwords in de/id/vi/tr/pl, same treatment as
            // ModeSelect/OnlineRankedTitle - translated in the Romance languages (es/fr/pt/it).
            ["Result/RankedCardTitle"] = new[] { "Ranked", "Clasificatoria", "Classé", "Ranked", "Ranqueada", "Classificata", "Ranked", "Ranked", "Ranked", "Ranked" },

            // --- Profile ---
            ["Profile/Title"] = new[] { "Profile", "Perfil", "Profil", "Profil", "Perfil", "Profilo", "Profil", "Hồ sơ", "Profil", "Profil" },
            // Two separate rows, not one: the top link opens the equip panel, the one at the bottom of
            // that panel leaves for the Store. They used to share this key and rendered identical text
            // twice on screen.
            ["Profile/EditLookLinkFormat"] = new[]
            {
                "{0} Edit look {1}", "{0} Editar look {1}", "{0} Modifier le look {1}",
                "{0} Look bearbeiten {1}", "{0} Editar visual {1}", "{0} Modifica look {1}",
                "{0} Edit tampilan {1}", "{0} Chỉnh giao diện {1}", "{0} Görünümü düzenle {1}", "{0} Edytuj wygląd {1}",
            },
            ["Profile/StoreLinkFormat"] = new[]
            {
                "{0} Go to Store {1}", "{0} Ir a la Tienda {1}", "{0} Aller à la Boutique {1}",
                "{0} Zum Shop {1}", "{0} Ir à Loja {1}", "{0} Vai al Negozio {1}",
                "{0} Ke Toko {1}", "{0} Đến Cửa hàng {1}", "{0} Mağazaya git {1}", "{0} Idź do Sklepu {1}",
            },
            ["Profile/WinRateCaption"] = new[] { "Win rate", "Tasa de victorias", "Taux de victoires", "Siegquote", "Taxa de vitórias", "Percentuale vittorie", "Tingkat menang", "Tỷ lệ thắng", "Kazanma oranı", "% wygranych" },
            ["Profile/StreakCaption"] = new[] { "Streak", "Racha", "Série", "Serie", "Sequência", "Serie", "Beruntun", "Chuỗi thắng", "Seri", "Seria" },
            ["Profile/MatchHistoryButton"] = new[] { "Match history", "Historial de partidas", "Historique des parties", "Spielverlauf", "Histórico de partidas", "Cronologia partite", "Riwayat pertandingan", "Lịch sử trận đấu", "Maç geçmişi", "Historia meczów" },
            ["Profile/LeaderboardButton"] = new[]
            {
                "Leaderboard (Ranked)", "Clasificación (Ranked)", "Classement (Ranked)", "Bestenliste (Ranked)",
                "Classificação (Ranked)", "Classifica (Ranked)", "Papan peringkat (Ranked)", "Bảng xếp hạng (Ranked)",
                "Sıralama (Ranked)", "Ranking (Ranked)",
            },

            // --- Profile Ranked section (Milestone 5) ---
            ["Profile/RankedSectionTitle"] = new[] { "Ranked", "Clasificatoria", "Classé", "Ranked", "Ranqueada", "Classificata", "Ranked", "Ranked", "Ranked", "Ranked" },
            ["Profile/RankedPlacementFormat"] = new[] { "Placement {0}/{1}", "Colocación {0}/{1}", "Placement {0}/{1}", "Platzierung {0}/{1}", "Colocação {0}/{1}", "Piazzamento {0}/{1}", "Penempatan {0}/{1}", "Xếp hạng {0}/{1}", "Yerleştirme {0}/{1}", "Plasowanie {0}/{1}" },

            // --- Equip panel section headings ---
            ["Profile/EquipAvatarsTitle"] = new[] { "Avatar", "Avatar", "Avatar", "Avatar", "Avatar", "Avatar", "Avatar", "Ảnh đại diện", "Avatar", "Awatar" },
            ["Profile/EquipFramesTitle"] = new[] { "Frame", "Marco", "Cadre", "Rahmen", "Moldura", "Cornice", "Bingkai", "Khung", "Çerçeve", "Ramka" },
            ["Profile/EquipBannersTitle"] = new[] { "Banner", "Banner", "Bannière", "Banner", "Banner", "Banner", "Spanduk", "Biểu ngữ", "Afiş", "Baner" },
            ["Profile/EquipPieceSkinsTitle"] = new[] { "Pieces", "Fichas", "Pions", "Spielsteine", "Peças", "Pedine", "Bidak", "Quân cờ", "Taşlar", "Pionki" },
            ["Profile/EquipBoardSkinsTitle"] = new[] { "Board", "Tablero", "Plateau", "Spielfeld", "Tabuleiro", "Tabellone", "Papan", "Bàn cờ", "Tahta", "Plansza" },
            ["Profile/RankedTierBronze"] = new[] { "Bronze", "Bronce", "Bronze", "Bronze", "Bronze", "Bronzo", "Perunggu", "Đồng", "Bronz", "Brąz" },
            ["Profile/RankedTierSilver"] = new[] { "Silver", "Plata", "Argent", "Silber", "Prata", "Argento", "Perak", "Bạc", "Gümüş", "Srebro" },
            ["Profile/RankedTierGold"] = new[] { "Gold", "Oro", "Or", "Gold", "Ouro", "Oro", "Emas", "Vàng", "Altın", "Złoto" },
            ["Profile/RankedTierPlatinum"] = new[] { "Platinum", "Platino", "Platine", "Platin", "Platina", "Platino", "Platinum", "Bạch kim", "Platin", "Platyna" },
            ["Profile/RankedTierDiamond"] = new[] { "Diamond", "Diamante", "Diamant", "Diamant", "Diamante", "Diamante", "Berlian", "Kim cương", "Elmas", "Diament" },

            ["Profile/DeleteDataButton"] = new[] { "Delete my data", "Eliminar mis datos", "Supprimer mes données", "Meine Daten löschen", "Excluir meus dados", "Elimina i miei dati", "Hapus data saya", "Xóa dữ liệu của tôi", "Verilerimi sil", "Usuń moje dane" },
            ["Profile/DeleteConfirmTitle"] = new[] { "Delete my data?", "¿Eliminar mis datos?", "Supprimer mes données ?", "Meine Daten löschen?", "Excluir meus dados?", "Eliminare i miei dati?", "Hapus data saya?", "Xóa dữ liệu của tôi?", "Verilerim silinsin mi?", "Usunąć moje dane?" },
            ["Profile/DeleteConfirmMessage"] = new[]
            {
                "This permanently deletes your local progress, currency and match history on this device. This cannot be undone.",
                "Esto elimina para siempre tu progreso local, tu moneda y tu historial de partidas en este dispositivo. Esta acción no se puede deshacer.",
                "Cela supprime définitivement votre progression locale, votre monnaie et votre historique de parties sur cet appareil. Cette action est irréversible.",
                "Dadurch werden dein lokaler Fortschritt, deine Währung und dein Spielverlauf auf diesem Gerät dauerhaft gelöscht. Das kann nicht rückgängig gemacht werden.",
                "Isso exclui permanentemente seu progresso local, sua moeda e seu histórico de partidas neste dispositivo. Essa ação não pode ser desfeita.",
                "Questa azione elimina definitivamente i tuoi progressi locali, la valuta e la cronologia partite su questo dispositivo. Non può essere annullata.",
                "Ini akan menghapus progres lokal, mata uang, dan riwayat pertandinganmu di perangkat ini secara permanen. Tindakan ini tidak dapat dibatalkan.",
                "Thao tác này sẽ xóa vĩnh viễn tiến trình, tiền tệ và lịch sử trận đấu của bạn trên thiết bị này. Không thể hoàn tác.",
                "Bu işlem bu cihazdaki yerel ilerlemeni, paranı ve maç geçmişini kalıcı olarak siler. Bu işlem geri alınamaz.",
                "To trwale usunie Twój lokalny postęp, walutę i historię meczów na tym urządzeniu. Tej operacji nie można cofnąć.",
            },
            ["Profile/DeleteConfirmButton"] = new[] { "Delete", "Eliminar", "Supprimer", "Löschen", "Excluir", "Elimina", "Hapus", "Xóa", "Sil", "Usuń" },

            // --- MatchHistory ---
            ["MatchHistory/Title"] = new[] { "Match History", "Historial de partidas", "Historique des parties", "Spielverlauf", "Histórico de partidas", "Cronologia partite", "Riwayat Pertandingan", "Lịch sử trận đấu", "Maç Geçmişi", "Historia meczów" },
            ["MatchHistory/FilterAll"] = new[] { "All", "Todas", "Toutes", "Alle", "Todas", "Tutte", "Semua", "Tất cả", "Tümü", "Wszystkie" },
            ["MatchHistory/FilterSingle"] = new[] { "Single", "1 jugador", "Solo", "Solo", "Solo", "Singolo", "Solo", "Đơn", "Tekli", "Solo" },
            ["MatchHistory/FilterLocal"] = new[] { "Local", "Local", "Local", "Lokal", "Local", "Locale", "Lokal", "Local", "Yerel", "Lokalnie" },
            ["MatchHistory/EmptyState"] = new[]
            {
                "No matches yet. Play a game to see it here!", "Aún no hay partidas. ¡Juega una para verla aquí!", "Aucune partie pour l'instant. Jouez une partie pour la voir ici !",
                "Noch keine Spiele. Spiele eine Partie, um sie hier zu sehen!", "Ainda não há partidas. Jogue uma para vê-la aqui!", "Ancora nessuna partita. Gioca una partita per vederla qui!",
                "Belum ada pertandingan. Main dulu untuk melihatnya di sini!", "Chưa có trận đấu nào. Chơi một ván để xem ở đây!", "Henüz maç yok. Burada görmek için bir oyun oyna!", "Brak meczów. Zagraj, żeby zobaczyć je tutaj!",
            },
            ["MatchHistory/ResultWin"] = new[] { "Win", "Victoria", "Victoire", "Sieg", "Vitória", "Vittoria", "Menang", "Thắng", "Galibiyet", "Wygrana" },
            ["MatchHistory/ResultLoss"] = new[] { "Loss", "Derrota", "Défaite", "Niederlage", "Derrota", "Sconfitta", "Kalah", "Thua", "Kayıp", "Porażka" },
            ["MatchHistory/ResultDraw"] = new[] { "Draw", "Empate", "Nul", "Unentschieden", "Empate", "Pareggio", "Seri", "Hòa", "Berabere", "Remis" },
            ["MatchHistory/LocalPlayer1Win"] = new[] { "Player 1 won", "Ganó el Jugador 1", "Le Joueur 1 a gagné", "Spieler 1 hat gewonnen", "Jogador 1 venceu", "Il Giocatore 1 ha vinto", "Pemain 1 menang", "Người chơi 1 thắng", "Oyuncu 1 kazandı", "Gracz 1 wygrał" },
            ["MatchHistory/LocalPlayer2Win"] = new[] { "Player 2 won", "Ganó el Jugador 2", "Le Joueur 2 a gagné", "Spieler 2 hat gewonnen", "Jogador 2 venceu", "Il Giocatore 2 ha vinto", "Pemain 2 menang", "Người chơi 2 thắng", "Oyuncu 2 kazandı", "Gracz 2 wygrał" },
            // Kept as universal one-letter units (m/s) across every locale, per the narrow-mobile-UI
            // guidance in Docs/02-GDD-TicTacToe.md#8 - most compact and unambiguous option.
            ["MatchHistory/DurationMinutesFormat"] = new[] { "{0}m {1}s", "{0}m {1}s", "{0}m {1}s", "{0}m {1}s", "{0}m {1}s", "{0}m {1}s", "{0}m {1}s", "{0}m {1}s", "{0}m {1}s", "{0}m {1}s" },
            ["MatchHistory/DurationSecondsFormat"] = new[] { "{0}s", "{0}s", "{0}s", "{0}s", "{0}s", "{0}s", "{0}s", "{0}s", "{0}s", "{0}s" },
            ["MatchHistory/TimeAgoJustNow"] = new[] { "Just now", "Recién", "À l'instant", "Gerade eben", "Agora mesmo", "Proprio ora", "Baru saja", "Vừa xong", "Az önce", "Przed chwilą" },
            ["MatchHistory/TimeAgoMinutesFormat"] = new[] { "{0}m ago", "hace {0}m", "il y a {0}m", "vor {0}m", "há {0}m", "{0}m fa", "{0}m lalu", "{0}m trước", "{0}m önce", "{0}m temu" },
            ["MatchHistory/TimeAgoHoursFormat"] = new[] { "{0}h ago", "hace {0}h", "il y a {0}h", "vor {0}h", "há {0}h", "{0}h fa", "{0}j lalu", "{0}h trước", "{0} sa önce", "{0}h temu" },
            ["MatchHistory/TimeAgoDaysFormat"] = new[] { "{0}d ago", "hace {0}d", "il y a {0}j", "vor {0}d", "há {0}d", "{0}g fa", "{0}h lalu", "{0}n trước", "{0}g önce", "{0}d temu" },

            // --- Leaderboard (Milestone 5) ---
            ["Leaderboard/Title"] = new[] { "Leaderboard", "Clasificación", "Classement", "Bestenliste", "Classificação", "Classifica", "Papan peringkat", "Bảng xếp hạng", "Sıralama", "Ranking" },
            ["Leaderboard/LoadingMessage"] = new[]
            {
                "Loading leaderboard...", "Cargando clasificación...", "Chargement du classement...", "Bestenliste wird geladen...",
                "Carregando classificação...", "Caricamento classifica...", "Memuat papan peringkat...", "Đang tải bảng xếp hạng...",
                "Sıralama yükleniyor...", "Ładowanie rankingu...",
            },
            ["Leaderboard/UnavailableMessage"] = new[]
            {
                "The Ranked leaderboard isn't available here yet. Check back soon.",
                "La clasificación de Ranked todavía no está disponible aquí. Vuelve pronto.",
                "Le classement Ranked n'est pas encore disponible ici. Revenez bientôt.",
                "Die Ranked-Bestenliste ist hier noch nicht verfügbar. Schau bald wieder vorbei.",
                "A classificação Ranqueada ainda não está disponível aqui. Volte em breve.",
                "La classifica Classificata non è ancora disponibile qui. Torna presto.",
                "Papan peringkat Ranked belum tersedia di sini. Kembali lagi nanti.",
                "Bảng xếp hạng Ranked chưa khả dụng ở đây. Hãy quay lại sau.",
                "Ranked sıralaması burada henüz kullanılamıyor. Yakında tekrar kontrol et.",
                "Ranking Ranked nie jest tu jeszcze dostępny. Sprawdź wkrótce ponownie.",
            },
            ["Leaderboard/EmptyMessage"] = new[]
            {
                "No ranked players yet on this board.", "Todavía no hay jugadores clasificados en este tablero.",
                "Aucun joueur classé pour l'instant sur ce plateau.", "Noch keine gerankten Spieler auf diesem Spielfeld.",
                "Ainda não há jogadores ranqueados neste tabuleiro.", "Nessun giocatore classificato ancora su questo tabellone.",
                "Belum ada pemain berperingkat di papan ini.", "Chưa có người chơi nào được xếp hạng trên bàn cờ này.",
                "Bu tahtada henüz sıralı oyuncu yok.", "Brak jeszcze rankingowych graczy na tej planszy.",
            },
            ["Leaderboard/PlacementRequiredMessage"] = new[]
            {
                "Finish your placement matches to join the ranking.", "Termina tus partidas de colocación para unirte a la clasificación.",
                "Terminez vos matchs de placement pour rejoindre le classement.", "Schließe deine Platzierungsspiele ab, um in die Bestenliste zu kommen.",
                "Termine suas partidas de colocação para entrar na classificação.", "Completa le partite di piazzamento per entrare in classifica.",
                "Selesaikan pertandingan penempatanmu untuk bergabung ke papan peringkat.", "Hoàn thành các trận xếp hạng để tham gia bảng xếp hạng.",
                "Sıralamaya katılmak için yerleştirme maçlarını tamamla.", "Ukończ mecze plasujące, aby dołączyć do rankingu.",
            },

            // --- Store ---
            ["Store/Title"] = new[] { "Store", "Tienda", "Boutique", "Shop", "Loja", "Negozio", "Toko", "Cửa hàng", "Mağaza", "Sklep" },
            ["Store/TabSkins"] = new[] { "Skins", "Aspectos", "Apparences", "Skins", "Skins", "Skin", "Skin", "Giao diện", "Skin", "Skórki" },
            ["Store/TabArenas"] = new[] { "Arenas", "Arenas", "Arènes", "Arenen", "Arenas", "Arene", "Arena", "Đấu trường", "Arenalar", "Arena" },
            ["Store/TabFx"] = new[] { "FX", "FX", "FX", "FX", "FX", "FX", "FX", "FX", "FX", "FX" },
            ["Store/ComingSoonCardTitle"] = new[] { "Coming soon", "Próximamente", "Bientôt disponible", "Demnächst", "Em breve", "In arrivo", "Segera hadir", "Sắp ra mắt", "Yakında", "Wkrótce" },
            ["Store/ComingSoonCardDescription"] = new[]
            {
                "New cosmetics are on their way.", "Nuevos cosméticos están en camino.", "De nouveaux cosmétiques arrivent.", "Neue Kosmetik ist unterwegs.",
                "Novos cosméticos estão a caminho.", "Nuovi cosmetici in arrivo.", "Kosmetik baru akan segera hadir.", "Vật phẩm trang trí mới sắp ra mắt.",
                "Yeni kozmetikler yolda.", "Nowe kosmetyki już w drodze.",
            },
            ["Store/BuyCurrencyToast"] = new[]
            {
                "Currency purchases are coming soon!", "¡La compra de moneda llega pronto!", "L'achat de monnaie arrive bientôt !", "Der Kauf von Währung kommt bald!",
                "A compra de moeda chega em breve!", "L'acquisto di valuta arriva presto!", "Pembelian mata uang segera hadir!", "Tính năng mua tiền sắp ra mắt!",
                "Para birimi satın alma yakında geliyor!", "Zakup waluty już wkrótce!",
            },

            // --- Settings ---
            ["Settings/Title"] = new[] { "Settings", "Ajustes", "Réglages", "Einstellungen", "Ajustes", "Impostazioni", "Pengaturan", "Cài đặt", "Ayarlar", "Ustawienia" },
            ["Settings/LanguageLabel"] = new[] { "Language", "Idioma", "Langue", "Sprache", "Idioma", "Lingua", "Bahasa", "Ngôn ngữ", "Dil", "Język" },
            ["Settings/SoundLabel"] = new[] { "Sound", "Sonido", "Son", "Sound", "Som", "Audio", "Suara", "Âm thanh", "Ses", "Dźwięk" },
            ["Settings/MusicLabel"] = new[] { "Music", "Música", "Musique", "Musik", "Música", "Musica", "Musik", "Âm nhạc", "Müzik", "Muzyka" },
            ["Settings/DeleteAccountButton"] = new[] { "Delete account & data", "Eliminar cuenta y datos", "Supprimer le compte et les données", "Konto & Daten löschen", "Excluir conta e dados", "Elimina account e dati", "Hapus akun & data", "Xóa tài khoản & dữ liệu", "Hesabı ve verileri sil", "Usuń konto i dane" },
            ["Settings/DeleteAccountConfirmTitle"] = new[] { "Delete account & data?", "¿Eliminar cuenta y datos?", "Supprimer le compte et les données ?", "Konto & Daten löschen?", "Excluir conta e dados?", "Eliminare account e dati?", "Hapus akun & data?", "Xóa tài khoản & dữ liệu?", "Hesap ve veriler silinsin mi?", "Usunąć konto i dane?" },

            // --- RPS (Rock-Paper-Scissors scene menu / mode picker / match screen) ---
            // "RPS" stays an untranslated code in every locale, same as the TTT Home secondary-row label.
            ["Home/RpsLabel"] = new[] { "RPS", "RPS", "RPS", "RPS", "RPS", "RPS", "RPS", "RPS", "RPS", "RPS" },
            ["RpsMenu/Title"] = new[]
            {
                "Guerrero, Maga y Asesino", "Guerrero, Maga y Asesino", "Guerrero, Maga y Asesino",
                "Guerrero, Maga y Asesino", "Guerrero, Maga y Asesino", "Guerrero, Maga y Asesino",
                "Guerrero, Maga y Asesino", "Guerrero, Maga y Asesino", "Guerrero, Maga y Asesino",
                "Guerrero, Maga y Asesino",
            },
            ["RpsMenu/Subtitle"] = new[]
            {
                "Rock, paper, scissors", "Piedra, papel o tijeras", "Pierre, papier, ciseaux", "Schnick, Schnack, Schnuck",
                "Pedra, papel e tesoura", "Sasso, carta, forbici", "Batu, kertas, gunting", "Oẳn tù tì",
                "Taş, kâğıt, makas", "Kamień, papier, nożyce",
            },
            ["RpsMenu/PlayButton"] = new[] { "Play", "Jugar", "Jouer", "Spielen", "Jogar", "Gioca", "Main", "Chơi", "Oyna", "Graj" },
            ["RpsMenu/ProfileLabel"] = new[] { "Profile", "Perfil", "Profil", "Profil", "Perfil", "Profilo", "Profil", "Hồ sơ", "Profil", "Profil" },
            ["RpsMenu/StoreLabel"] = new[] { "Store", "Tienda", "Boutique", "Shop", "Loja", "Negozio", "Toko", "Cửa hàng", "Mağaza", "Sklep" },
            ["RpsMenu/SettingsLabel"] = new[] { "Settings", "Ajustes", "Réglages", "Einstellungen", "Ajustes", "Impostazioni", "Pengaturan", "Cài đặt", "Ayarlar", "Ustawienia" },

            ["RpsModeSelect/Title"] = new[] { "Choose a mode", "Elige un modo", "Choisissez un mode", "Modus wählen", "Escolha um modo", "Scegli una modalità", "Pilih mode", "Chọn chế độ", "Bir mod seç", "Wybierz tryb" },
            ["RpsModeSelect/SinglePlayerTitle"] = new[] { "Single Player", "Un jugador", "Un joueur", "Einzelspieler", "Um jogador", "Giocatore singolo", "Satu pemain", "Một người chơi", "Tek oyunculu", "Jeden gracz" },
            ["RpsModeSelect/SinglePlayerDescription"] = new[]
            {
                "Best of five vs AI", "Mejor de cinco contra la IA", "Meilleure des cinq contre l'IA", "Best of Five gegen die KI",
                "Melhor de cinco contra a IA", "Al meglio di cinque contro l'IA", "Best of five lawan AI", "Best of five với AI",
                "Beşte üçe karşı yapay zeka", "Best of five z AI",
            },
            ["RpsModeSelect/LocalMultiplayerTitle"] = new[] { "Local 2P", "2 jugadores local", "2 joueurs local", "Lokal 2 Spieler", "2 jogadores local", "2 giocatori locale", "2 pemain lokal", "2 người local", "Yerel 2 kişilik", "Lokalnie 2 os." },
            ["RpsModeSelect/LocalMultiplayerDescription"] = new[]
            {
                "Pass the device, round by round", "Pasa el dispositivo, ronda a ronda", "Passez l'appareil, manche après manche",
                "Gerät von Runde zu Runde weitergeben", "Passe o dispositivo, rodada a rodada", "Passa il dispositivo, manche per manche",
                "Operkan perangkat secara bergantian", "Chuyền thiết bị từng lượt", "Cihazı tur tur devret", "Podawaj urządzenie rundami",
            },
            ["RpsModeSelect/OnlineTitle"] = new[] { "Online", "Online", "En ligne", "Online", "Online", "Online", "Online", "Trực tuyến", "Çevrimiçi", "Online" },
            ["RpsModeSelect/OnlineDescription"] = new[]
            {
                "Versus a rival over UGS", "Contra un rival vía UGS", "Contre un adversaire via UGS", "Gegen einen Rivalen per UGS",
                "Contra um rival pela UGS", "Contro un rivale via UGS", "Melawan rival lewat UGS", "Đấu với đối thủ qua UGS",
                "UGS üzerinden bir rakibe karşı", "Przeciw rywalowi przez UGS",
            },
            ["RpsModeSelect/OnlineComingSoonMessage"] = new[]
            {
                "Online RPS is coming soon!", "¡El RPS online llega pronto!", "Le RPS en ligne arrive bientôt !", "Online-RPS kommt bald!",
                "O RPS online chega em breve!", "L'RPS online arriva presto!", "Mode online RPS segera hadir!", "RPS trực tuyến sắp ra mắt!",
                "Çevrimiçi RPS yakında geliyor!", "RPS online wkrótce dostępny!",
            },

            ["RpsCharacterSelect/Title"] = new[] { "Choose your fighter", "Elige a tu luchador", "Choisis ton combattant", "Wähle deinen Kämpfer", "Escolha seu lutador", "Scegli il tuo combattente", "Pilih petarungmu", "Chọn đấu sĩ của bạn", "Dövüşçünü seç", "Wybierz swojego wojownika" },
            ["RpsCharacterSelect/Subtitle"] = new[] { "Each fighter has unique animations", "Cada luchador tiene animaciones únicas", "Chaque combattant a des animations uniques", "Jeder Kämpfer hat einzigartige Animationen", "Cada lutador tem animações únicas", "Ogni combattente ha animazioni uniche", "Setiap petarung punya animasi unik", "Mỗi đấu sĩ có animation riêng", "Her dövüşçünün benzersiz animasyonları var", "Każdy wojownik ma unikalne animacje" },
            ["RpsCharacterSelect/Guerrero"] = new[] { "Guerrero", "Guerrero", "Guerrier", "Krieger", "Guerreiro", "Guerriero", "Ksatria", "Chiến binh", "Savaşçı", "Wojownik" },
            ["RpsCharacterSelect/Maga"] = new[] { "Maga", "Maga", "Mage", "Magier", "Maga", "Maga", "Mage", "Pháp sư", "Büyücü", "Mag" },
            ["RpsCharacterSelect/Asesino"] = new[] { "Asesino", "Asesino", "Assassin", "Attentäter", "Assassino", "Assassino", "Assassin", "Sát thủ", "Suikastçı", "Zabójca" },
            ["RpsCharacterSelect/SingleTurn"] = new[] { "Choose your fighter", "Elige a tu luchador", "Choisis ton combattant", "Wähle deinen Kämpfer", "Escolha seu lutador", "Scegli il tuo combattente", "Pilih petarungmu", "Chọn đấu sĩ của bạn", "Dövüşçünü seç", "Wybierz swojego wojownika" },
            ["RpsCharacterSelect/PlayerOneTurn"] = new[] { "Player 1, choose your fighter", "Jugador 1, elige a tu luchador", "Joueur 1, choisis ton combattant", "Spieler 1, wähle deinen Kämpfer", "Jogador 1, escolha seu lutador", "Giocatore 1, scegli il tuo combattente", "Pemain 1, pilih petarungmu", "Người chơi 1, chọn đấu sĩ của bạn", "Oyuncu 1, dövüşçünü seç", "Gracz 1, wybierz swojego wojownika" },
            ["RpsCharacterSelect/PlayerTwoTurn"] = new[] { "Player 2, choose your fighter", "Jugador 2, elige a tu luchador", "Joueur 2, choisis ton combattant", "Spieler 2, wähle deinen Kämpfer", "Jogador 2, escolha seu lutador", "Giocatore 2, scegli il tuo combattente", "Pemain 2, pilih petarungmu", "Người chơi 2, chọn đấu sĩ của bạn", "Oyuncu 2, dövüşçünü seç", "Gracz 2, wybierz swojego wojownika" },
            ["RpsCharacterSelect/RoundFormat"] = new[] { "Round {0} · Best of five", "Ronda {0} · Mejor de cinco", "Manche {0} · Au meilleur des cinq", "Runde {0} · Best of five", "Rodada {0} · Melhor de cinco", "Manche {0} · Al meglio di cinque", "Ronde {0} · Best of five", "Vòng {0} · Best of five", "Tur {0} · Beşten üç", "Runda {0} · Best of five" },
            ["RpsCharacterSelect/ScoreFormat"] = new[] { "{0} {1}   ·   {2} {3}", "{0} {1}   ·   {2} {3}", "{0} {1}   ·   {2} {3}", "{0} {1}   ·   {2} {3}", "{0} {1}   ·   {2} {3}", "{0} {1}   ·   {2} {3}", "{0} {1}   ·   {2} {3}", "{0} {1}   ·   {2} {3}", "{0} {1}   ·   {2} {3}", "{0} {1}   ·   {2} {3}" },

            ["RpsGame/Rock"] = new[] { "Rock", "Piedra", "Pierre", "Stein", "Pedra", "Sasso", "Batu", "Đá", "Taş", "Kamień" },
            ["RpsGame/RoundFormat"] = new[] { "Round {0}", "Ronda {0}", "Manche {0}", "Runde {0}", "Rodada {0}", "Manche {0}", "Ronde {0}", "Vòng {0}", "Tur {0}", "Runda {0}" },
            ["RpsGame/Paper"] = new[] { "Paper", "Papel", "Papier", "Papier", "Papel", "Carta", "Kertas", "Giấy", "Kâğıt", "Papier" },
            ["RpsGame/Scissors"] = new[] { "Scissors", "Tijeras", "Ciseaux", "Schere", "Tesoura", "Forbici", "Gunting", "Kéo", "Makas", "Nożyce" },
            ["RpsGame/RoundYourMove"] = new[]
            {
                "Round {0} - your move", "Ronda {0} - tu turno", "Manche {0} - à vous", "Runde {0} - dein Zug",
                "Rodada {0} - sua vez", "Manche {0} - tocca a te", "Ronde {0} - giliranmu", "Vòng {0} - lượt của bạn",
                "Tur {0} - sıra sende", "Runda {0} - Twój ruch",
            },
            ["RpsGame/RoundWaiting"] = new[]
            {
                "Round {0} - waiting for {1}", "Ronda {0} - esperando a {1}", "Manche {0} - en attente de {1}", "Runde {0} - warte auf {1}",
                "Rodada {0} - esperando {1}", "Manche {0} - in attesa di {1}", "Ronde {0} - menunggu {1}", "Vòng {0} - chờ {1}",
                "Tur {0} - {1} bekleniyor", "Runda {0} - czekanie na {1}",
            },
            ["RpsGame/AiThinking"] = new[] { "AI is thinking...", "La IA está pensando...", "L'IA réfléchit...", "KI denkt nach...", "A IA está pensando...", "L'IA sta pensando...", "AI sedang berpikir...", "AI đang suy nghĩ...", "YZ düşünüyor...", "SI myśli..." },
            ["RpsGame/YouLabel"] = new[] { "You", "Tú", "Toi", "Du", "Você", "Tu", "Kamu", "Bạn", "Sen", "Ty" },
            ["RpsGame/AiLabel"] = new[] { "AI", "IA", "IA", "KI", "IA", "IA", "AI", "AI", "YZ", "SI" },
            ["RpsGame/Player1Label"] = new[] { "Player 1", "Jugador 1", "Joueur 1", "Spieler 1", "Jogador 1", "Giocatore 1", "Pemain 1", "Người chơi 1", "Oyuncu 1", "Gracz 1" },
            ["RpsGame/Player2Label"] = new[] { "Player 2", "Jugador 2", "Joueur 2", "Spieler 2", "Jogador 2", "Giocatore 2", "Pemain 2", "Người chơi 2", "Oyuncu 2", "Gracz 2" },
            ["RpsGame/HandoffLabel"] = new[]
            {
                "Pass the device to {0}", "Pasa el dispositivo a {0}", "Passez l'appareil à {0}", "Gerät an {0} weitergeben",
                "Passe o dispositivo para {0}", "Passa il dispositivo a {0}", "Operkan perangkat ke {0}", "Chuyền thiết bị cho {0}",
                "Cihazı {0}'a devret", "Podaj urządzenie {0}",
            },
            ["RpsGame/YouWon"] = new[]
            {
                "You won the series!", "¡Ganaste la serie!", "Vous avez gagné la série !", "Du gewinnst die Serie!",
                "Você venceu a série!", "Hai vinto la serie!", "Kamu memenangkan seri!", "Bạn thắng cả loạt!",
                "Seriyi kazandın!", "Wygrałeś serię!",
            },
            ["RpsGame/YouLost"] = new[]
            {
                "AI won the series", "La IA ganó la serie", "L'IA a gagné la série", "Die KI gewinnt die Serie",
                "A IA venceu a série", "L'IA ha vinto la serie", "AI memenangkan seri", "AI thắng cả loạt",
                "YZ seriyi kazandı", "SI wygrała serię",
            },
            ["RpsGame/Player1Won"] = new[]
            {
                "Player 1 won the series!", "¡El Jugador 1 ganó la serie!", "Le Joueur 1 a gagné la série !", "Spieler 1 gewinnt die Serie!",
                "Jogador 1 venceu a série!", "Il Giocatore 1 ha vinto la serie!", "Pemain 1 memenangkan seri!", "Người chơi 1 thắng cả loạt!",
                "Oyuncu 1 seriyi kazandı!", "Gracz 1 wygrał serię!",
            },
            ["RpsGame/Player2Won"] = new[]
            {
                "Player 2 won the series!", "¡El Jugador 2 ganó la serie!", "Le Joueur 2 a gagné la série !", "Spieler 2 gewinnt die Serie!",
                "Jogador 2 venceu a série!", "Il Giocatore 2 ha vinto la serie!", "Pemain 2 memenangkan seri!", "Người chơi 2 thắng cả loạt!",
                "Oyuncu 2 seriyi kazandı!", "Gracz 2 wygrał serię!",
            },
            ["RpsGame/SeriesDraw"] = new[]
            {
                "Series tied", "Serie empatada", "Série à égalité", "Serie unentschieden",
                "Série empatada", "Serie in pareggio", "Seri imbang", "Loạt hòa",
                "Seri berabere", "Seria remisowa",
            },
            ["RpsGame/DrawLabel"] = new[] { "EMPATE", "EMPATE", "ÉGALITÉ", "UNENTSCHIEDEN", "EMPATE", "PAREGGIO", "SERI", "HÒA", "BERABERE", "REMIS" },
            ["RpsGame/RematchButton"] = new[] { "Rematch", "Revancha", "Revanche", "Revanche", "Revanche", "Rivincita", "Main lagi", "Đấu lại", "Rövanş", "Rewanż" },
            ["RpsGame/MenuButton"] = new[] { "Menu", "Menú", "Menu", "Menü", "Menu", "Menu", "Menu", "Menu", "Menü", "Menu" },

            // --- Async duel (Hub Central + combat) ---
            ["RpsMenu/DuelsLabel"] = new[] { "Duels", "Duelos", "Duels", "Duelle", "Duelos", "Duelli", "Duel", "Trận đấu", "Düellolar", "Pojedynki" },
            ["Settings/DuelSectionLabel"] = new[] { "Duels", "Duelos", "Duels", "Duelle", "Duelos", "Duelli", "Duel", "Trận đấu", "Düellolar", "Pojedynki" },
            ["Settings/DuelDemoTimeoutLabel"] = new[]
            {
                "Short timeout (2 min) for demos and evaluation",
                "Tiempo límite corto (2 min) para demos y evaluación",
                "Délai court (2 min) pour les démos et l'évaluation",
                "Kurzes Zeitlimit (2 Min.) für Demos und Bewertung",
                "Tempo limite curto (2 min) para demos e avaliação",
                "Tempo limite breve (2 min) per demo e valutazione",
                "Batas waktu singkat (2 menit) untuk demo dan evaluasi",
                "Giới hạn ngắn (2 phút) cho bản demo và đánh giá",
                "Demo ve değerlendirme için kısa süre (2 dk)",
                "Krótki limit czasu (2 min) do dem i oceny",
            },
            ["Duel/HubTitle"] = new[] { "Active duels", "Duelos activos", "Duels en cours", "Aktive Duelle", "Duelos ativos", "Duelli attivi", "Duel aktif", "Trận đấu đang diễn ra", "Aktif düellolar", "Aktywne pojedynki" },
            ["Duel/HubEmpty"] = new[]
            {
                "You have no active duels. Start a new one and come back whenever you want.",
                "No tienes duelos activos. Empieza uno nuevo y vuelve cuando quieras.",
                "Vous n'avez aucun duel en cours. Lancez-en un et revenez quand vous voulez.",
                "Du hast keine aktiven Duelle. Starte ein neues und komm zurück, wann du willst.",
                "Você não tem duelos ativos. Comece um novo e volte quando quiser.",
                "Non hai duelli attivi. Iniziane uno e torna quando vuoi.",
                "Kamu tidak punya duel aktif. Mulai yang baru dan kembali kapan saja.",
                "Bạn không có trận đấu nào đang diễn ra. Hãy bắt đầu trận mới và quay lại bất cứ lúc nào.",
                "Aktif düellon yok. Yeni bir tane başlat ve istediğin zaman geri dön.",
                "Nie masz aktywnych pojedynków. Zacznij nowy i wróć, kiedy chcesz.",
            },
            ["Duel/NewDuelButton"] = new[] { "New duel (practice)", "Nuevo duelo (práctica)", "Nouveau duel (entraînement)", "Neues Duell (Übung)", "Novo duelo (prática)", "Nuovo duello (pratica)", "Duel baru (latihan)", "Trận mới (luyện tập)", "Yeni düello (alıştırma)", "Nowy pojedynek (trening)" },
            ["Duel/IdleDemoButton"] = new[] { "Demo: silent rival (timeout)", "Demo: rival inactivo (tiempo límite)", "Démo : rival silencieux (délai)", "Demo: stummer Gegner (Zeitlimit)", "Demo: rival silencioso (tempo limite)", "Demo: rivale silenzioso (timeout)", "Demo: lawan diam (batas waktu)", "Demo: đối thủ im lặng (hết giờ)", "Demo: sessiz rakip (zaman aşımı)", "Demo: milczący rywal (limit czasu)" },
            ["Duel/CardYourTurn"] = new[] { "Your turn", "Tu turno", "À vous", "Du bist dran", "Sua vez", "Tocca a te", "Giliranmu", "Lượt của bạn", "Sıra sende", "Twoja kolej" },
            ["Duel/CardWaitingRival"] = new[] { "Waiting for rival", "Esperando rival", "En attente du rival", "Warten auf Gegner", "Aguardando rival", "In attesa del rivale", "Menunggu lawan", "Đang chờ đối thủ", "Rakip bekleniyor", "Czekam na rywala" },
            ["Duel/CardFinished"] = new[] { "Finished", "Finalizado", "Terminé", "Beendet", "Finalizado", "Concluso", "Selesai", "Đã kết thúc", "Bitti", "Zakończony" },
            ["Duel/CardSummaryFormat"] = new[]
            {
                "Round {0} · You {1} HP · Rival {2} HP",
                "Ronda {0} · Tú {1} PV · Rival {2} PV",
                "Manche {0} · Vous {1} PV · Rival {2} PV",
                "Runde {0} · Du {1} LP · Gegner {2} LP",
                "Rodada {0} · Você {1} PV · Rival {2} PV",
                "Manche {0} · Tu {1} PV · Rivale {2} PV",
                "Ronde {0} · Kamu {1} HP · Lawan {2} HP",
                "Vòng {0} · Bạn {1} HP · Đối thủ {2} HP",
                "Tur {0} · Sen {1} HP · Rakip {2} HP",
                "Runda {0} · Ty {1} HP · Rywal {2} HP",
            },
            ["Duel/PracticeRivalName"] = new[] { "Practice rival", "Rival de práctica", "Rival d'entraînement", "Übungsgegner", "Rival de treino", "Rivale di pratica", "Lawan latihan", "Đối thủ luyện tập", "Alıştırma rakibi", "Rywal treningowy" },
            ["Duel/IdleRivalName"] = new[] { "Silent rival", "Rival inactivo", "Rival silencieux", "Stummer Gegner", "Rival silencioso", "Rivale silenzioso", "Lawan diam", "Đối thủ im lặng", "Sessiz rakip", "Milczący rywal" },
            ["Duel/Title"] = new[] { "Duel", "Duelo", "Duel", "Duell", "Duelo", "Duello", "Duel", "Trận đấu", "Düello", "Pojedynek" },
            ["Duel/RoundFormat"] = new[] { "Round {0}", "Ronda {0}", "Manche {0}", "Runde {0}", "Rodada {0}", "Manche {0}", "Ronde {0}", "Vòng {0}", "Tur {0}", "Runda {0}" },
            ["Duel/YourTurn"] = new[]
            {
                "Your turn: fill the three slots",
                "Tu turno: completa las 3 ranuras",
                "À vous : remplissez les trois cases",
                "Du bist dran: fülle die drei Felder",
                "Sua vez: preencha as três casas",
                "Tocca a te: riempi le tre caselle",
                "Giliranmu: isi ketiga slot",
                "Lượt của bạn: điền ba ô",
                "Sıra sende: üç alanı doldur",
                "Twoja kolej: wypełnij trzy pola",
            },
            ["Duel/WaitingRival"] = new[]
            {
                "Moves sent. Your rival will answer the next time you open this duel.",
                "Jugada enviada. El rival responderá la próxima vez que abras este duelo.",
                "Coups envoyés. Votre rival répondra à la prochaine ouverture de ce duel.",
                "Züge gesendet. Dein Gegner antwortet, wenn du dieses Duell das nächste Mal öffnest.",
                "Jogada enviada. O rival responderá na próxima vez que você abrir este duelo.",
                "Mosse inviate. Il rivale risponderà la prossima volta che apri questo duello.",
                "Jurus terkirim. Lawan akan menjawab saat kamu membuka duel ini lagi.",
                "Đã gửi nước đi. Đối thủ sẽ trả lời lần tới khi bạn mở trận này.",
                "Hamleler gönderildi. Rakibin bu düelloyu bir sonraki açışında cevap verecek.",
                "Ruchy wysłane. Rywal odpowie przy następnym otwarciu tego pojedynku.",
            },
            ["Duel/WaitingCountdownFormat"] = new[]
            {
                "Claim unlocks in {0}:{1:00} · inactivity window {2} min",
                "Reclamar se habilita en {0}:{1:00} · ventana de inactividad {2} min",
                "Réclamation dans {0}:{1:00} · inactivité {2} min",
                "Anspruch in {0}:{1:00} · Inaktivität {2} Min.",
                "Reivindicar em {0}:{1:00} · inatividade {2} min",
                "Riscatto tra {0}:{1:00} · inattività {2} min",
                "Klaim terbuka dalam {0}:{1:00} · batas {2} menit",
                "Nhận thắng sau {0}:{1:00} · giới hạn {2} phút",
                "Talep {0}:{1:00} sonra · hareketsizlik {2} dk",
                "Roszczenie za {0}:{1:00} · bezczynność {2} min",
            },
            ["Duel/ClaimButton"] = new[] { "Claim victory", "Reclamar victoria", "Revendiquer la victoire", "Sieg beanspruchen", "Reivindicar vitória", "Rivendica la vittoria", "Klaim kemenangan", "Nhận thắng", "Zaferi talep et", "Zgłoś zwycięstwo" },
            ["Duel/ConfirmButton"] = new[] { "Send moves", "Enviar jugada", "Envoyer les coups", "Züge senden", "Enviar jogada", "Invia mosse", "Kirim jurus", "Gửi nước đi", "Hamleleri gönder", "Wyślij ruchy" },
            ["Duel/ConfirmIncomplete"] = new[] { "Fill the three slots", "Completa las 3 ranuras", "Remplissez les trois cases", "Fülle die drei Felder", "Preencha as três casas", "Riempi le tre caselle", "Isi ketiga slot", "Điền ba ô", "Üç alanı doldur", "Wypełnij trzy pola" },
            ["Duel/ResultRoundTitleFormat"] = new[] { "Clashes of round {0}", "Choques de la ronda {0}", "Chocs de la manche {0}", "Aufeinandertreffen der Runde {0}", "Choques da rodada {0}", "Scontri della manche {0}", "Bentrokan ronde {0}", "Va chạm vòng {0}", "Tur {0} çarpışmaları", "Starcia rundy {0}" },
            ["Duel/ResultWin"] = new[] { "Victory!", "¡Victoria!", "Victoire !", "Sieg!", "Vitória!", "Vittoria!", "Menang!", "Chiến thắng!", "Zafer!", "Zwycięstwo!" },
            ["Duel/ResultLoss"] = new[] { "Defeat", "Derrota", "Défaite", "Niederlage", "Derrota", "Sconfitta", "Kalah", "Thất bại", "Yenilgi", "Porażka" },
            ["Duel/ResultDraw"] = new[] { "Draw", "Empate", "Égalité", "Unentschieden", "Empate", "Pareggio", "Seri", "Hòa", "Berabere", "Remis" },
            ["Duel/ResultContinue"] = new[] { "Continue", "Continuar", "Continuer", "Weiter", "Continuar", "Continua", "Lanjutkan", "Tiếp tục", "Devam", "Kontynuuj" },
            ["Duel/ResultBackToHub"] = new[] { "Back to Hub", "Volver al Hub", "Retour au hub", "Zurück zum Hub", "Voltar ao Hub", "Torna al Hub", "Kembali ke Hub", "Về trung tâm", "Hub'a dön", "Wróć do huba" },
            ["Duel/ClashFormat"] = new[] { "{0} vs {1}", "{0} vs {1}", "{0} vs {1}", "{0} vs {1}", "{0} vs {1}", "{0} vs {1}", "{0} vs {1}", "{0} vs {1}", "{0} vs {1}", "{0} vs {1}" },
            ["Duel/ClashDraw"] = new[]
            {
                "Same class · no damage",
                "Misma clase · sin daño",
                "Même classe · aucun dégât",
                "Gleiche Klasse · kein Schaden",
                "Mesma classe · sem dano",
                "Stessa classe · nessun danno",
                "Kelas sama · tanpa damage",
                "Cùng lớp · không sát thương",
                "Aynı sınıf · hasar yok",
                "Ta sama klasa · brak obrażeń",
            },
            ["Duel/ClashDamageFormat"] = new[]
            {
                "{0} wins · {1} damage",
                "{0} gana · {1} de daño",
                "{0} gagne · {1} dégâts",
                "{0} gewinnt · {1} Schaden",
                "{0} vence · {1} de dano",
                "{0} vince · {1} danni",
                "{0} menang · {1} damage",
                "{0} thắng · {1} sát thương",
                "{0} kazanır · {1} hasar",
                "{0} wygrywa · {1} obrażeń",
            },
            ["Duel/WinByKo"] = new[] { "Victory by KO", "Victoria por KO", "Victoire par K.-O.", "Sieg durch K.o.", "Vitória por nocaute", "Vittoria per KO", "Menang KO", "Thắng bằng KO", "Nakavtla zafer", "Zwycięstwo przez nokaut" },
            ["Duel/WinByAbandon"] = new[] { "Victory by abandonment", "Victoria por abandono", "Victoire par abandon", "Sieg durch Aufgabe", "Vitória por abandono", "Vittoria per abbandono", "Menang karena lawan pergi", "Thắng do bỏ cuộc", "Terk eden rakibe karşı zafer", "Zwycięstwo przez porzucenie" },
            ["Duel/DrawByLimit"] = new[] { "Draw: round limit reached", "Empate: se alcanzó el límite de rondas", "Égalité : limite de manches atteinte", "Unentschieden: Rundenlimit erreicht", "Empate: limite de rodadas atingido", "Pareggio: limite di manche raggiunto", "Seri: batas ronde tercapai", "Hòa: đã đạt giới hạn vòng", "Berabere: tur sınırına ulaşıldı", "Remis: osiągnięto limit rund" },
            ["Duel/LossReasonFormat"] = new[] { "Your rival wins · {0}", "Tu rival gana · {0}", "Votre rival gagne · {0}", "Dein Gegner gewinnt · {0}", "Seu rival vence · {0}", "Il rivale vince · {0}", "Lawanmu menang · {0}", "Đối thủ thắng · {0}", "Rakibin kazanır · {0}", "Rywal wygrywa · {0}" },
            ["Duel/ReasonKo"] = new[] { "KO", "KO", "K.-O.", "K.o.", "Nocaute", "KO", "KO", "KO", "Nakavt", "Nokaut" },
            ["Duel/ReasonAbandon"] = new[] { "abandonment", "abandono", "abandon", "Aufgabe", "abandono", "abbandono", "lawan pergi", "bỏ cuộc", "terk edilme", "porzucenie" },
        };

        [MenuItem("TTTXO/Setup/Create Localization Assets")]
        public static void CreateLocalizationAssets()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(LocalesFolder);
            EnsureFolder(TablesFolder);

            var settings = GetOrCreateSettings();

            var locales = new List<Locale>();
            foreach (var entry in SupportedLocales.All)
            {
                locales.Add(GetOrCreateLocale(entry.Code, entry.NativeName));
            }

            ConfigureStartupSelectors(settings);

            var collection = GetOrCreateCollection(locales);
            int updatedKeys = PopulateEntries(collection);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"TTTXO LocalizationSetup: {locales.Count} locales ready, table '{TableCollectionName}' has {updatedKeys}/{Translations.Count} keys populated.");
        }

        private static LocalizationSettings GetOrCreateSettings()
        {
            var active = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (active != null)
            {
                return active;
            }

            var fromDisk = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsAssetPath);
            if (fromDisk != null)
            {
                LocalizationEditorSettings.ActiveLocalizationSettings = fromDisk;
                return fromDisk;
            }

            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            return settings;
        }

        private static Locale GetOrCreateLocale(string code, string nativeName)
        {
            var existing = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == code);
            if (existing != null)
            {
                return existing;
            }

            string assetPath = $"{LocalesFolder}/Locale-{code}.asset";
            var fromDisk = AssetDatabase.LoadAssetAtPath<Locale>(assetPath);
            if (fromDisk != null)
            {
                LocalizationEditorSettings.AddLocale(fromDisk);
                return fromDisk;
            }

            var locale = Locale.CreateLocale(new LocaleIdentifier(code));
            locale.name = $"Locale-{code}";
            locale.LocaleName = nativeName;
            AssetDatabase.CreateAsset(locale, assetPath);
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        /// <summary>Configures the startup locale resolution chain: an explicit past choice (PlayerPrefs) wins, then the device's own locale, then a hard English fallback - matches Docs/02-GDD-TicTacToe.md#8 ("selected locale por defecto = system locale con fallback a en").</summary>
        private static void ConfigureStartupSelectors(LocalizationSettings settings)
        {
            var selectors = settings.GetStartupLocaleSelectors();
            selectors.Clear();
            selectors.Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = LocalePlayerPrefKey });
            selectors.Add(new SystemLocaleSelector());
            selectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier(SupportedLocales.DefaultLocaleCode) });
            EditorUtility.SetDirty(settings);
        }

        private static StringTableCollection GetOrCreateCollection(IList<Locale> locales)
        {
            var existing = LocalizationEditorSettings.GetStringTableCollection(TableCollectionName);
            if (existing == null)
            {
                return LocalizationEditorSettings.CreateStringTableCollection(TableCollectionName, TablesFolder, locales);
            }

            foreach (var locale in locales)
            {
                if (existing.GetTable(locale.Identifier) == null)
                {
                    existing.AddNewTable(locale.Identifier);
                }
            }

            return existing;
        }

        /// <summary>Idempotently writes every <see cref="Translations"/> entry into the collection: existing keys are updated in place, missing ones are added - no duplicates on re-run. Returns the number of keys actually written.</summary>
        private static int PopulateEntries(StringTableCollection collection)
        {
            var tablesByCode = new Dictionary<string, StringTable>();
            foreach (var entry in SupportedLocales.All)
            {
                var table = collection.GetTable(new LocaleIdentifier(entry.Code)) as StringTable;
                if (table == null)
                {
                    Debug.LogError($"LocalizationSetup: missing '{entry.Code}' table in collection '{TableCollectionName}' - skipping that locale.");
                    continue;
                }

                tablesByCode[entry.Code] = table;
            }

            int writtenKeys = 0;
            foreach (var pair in Translations)
            {
                string key = pair.Key;
                string[] values = pair.Value;

                if (values.Length != SupportedLocales.All.Length)
                {
                    Debug.LogError($"LocalizationSetup: key '{key}' has {values.Length} translations, expected {SupportedLocales.All.Length} - skipped.");
                    continue;
                }

                for (int i = 0; i < SupportedLocales.All.Length; i++)
                {
                    string code = SupportedLocales.All[i].Code;
                    if (!tablesByCode.TryGetValue(code, out var table))
                    {
                        continue;
                    }

                    var tableEntry = table.GetEntry(key);
                    if (tableEntry != null)
                    {
                        tableEntry.Value = values[i];
                    }
                    else
                    {
                        table.AddEntry(key, values[i]);
                    }
                }

                writtenKeys++;
            }

            EditorUtility.SetDirty(collection.SharedData);
            foreach (var table in tablesByCode.Values)
            {
                EditorUtility.SetDirty(table);
            }

            return writtenKeys;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
