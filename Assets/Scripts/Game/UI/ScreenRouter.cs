using System;
using System.Collections.Generic;
using TTTXO.Game.Bootstrap;
using TTTXO.Game.Duel;
using TTTXO.Game.UI.Screens;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI
{
    /// <summary>
    /// Single root UIDocument navigation host. Every screen is a sub-tree of the same UXML document
    /// (Assets/UI/Screens/Root.uxml); this router shows exactly one at a time by toggling display
    /// style and forwards lifecycle events to each screen's controller. It also mounts the two
    /// shared overlays (confirm dialog, toast) used across several screens.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ScreenRouter : MonoBehaviour
    {
        private static readonly IReadOnlyDictionary<ScreenId, string> ScreenElementNames = new Dictionary<ScreenId, string>
        {
            { ScreenId.RpsMenu, "screen-rps-menu" },
            { ScreenId.RpsModeSelect, "screen-rps-mode-select" },
            { ScreenId.RpsCharacterSelect, "screen-rps-character-select" },
            { ScreenId.RpsVs, "screen-rps-vs" },
            { ScreenId.Store, "screen-store" },
            { ScreenId.Settings, "screen-settings" },
            { ScreenId.DuelHub, "screen-duel-hub" },
            { ScreenId.DuelCombat, "screen-duel" }
        };

        /// <summary>Where <see cref="Initialize"/> lands after startup.</summary>
        [SerializeField]
        private ScreenId _startScreen = ScreenId.RpsMenu;

        public ScreenId StartScreen => _startScreen;

        private UIDocument _document;
        private readonly Dictionary<ScreenId, VisualElement> _screenRoots = new();
        private readonly Dictionary<ScreenId, IScreenController> _controllers = new();
        private ScreenId? _currentScreen;
        private bool _initialized;

        public RpsVsScreenController RpsVsScreen { get; private set; }
        public RpsCharacterSelectScreenController RpsCharacterSelectScreen { get; private set; }

        /// <summary>
        /// Async duel state (active matches and the open one). Shared by the Hub and the combat
        /// screen; lives here for the same reason <see cref="RpsSeries"/> does - both screens must see
        /// the same documents.
        /// </summary>
        public DuelHub Duels { get; private set; }

        /// <summary>
        /// Store behind <see cref="Duels"/>: the local mirror plus the background Cloud Save push.
        /// Exposed so the bootstrap can pull the account's documents into the mirror at startup.
        /// </summary>
        public DuelCloudStore DuelStore { get; private set; }

        /// <summary>The screen currently visible, or null before <see cref="Initialize"/> shows one.</summary>
        public ScreenId? CurrentScreen => _currentScreen;

        /// <summary>
        /// Match state shared by Character Select and the VS screen. It survives the navigation
        /// between rounds and is reset when a new series starts or the player returns to the menu.
        /// </summary>
        public RpsSeries RpsSeries { get; } = new();

        /// <summary>
        /// Navigation history for generic "back" buttons on sub-screens. The rules - and why a
        /// single-level "previous screen" field is not enough - live in <see cref="ScreenHistory"/>,
        /// which holds them without a UIDocument so they can be exercised by a test.
        /// </summary>
        private readonly ScreenHistory _history = new();
        private bool _navigatingBack;

        public ConfirmDialogController ConfirmDialog { get; private set; }
        public ToastController Toast { get; private set; }

        /// <summary>Keeps every screen's content clear of the notch and the home indicator.</summary>
        public SafeAreaController SafeArea { get; private set; }

        /// <summary>Binds every screen root, constructs the per-screen controllers, and shows the menu.</summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;

            foreach (var pair in ScreenElementNames)
            {
                var element = root.Q<VisualElement>(pair.Value);
                if (element == null)
                {
                    Debug.LogError($"ScreenRouter: could not find root element '{pair.Value}' for screen {pair.Key}.");
                    continue;
                }

                _screenRoots[pair.Key] = element;
            }

            ConfirmDialog = new ConfirmDialogController(root.Q<VisualElement>("confirm-dialog-overlay"));
            Toast = new ToastController(root.Q<VisualElement>("toast"));

            var gameManager = GameManager.Instance;

            // Duel identity/timeout: from the local profile while there is no account, from Unity
            // Authentication once Phase 4 lands. The Hub only needs the values, not the source.
            var localPlayerId = gameManager != null ? gameManager.LocalPlayerId : "jugador-local";
            var localPlayerName = gameManager != null ? gameManager.PlayerName : "Invitado";
            Func<TimeSpan> duelTimeout = gameManager != null
                ? (Func<TimeSpan>)(() => gameManager.DuelTimeout)
                : null;
            // Offline-first: the local mirror is the source of truth and every save is also pushed to
            // Cloud Save in the background once the anonymous session is up. If the network (or the
            // project link) is missing, the whole flow still works against the files.
            DuelStore = new DuelCloudStore();
            Duels = new DuelHub(DuelStore, localPlayerId, localPlayerName, duelTimeout);

            _controllers[ScreenId.Store] = new StoreScreenController(_screenRoots[ScreenId.Store], this, gameManager);
            _controllers[ScreenId.Settings] = new SettingsScreenController(_screenRoots[ScreenId.Settings], this, gameManager);
            _controllers[ScreenId.RpsMenu] = new RpsMenuScreenController(_screenRoots[ScreenId.RpsMenu], this);
            _controllers[ScreenId.RpsModeSelect] = new RpsModeSelectScreenController(_screenRoots[ScreenId.RpsModeSelect], this);
            RpsCharacterSelectScreen = new RpsCharacterSelectScreenController(_screenRoots[ScreenId.RpsCharacterSelect], this);
            RpsVsScreen = new RpsVsScreenController(_screenRoots[ScreenId.RpsVs], this);
            _controllers[ScreenId.RpsCharacterSelect] = RpsCharacterSelectScreen;
            _controllers[ScreenId.RpsVs] = RpsVsScreen;
            _controllers[ScreenId.DuelHub] = new DuelHubScreenController(_screenRoots[ScreenId.DuelHub], this, Duels);
            _controllers[ScreenId.DuelCombat] = new DuelCombatScreenController(_screenRoots[ScreenId.DuelCombat], this, Duels);

            foreach (var pair in _screenRoots)
            {
                pair.Value.style.display = DisplayStyle.None;
            }

            // Insets every screen out from under the notch and the home indicator. Constructed after the
            // screen roots are bound and before anything is shown, so the first painted frame is already
            // inside the safe area rather than jumping once the first resize arrives.
            SafeArea = new SafeAreaController(root, new List<VisualElement>(_screenRoots.Values));

            _initialized = true;

            // Every screen's static UiText-sourced labels/buttons are assigned once from each
            // controller's constructor (see IScreenController.RefreshLocalizedText). Re-run that
            // assignment for every constructed screen whenever the player picks a different language
            // on the Settings screen.
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;

            // Localization boots asynchronously; the controllers above are constructed immediately and
            // may have captured English fallbacks. Re-run their copy once the tables are really ready.
            try
            {
                if (LocalizationSettings.HasSettings)
                {
                    var initialization = LocalizationSettings.InitializationOperation;
                    if (initialization.IsDone)
                    {
                        RefreshAllLocalizedText();
                    }
                    else
                    {
                        initialization.Completed += OnLocalizationInitialized;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ScreenRouter: could not observe localization initialization - {ex.Message}");
            }

            // The scene was serialized with the old enum value (Splash/Home) before this build.
            // Falling back to the menu keeps an out-of-date scene playable without a re-save.
            var start = ScreenElementNames.ContainsKey(_startScreen) ? _startScreen : ScreenId.RpsMenu;
            Show(start);

            // The table assets load lazily on the first lookup: the first GetLocalizedString call
            // returns the fallback and only then kicks off the load. Preload the selected locale's
            // table and refresh once it is available.
            RefreshWhenTableReady();
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnSelectedLocaleChanged(Locale _)
        {
            RefreshAllLocalizedText();
            RefreshWhenTableReady();
        }

        /// <summary>
        /// Re-applies the copy once the selected locale's string table has actually loaded. The first
        /// synchronous lookup only starts that load and returns the fallback, so a locale switch (or
        /// the initial boot) would otherwise keep the old/English text until the next navigation.
        /// </summary>
        private void RefreshWhenTableReady()
        {
            if (!_initialized || !LocalizationSettings.HasSettings)
            {
                return;
            }

            var locale = LocalizationSettings.SelectedLocale;
            if (locale == null)
            {
                return;
            }

            try
            {
                var table = LocalizationSettings.StringDatabase.GetTableAsync(UiText.TableName, locale);
                table.Completed += _ =>
                {
                    if (_initialized)
                    {
                        RefreshAllLocalizedText();
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ScreenRouter: could not preload localization table - {ex.Message}");
            }
        }

        /// <summary>
        /// Re-applies every controller's static UiText copy. Called on a locale change and once when
        /// localization finishes booting: controllers are constructed during the first frame, often
        /// before the tables are ready, and UiText deliberately returns the English fallback while
        /// initializing. Without this second pass the menu title, duel buttons and similar static
        /// labels stayed in English for the whole session even though the selected locale was Spanish.
        /// </summary>
        private void RefreshAllLocalizedText()
        {
            // Defensive: a broken screen refresh must never take down the rest of the app or block
            // navigation, so each controller is refreshed independently.
            foreach (var pair in _controllers)
            {
                try
                {
                    pair.Value.RefreshLocalizedText();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"ScreenRouter: RefreshLocalizedText failed for {pair.Key} - {ex.Message}");
                }
            }

            ConfirmDialog?.RefreshLocalizedText();
        }

        private void OnLocalizationInitialized(AsyncOperationHandle<LocalizationSettings> _)
        {
            if (!_initialized)
            {
                return;
            }

            RefreshAllLocalizedText();
            RefreshWhenTableReady();
        }

        /// <summary>Hides the current screen (if any) and shows the requested one.</summary>
        public void Show(ScreenId screenId)
        {
            if (!_screenRoots.TryGetValue(screenId, out var nextRoot))
            {
                Debug.LogError($"ScreenRouter: screen {screenId} is not registered.");
                return;
            }

            ScreenId? leavingScreen = null;
            if (_currentScreen.HasValue && _screenRoots.TryGetValue(_currentScreen.Value, out var currentRoot))
            {
                currentRoot.style.display = DisplayStyle.None;
                if (_controllers.TryGetValue(_currentScreen.Value, out var currentController))
                {
                    currentController.OnHide();
                }

                leavingScreen = _currentScreen.Value;
            }

            _history.RecordNavigation(leavingScreen, screenId, _navigatingBack);

            nextRoot.style.display = DisplayStyle.Flex;
            _currentScreen = screenId;

            if (_controllers.TryGetValue(screenId, out var nextController))
            {
                nextController.OnShow();
                // Cheap and idempotent: guarantees the screen's static copy is in the selected locale
                // even when it was constructed before the string table finished loading.
                nextController.RefreshLocalizedText();
            }
        }

        /// <summary>
        /// Convenience for sub-screens whose back button should return wherever the player came from
        /// (Store/Settings can be reached from more than one place), falling back to
        /// <paramref name="fallback"/> if there is no recorded history. Pops the history instead of
        /// pushing onto it, so back-and-forth between two screens can never loop.
        /// </summary>
        public void GoBack(ScreenId fallback)
        {
            var target = _history.ResolveBack(fallback);
            _navigatingBack = true;
            try
            {
                Show(target);
            }
            finally
            {
                _navigatingBack = false;
            }
        }
    }
}
