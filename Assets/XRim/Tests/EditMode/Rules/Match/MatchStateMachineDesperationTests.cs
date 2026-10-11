using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>
    /// The match flow after limbs are lost (GDD §12): the next turn offers the club, an armless dummy plans its body blow, a weapon out
    /// of hand runs as an idle plan, and S4's option gives up a slot for the club. Left (right-handed) holds rapier, mace, shield;
    /// right (left-handed) mace, spear, shield.
    /// </summary>
    public sealed class MatchStateMachineDesperationTests
    {
        private static readonly PerSide<IWeaponTipSource> Tips = new PerSide<IWeaponTipSource>(
            new FixedWeaponTipSource(PlanningTestData.Tip), new FixedWeaponTipSource(PlanningTestData.Tip));

        private RulesSettings _settings;
        private MatchStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _machine = new MatchStateMachine(TestData.CreateMatchSetup(), _settings, new RulePolicies(), new ManualClock(100.0));
            _machine.Start();
        }

        private FighterState Left => _machine.State.Fighters[Side.Left];

        /// <summary>Plays the turn already open, with the simulated state changed the way the test wants, and opens the next.</summary>
        private void PlayTurn(Action<MatchState> resolved = null, Action beforeReady = null)
        {
            _machine.BeginPlanning(Tips);
            beforeReady?.Invoke();
            _machine.Apply(Side.Left, new SetReadyCommand(true));
            _machine.Apply(Side.Right, new SetReadyCommand(true));
            _machine.BeginExecution();
            MatchState state = _machine.State.Clone();
            resolved?.Invoke(state);
            _machine.CompleteExecution(new ExecutionReport(state, new PerSide<SimTime?>(null, null), false));
        }

        [Test]
        public void TheTurnAfterASever_OffersTheClub_ToBothDummies()
        {
            PlayTurn(state => state.Fighters[Side.Right].MarkSevered(BodyPart.RightLeg));

            Assert.That(_machine.Constraints[Side.Left].ExtraWeapons, Is.EqualTo(new[] { WeaponIds.SeveredLimb }), "G1: anyone's limb");
            Assert.That(_machine.Constraints[Side.Right].ExtraWeapons, Is.EqualTo(new[] { WeaponIds.SeveredLimb }));

            _machine.BeginPlanning(Tips);
            Assert.That(_machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.SeveredLimb)).Accepted, Is.True);
        }

        [Test]
        public void AnArmlessDummy_PlansItsBodyBlow()
        {
            PlayTurn(state =>
            {
                state.Fighters[Side.Left].MarkSevered(BodyPart.LeftArm);
                state.Fighters[Side.Left].MarkSevered(BodyPart.RightArm);
            });

            Assert.That(Left.CurrentWeapon, Is.EqualTo(WeaponIds.BodyBlow), "§12: body blows only");
            _machine.BeginPlanning(Tips);
            Assert.That(_machine.Sessions[Side.Left].Weapon, Is.EqualTo(WeaponIds.BodyBlow));
            Assert.That(_machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Rapier)).Rejection,
                Is.EqualTo(CommandRejection.WeaponNotInLoadout));
            Assert.That(_machine.Apply(Side.Left, new SetPathCommand(PlanningTestData.Thrust(250f))).Accepted, Is.True);
        }

        [Test]
        public void TheClubWithTheExtraOptionOff_TakesTheSlotOfTheWeaponHeld()
        {
            _settings.Loadout.SeveredLimbIsExtraOption = false;
            PlayTurn(state => state.Fighters[Side.Right].MarkSevered(BodyPart.RightLeg));

            PlayTurn(beforeReady: () => _machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.SeveredLimb)));

            Assert.That(Left.LostWeapons, Is.EquivalentTo(new[] { WeaponIds.Rapier }), "S4: the club took the rapier's slot");
            Assert.That(_machine.Constraints[Side.Left].IsWeaponAvailable(WeaponIds.Rapier, _machine.Setup.Fighters[Side.Left].Loadout), Is.False);
        }

        [Test]
        public void TheClubByDefault_CostsNoSlot()
        {
            PlayTurn(state => state.Fighters[Side.Right].MarkSevered(BodyPart.RightLeg));

            PlayTurn(beforeReady: () => _machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.SeveredLimb)));

            Assert.That(Left.CurrentWeapon, Is.EqualTo(WeaponIds.SeveredLimb));
            Assert.That(Left.LostWeapons, Is.Empty);
        }
    }
}
