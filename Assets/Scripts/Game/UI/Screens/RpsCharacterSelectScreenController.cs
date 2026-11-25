using System;
using TTTXO.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Character selection for both RPS modes. Shows the three combatants in Idle and turns the
    /// chosen fighter into the player's move (Guerrero = Rock, Maga = Paper, Asesino = Scissors).
    ///
    /// Single player: one pick resolves the round against the AI and opens the VS screen.
    /// Local 2P: Player One picks, the turn label switches, Player Two picks, then the VS screen
    /// reveals both choices. A tie is a valid result and the series continues.
    /// </summary>
    public sealed class RpsCharacterSelectScreenController : IScreenController
    {
        private readonly VisualElement _root;
        private readonly ScreenRouter _router;

        private readonly VisualElement _caballeroBtn;
        private readonly VisualElement _magaBtn;
        private readonly VisualElement _asesinoBtn;
        private readonly Label _caballeroName;
        private readonly Label _magaName;
        private readonly Label _asesinoName;
        private readonly Label _subtitleLabel;
        private readonly Label _turnLabel;
        private readonly Label _scoreLabel;

        private VSAnimationController _caballeroAnim;
        private VSAnimationController _magaAnim;
        private VSAnimationController _asesinoAnim;
        private RpsCharacterDatabase _database;

        public RpsCharacterSelectScreenController(VisualElement root, ScreenRouter router)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _router = router ?? throw new ArgumentNullException(nameof(router));

            _database = Resources.Load<RpsCharacterDatabase>("RpsCharacterDatabase");

            _caballeroBtn = _root.Q<VisualElement>("charselect-caballero");
            _magaBtn = _root.Q<VisualElement>("charselect-maga");
            _asesinoBtn = _root.Q<VisualElement>("charselect-asesino");
            _caballeroName = _root.Q<Label>("charselect-caballero-name");
            _magaName = _root.Q<Label>("charselect-maga-name");
            _asesinoName = _root.Q<Label>("charselect-asesino-name");
            _subtitleLabel = _root.Q<Label>("charselect-subtitle-label");
            _turnLabel = _root.Q<Label>("charselect-turn-label");
            _scoreLabel = _root.Q<Label>("charselect-score-label");

            var backButton = _root.Q<Button>("charselect-back-button");
            backButton.text = UiText.Common.BackIcon;
            backButton.clicked += () =>
            {
                _router.RpsSeries.End();
                _router.Show(ScreenId.RpsModeSelect);
            };

            _caballeroBtn.RegisterCallback<ClickEvent>(_ => OnCharacterPicked(RpsMove.Rock));
            _magaBtn.RegisterCallback<ClickEvent>(_ => OnCharacterPicked(RpsMove.Paper));
            _asesinoBtn.RegisterCallback<ClickEvent>(_ => OnCharacterPicked(RpsMove.Scissors));

            ApplyLocalizedText();
        }

        private void ApplyLocalizedText()
        {
            _root.Q<Label>("charselect-title-label").text = UiText.RpsCharacterSelect.Title;
            _caballeroName.text = DisplayName(RpsMove.Rock);
            _magaName.text = DisplayName(RpsMove.Paper);
            _asesinoName.text = DisplayName(RpsMove.Scissors);
        }

        private string DisplayName(RpsMove move)
        {
            if (_database != null)
            {
                return _database.GetDisplayName(move);
            }

            return move switch
            {
                RpsMove.Rock => UiText.RpsCharacterSelect.Guerrero,
                RpsMove.Paper => UiText.RpsCharacterSelect.Maga,
                _ => UiText.RpsCharacterSelect.Asesino
            };
        }

        public void OnShow()
        {
            if (_database == null)
            {
                _database = Resources.Load<RpsCharacterDatabase>("RpsCharacterDatabase");
            }

            DisposeAnimations();

            if (_database != null)
            {
                var rock = _database.GetFrames(RpsMove.Rock);
                var paper = _database.GetFrames(RpsMove.Paper);
                var scissors = _database.GetFrames(RpsMove.Scissors);

                var cabSpriteEl = _caballeroBtn.Q<VisualElement>("charselect-caballero-sprite");
                var magaSpriteEl = _magaBtn.Q<VisualElement>("charselect-maga-sprite");
                var asesinoSpriteEl = _asesinoBtn.Q<VisualElement>("charselect-asesino-sprite");

                if (cabSpriteEl != null && magaSpriteEl != null && asesinoSpriteEl != null)
                {
                    _caballeroAnim = new VSAnimationController(cabSpriteEl, _caballeroName, DisplayName(RpsMove.Rock), rock.Idle, rock.Attack, rock.Hurt);
                    _magaAnim = new VSAnimationController(magaSpriteEl, _magaName, DisplayName(RpsMove.Paper), paper.Idle, paper.Attack, paper.Hurt);
                    _asesinoAnim = new VSAnimationController(asesinoSpriteEl, _asesinoName, DisplayName(RpsMove.Scissors), scissors.Idle, scissors.Attack, scissors.Hurt);
                }
                else
                {
                    Debug.LogError("Character select: sprite elements not found in UXML!");
                }
            }
            else
            {
                Debug.LogError("RpsCharacterDatabase not found in Resources!");
            }

            _caballeroAnim?.PlayIdle();
            _magaAnim?.PlayIdle();
            _asesinoAnim?.PlayIdle();

            ApplyLocalizedText();
            UpdateStatusUi();
        }

        public void OnHide()
        {
            DisposeAnimations();
        }

        private void DisposeAnimations()
        {
            _caballeroAnim?.Dispose();
            _magaAnim?.Dispose();
            _asesinoAnim?.Dispose();
            _caballeroAnim = null;
            _magaAnim = null;
            _asesinoAnim = null;
        }

        private void OnCharacterPicked(RpsMove move)
        {
            var series = _router.RpsSeries;

            if (!series.IsActive)
            {
                series.Start(RpsMatchMode.SinglePlayer);
            }

            if (series.Mode == RpsMatchMode.LocalTwoPlayer)
            {
                if (series.AwaitingPlayerTwo)
                {
                    if (series.TrySubmitPlayerTwo(move, out var result) && result != null)
                    {
                        _router.Show(ScreenId.RpsVs);
                    }

                    return;
                }

                if (series.TrySubmitPlayerOne(move, out _))
                {
                    UpdateStatusUi();
                }

                return;
            }

            if (series.TrySubmitPlayerOne(move, out var singleResult) && singleResult != null)
            {
                _router.Show(ScreenId.RpsVs);
            }
        }

        private void UpdateStatusUi()
        {
            var series = _router.RpsSeries;
            var isTwoPlayer = series.Mode == RpsMatchMode.LocalTwoPlayer;

            _subtitleLabel.text = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                UiText.RpsCharacterSelect.RoundFormat,
                series.RoundNumber + 1);

            _turnLabel.text = series.AwaitingPlayerTwo
                ? UiText.RpsCharacterSelect.PlayerTwoTurn
                : isTwoPlayer
                    ? UiText.RpsCharacterSelect.PlayerOneTurn
                    : UiText.RpsCharacterSelect.SingleTurn;

            // In single player this label repeats the header title ("Choose your fighter"). Keep it
            // only for local 2P, where it is the thing that tells the players whose turn it is.
            _turnLabel.style.display = isTwoPlayer ? DisplayStyle.Flex : DisplayStyle.None;

            var oneLabel = isTwoPlayer ? UiText.RpsGame.Player1Label : UiText.RpsGame.YouLabel;
            var twoLabel = isTwoPlayer ? UiText.RpsGame.Player2Label : UiText.RpsGame.AiLabel;
            _scoreLabel.text = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                UiText.RpsCharacterSelect.ScoreFormat,
                oneLabel,
                series.ScoreOne,
                twoLabel,
                series.ScoreTwo);
        }

        public void RefreshLocalizedText()
        {
            ApplyLocalizedText();
            UpdateStatusUi();
        }
    }
}
