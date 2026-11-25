using System;
using System.Globalization;
using TTTXO.Core.Duel;
using TTTXO.Game.Duel;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Hub Central: every active duel the player has, so they can play one while the others wait.
    /// Cards are built from the documents themselves (rival, round, HP, whose turn it is), which is
    /// why the screen has no per-match state of its own.
    /// </summary>
    public sealed class DuelHubScreenController : IScreenController
    {
        private readonly VisualElement _root;
        private readonly ScreenRouter _router;
        private readonly DuelHub _hub;

        private readonly VisualElement _emptyIcon;
        private readonly Label _emptyLabel;
        private readonly ScrollView _list;
        private readonly Button _newButton;
        private readonly Button _idleButton;

        public DuelHubScreenController(VisualElement root, ScreenRouter router, DuelHub hub)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));

            _emptyIcon = _root.Q<VisualElement>(className: "duel-empty-icon");
            _emptyLabel = _root.Q<Label>("duel-hub-empty-label");
            _list = _root.Q<ScrollView>("duel-hub-list");
            _newButton = _root.Q<Button>("duel-hub-new-button");
            _idleButton = _root.Q<Button>("duel-hub-idle-button");

            var back = _root.Q<Button>("duel-hub-back-button");
            back.text = UiText.Common.BackIcon;
            back.clicked += () => _router.Show(ScreenId.RpsMenu);

            _newButton.clicked += OnNewDuelClicked;
            _idleButton.clicked += OnIdleDemoClicked;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _idleButton.style.display = DisplayStyle.Flex;
#else
            _idleButton.style.display = DisplayStyle.None;
#endif

            RefreshLocalizedText();
        }

        public void OnShow()
        {
            _hub.Refresh();
            Rebuild();
        }

        public void OnHide()
        {
        }

        public void RefreshLocalizedText()
        {
            _root.Q<Label>("duel-hub-title-label").text = UiText.Duel.HubTitle;
            _emptyLabel.text = UiText.Duel.HubEmpty;
            _newButton.text = UiText.Duel.NewDuelButton;
            _idleButton.text = UiText.Duel.IdleDemoButton;
        }

        private void Rebuild()
        {
            _list.Clear();

            var duels = _hub.ActiveDuels;
            bool showEmpty = duels.Count == 0;
            _emptyIcon.style.display = showEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyLabel.style.display = showEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            _list.style.display = showEmpty ? DisplayStyle.None : DisplayStyle.Flex;

            foreach (var duel in duels)
            {
                _list.Add(BuildCard(duel));
            }
        }

        private VisualElement BuildCard(DuelStateDto duel)
        {
            var card = new VisualElement();
            card.AddToClassList("duel-card");

            var text = new VisualElement();
            text.AddToClassList("duel-card-text");

            var rival = duel.OpponentOf(_hub.LocalPlayerId);
            var me = duel.PlayerById(_hub.LocalPlayerId);

            var name = new Label(rival?.DisplayName ?? "?");
            name.AddToClassList("duel-card-name");

            var summary = new Label(string.Format(
                CultureInfo.InvariantCulture,
                UiText.Duel.CardSummaryFormat,
                duel.Round,
                me?.CurrentHp ?? 0,
                rival?.CurrentHp ?? 0));
            summary.AddToClassList("duel-card-summary");

            text.Add(name);
            text.Add(summary);

            var playerState = duel.StateFor(_hub.LocalPlayerId);
            var state = new Label(StateText(playerState));
            state.AddToClassList("duel-card-state");
            if (playerState == DuelRules.StateWaitingRival)
            {
                state.AddToClassList("duel-card-state--waiting");
            }
            else if (playerState == DuelRules.StateFinished)
            {
                state.AddToClassList("duel-card-state--finished");
            }

            card.Add(text);
            card.Add(state);

            var chevron = new VisualElement();
            chevron.AddToClassList("duel-card-chevron");
            chevron.pickingMode = PickingMode.Ignore;
            card.Add(chevron);

            var matchId = duel.match_id;
            card.RegisterCallback<ClickEvent>(_ => OpenDuel(matchId));
            return card;
        }

        private static string StateText(string playerState)
        {
            return playerState switch
            {
                DuelRules.StateWaitingRival => UiText.Duel.CardWaitingRival,
                DuelRules.StateFinished => UiText.Duel.CardFinished,
                _ => UiText.Duel.CardYourTurn
            };
        }

        private void OpenDuel(string matchId)
        {
            _hub.Open(matchId);
            _router.Show(ScreenId.DuelCombat);
        }

        private void OnNewDuelClicked()
        {
            _hub.CreatePracticeDuel(UiText.Duel.PracticeRivalName);
            _router.Show(ScreenId.DuelCombat);
        }

        private void OnIdleDemoClicked()
        {
            _hub.CreateIdleRivalDuel(UiText.Duel.IdleRivalName);
            _router.Show(ScreenId.DuelCombat);
        }
    }
}
