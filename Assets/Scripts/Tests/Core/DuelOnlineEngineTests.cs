using System;
using System.Linq;
using NUnit.Framework;
using TTTXO.Core.Duel;
using TTTXO.Core.Duel.Online;
using Proto = TTTXO.Core.Duel.Online.DuelOnlineProtocol;

namespace TTTXO.Core.Tests
{
    /// <summary>
    /// The authoritative online rules of the duel (<see cref="DuelOnlineEngine"/>), exercised without
    /// Cloud Code, the network or a clock: every call receives "now". The same file also runs in
    /// <c>CloudCode~/DuelModule.Tests</c>, so the module is tested with the very code it ships.
    /// Several tests are the cheats the course warns about: submitting twice, replaying a request,
    /// reading the rival's pending sequence, claiming a victory too early.
    /// </summary>
    public class DuelOnlineEngineTests
    {
        private const string Alice = "alice";
        private const string Bob = "bob";
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

        private static readonly string[] AllWarriors = { "Guerrero", "Guerrero", "Guerrero" };
        private static readonly string[] AllAssassins = { "Asesino", "Asesino", "Asesino" };
        private static readonly string[] AllMages = { "Maga", "Maga", "Maga" };

        private static int _counter;

        private static DuelRecord NewWaitingDuel() => DuelOnlineEngine.Create("ABC234", Alice, "Alice", "create-1", T0);

        private static DuelRecord NewStartedDuel()
        {
            var record = NewWaitingDuel();
            var join = DuelOnlineEngine.Join(record, Bob, "Bob", "join-1", T0.AddSeconds(10));
            Assert.AreEqual(Proto.Result.Applied, join.Result);
            return record;
        }

        private static DuelEngineResult Submit(DuelRecord record, string player, string[] moves, DateTimeOffset? now = null)
        {
            return DuelOnlineEngine.SubmitSequence(record, player, moves, record.Round, "req-" + (++_counter), now ?? T0.AddSeconds(20));
        }

        private static DuelView ViewFor(DuelRecord record, string player, string result = Proto.Result.Applied)
        {
            return DuelOnlineEngine.BuildView(record, player, T0.AddSeconds(30), result, Window);
        }

        // ---- lobby -----------------------------------------------------------------------------

        [Test]
        public void Create_OpensAWaitingDuelWithOnlyTheCreator()
        {
            var record = NewWaitingDuel();

            Assert.AreEqual(Proto.Phase.WaitingForRival, record.Phase);
            CollectionAssert.AreEqual(new[] { Alice }, record.PlayerIds);
            Assert.AreEqual(0, DuelOnlineEngine.SeatOf(record, Alice));
            Assert.AreEqual(-1, DuelOnlineEngine.SeatOf(record, Bob));
            Assert.AreEqual(DuelRules.StateWaitingRival, ViewFor(record, Alice).EstadoPartida);
        }

        [Test]
        public void Join_StartsTheDuelWithFullHpAndNotifiesTheCreator()
        {
            var record = NewWaitingDuel();

            var result = DuelOnlineEngine.Join(record, Bob, "Bob", "join-1", T0.AddSeconds(10));

            Assert.AreEqual(Proto.Result.Applied, result.Result);
            Assert.AreEqual(Proto.Phase.InProgress, record.Phase);
            Assert.AreEqual(1, record.Round);
            CollectionAssert.AreEqual(new[] { DuelRules.MaxHp, DuelRules.MaxHp }, record.Hp);
            Assert.AreEqual(0, result.Notices.Single().Seat);
            Assert.AreEqual(Proto.PushEvent.RivalJoined, result.Notices.Single().Event);
        }

        [Test]
        public void Join_TheCreatorOwnDuel_IsRejected()
        {
            var record = NewWaitingDuel();

            Assert.AreEqual(Proto.Result.WrongState, DuelOnlineEngine.Join(record, Alice, "Alice", "join-x", T0).Result);
            Assert.AreEqual(Proto.Phase.WaitingForRival, record.Phase);
        }

        [Test]
        public void Join_AThirdPlayer_IsRejected()
        {
            var record = NewStartedDuel();

            Assert.AreEqual(Proto.Result.WrongState, DuelOnlineEngine.Join(record, "carol", "Carol", "join-c", T0).Result);
            CollectionAssert.AreEqual(new[] { Alice, Bob }, record.PlayerIds);
        }

        [Test]
        public void Join_RetryOfTheSameRequest_IsReplayed()
        {
            var record = NewStartedDuel();

            var retry = DuelOnlineEngine.Join(record, Bob, "Bob", "join-1", T0.AddSeconds(15));

            Assert.AreEqual(Proto.Result.Replayed, retry.Result);
            Assert.IsFalse(retry.Changed);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("abc")]
        [TestCase("ABC2345")]
        [TestCase("ABC0O1")]
        public void NormalizeCode_RejectsAnythingThatIsNotACode(string input)
        {
            Assert.IsNull(DuelOnlineEngine.NormalizeCode(input));
        }

        [Test]
        public void NormalizeCode_AcceptsLowercaseAndWhitespace()
        {
            Assert.AreEqual("ABC234", DuelOnlineEngine.NormalizeCode("  abc234 "));
        }

        [Test]
        public void DisplayNames_AreCleanedBeforeTheRivalSeesThem()
        {
            var record = DuelOnlineEngine.Create("ABC234", Alice, "  Al\u0007ice\n ", "r1", T0);
            DuelOnlineEngine.Join(record, Bob, new string('x', 100), "r2", T0);
            var blank = DuelOnlineEngine.Create("ZZZ234", Alice, "   ", "r3", T0);

            Assert.AreEqual("Alice", record.PlayerNames[0]);
            Assert.AreEqual(24, record.PlayerNames[1].Length);
            Assert.AreEqual("Player", blank.PlayerNames[0]);
        }

        // ---- hidden sequences ------------------------------------------------------------------

        [Test]
        public void Submit_FirstSequence_StaysHiddenFromTheRival()
        {
            var record = NewStartedDuel();

            var result = Submit(record, Alice, AllWarriors);

            Assert.AreEqual(Proto.Result.Applied, result.Result);
            Assert.AreEqual(Proto.Phase.InProgress, record.Phase);

            var bobView = ViewFor(record, Bob);
            Assert.IsTrue(bobView.RivalHasSubmitted, "Bob may know that Alice played...");
            Assert.IsFalse(bobView.YouHaveSubmitted);
            Assert.IsEmpty(bobView.YourPendingSequence, "...but not what she played.");
            Assert.IsNull(bobView.LastRound);
            Assert.AreEqual(DuelRules.StateYourTurn, bobView.EstadoPartida);

            var aliceView = ViewFor(record, Alice);
            Assert.IsTrue(aliceView.YouHaveSubmitted);
            CollectionAssert.AreEqual(AllWarriors, aliceView.YourPendingSequence, "A reconnecting player sees their own sequence.");
            Assert.IsFalse(aliceView.RivalHasSubmitted);
            Assert.AreEqual(DuelRules.StateWaitingRival, aliceView.EstadoPartida);
        }

        [Test]
        public void Submit_FirstSequence_NotifiesTheRival()
        {
            var record = NewStartedDuel();

            var result = Submit(record, Alice, AllWarriors);

            Assert.AreEqual(1, result.Notices.Single().Seat);
            Assert.AreEqual(Proto.PushEvent.RivalSubmitted, result.Notices.Single().Event);
        }

        [Test]
        public void Submit_SecondSequence_ResolvesTheRoundWithTheProjectsOwnRules()
        {
            var record = NewStartedDuel();

            Submit(record, Alice, AllWarriors);
            var result = Submit(record, Bob, AllAssassins); // Guerrero beats Asesino

            Assert.AreEqual(Proto.Result.Applied, result.Result);
            Assert.AreEqual(2, record.Round);
            Assert.AreEqual(DuelRules.MaxHp, record.Hp[0]);
            Assert.AreEqual(DuelRules.MaxHp - 3 * DuelRules.ClashDamage, record.Hp[1]);
            Assert.AreEqual(Proto.PushEvent.RoundResolved, result.Notices.Single().Event);
            Assert.IsFalse(record.Sequence0.Any(s => s.Length > 0), "The round's sequences are cleared once it is scored.");

            var aliceRound = ViewFor(record, Alice).LastRound;
            Assert.AreEqual(1, aliceRound.Round);
            Assert.AreEqual(0, aliceRound.DamageToYou);
            Assert.AreEqual(45, aliceRound.DamageToRival);
            Assert.AreEqual("Guerrero", aliceRound.Clashes[0].YourMove);
            Assert.AreEqual("Asesino", aliceRound.Clashes[0].RivalMove);
            Assert.AreEqual(Proto.ClashOutcome.You, aliceRound.Clashes[0].Outcome);

            var bobRound = ViewFor(record, Bob).LastRound;
            Assert.AreEqual(45, bobRound.DamageToYou, "Each player sees the round from their own side.");
            Assert.AreEqual("Asesino", bobRound.Clashes[0].YourMove);
            Assert.AreEqual(Proto.ClashOutcome.Rival, bobRound.Clashes[0].Outcome);
        }

        [Test]
        public void Submit_IdenticalSequences_AreAllDrawsWithNoDamage()
        {
            var record = NewStartedDuel();

            Submit(record, Alice, AllMages);
            Submit(record, Bob, AllMages);

            CollectionAssert.AreEqual(new[] { 100, 100 }, record.Hp);
            Assert.IsTrue(ViewFor(record, Alice).LastRound.Clashes.All(c => c.Outcome == Proto.ClashOutcome.Draw));
            Assert.AreEqual(2, record.Round);
        }

        [Test]
        public void TheDuel_EndsByKnockoutWhenAPlayerRunsOutOfHp()
        {
            var record = NewStartedDuel();

            // 45 damage per round: 100 -> 55 -> 10 -> 0 (KO in the third round).
            for (var round = 0; round < 3; round++)
            {
                Submit(record, Alice, AllWarriors);
                Submit(record, Bob, AllAssassins);
            }

            Assert.AreEqual(Proto.Phase.Finished, record.Phase);
            Assert.AreEqual(DuelRules.EndKo, record.EndReason);
            Assert.AreEqual(Alice, record.WinnerId);
            Assert.AreEqual(0, record.Hp[1]);

            var bobView = ViewFor(record, Bob);
            Assert.AreEqual(Proto.ClashOutcome.Rival, bobView.Winner);
            Assert.AreEqual(DuelRules.StateFinished, bobView.EstadoPartida);
            Assert.AreEqual(Proto.Result.WrongState, Submit(record, Bob, AllMages).Result);
        }

        // ---- cheats and mistakes ---------------------------------------------------------------

        [Test]
        public void Submit_TwiceInTheSameRound_IsRejectedAndTheFirstSequenceStands()
        {
            var record = NewStartedDuel();
            Submit(record, Alice, AllWarriors);

            var second = Submit(record, Alice, AllMages);

            Assert.AreEqual(Proto.Result.AlreadySubmitted, second.Result);
            CollectionAssert.AreEqual(AllWarriors, record.Sequence0, "A hidden sequence cannot be changed.");
        }

        [Test]
        public void Submit_ReplayOfAnAppliedRequest_IsReplayedEvenAfterTheRoundMovedOn()
        {
            var record = NewStartedDuel();
            DuelOnlineEngine.SubmitSequence(record, Alice, AllWarriors, 1, "alice-r1", T0.AddSeconds(20));
            DuelOnlineEngine.SubmitSequence(record, Bob, AllAssassins, 1, "bob-r1", T0.AddSeconds(20));
            Assert.AreEqual(2, record.Round);

            // The network dropped Alice's answer; her client retries with the same requestId.
            var retry = DuelOnlineEngine.SubmitSequence(record, Alice, AllWarriors, 1, "alice-r1", T0.AddSeconds(25));

            Assert.AreEqual(Proto.Result.Replayed, retry.Result, "A retry must not look like a stale submission.");
            Assert.IsFalse(retry.Changed);
            Assert.AreEqual(55, record.Hp[1], "The retry did not deal the damage a second time.");
        }

        [Test]
        public void Submit_ForAnotherRound_IsStale()
        {
            var record = NewStartedDuel();

            var result = DuelOnlineEngine.SubmitSequence(record, Alice, AllWarriors, 9, "req-stale", T0.AddSeconds(20));

            Assert.AreEqual(Proto.Result.StaleRound, result.Result);
            Assert.IsFalse(record.Sequence0.Any(s => s.Length > 0));
        }

        [Test]
        public void Submit_WithAnInvalidSequence_IsRejectedWithoutChangingAnything()
        {
            var record = NewStartedDuel();
            var bad = new[]
            {
                null,
                new string[0],
                new[] { "Guerrero", "Maga" },
                new[] { "Guerrero", "Maga", "Asesino", "Guerrero" },
                new[] { "Guerrero", "Maga", "Lizard" },
                new[] { "Guerrero", "", "Asesino" },
                new[] { "Guerrero", null, "Asesino" }
            };

            foreach (var moves in bad)
            {
                var result = DuelOnlineEngine.SubmitSequence(record, Alice, moves, 1, "req-bad-" + (++_counter), T0.AddSeconds(20));

                Assert.AreEqual(Proto.Result.InvalidArgument, result.Result);
                Assert.IsFalse(result.Changed);
            }

            Assert.IsFalse(record.Sequence0.Any(s => s.Length > 0));
            Assert.IsEmpty(record.ProcessedRequestIds.Where(id => id.StartsWith("req-bad-")));
        }

        [Test]
        public void Submit_WithoutARequestId_IsRejected()
        {
            var record = NewStartedDuel();

            Assert.AreEqual(Proto.Result.InvalidArgument,
                DuelOnlineEngine.SubmitSequence(record, Alice, AllWarriors, 1, null, T0.AddSeconds(20)).Result);
        }

        [Test]
        public void Submit_FromSomeoneOutsideTheDuel_IsRejectedAndLeaksNothing()
        {
            var record = NewStartedDuel();
            Submit(record, Alice, AllWarriors);

            var intruder = DuelOnlineEngine.SubmitSequence(record, "mallory", AllMages, 1, "req-m", T0.AddSeconds(21));
            var view = ViewFor(record, "mallory", intruder.Result);

            Assert.AreEqual(Proto.Result.NotAParticipant, intruder.Result);
            Assert.AreEqual(-1, view.YourSeat);
            Assert.IsFalse(view.RivalHasSubmitted);
            Assert.IsEmpty(view.YourPendingSequence);
            Assert.IsNull(view.LastRound);
            Assert.IsNull(view.YourName);
        }

        [Test]
        public void Submit_BeforeTheRivalJoined_IsRejected()
        {
            var record = NewWaitingDuel();

            Assert.AreEqual(Proto.Result.WrongState, Submit(record, Alice, AllWarriors).Result);
        }

        // ---- reclamar victoria -----------------------------------------------------------------

        [Test]
        public void ClaimVictory_WhenTheRivalWasSilentForTheWholeWindow_WinsByAbandonment()
        {
            var record = NewStartedDuel();
            var submittedAt = T0.AddSeconds(20);
            Submit(record, Alice, AllWarriors, submittedAt);

            var result = DuelOnlineEngine.ClaimVictory(record, Alice, "claim-1", submittedAt + Window, Window);

            Assert.AreEqual(Proto.Result.Applied, result.Result);
            Assert.AreEqual(Proto.Phase.Finished, record.Phase);
            Assert.AreEqual(Alice, record.WinnerId);
            Assert.AreEqual(DuelRules.EndAbandon, record.EndReason);
            Assert.AreEqual(Proto.PushEvent.Finished, result.Notices.Single().Event);
        }

        [Test]
        public void ClaimVictory_BeforeTheWindowEnds_IsNotAvailable()
        {
            var record = NewStartedDuel();
            var submittedAt = T0.AddSeconds(20);
            Submit(record, Alice, AllWarriors, submittedAt);

            var result = DuelOnlineEngine.ClaimVictory(record, Alice, "claim-1", submittedAt + Window - TimeSpan.FromSeconds(1), Window);

            Assert.AreEqual(Proto.Result.ClaimNotAvailable, result.Result);
            Assert.AreEqual(Proto.Phase.InProgress, record.Phase);
        }

        [Test]
        public void ClaimVictory_WithoutHavingPlayedTheRound_IsNotAvailable()
        {
            var record = NewStartedDuel();

            // Nobody played: a player who never submitted cannot claim a match they did not play.
            var result = DuelOnlineEngine.ClaimVictory(record, Alice, "claim-1", T0.AddDays(1), Window);

            Assert.AreEqual(Proto.Result.ClaimNotAvailable, result.Result);
        }

        [Test]
        public void ClaimVictory_BySomeoneOutsideTheDuel_IsRejected()
        {
            var record = NewStartedDuel();
            Submit(record, Alice, AllWarriors);

            Assert.AreEqual(Proto.Result.NotAParticipant,
                DuelOnlineEngine.ClaimVictory(record, "mallory", "claim-m", T0.AddDays(1), Window).Result);
        }

        [Test]
        public void ClaimVictory_RetryOfTheSameRequest_IsReplayed()
        {
            var record = NewStartedDuel();
            var submittedAt = T0.AddSeconds(20);
            Submit(record, Alice, AllWarriors, submittedAt);
            DuelOnlineEngine.ClaimVictory(record, Alice, "claim-1", submittedAt + Window, Window);

            Assert.AreEqual(Proto.Result.Replayed,
                DuelOnlineEngine.ClaimVictory(record, Alice, "claim-1", submittedAt + Window, Window).Result);
        }

        [Test]
        public void View_TellsThePlayerWhenTheClaimUnlocksByTheServerClock()
        {
            var record = NewStartedDuel();
            var submittedAt = T0.AddSeconds(20);
            Submit(record, Alice, AllWarriors, submittedAt);

            Assert.AreEqual((submittedAt + Window).ToUnixTimeSeconds(), ViewFor(record, Alice).ClaimAvailableAtUnix);
            Assert.AreEqual(0, ViewFor(record, Bob).ClaimAvailableAtUnix, "Only the player who is waiting can claim.");
        }

        // ---- clock and bookkeeping -------------------------------------------------------------

        [Test]
        public void Tick_OnADuelNobodyJoined_ExpiresIt()
        {
            var record = NewWaitingDuel();

            Assert.IsFalse(DuelOnlineEngine.Tick(record, T0.AddSeconds(Proto.WaitingForRivalSeconds)).Changed);

            var late = DuelOnlineEngine.Tick(record, T0.AddSeconds(Proto.WaitingForRivalSeconds + 1));
            Assert.IsTrue(late.Changed);
            Assert.AreEqual(Proto.Phase.Finished, record.Phase);
            Assert.AreEqual(Proto.FinishReason.Expired, record.EndReason);
        }

        [Test]
        public void Tick_OnADuelInProgress_DoesNotDecideAbsences()
        {
            var record = NewStartedDuel();
            Submit(record, Alice, AllWarriors);

            var tick = DuelOnlineEngine.Tick(record, T0.AddDays(30));

            Assert.IsFalse(tick.Changed, "Absence is resolved only by an explicit, validated claim.");
            Assert.AreEqual(Proto.Phase.InProgress, record.Phase);
        }

        [Test]
        public void Views_CarryTheProtocolVersion_EvenForAnOutsider()
        {
            var record = NewStartedDuel();

            Assert.AreEqual(Proto.Version, ViewFor(record, Alice).ProtocolVersion);
            Assert.AreEqual(Proto.Version, ViewFor(record, "mallory", Proto.Result.NotAParticipant).ProtocolVersion);
        }

        [Test]
        public void ToDto_RoundTripsThroughTheSharedDocumentWithoutLosingState()
        {
            var record = NewStartedDuel();
            Submit(record, Alice, AllWarriors);

            var dto = DuelOnlineEngine.ToDto(record);

            Assert.AreEqual(Alice, dto.jugador1.id);
            Assert.AreEqual(Bob, dto.jugador2.id);
            Assert.IsTrue(dto.jugador1.HasSubmitted);
            Assert.IsFalse(dto.jugador2.HasSubmitted);
            Assert.AreEqual(DuelRules.StateWaitingRival, dto.StateFor(Alice));
        }

        [Test]
        public void ProcessedRequestIds_NeverForgetARequestOfALiveDuel()
        {
            var record = NewStartedDuel();

            // Draws are the longest a duel can run: MaxRounds is 30, so at most 29 drawn rounds before
            // the last one ends it. That is 2 + 2 * 29 = 60 accepted requests, under the memory of 64.
            // If the cap were lower than that, a very late retry of an early request would be re-applied.
            for (var i = 0; i < DuelRules.MaxRounds - 1; i++)
            {
                Submit(record, Alice, AllMages);
                Submit(record, Bob, AllMages);
            }

            Assert.AreEqual(Proto.Phase.InProgress, record.Phase);
            Assert.AreEqual(2 + 2 * (DuelRules.MaxRounds - 1), record.ProcessedRequestIds.Count);
            Assert.LessOrEqual(record.ProcessedRequestIds.Count, Proto.MaxRememberedRequestIds);
            CollectionAssert.Contains(record.ProcessedRequestIds, "create-1", "The first request must still be remembered.");
        }
    }
}
