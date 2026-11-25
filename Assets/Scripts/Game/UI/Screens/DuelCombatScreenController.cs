using System;
using System.Collections.Generic;
using System.Globalization;
using TTTXO.Core;
using TTTXO.Core.Duel;
using TTTXO.Game.Duel;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Combat screen of the async duel: two HP bars, the three action slots and the class picker.
    ///
    /// The screen never decides anything itself. It only drafts three moves, hands them to
    /// <see cref="DuelHub"/>, and renders the <see cref="DuelRoundResult"/> the hub (or
    /// <see cref="DuelResolver"/>) produced - the same division the VS screen uses with RpsSeries.
    /// The rival's sequence is never shown before the round is resolved.
    /// </summary>
    public sealed class DuelCombatScreenController : IScreenController
    {
        private const int ClashRevealDelayMs = 800;

        private static readonly RpsMove[] PickerMoves = { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors };

        private readonly VisualElement _root;
        private readonly ScreenRouter _router;
        private readonly DuelHub _hub;
        private readonly RpsCharacterDatabase _database;

        private readonly Label _roundLabel;
        private readonly Label _leftName;
        private readonly Label _rightName;
        private readonly Label _leftHpLabel;
        private readonly Label _rightHpLabel;
        private readonly Label _statusLabel;
        private readonly Label _countdownLabel;
        private readonly VisualElement _leftFill;
        private readonly VisualElement _rightFill;
        private readonly VisualElement _leftSprite;
        private readonly VisualElement _rightSprite;
        private readonly VisualElement _pickerRow;
        private readonly VisualElement _slotRow;
        private readonly Button _confirmButton;
        private readonly Button _claimButton;

        private readonly Button[] _slots = new Button[DuelRules.SlotsPerRound];
        private readonly Label[] _rivalSlots = new Label[DuelRules.SlotsPerRound];
        private readonly VisualElement[] _picks = new VisualElement[PickerMoves.Length];
        private readonly Label[] _pickLabels = new Label[PickerMoves.Length];

        private readonly VisualElement _overlay;
        private readonly Label _resultTitle;
        private readonly Label _resultDetail;
        private readonly VisualElement _resultRows;
        private readonly Button _resultContinue;
        private readonly List<VisualElement> _resultRowElements = new();

        private readonly RpsMove?[] _draft = new RpsMove?[DuelRules.SlotsPerRound];

        private IVisualElementScheduledItem _tickTask;
        private IVisualElementScheduledItem _revealTask;
        private VSAnimationController _leftAnim;
        private VSAnimationController _rightAnim;
        private bool _disposed;
        private int _generation;

        private DuelRoundResult _revealedResult;
        private int _nextClashToReveal;
        private int _hpOneShown;
        private int _hpTwoShown;

        public DuelCombatScreenController(VisualElement root, ScreenRouter router, DuelHub hub)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _database = UnityEngine.Resources.Load<RpsCharacterDatabase>("RpsCharacterDatabase");

            _roundLabel = _root.Q<Label>("duel-round-label");
            _leftName = _root.Q<Label>("duel-left-name");
            _rightName = _root.Q<Label>("duel-right-name");
            _leftHpLabel = _root.Q<Label>("duel-left-hp-label");
            _rightHpLabel = _root.Q<Label>("duel-right-hp-label");
            _statusLabel = _root.Q<Label>("duel-status-label");
            _countdownLabel = _root.Q<Label>("duel-countdown-label");
            _leftFill = _root.Q<VisualElement>("duel-left-hp-fill");
            _rightFill = _root.Q<VisualElement>("duel-right-hp-fill");
            _leftSprite = _root.Q<VisualElement>("duel-left-sprite");
            _rightSprite = _root.Q<VisualElement>("duel-right-sprite");
            _pickerRow = _root.Q<VisualElement>("duel-picker-row");
            _slotRow = _root.Q<VisualElement>("duel-slot-row");
            _confirmButton = _root.Q<Button>("duel-confirm-button");
            _claimButton = _root.Q<Button>("duel-claim-button");

            _overlay = _root.Q<VisualElement>("duel-result-overlay");
            _resultTitle = _root.Q<Label>("duel-result-title-label");
            _resultDetail = _root.Q<Label>("duel-result-detail-label");
            _resultRows = _root.Q<VisualElement>("duel-result-rows");
            _resultContinue = _root.Q<Button>("duel-result-continue-button");

            var back = _root.Q<Button>("duel-back-button");
            back.text = UiText.Common.BackIcon;
            back.clicked += () => _router.Show(ScreenId.DuelHub);

            for (int i = 0; i < _slots.Length; i++)
            {
                int index = i;
                _slots[i] = _root.Q<Button>("duel-slot-" + i);
                _slots[i].clicked += () => OnSlotClicked(index);
                _rivalSlots[i] = _root.Q<Label>("duel-rival-slot-" + i);
            }

            _picks[0] = _root.Q<VisualElement>("duel-pick-guerrero");
            _picks[1] = _root.Q<VisualElement>("duel-pick-maga");
            _picks[2] = _root.Q<VisualElement>("duel-pick-asesino");
            _pickLabels[0] = _root.Q<Label>("duel-pick-guerrero-label");
            _pickLabels[1] = _root.Q<Label>("duel-pick-maga-label");
            _pickLabels[2] = _root.Q<Label>("duel-pick-asesino-label");

            for (int i = 0; i < _picks.Length; i++)
            {
                var move = PickerMoves[i];
                _picks[i].RegisterCallback<ClickEvent>(_ => OnPick(move));
            }

            _root.Q<Label>("duel-pick-guerrero-icon").text = UiText.Icons.RpsRock;
            _root.Q<Label>("duel-pick-maga-icon").text = UiText.Icons.RpsPaper;
            _root.Q<Label>("duel-pick-asesino-icon").text = UiText.Icons.RpsScissors;
            ApplyPickArt();

            _confirmButton.clicked += OnConfirmClicked;
            _claimButton.clicked += OnClaimClicked;
            _resultContinue.clicked += OnResultContinueClicked;

            HideOverlay();
            RefreshLocalizedText();
        }

        public void OnShow()
        {
            _disposed = false;
            _generation++;
            CancelTimers();
            DisposeAnimations();
            HideOverlay();
            ClearDraft();

            var duel = _hub.Current;
            if (duel == null)
            {
                _router.Show(ScreenId.DuelHub);
                return;
            }

            var result = _hub.TakeLastResult();
            if (result != null)
            {
                ShowResolution(result);
                return;
            }

            if (duel.IsFinished)
            {
                ShowFinalOutcome(duel);
                return;
            }

            ApplyTurnState();
        }

        public void OnHide()
        {
            _disposed = true;
            _generation++;
            CancelTimers();
            DisposeAnimations();
            _revealedResult = null;
        }

        public void RefreshLocalizedText()
        {
            _root.Q<Label>("duel-vs-label").text = "VS";

            _pickLabels[0].text = UiText.RpsCharacterSelect.Guerrero;
            _pickLabels[1].text = UiText.RpsCharacterSelect.Maga;
            _pickLabels[2].text = UiText.RpsCharacterSelect.Asesino;

            _claimButton.text = UiText.Duel.ClaimButton;
            _resultContinue.text = UiText.Duel.ResultBackToHub;

            if (_revealedResult == null && _hub.Current != null && !_hub.Current.IsFinished)
            {
                ApplyTurnState();
            }
        }

        /// <summary>
        /// Replaces the emoji glyphs of the class picker with the idle frame of each fighter. The
        /// glyphs are cleared only when a frame exists, so a missing database keeps the old fallback.
        /// </summary>
        private void ApplyPickArt()
        {
            if (_database == null)
            {
                return;
            }

            SetPickArt(_root.Q<Label>("duel-pick-guerrero-icon"), RpsMove.Rock);
            SetPickArt(_root.Q<Label>("duel-pick-maga-icon"), RpsMove.Paper);
            SetPickArt(_root.Q<Label>("duel-pick-asesino-icon"), RpsMove.Scissors);
        }

        private void SetPickArt(Label icon, RpsMove move)
        {
            var frames = _database.GetFrames(move);
            if (icon == null || frames.Idle == null || frames.Idle.Length == 0)
            {
                return;
            }

            icon.text = string.Empty;
            icon.AddToClassList("duel-pick-icon--art");
            icon.style.backgroundImage = new StyleBackground(frames.Idle[0]);
        }

        // ---- Turn state -------------------------------------------------------------------

        private void ApplyTurnState()
        {
            var duel = _hub.Current;
            if (duel == null)
            {
                return;
            }

            var me = duel.PlayerById(_hub.LocalPlayerId);
            var rival = duel.OpponentOf(_hub.LocalPlayerId);

            _roundLabel.text = string.Format(CultureInfo.InvariantCulture, UiText.Duel.RoundFormat, duel.Round);
            _leftName.text = me?.DisplayName ?? _hub.LocalPlayerName;
            _rightName.text = rival?.DisplayName ?? "?";
            UpdateHpBars(me?.CurrentHp ?? 0, rival?.CurrentHp ?? 0);

            bool canSubmit = duel.CanSubmit(_hub.LocalPlayerId);
            bool waiting = _hub.IsWaitingOnRival();

            _pickerRow.style.display = canSubmit ? DisplayStyle.Flex : DisplayStyle.None;
            _confirmButton.style.display = canSubmit ? DisplayStyle.Flex : DisplayStyle.None;
            _slotRow.SetEnabled(canSubmit);

            _statusLabel.text = canSubmit ? UiText.Duel.YourTurn : UiText.Duel.WaitingRival;
            _statusLabel.style.display = DisplayStyle.Flex;

            UpdateSlots(canSubmit);
            UpdateRivalSlots(null);
            UpdateConfirmLabel();

            // While it is your turn the sprite follows the draft; once the moves are sent it shows
            // what you locked in, so the waiting screen is not an empty board. The rival's sprite
            // stays hidden until the round is resolved.
            SetSprite(_leftSprite, canSubmit ? FirstDraftMove() : FirstSequenceMove(me));
            SetSprite(_rightSprite, null);

            RefreshClaimState();

            if (waiting)
            {
                _countdownLabel.style.display = DisplayStyle.Flex;
                StartTicking();
            }
            else
            {
                _countdownLabel.style.display = DisplayStyle.None;
                StopTicking();
            }
        }

        private void UpdateSlots(bool canSubmit)
        {
            // When the round is already sent, the draft is empty but the document still holds the
            // three locked-in moves; show those instead of three empty slots.
            var me = canSubmit ? null : _hub.Current?.PlayerById(_hub.LocalPlayerId);

            for (int i = 0; i < _slots.Length; i++)
            {
                RpsMove? move = _draft[i];
                if (!move.HasValue && me?.secuencia_actual != null && i < me.secuencia_actual.Length)
                {
                    if (DuelMoveCodec.TryParse(me.secuencia_actual[i], out var submitted))
                    {
                        move = submitted;
                    }
                }

                _slots[i].text = move.HasValue ? DisplayName(move.Value) : UiText.Duel.SlotEmpty;
                _slots[i].EnableInClassList("duel-slot--filled", move.HasValue);
                _slots[i].EnableInClassList("duel-slot--locked", !canSubmit);
            }
        }

        /// <summary>
        /// Rival's three slots. Empty while the round is open (their move is secret), filled once the
        /// round has been resolved and the clash rows are already showing the same information.
        /// </summary>
        private void UpdateRivalSlots(IReadOnlyList<RpsMove> moves)
        {
            for (int i = 0; i < _rivalSlots.Length; i++)
            {
                bool filled = moves != null && i < moves.Count;
                _rivalSlots[i].text = filled ? DisplayName(moves[i]) : UiText.Duel.SlotEmpty;
                _rivalSlots[i].EnableInClassList("duel-slot--filled", filled);
            }
        }

        private void UpdateConfirmLabel()
        {
            bool complete = CountDraft() == DuelRules.SlotsPerRound;
            _confirmButton.text = complete ? UiText.Duel.ConfirmButton : UiText.Duel.ConfirmIncomplete;
            _confirmButton.SetEnabled(complete);
        }

        private void OnPick(RpsMove move)
        {
            var duel = _hub.Current;
            if (duel == null || !duel.CanSubmit(_hub.LocalPlayerId))
            {
                return;
            }

            for (int i = 0; i < _draft.Length; i++)
            {
                if (_draft[i].HasValue)
                {
                    continue;
                }

                _draft[i] = move;
                UpdateSlots(canSubmit: true);
                UpdateConfirmLabel();
                SetSprite(_leftSprite, FirstDraftMove());
                return;
            }
        }

        private void OnSlotClicked(int index)
        {
            var duel = _hub.Current;
            if (duel == null || !duel.CanSubmit(_hub.LocalPlayerId))
            {
                return;
            }

            if (!_draft[index].HasValue)
            {
                return;
            }

            _draft[index] = null;
            UpdateSlots(canSubmit: true);
            UpdateConfirmLabel();
            SetSprite(_leftSprite, FirstDraftMove());
        }

        private void OnConfirmClicked()
        {
            if (CountDraft() != DuelRules.SlotsPerRound)
            {
                return;
            }

            var moves = new List<RpsMove>(DuelRules.SlotsPerRound);
            foreach (var move in _draft)
            {
                if (!move.HasValue)
                {
                    return;
                }

                moves.Add(move.Value);
            }

            var result = _hub.Submit(moves);
            ClearDraft();

            if (result != null)
            {
                ShowResolution(result);
                return;
            }

            ApplyTurnState();
        }

        private void OnClaimClicked()
        {
            if (!_hub.ClaimVictory())
            {
                return;
            }

            ShowFinalOutcome(_hub.Current);
        }

        // ---- Result overlay ---------------------------------------------------------------

        private void ShowResolution(DuelRoundResult result)
        {
            var duel = _hub.Current;
            if (duel != null)
            {
                var me = duel.PlayerById(_hub.LocalPlayerId);
                var rival = duel.OpponentOf(_hub.LocalPlayerId);
                _roundLabel.text = string.Format(CultureInfo.InvariantCulture, UiText.Duel.RoundFormat, result.RoundNumber);
                _leftName.text = me?.DisplayName ?? _hub.LocalPlayerName;
                _rightName.text = rival?.DisplayName ?? "?";
            }

            _revealedResult = result;
            _nextClashToReveal = 0;
            _resultRowElements.Clear();
            _resultRows.Clear();

            _hpOneShown = result.PlayerOneHp + result.DamageToPlayerOne;
            _hpTwoShown = result.PlayerTwoHp + result.DamageToPlayerTwo;
            UpdateHpBars(_hpOneShown, _hpTwoShown);

            _resultTitle.text = ResultTitle(result);
            _resultDetail.text = ResultDetail(result);
            _resultDetail.style.display = string.IsNullOrEmpty(_resultDetail.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            foreach (var clash in result.Clashes)
            {
                var row = BuildClashRow(clash);
                row.style.display = DisplayStyle.None;
                _resultRows.Add(row);
                _resultRowElements.Add(row);
            }

            var rivalMoves = new List<RpsMove>(result.Clashes.Count);
            foreach (var clash in result.Clashes)
            {
                rivalMoves.Add(clash.PlayerTwoMove);
            }

            UpdateRivalSlots(rivalMoves);

            _resultContinue.style.display = DisplayStyle.None;
            SetSpritesForResult(result);
            _overlay.style.display = DisplayStyle.Flex;

            var generation = _generation;
            _revealTask = _root.schedule.Execute(() => RevealNextClash(generation)).StartingIn(ClashRevealDelayMs / 2);
        }

        private void RevealNextClash(int generation)
        {
            if (_disposed || generation != _generation || _revealedResult == null)
            {
                return;
            }

            if (_nextClashToReveal >= _resultRowElements.Count)
            {
                FinishReveal();
                return;
            }

            var clash = _revealedResult.Clashes[_nextClashToReveal];
            _resultRowElements[_nextClashToReveal].style.display = DisplayStyle.Flex;

            if (clash.Winner == RpsPlayer.Two)
            {
                _hpOneShown = Math.Max(_revealedResult.PlayerOneHp, _hpOneShown - clash.Damage);
            }
            else if (clash.Winner == RpsPlayer.One)
            {
                _hpTwoShown = Math.Max(_revealedResult.PlayerTwoHp, _hpTwoShown - clash.Damage);
            }

            UpdateHpBars(_hpOneShown, _hpTwoShown);
            _nextClashToReveal++;

            if (_nextClashToReveal >= _resultRowElements.Count)
            {
                FinishReveal();
                return;
            }

            _revealTask = _root.schedule.Execute(() => RevealNextClash(generation))
                .StartingIn(ClashRevealDelayMs);
        }

        private void FinishReveal()
        {
            UpdateHpBars(_revealedResult.PlayerOneHp, _revealedResult.PlayerTwoHp);
            _resultContinue.text = _revealedResult.IsFinished
                ? UiText.Duel.ResultBackToHub
                : UiText.Duel.ResultContinue;
            _resultContinue.style.display = DisplayStyle.Flex;
            PlayOutcomeAnimation();
        }

        private void ShowFinalOutcome(DuelStateDto duel)
        {
            if (duel == null)
            {
                _router.Show(ScreenId.DuelHub);
                return;
            }

            var me = duel.PlayerById(_hub.LocalPlayerId);
            var rival = duel.OpponentOf(_hub.LocalPlayerId);
            UpdateHpBars(me?.CurrentHp ?? 0, rival?.CurrentHp ?? 0);
            _leftName.text = me?.DisplayName ?? _hub.LocalPlayerName;
            _rightName.text = rival?.DisplayName ?? "?";
            if (me != null)
            {
                SetSprite(_leftSprite, FirstSequenceMove(me));
            }

            // The resolution clears both sequences, so the rival has no class left to draw; clearing
            // the sprite also drops whatever the previous round left on screen.
            SetSprite(_rightSprite, null);

            _revealedResult = null;
            _resultRows.Clear();
            _resultRowElements.Clear();
            UpdateRivalSlots(null);

            if (duel.WinnerId == _hub.LocalPlayerId)
            {
                _resultTitle.text = UiText.Duel.ResultWin;
                _resultDetail.text = duel.EndReason switch
                {
                    DuelRules.EndAbandon => UiText.Duel.WinByAbandon,
                    DuelRules.EndKo => UiText.Duel.WinByKo,
                    _ => string.Empty
                };
            }
            else if (string.IsNullOrEmpty(duel.WinnerId))
            {
                _resultTitle.text = UiText.Duel.ResultDraw;
                _resultDetail.text = duel.EndReason == DuelRules.EndRoundLimit ? UiText.Duel.DrawByLimit : string.Empty;
            }
            else
            {
                _resultTitle.text = UiText.Duel.ResultLoss;
                _resultDetail.text = duel.EndReason switch
                {
                    DuelRules.EndAbandon => string.Format(
                        CultureInfo.InvariantCulture, UiText.Duel.LossReasonFormat, UiText.Duel.ReasonAbandon),
                    DuelRules.EndKo => string.Format(
                        CultureInfo.InvariantCulture, UiText.Duel.LossReasonFormat, UiText.Duel.ReasonKo),
                    _ => string.Empty
                };
            }

            _resultDetail.style.display = string.IsNullOrEmpty(_resultDetail.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _resultContinue.text = UiText.Duel.ResultBackToHub;
            _resultContinue.style.display = DisplayStyle.Flex;
            _overlay.style.display = DisplayStyle.Flex;

            StopTicking();
        }

        private void OnResultContinueClicked()
        {
            var finished = _revealedResult == null || _revealedResult.IsFinished;
            _revealedResult = null;

            if (finished)
            {
                ClearDraft();
                _hub.Refresh();
                _router.Show(ScreenId.DuelHub);
                return;
            }

            HideOverlay();
            ClearDraft();
            ApplyTurnState();
        }

        private VisualElement BuildClashRow(DuelClash clash)
        {
            var row = new VisualElement();
            row.AddToClassList("duel-clash-row");

            var title = new Label(string.Format(
                CultureInfo.InvariantCulture,
                UiText.Duel.ClashFormat,
                DisplayName(clash.PlayerOneMove),
                DisplayName(clash.PlayerTwoMove)));
            title.AddToClassList("duel-clash-title");

            string detailText;
            if (clash.IsDraw)
            {
                row.AddToClassList("duel-clash-row--draw");
                detailText = UiText.Duel.ClashDraw;
            }
            else if (clash.Winner == RpsPlayer.One)
            {
                row.AddToClassList("duel-clash-row--win");
                detailText = string.Format(
                    CultureInfo.InvariantCulture,
                    UiText.Duel.ClashDamageFormat,
                    DisplayName(clash.PlayerOneMove),
                    clash.Damage);
            }
            else
            {
                row.AddToClassList("duel-clash-row--loss");
                detailText = string.Format(
                    CultureInfo.InvariantCulture,
                    UiText.Duel.ClashDamageFormat,
                    DisplayName(clash.PlayerTwoMove),
                    clash.Damage);
            }

            var detail = new Label(detailText);
            detail.AddToClassList("duel-clash-detail");

            row.Add(title);
            row.Add(detail);
            return row;
        }

        private string ResultTitle(DuelRoundResult result)
        {
            if (!result.IsFinished)
            {
                return string.Format(CultureInfo.InvariantCulture, UiText.Duel.ResultRoundTitleFormat, result.RoundNumber);
            }

            if (result.WinnerId == _hub.LocalPlayerId)
            {
                return UiText.Duel.ResultWin;
            }

            return result.IsDraw ? UiText.Duel.ResultDraw : UiText.Duel.ResultLoss;
        }

        private string ResultDetail(DuelRoundResult result)
        {
            if (!result.IsFinished)
            {
                return string.Empty;
            }

            if (result.IsDraw)
            {
                return result.EndReason == DuelRules.EndRoundLimit ? UiText.Duel.DrawByLimit : string.Empty;
            }

            if (result.WinnerId == _hub.LocalPlayerId)
            {
                return result.EndReason switch
                {
                    DuelRules.EndAbandon => UiText.Duel.WinByAbandon,
                    DuelRules.EndKo => UiText.Duel.WinByKo,
                    _ => string.Empty
                };
            }

            return result.EndReason switch
            {
                DuelRules.EndAbandon => string.Format(
                    CultureInfo.InvariantCulture, UiText.Duel.LossReasonFormat, UiText.Duel.ReasonAbandon),
                DuelRules.EndKo => string.Format(
                    CultureInfo.InvariantCulture, UiText.Duel.LossReasonFormat, UiText.Duel.ReasonKo),
                _ => string.Empty
            };
        }

        // ---- Timeout ----------------------------------------------------------------------

        private void StartTicking()
        {
            StopTicking();
            _tickTask = _root.schedule.Execute(Tick).Every(1000);
            Tick();
        }

        private void StopTicking()
        {
            _tickTask?.Pause();
            _tickTask = null;
        }

        private void Tick()
        {
            if (_disposed)
            {
                return;
            }

            RefreshClaimState();
        }

        private void RefreshClaimState()
        {
            var duel = _hub.Current;
            if (duel == null || duel.IsFinished || !_hub.IsWaitingOnRival())
            {
                _claimButton.style.display = DisplayStyle.None;
                _countdownLabel.style.display = DisplayStyle.None;
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (DuelTimeout.CanClaimVictory(duel, _hub.LocalPlayerId, now, _hub.Timeout))
            {
                _claimButton.style.display = DisplayStyle.Flex;
                _claimButton.SetEnabled(true);
                _countdownLabel.style.display = DisplayStyle.None;
                return;
            }

            _claimButton.style.display = DisplayStyle.None;

            if (!DuelTimestamp.TryParse(duel.timestamp_ultimo_movimiento, out var last))
            {
                return;
            }

            var remaining = _hub.Timeout - (now - last);
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            _countdownLabel.text = string.Format(
                CultureInfo.InvariantCulture,
                UiText.Duel.WaitingCountdownFormat,
                (int)remaining.TotalMinutes,
                remaining.Seconds,
                (int)_hub.Timeout.TotalMinutes);
            _countdownLabel.style.display = DisplayStyle.Flex;
        }

        // ---- Presentation helpers ----------------------------------------------------------

        private void UpdateHpBars(int playerOneHp, int playerTwoHp)
        {
            _leftFill.style.width = Length.Percent(ClampHp(playerOneHp));
            _rightFill.style.width = Length.Percent(ClampHp(playerTwoHp));
            _leftHpLabel.text = string.Format(CultureInfo.InvariantCulture, UiText.Duel.HpFormat, ClampHp(playerOneHp), DuelRules.MaxHp);
            _rightHpLabel.text = string.Format(CultureInfo.InvariantCulture, UiText.Duel.HpFormat, ClampHp(playerTwoHp), DuelRules.MaxHp);
        }

        private static float ClampHp(int hp)
        {
            return Math.Max(0, Math.Min(DuelRules.MaxHp, hp));
        }

        private void SetSprite(VisualElement element, RpsMove? move)
        {
            if (element == null)
            {
                return;
            }

            if (!move.HasValue || _database == null)
            {
                element.style.visibility = Visibility.Hidden;
                element.style.backgroundImage = StyleKeyword.Null;
                return;
            }

            var frames = _database.GetFrames(move.Value);
            element.style.visibility = Visibility.Visible;

            if (frames.Idle != null && frames.Idle.Length > 0)
            {
                element.style.backgroundImage = new StyleBackground(frames.Idle[0]);
            }
            else
            {
                element.style.backgroundImage = StyleKeyword.Null;
            }
        }

        private void SetSpritesForResult(DuelRoundResult result)
        {
            if (result.Clashes.Count == 0)
            {
                return;
            }

            SetSprite(_leftSprite, result.Clashes[0].PlayerOneMove);
            SetSprite(_rightSprite, result.Clashes[0].PlayerTwoMove);
        }

        /// <summary>
        /// Plays one Attack/Hurt beat on the two sprites once the clashes are on screen. The full
        /// frame-by-frame clash animation is still pending; this reuses the VS screen's controller so
        /// at least the outcome reads as a hit instead of a number changing.
        /// </summary>
        private void PlayOutcomeAnimation()
        {
            if (_database == null || _revealedResult == null || _revealedResult.IsDraw)
            {
                return;
            }

            DisposeAnimations();

            if (_revealedResult.Clashes.Count == 0)
            {
                return;
            }

            var first = _revealedResult.Clashes[0];
            bool playerOneWonRound = _revealedResult.DamageToPlayerTwo > _revealedResult.DamageToPlayerOne;

            var leftFrames = _database.GetFrames(first.PlayerOneMove);
            var rightFrames = _database.GetFrames(first.PlayerTwoMove);

            _leftAnim = new VSAnimationController(
                _leftSprite, _leftName, _leftName.text,
                leftFrames.Idle, leftFrames.Attack, leftFrames.Hurt);
            _rightAnim = new VSAnimationController(
                _rightSprite, _rightName, _rightName.text,
                rightFrames.Idle, rightFrames.Attack, rightFrames.Hurt);

            _leftAnim.PlayIdle();
            _rightAnim.PlayIdle();

            if (playerOneWonRound)
            {
                _leftAnim.PlayAttack();
                _rightAnim.PlayHurt();
            }
            else
            {
                _leftAnim.PlayHurt();
                _rightAnim.PlayAttack();
            }
        }

        private void DisposeAnimations()
        {
            _leftAnim?.Dispose();
            _rightAnim?.Dispose();
            _leftAnim = null;
            _rightAnim = null;
        }

        private void CancelTimers()
        {
            _tickTask?.Pause();
            _revealTask?.Pause();
            _tickTask = null;
            _revealTask = null;
        }

        private void HideOverlay()
        {
            _overlay.style.display = DisplayStyle.None;
        }

        private void ClearDraft()
        {
            for (int i = 0; i < _draft.Length; i++)
            {
                _draft[i] = null;
            }

            UpdateSlots(canSubmit: _hub.Current != null && _hub.Current.CanSubmit(_hub.LocalPlayerId));
            UpdateConfirmLabel();
        }

        private int CountDraft()
        {
            int count = 0;
            foreach (var move in _draft)
            {
                if (move.HasValue)
                {
                    count++;
                }
            }

            return count;
        }

        private RpsMove? FirstDraftMove()
        {
            foreach (var move in _draft)
            {
                if (move.HasValue)
                {
                    return move;
                }
            }

            return null;
        }

        /// <summary>First move of a player's stored sequence, if it still has one.</summary>
        private static RpsMove? FirstSequenceMove(DuelPlayerDto player)
        {
            if (player?.secuencia_actual == null)
            {
                return null;
            }

            foreach (var code in player.secuencia_actual)
            {
                if (DuelMoveCodec.TryParse(code, out var move))
                {
                    return move;
                }
            }

            return null;
        }

        /// <summary>Localized class name for a move (the database strings are not translated).</summary>
        private static string DisplayName(RpsMove move)
        {
            return move switch
            {
                RpsMove.Rock => UiText.RpsCharacterSelect.Guerrero,
                RpsMove.Paper => UiText.RpsCharacterSelect.Maga,
                _ => UiText.RpsCharacterSelect.Asesino
            };
        }
    }
}
