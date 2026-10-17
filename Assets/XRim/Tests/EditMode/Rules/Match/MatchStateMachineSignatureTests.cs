using System;
using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>
    /// Signature moves in the match flow (GDD §8, TBD; designer 2026-10-15: once per match by default, hidden until execution, spent when
    /// the plan locks). The catalogue is <see cref="SignatureTestData"/>; Left brings the rapier, Right the mace.
    /// </summary>
    public sealed class MatchStateMachineSignatureTests
    {
        private static readonly PerSide<IWeaponTipSource> Tips = new PerSide<IWeaponTipSource>(
            new FixedWeaponTipSource(PlanningTestData.Tip), new FixedWeaponTipSource(PlanningTestData.Tip));

        private RulesSettings _settings;
        private RulePolicies _policies;
        private MatchStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _settings = SignatureTestData.CreateSettings();
            _policies = new RulePolicies();
        }

        private void Start(IReadOnlyList<SignatureMoveId> leftBrings = null, IReadOnlyList<SignatureMoveId> rightBrings = null)
        {
            var setup = new MatchSetup(
                new PerSide<FighterSetup>(
                    new FighterSetup(Handedness.Right, new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }, leftBrings),
                    new FighterSetup(Handedness.Left, new[] { WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield }, rightBrings)),
                TestData.Seed);
            _machine = new MatchStateMachine(setup, _settings, _policies, new ManualClock(100.0));
            _machine.Start();
        }

        private FighterState Left => _machine.State.Fighters[Side.Left];
        private FighterState Right => _machine.State.Fighters[Side.Right];

        private CommandResult Use(Side side, SignatureMoveId id) => _machine.Apply(side, new UseSignatureCommand(id));

        private void ReadyBoth()
        {
            _machine.Apply(Side.Left, new SetReadyCommand(true));
            _machine.Apply(Side.Right, new SetReadyCommand(true));
        }

        /// <summary>Executes the locked turn with a resolved state the test shapes.</summary>
        private EndCheckResult Resolve(Action<MatchState> resolved = null)
        {
            _machine.BeginExecution();
            MatchState state = _machine.State.Clone();
            resolved?.Invoke(state);
            return _machine.CompleteExecution(new ExecutionReport(state, new PerSide<SimTime?>(null, null), false, null));
        }

        private void EnterSuddenDeath()
        {
            _machine.BeginPlanning(Tips);
            _machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Crouch));
            _machine.Apply(Side.Right, new SetBodyMoveCommand(BodyMove.Crouch));
            ReadyBoth();
            Resolve(state =>
            {
                state.Fighters[Side.Left].Hp = 0f;
                state.Fighters[Side.Right].Hp = 0f;
            });
        }

        // --- Spent when the plan locks ------------------------------------------------------------

        [Test]
        public void AMove_IsSpentWhenThePlanLocks_NotWhenItIsChosen()
        {
            Start();
            _machine.BeginPlanning(Tips);

            Assert.That(Use(Side.Left, SignatureTestData.Jab).Accepted, Is.True);
            Assert.That(Left.SignatureUses, Is.EqualTo(0), "choosing costs nothing");

            ReadyBoth();

            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Locked));
            Assert.That(_machine.LockedPlans[Side.Left].Signature, Is.EqualTo(SignatureTestData.Jab));
            Assert.That(Left.SignatureUses, Is.EqualTo(1));
            Assert.That(Right.SignatureUses, Is.EqualTo(0));
        }

        [Test]
        public void ChoosingAndThenCancelling_CostsNothing()
        {
            Start();
            _machine.BeginPlanning(Tips);
            Use(Side.Left, SignatureTestData.Jab);
            _machine.Apply(Side.Left, new ClearPathCommand());
            _machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Crouch));

            ReadyBoth();

            Assert.That(_machine.LockedPlans[Side.Left].Signature.IsEmpty, Is.True);
            Assert.That(Left.SignatureUses, Is.EqualTo(0));
        }

        [Test]
        public void OncePerMatch_TheNextTurnHasNoCharge()
        {
            Start();
            _machine.BeginPlanning(Tips);
            Use(Side.Left, SignatureTestData.Jab);
            ReadyBoth();
            Resolve();

            _machine.BeginPlanning(Tips);

            Assert.That(_machine.Sessions[Side.Left].SignatureCharge.IsAvailable, Is.False);
            Assert.That(_machine.Sessions[Side.Right].SignatureCharge.IsAvailable, Is.True, "the other dummy has not used its move");
            Assert.That(Use(Side.Left, SignatureTestData.Jab).Rejection, Is.EqualTo(CommandRejection.SignatureNotCharged));
        }

        [Test]
        public void MovesPerMatch_SetsHowManyTurnsInAMatchAMoveCanBeUsed()
        {
            _settings.Signature.MovesPerMatch = 2;
            Start();

            for (int turn = 0; turn < 2; turn++)
            {
                _machine.BeginPlanning(Tips);
                Assert.That(Use(Side.Left, SignatureTestData.Jab).Accepted, Is.True, $"turn {turn}");
                ReadyBoth();
                Resolve();
            }

            _machine.BeginPlanning(Tips);
            Assert.That(Use(Side.Left, SignatureTestData.Jab).Rejection, Is.EqualTo(CommandRejection.SignatureNotCharged));
        }

        // --- Hidden until execution (§8) ------------------------------------------------------------

        [Test]
        public void TheOpponentSeesNothingOfTheChosenMove_DuringPlanning()
        {
            Start();
            _machine.BeginPlanning(Tips);
            PublicPlanningState before = _machine.PublicStateOf(Side.Left);

            Use(Side.Left, SignatureTestData.Jab);

            Assert.That(_machine.PublicStateOf(Side.Left), Is.EqualTo(before));
            Assert.That(before.SignatureChargedVisible, Is.False, "the charge is hidden by default too");
        }

        [Test]
        public void ACharge_IsVisibleOnlyWhileChargedAndOnlyWhenTheFlagAllowsIt()
        {
            _settings.Signature.OpponentSeesCharged = true;
            Start();
            _machine.BeginPlanning(Tips);
            Assert.That(_machine.PublicStateOf(Side.Left).SignatureChargedVisible, Is.True);
            Use(Side.Left, SignatureTestData.Jab);
            ReadyBoth();
            Resolve();

            _machine.BeginPlanning(Tips);

            Assert.That(_machine.PublicStateOf(Side.Left).SignatureChargedVisible, Is.False, "spent: nothing charged");
            Assert.That(_machine.PublicStateOf(Side.Right).SignatureChargedVisible, Is.True);
        }

        // --- The forfeit counter (D10) --------------------------------------------------------------

        [Test]
        public void AMoveAlone_CountsAsAMove_SoTheOtherDummyForfeitsFirst()
        {
            _settings.Signature.MovesPerMatch = 5;
            Start();

            for (int turn = 0; turn < 3; turn++)
            {
                _machine.BeginPlanning(Tips);
                Use(Side.Left, SignatureTestData.Jab);
                ReadyBoth();
                EndCheckResult result = Resolve();

                Assert.That(Left.ConsecutiveIdleTurns, Is.EqualTo(0), $"turn {turn}: a signature move is a move");
                if (turn < 2) Assert.That(result.Resolution, Is.EqualTo(TurnResolution.NextTurn));
                else Assert.That(result.Resolution, Is.EqualTo(TurnResolution.MatchOver));
            }

            Assert.That(_machine.Outcome.Winner, Is.EqualTo(Side.Left));
            Assert.That(_machine.Outcome.Reason, Is.EqualTo(MatchEndReason.Forfeit));
        }

        // --- The Kinetic Energy Bar through the match -----------------------------------------------

        [Test]
        public void WithTheBar_AFullBarIsNeeded_AndAUseEmptiesIt()
        {
            _settings.Signature.ChargeMode = SignatureChargeMode.KineticEnergyBar;
            Start();
            _machine.BeginPlanning(Tips);
            Assert.That(Use(Side.Left, SignatureTestData.Jab).Rejection, Is.EqualTo(CommandRejection.SignatureNotCharged), "an empty bar");
            _machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Crouch));
            ReadyBoth();
            Resolve(state => state.Fighters[Side.Left].SignatureCharge = 1f);

            _machine.BeginPlanning(Tips);
            Assert.That(_machine.Sessions[Side.Left].SignatureCharge.ChargeFraction, Is.EqualTo(1f));
            Assert.That(Use(Side.Left, SignatureTestData.Jab).Accepted, Is.True);
            ReadyBoth();

            Assert.That(Left.SignatureCharge, Is.EqualTo(0f), "a use spends the whole bar");
            Assert.That(Left.SignatureUses, Is.EqualTo(1));
        }

        // --- What each dummy brings -------------------------------------------------------------------

        [Test]
        public void ADummyCanOnlyUseTheMovesItBrought()
        {
            Start(leftBrings: new[] { SignatureTestData.Sweep });
            _machine.BeginPlanning(Tips);

            Assert.That(Use(Side.Left, SignatureTestData.Jab).Rejection, Is.EqualTo(CommandRejection.SignatureUnavailable));
            Assert.That(Use(Side.Left, SignatureTestData.Sweep).Accepted, Is.True);
            Assert.That(Use(Side.Right, SignatureTestData.Jab).Accepted, Is.True, "no list: the whole catalogue");
        }

        // --- Sudden death (§14) ------------------------------------------------------------------------

        [Test]
        public void InSuddenDeath_MovesStayByDefault_WithTheChargeAsItWas()
        {
            Start();
            EnterSuddenDeath();
            _machine.BeginPlanning(Tips);

            Assert.That(_machine.State.IsSuddenDeath, Is.True);
            Assert.That(Use(Side.Left, SignatureTestData.Jab).Accepted, Is.True);
        }

        [Test]
        public void InSuddenDeath_TheFlagCanTurnMovesOff()
        {
            _settings.SuddenDeath.AllowSignatureMoves = false;
            Start();
            _machine.BeginPlanning(Tips);
            Assert.That(Use(Side.Left, SignatureTestData.Jab).Accepted, Is.True, "an ordinary turn");
            _machine.Apply(Side.Left, new ClearPathCommand());
            _machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Crouch));
            _machine.Apply(Side.Right, new SetBodyMoveCommand(BodyMove.Crouch));
            ReadyBoth();
            Resolve(state =>
            {
                state.Fighters[Side.Left].Hp = 0f;
                state.Fighters[Side.Right].Hp = 0f;
            });

            _machine.BeginPlanning(Tips);

            Assert.That(Use(Side.Left, SignatureTestData.Jab).Rejection, Is.EqualTo(CommandRejection.SignatureUnavailable));
            Assert.That(Use(Side.Right, SignatureTestData.Sweep).Rejection, Is.EqualTo(CommandRejection.SignatureUnavailable));
        }

        [Test]
        public void ASpentMove_StaysSpentIntoSuddenDeath_AndABarCarriesOver()
        {
            _settings.Signature.ChargeMode = SignatureChargeMode.KineticEnergyBar;
            Start();
            _machine.BeginPlanning(Tips);
            _machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Crouch));
            _machine.Apply(Side.Right, new SetBodyMoveCommand(BodyMove.Crouch));
            ReadyBoth();
            Resolve(state =>
            {
                state.Fighters[Side.Left].SignatureCharge = 0.5f;
                state.Fighters[Side.Left].SignatureUses = 2;
                state.Fighters[Side.Left].Hp = 0f;
                state.Fighters[Side.Right].Hp = 0f;
            });

            Assert.That(_machine.State.IsSuddenDeath, Is.True);
            Assert.That(Left.SignatureCharge, Is.EqualTo(0.5f));
            Assert.That(Left.SignatureUses, Is.EqualTo(2));
        }
    }
}
