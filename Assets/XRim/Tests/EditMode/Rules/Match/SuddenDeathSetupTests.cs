using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Rules.SuddenDeath;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>
    /// The rules half of sudden death's setup (GDD §14): 1 HP (Decided), the HP each had, the board mode (P5: carry over by default, or
    /// reset), the electric walls (off by default) and the repeats (P6). Designer's choices of 2026-10-11.
    /// </summary>
    public sealed class SuddenDeathSetupTests
    {
        private readonly SuddenDeathSetup _setup = new SuddenDeathSetup();
        private RulesSettings _settings;
        private MatchSetup _match;
        private MatchState _state;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _match = TestData.CreateMatchSetup();
            _state = MatchState.CreateInitial(_match, _settings);
        }

        private FighterState Left => _state.Fighters[Side.Left];
        private FighterState Right => _state.Fighters[Side.Right];

        private void Apply() => _setup.Apply(_state, _settings, _match);

        /// <summary>Both walls standing: left's live, right's a warning.</summary>
        private void WallsUp()
        {
            _state.Walls[Side.Left].Phase = ElectricWallPhase.Active;
            _state.Walls[Side.Left].IsUp = true;
            _state.Walls[Side.Left].PositionUnits = -500f;
            _state.Walls[Side.Right].Phase = ElectricWallPhase.Warning;
            _state.Walls[Side.Right].IsUp = true;
            _state.Walls[Side.Right].PositionUnits = 500f;
        }

        /// <summary>The board a long match leaves: limb damage, a lost leg, a club in hand, a lost weapon and a pending stun.</summary>
        private void BattleScarred()
        {
            Left.SetLimbDamage(BodyPart.LeftArm, 25f);
            Left.MarkSevered(BodyPart.RightArm);
            Left.CurrentWeapon = WeaponIds.SeveredLimb;
            Left.HeldLimb = new SeveredLimbRef(Side.Right, BodyPart.RightLeg);
            Left.LostWeapons.Add(WeaponIds.Rapier);
            Right.MarkSevered(BodyPart.RightLeg);
            Right.CurrentWeapon = WeaponIds.Shield;
            Right.Statuses.Add(new NoBodyMoveStatus(StatusKind.Stunned, 1));
        }

        // --- 1 HP (Decided) and the HP each had ---------------------------------------------------

        [Test]
        public void ADoubleKo_BothGoToOneHp_HavingHadNone()
        {
            Left.Hp = 0f;
            Right.Hp = 0f;

            Apply();

            Assert.That(_state.IsSuddenDeath, Is.True);
            Assert.That(Left.Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            Assert.That(Right.Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            Assert.That(Left.HpBeforeSuddenDeath, Is.EqualTo(0f));
            Assert.That(Right.HpBeforeSuddenDeath, Is.EqualTo(0f));
            Assert.That(_state.SuddenDeathRepeats, Is.EqualTo(0));
        }

        [Test]
        public void TheTurnCap_BothGoToOneHp_AndTheHpTheyHadIsKept()
        {
            Left.Hp = 70f;
            Right.Hp = 12f;

            Apply();

            Assert.That(Left.Hp, Is.EqualTo(1f));
            Assert.That(Right.Hp, Is.EqualTo(1f));
            Assert.That(Left.HpBeforeSuddenDeath, Is.EqualTo(70f));
            Assert.That(Right.HpBeforeSuddenDeath, Is.EqualTo(12f));
        }

        [Test]
        public void TheIdleCounters_RestartForSuddenDeath()
        {
            Left.ConsecutiveIdleTurns = RuleConstants.IdleTurnsBeforeForfeit;
            Right.ConsecutiveIdleTurns = 2;

            Apply();

            Assert.That(Left.ConsecutiveIdleTurns, Is.EqualTo(0));
            Assert.That(Right.ConsecutiveIdleTurns, Is.EqualTo(0));
        }

        // --- The board (P5) -----------------------------------------------------------------------

        [Test]
        public void ByDefault_TheBoardCarriesOver_LimbsClubsLostWeaponsAndStatuses()
        {
            Assert.That(_settings.SuddenDeath.BoardMode, Is.EqualTo(SuddenDeathBoardMode.CarryOver), "the designer's default (2026-10-11)");
            _state.TurnIndex = 29;
            BattleScarred();

            Apply();

            Assert.That(_state.TurnIndex, Is.EqualTo(29), "the machine advances the turn counter, not the setup");
            Assert.That(Left.GetLimbDamage(BodyPart.LeftArm), Is.EqualTo(25f));
            Assert.That(Left.IsSevered(BodyPart.RightArm), Is.True);
            Assert.That(Left.CurrentWeapon, Is.EqualTo(WeaponIds.SeveredLimb));
            Assert.That(Left.HeldLimb, Is.EqualTo(new SeveredLimbRef(Side.Right, BodyPart.RightLeg)), "the club stays in hand");
            Assert.That(Left.LostWeapons, Does.Contain(WeaponIds.Rapier));
            Assert.That(Right.IsSevered(BodyPart.RightLeg), Is.True);
            Assert.That(Right.CurrentWeapon, Is.EqualTo(WeaponIds.Shield));
            Assert.That(Right.Statuses, Has.Count.EqualTo(1), "a pending stun still shapes the first sudden-death turn");
        }

        [Test]
        public void TheResetMode_GivesWholeUndamagedDummies_NoClubNoLostWeaponNoStatus()
        {
            _settings.SuddenDeath.BoardMode = SuddenDeathBoardMode.ResetToMatchStart;
            BattleScarred();

            Apply();

            foreach (BodyPart part in BodyParts.All)
            {
                Assert.That(Left.IsSevered(part) || Right.IsSevered(part), Is.False, part.ToString());
                Assert.That(Left.GetLimbDamage(part), Is.EqualTo(0f), part.ToString());
            }

            Assert.That(Left.HeldLimb, Is.Null);
            Assert.That(Left.LostWeapons, Is.Empty);
            Assert.That(Left.CurrentWeapon, Is.EqualTo(_match.Fighters[Side.Left].Loadout[0]), "a club gives way to the first loadout weapon");
            Assert.That(Right.CurrentWeapon, Is.EqualTo(WeaponIds.Shield), "a loadout weapon stays in hand");
            Assert.That(Right.Statuses, Is.Empty);
            Assert.That(Left.Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
        }

        // --- The electric walls (§14 TBD: off) -----------------------------------------------------

        [Test]
        public void ByDefault_BothWallsGoDown()
        {
            WallsUp();

            Apply();

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                Assert.That(_state.Walls[side].IsUp, Is.False, side.ToString());
                Assert.That(_state.Walls[side].Phase, Is.EqualTo(ElectricWallPhase.Inactive), side.ToString());
            }
        }

        [Test]
        public void WallsLiveInSuddenDeath_CarryOverWithTheBoard()
        {
            _settings.ElectricWall.ActiveInSuddenDeath = true;
            WallsUp();

            Apply();

            Assert.That((_state.Walls[Side.Left].IsUp, _state.Walls[Side.Left].PositionUnits), Is.EqualTo((true, -500f)));
            Assert.That(_state.Walls[Side.Right].Phase, Is.EqualTo(ElectricWallPhase.Warning));
        }

        [Test]
        public void AReset_AlwaysTakesTheWallsDown()
        {
            _settings.ElectricWall.ActiveInSuddenDeath = true;
            _settings.SuddenDeath.BoardMode = SuddenDeathBoardMode.ResetToMatchStart;
            WallsUp();

            Apply();

            Assert.That(_state.Walls[Side.Left].IsUp || _state.Walls[Side.Right].IsUp, Is.False);
        }

        // --- Repeats (P6) ------------------------------------------------------------------------

        [Test]
        public void ARepeat_IsCounted_AndPutsBothBackAtOneHp_TheBoardAsItEnded()
        {
            Apply();
            Left.Hp = 0f; // a tie: both fell at the same instant
            Right.Hp = 0f;
            Left.SetLimbDamage(BodyPart.LeftLeg, 10f);
            Left.ConsecutiveIdleTurns = 1;

            _setup.Repeat(_state);

            Assert.That(_state.SuddenDeathRepeats, Is.EqualTo(1));
            Assert.That(Left.Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            Assert.That(Right.Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            Assert.That(Left.GetLimbDamage(BodyPart.LeftLeg), Is.EqualTo(10f));
            Assert.That(Left.ConsecutiveIdleTurns, Is.EqualTo(1), "idle turns keep counting toward a forfeit");
            Assert.That(_state.IsSuddenDeath, Is.True);
        }

        [Test]
        public void AfterTheSetup_ADoubleKoDoesNotTriggerSuddenDeathAgain()
        {
            // The loop the 1 HP reset prevents: both at 0 HP would be a double KO on every later turn.
            Left.Hp = 0f;
            Right.Hp = 0f;
            Apply();

            EndCheckResult next = new EndConditionEvaluator().Evaluate(
                new ExecutionReport(_state, new PerSide<SimTime?>(null, null), false), _settings, new RulePolicies(), TestData.Seed);

            Assert.That(next.Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn), "nobody has hit yet");
        }
    }
}
