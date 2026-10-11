using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Arena
{
    /// <summary>
    /// The electric wall sequence (GDD §13, Decided), every row of its table, and the designer's choices around it (2026-10-11): the
    /// geometry (P1), flat damage that can knock out (P2), any other move ending the sequence (P3), the §13 TBD flags (growth, knocked
    /// into) and the wall in sudden death (§14, off). Left faces +x, so its wall stands at lower x; right's at higher x.
    /// </summary>
    public sealed class ElectricWallRulesTests
    {
        private const float LeftPelvisX = -350f;
        private const float RightPelvisX = 350f;
        private static readonly SimTime At = SimTime.FromSeconds(0.25);
        private static readonly ArenaEdges SolidEdges = new ArenaEdges(-1000f, 1000f, isSolid: true);

        private readonly ElectricWallRules _rules = new ElectricWallRules();
        private RulesSettings _settings;
        private MatchState _state;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _state = MatchState.CreateInitial(TestData.CreateMatchSetup(), _settings);
        }

        private ElectricWallSettings Wall => _settings.ElectricWall;

        private ElectricWallState WallOf(Side side) => _state.Walls[side];

        private void Lock(Side side, BodyMove move, bool isSuddenDeath = false) => _rules.OnPlansLocked(WallOf(side), move, isSuddenDeath, Wall);

        private ElectricWallEvent Raise(Side side, float pelvisX) => _rules.RaiseWarning(At, side, WallOf(side), pelvisX, SolidEdges, Wall);

        private ElectricWallZap Touch(Side side, bool retreatZapDone = false) => _rules.OnTouched(At, side, _state, retreatZapDone, SolidEdges, Wall);

        /// <summary>Turn N as the simulation plays it: the backward swipe locks, then the warning springs up when the step back ends.</summary>
        private void RetreatOnce(Side side, float pelvisX)
        {
            Lock(side, BodyMove.StepBack);
            Raise(side, pelvisX);
        }

        /// <summary>One more retreat into the standing wall: the lock makes it active and the touch zaps.</summary>
        private ElectricWallZap RetreatIntoTheWall(Side side)
        {
            Lock(side, BodyMove.StepBack);
            return Touch(side);
        }

        private float Hp(Side side) => _state.Fighters[side].Hp;

        // --- The sequence, row by row (§13, Decided) -------------------------------------------------

        [Test]
        public void TurnN_ABackwardSwipe_RaisesTheWarning_WhenTheStepBackEnds()
        {
            Lock(Side.Left, BodyMove.StepBack);

            Assert.That(WallOf(Side.Left).Phase, Is.EqualTo(ElectricWallPhase.Warning));
            Assert.That(WallOf(Side.Left).IsUp, Is.False, "it springs up during the turn, when the step back ends");
            Assert.That(WallOf(Side.Left).ConsecutiveRetreats, Is.EqualTo(1));

            ElectricWallEvent raised = Raise(Side.Left, LeftPelvisX);

            Assert.That(WallOf(Side.Left).IsUp, Is.True);
            Assert.That(raised.Phase, Is.EqualTo(ElectricWallPhase.Warning));
            Assert.That(raised.HpDamage, Is.EqualTo(0f));
            Assert.That(raised.Time, Is.EqualTo(At));
            Assert.That(raised.PositionUnits, Is.EqualTo(WallOf(Side.Left).PositionUnits));
        }

        [Test]
        public void TurnN_TheWarning_DoesNoDamage_EvenWhenTouched()
        {
            RetreatOnce(Side.Left, LeftPelvisX);

            Assert.That(Touch(Side.Left), Is.Null);
            Assert.That(Hp(Side.Left), Is.EqualTo(_settings.Damage.MaxHp));
        }

        [TestCase(BodyMove.None)]
        [TestCase(BodyMove.Crouch)]
        [TestCase(BodyMove.Lunge)]
        [TestCase(BodyMove.Jump)]
        public void TurnN1_AnyOtherMove_IncludingNone_RemovesTheWallCompletely(BodyMove move)
        {
            RetreatOnce(Side.Left, LeftPelvisX);

            Lock(Side.Left, move);

            AssertGone(WallOf(Side.Left));
        }

        [Test]
        public void TurnN1_AnotherBackwardSwipe_HitsTheWall_DamageBounceAndTheWallMovesForward()
        {
            RetreatOnce(Side.Left, LeftPelvisX);
            float before = WallOf(Side.Left).PositionUnits;

            ElectricWallZap zap = RetreatIntoTheWall(Side.Left);

            Assert.That(WallOf(Side.Left).Phase, Is.EqualTo(ElectricWallPhase.Active));
            Assert.That(WallOf(Side.Left).ConsecutiveRetreats, Is.EqualTo(2));
            Assert.That(zap, Is.Not.Null);
            Assert.That(zap.HpDamage, Is.EqualTo(Wall.Damage));
            Assert.That(Hp(Side.Left), Is.EqualTo(_settings.Damage.MaxHp - Wall.Damage));
            Assert.That(zap.BounceDistanceUnits, Is.EqualTo(Wall.BounceDistanceUnits));
            Assert.That(zap.WallPositionUnits, Is.EqualTo(before + Wall.AdvanceDistanceUnits), "forward is toward the opponent: +x for left");
            Assert.That(WallOf(Side.Left).PositionUnits, Is.EqualTo(zap.WallPositionUnits));
            Assert.That(zap.Died, Is.False);

            var wallEvent = (ElectricWallEvent)zap.Events.Single();
            Assert.That(wallEvent.Phase, Is.EqualTo(ElectricWallPhase.Active));
            Assert.That(wallEvent.HpDamage, Is.EqualTo(Wall.Damage));
            Assert.That(wallEvent.PositionUnits, Is.EqualTo(zap.WallPositionUnits));
            Assert.That(wallEvent.IsKnockedInto, Is.False);
        }

        [Test]
        public void TurnN2AndLater_BackwardAgain_DamageAndBounceAgain_AndTheWallAdvancesAgain()
        {
            RetreatOnce(Side.Right, RightPelvisX);
            float start = WallOf(Side.Right).PositionUnits;

            for (int zap = 1; zap <= 3; zap++)
            {
                ElectricWallZap result = RetreatIntoTheWall(Side.Right);

                Assert.That(result.HpDamage, Is.EqualTo(Wall.Damage), $"zap {zap}");
                Assert.That(result.BounceDistanceUnits, Is.EqualTo(Wall.BounceDistanceUnits), $"zap {zap}");
                Assert.That(result.WallPositionUnits, Is.EqualTo(start - zap * Wall.AdvanceDistanceUnits).Within(1e-3f),
                    $"zap {zap}: right's forward is -x");
                Assert.That(WallOf(Side.Right).ConsecutiveRetreats, Is.EqualTo(zap + 1));
            }

            Assert.That(Hp(Side.Right), Is.EqualTo(_settings.Damage.MaxHp - 3f * Wall.Damage));
        }

        [Test]
        public void P3_AfterAZap_AnyOtherMove_RemovesTheWall_AndTheSequenceStartsOver()
        {
            RetreatOnce(Side.Left, LeftPelvisX);
            RetreatIntoTheWall(Side.Left);

            Lock(Side.Left, BodyMove.None);
            AssertGone(WallOf(Side.Left));

            Lock(Side.Left, BodyMove.StepBack);
            Assert.That(WallOf(Side.Left).Phase, Is.EqualTo(ElectricWallPhase.Warning), "a fresh warning, not a zap");
            Assert.That(WallOf(Side.Left).ConsecutiveRetreats, Is.EqualTo(1));
        }

        [Test]
        public void OneTouchZapsOncePerRetreat_ALaterTouchNeedsTheKnockedIntoFlag()
        {
            RetreatOnce(Side.Left, LeftPelvisX);
            RetreatIntoTheWall(Side.Left);

            Assert.That(Wall.DamageWhenKnockedInto, Is.False, "the designer's default (2026-10-11)");
            Assert.That(Touch(Side.Left, retreatZapDone: true), Is.Null);
            Assert.That(Hp(Side.Left), Is.EqualTo(_settings.Damage.MaxHp - Wall.Damage));
        }

        // --- Only the input counts (§13, Decided) ----------------------------------------------------

        [Test]
        public void OnlyABackwardSwipe_StartsTheSequence()
        {
            foreach (BodyMove move in (BodyMove[])Enum.GetValues(typeof(BodyMove)))
            {
                var wall = new ElectricWallState();

                _rules.OnPlansLocked(wall, move, false, Wall);

                Assert.That(wall.Phase, Is.EqualTo(move == BodyMove.StepBack ? ElectricWallPhase.Warning : ElectricWallPhase.Inactive), move.ToString());
            }
        }

        [Test]
        public void StandingStill_OrBeingKnockedBack_NeverAdvancesTheSequence()
        {
            // The lock only ever sees the planned move: a dummy that planned no move stays clear whatever the turn did to its body
            // (knocked back by a hit, pushed back by a block, standing behind its shield).
            RetreatOnce(Side.Left, LeftPelvisX);

            Lock(Side.Left, BodyMove.None);
            Lock(Side.Left, BodyMove.None);

            AssertGone(WallOf(Side.Left));
        }

        [Test]
        public void AWarningThatNeverStood_IsRaisedAgain_NotTurnedIntoAZap()
        {
            Lock(Side.Left, BodyMove.StepBack); // the turn ended without the warning springing up (a scripted turn)

            Lock(Side.Left, BodyMove.StepBack);

            Assert.That(WallOf(Side.Left).Phase, Is.EqualTo(ElectricWallPhase.Warning));
            Assert.That(WallOf(Side.Left).ConsecutiveRetreats, Is.EqualTo(1));
            Assert.That(Touch(Side.Left), Is.Null);
        }

        [Test]
        public void OnlyAWarningRaisedThisTurn_CanSpringUp()
        {
            Assert.Throws<InvalidOperationException>(() => Raise(Side.Left, LeftPelvisX), "no backward swipe");

            RetreatOnce(Side.Left, LeftPelvisX);
            Assert.Throws<InvalidOperationException>(() => Raise(Side.Left, LeftPelvisX), "already up");
        }

        // --- Both walls (§13) ------------------------------------------------------------------------

        [Test]
        public void EachPlayerHasTheirOwnWall_BothCanBeActiveAtOnce()
        {
            RetreatOnce(Side.Left, LeftPelvisX);
            RetreatOnce(Side.Right, RightPelvisX);

            ElectricWallZap left = RetreatIntoTheWall(Side.Left);
            ElectricWallZap right = RetreatIntoTheWall(Side.Right);

            Assert.That(WallOf(Side.Left).Phase, Is.EqualTo(ElectricWallPhase.Active));
            Assert.That(WallOf(Side.Right).Phase, Is.EqualTo(ElectricWallPhase.Active));
            Assert.That(left.Side, Is.EqualTo(Side.Left));
            Assert.That(right.Side, Is.EqualTo(Side.Right));
            Assert.That(Hp(Side.Left), Is.EqualTo(Hp(Side.Right)));
            Assert.That(WallOf(Side.Left).PositionUnits, Is.LessThan(LeftPelvisX), "behind left");
            Assert.That(WallOf(Side.Right).PositionUnits, Is.GreaterThan(RightPelvisX), "behind right");
        }

        [Test]
        public void OneSideRetreating_LeavesTheOtherWallAlone()
        {
            RetreatOnce(Side.Left, LeftPelvisX);
            Lock(Side.Right, BodyMove.Lunge);

            RetreatIntoTheWall(Side.Left);

            AssertGone(WallOf(Side.Right));
            Assert.That(Hp(Side.Right), Is.EqualTo(_settings.Damage.MaxHp));
        }

        // --- Geometry (P1) ---------------------------------------------------------------------------

        [Test]
        public void TheWarning_StandsBehindThePelvis_AwayFromTheOpponent()
        {
            RetreatOnce(Side.Left, LeftPelvisX);
            RetreatOnce(Side.Right, RightPelvisX);

            Assert.That(WallOf(Side.Left).PositionUnits, Is.EqualTo(LeftPelvisX - Wall.SpawnOffsetBehindUnits));
            Assert.That(WallOf(Side.Right).PositionUnits, Is.EqualTo(RightPelvisX + Wall.SpawnOffsetBehindUnits));
        }

        [Test]
        public void TheWarning_NeverStandsBeyondASolidEdge_ItStandsOnTheEdgeLine()
        {
            RetreatOnce(Side.Left, SolidEdges.LeftXUnits + 20f);
            RetreatOnce(Side.Right, SolidEdges.RightXUnits - 5f);

            Assert.That(WallOf(Side.Left).PositionUnits, Is.EqualTo(SolidEdges.LeftXUnits));
            Assert.That(WallOf(Side.Right).PositionUnits, Is.EqualTo(SolidEdges.RightXUnits));
        }

        [Test]
        public void OpenEdges_DoNotHoldTheWarningBack()
        {
            var open = new ArenaEdges(-1000f, 1000f, isSolid: false);

            Assert.That(ElectricWallRules.WarningPositionUnits(Side.Left, -990f, open, Wall), Is.EqualTo(-990f - Wall.SpawnOffsetBehindUnits));
        }

        // --- Damage (P2, §13 TBD growth) ---------------------------------------------------------------

        [Test]
        public void DamageIsFlat_ByDefault()
        {
            Assert.That(Wall.DamageGrowthPerConsecutiveRetreat, Is.EqualTo(0f), "the designer's default (2026-10-11)");
            RetreatOnce(Side.Left, LeftPelvisX);

            var damage = new List<float>();
            for (int i = 0; i < 3; i++) damage.Add(RetreatIntoTheWall(Side.Left).HpDamage);

            Assert.That(damage, Is.EqualTo(new[] { Wall.Damage, Wall.Damage, Wall.Damage }));
        }

        [Test]
        public void TheGrowthFlag_AddsDamageForEachZapInARow()
        {
            Wall.DamageGrowthPerConsecutiveRetreat = 5f;
            RetreatOnce(Side.Left, LeftPelvisX);

            var damage = new List<float>();
            for (int i = 0; i < 3; i++) damage.Add(RetreatIntoTheWall(Side.Left).HpDamage);

            Assert.That(damage, Is.EqualTo(new[] { Wall.Damage, Wall.Damage + 5f, Wall.Damage + 10f }));
        }

        [Test]
        public void TheGrowth_StartsAgain_AfterTheSequenceEnds()
        {
            Wall.DamageGrowthPerConsecutiveRetreat = 5f;
            RetreatOnce(Side.Left, LeftPelvisX);
            RetreatIntoTheWall(Side.Left);
            RetreatIntoTheWall(Side.Left);
            Lock(Side.Left, BodyMove.Crouch);

            RetreatOnce(Side.Left, LeftPelvisX);

            Assert.That(RetreatIntoTheWall(Side.Left).HpDamage, Is.EqualTo(Wall.Damage));
        }

        [Test]
        public void TheKnockedIntoFlag_ALaterTouch_DealsDamageOnly()
        {
            Wall.DamageWhenKnockedInto = true;
            RetreatOnce(Side.Left, LeftPelvisX);
            ElectricWallZap first = RetreatIntoTheWall(Side.Left);

            ElectricWallZap knocked = Touch(Side.Left, retreatZapDone: true);

            Assert.That(knocked, Is.Not.Null);
            Assert.That(knocked.IsKnockedInto, Is.True);
            Assert.That(knocked.HpDamage, Is.EqualTo(Wall.Damage));
            Assert.That(knocked.BounceDistanceUnits, Is.EqualTo(0f), "no bounce");
            Assert.That(knocked.WallPositionUnits, Is.EqualTo(first.WallPositionUnits), "no advance");
            Assert.That(((ElectricWallEvent)knocked.Events[0]).IsKnockedInto, Is.True);
            Assert.That(Hp(Side.Left), Is.EqualTo(_settings.Damage.MaxHp - 2f * Wall.Damage));
        }

        [Test]
        public void TheKnockedIntoFlag_NeverMakesTheWarningHurt()
        {
            Wall.DamageWhenKnockedInto = true;
            RetreatOnce(Side.Left, LeftPelvisX);

            Assert.That(Touch(Side.Left, retreatZapDone: true), Is.Null);
            Assert.That(Touch(Side.Left), Is.Null);
        }

        [Test]
        public void AZap_CanKnockOut_AndSaysSo()
        {
            RetreatOnce(Side.Left, LeftPelvisX);
            _state.Fighters[Side.Left].Hp = 4f;

            ElectricWallZap zap = RetreatIntoTheWall(Side.Left);

            Assert.That(zap.Died, Is.True);
            Assert.That(zap.HpDamage, Is.EqualTo(4f), "the HP it actually took");
            Assert.That(Hp(Side.Left), Is.EqualTo(0f));
            var died = zap.Events.OfType<FighterDiedEvent>().Single();
            Assert.That(died.Side, Is.EqualTo(Side.Left));
            Assert.That(died.Time, Is.EqualTo(At));
        }

        [Test]
        public void ADeadDummy_IsNotZapped()
        {
            RetreatOnce(Side.Left, LeftPelvisX);
            Lock(Side.Left, BodyMove.StepBack);
            _state.Fighters[Side.Left].Hp = 0f;

            Assert.That(Touch(Side.Left), Is.Null);
        }

        // --- Sudden death (§14, TBD: off) ------------------------------------------------------------

        [Test]
        public void InSuddenDeath_WallsAreOffByDefault_ABackwardSwipeRaisesNothing()
        {
            Assert.That(Wall.ActiveInSuddenDeath, Is.False, "the designer's default (2026-10-11)");
            RetreatOnce(Side.Left, LeftPelvisX);

            Lock(Side.Left, BodyMove.StepBack, isSuddenDeath: true);

            AssertGone(WallOf(Side.Left));
        }

        [Test]
        public void InSuddenDeath_WallsCanBeSwitchedOn_AndThenAZapAtOneHpKills()
        {
            Wall.ActiveInSuddenDeath = true;
            RetreatOnce(Side.Left, LeftPelvisX);
            _state.Fighters[Side.Left].Hp = RuleConstants.SuddenDeathHp;

            Lock(Side.Left, BodyMove.StepBack, isSuddenDeath: true);
            ElectricWallZap zap = Touch(Side.Left);

            Assert.That(zap.Died, Is.True);
        }

        // --- Settings -------------------------------------------------------------------------------

        [Test]
        public void TheStartingValues_AreThePlaceholdersAndTheDesignersDefaults()
        {
            var wall = new ElectricWallSettings();

            Assert.That(wall.Damage, Is.EqualTo(10f));
            Assert.That(wall.BounceDistanceUnits, Is.EqualTo(80f));
            Assert.That(wall.AdvanceDistanceUnits, Is.EqualTo(50f));
            Assert.That(wall.SpawnOffsetBehindUnits, Is.EqualTo(60f));
            Assert.That(wall.DamageGrowthPerConsecutiveRetreat, Is.EqualTo(0f));
            Assert.That(wall.DamageWhenKnockedInto, Is.False);
            Assert.That(wall.ActiveInSuddenDeath, Is.False);
        }

        [Test]
        public void NegativeWallNumbers_AreReported()
        {
            Wall.Damage = -1f;
            Wall.AdvanceDistanceUnits = -5f;
            var issues = new List<string>();

            _settings.Validate(issues);

            Assert.That(issues, Has.Some.StartsWith("Electric wall: the damage"));
            Assert.That(issues, Has.Some.StartsWith("Electric wall: the bounce, advance and spawn"));
        }

        private static void AssertGone(ElectricWallState wall)
        {
            Assert.That(wall.Phase, Is.EqualTo(ElectricWallPhase.Inactive));
            Assert.That(wall.IsUp, Is.False);
            Assert.That(wall.ConsecutiveRetreats, Is.EqualTo(0));
        }
    }
}
