using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using static XRim.Tests.EditMode.Rules.Planning.PlanningTestData;

namespace XRim.Tests.EditMode.Rules.Planning
{
    /// <summary>
    /// The §12 extra options in a planning phase and in the authority's check: the severed-limb club beyond the 3 slots, the body blow
    /// when no arm is left (its reach is its own length from the shoulder), and loadout weapons out of hand.
    /// </summary>
    public sealed class PlanningSessionExtraWeaponTests
    {
        private const float PathTolerance = 0.5f;

        private RulesSettings _settings;
        private PlanningConstraints _constraints;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _constraints = PlanningConstraints.CreateDefault(_settings.Match);
        }

        private PlanningSession Session(WeaponId current) =>
            new PlanningSession(Side.Left, current, Loadout, new FixedWeaponTipSource(Tip), _constraints, DeadlineSeconds, _settings,
                new RulePolicies());

        private void Armless()
        {
            _constraints.CanUseLoadoutWeapons = false;
            _constraints.AddExtraWeapon(WeaponIds.BodyBlow);
        }

        [Test]
        public void TheClub_CanBePicked_WhenItIsOffered()
        {
            _constraints.AddExtraWeapon(WeaponIds.SeveredLimb);
            PlanningSession session = Session(WeaponIds.Rapier);

            Assert.That(session.Apply(new SelectWeaponCommand(WeaponIds.SeveredLimb), 1.0).Accepted, Is.True);
            Assert.That(session.PublicState.Weapon, Is.EqualTo(WeaponIds.SeveredLimb), "§6: the opponent sees the switch");
            Assert.That(session.AvailableWeapons, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield, WeaponIds.SeveredLimb }));
        }

        [Test]
        public void TheClub_IsRefused_WhenNoLimbIsOffered()
        {
            PlanningSession session = Session(WeaponIds.Rapier);

            CommandResult result = session.Apply(new SelectWeaponCommand(WeaponIds.SeveredLimb), 1.0);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.WeaponNotInLoadout));
        }

        [Test]
        public void WithoutArms_ALoadoutWeaponIsRefused()
        {
            Armless();
            PlanningSession session = Session(WeaponIds.BodyBlow);

            Assert.That(session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 1.0).Rejection, Is.EqualTo(CommandRejection.WeaponNotInLoadout));
            Assert.That(session.AvailableWeapons, Is.EqualTo(new[] { WeaponIds.BodyBlow }));
        }

        [Test]
        public void TheBodyBlow_DrawsWithinItsOwnReach()
        {
            Armless();
            PlanningSession session = Session(WeaponIds.BodyBlow);

            Assert.That(session.Apply(new SetPathCommand(Thrust(600f)), 1.0).Accepted, Is.True);

            float reach = _settings.FindWeapon(WeaponIds.BodyBlow).LengthUnits;
            Vec2 end = End(session.CurrentPlan.Path);
            Assert.That(Vec2.Distance(end, _settings.Paths.ShoulderOffsetUnits), Is.EqualTo(reach).Within(PathTolerance), "D4: clamped at its reach");
            Assert.That(session.CurrentPath.WasClampedByReach, Is.True);
        }

        [Test]
        public void ALostWeapon_IsRefused()
        {
            _constraints.ForbidWeapon(WeaponIds.Mace);
            PlanningSession session = Session(WeaponIds.Rapier);

            Assert.That(session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 1.0).Rejection, Is.EqualTo(CommandRejection.WeaponNotInLoadout));
            Assert.That(session.AvailableWeapons, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Shield }));
        }

        [Test]
        public void TheValidator_AcceptsAnOfferedClub_AndRefusesAnOtherwise()
        {
            var validator = new PlanValidator(_settings, new RulePolicies());
            TurnPlan club = new TurnPlan(WeaponIds.SeveredLimb, BodyMove.None, WeaponPath.Empty, default, false);
            var audit = new PlanningAudit(DeadlineSeconds, null, null, null);

            Assert.That(validator.Validate(club, audit, Loadout, _constraints).Rejection, Is.EqualTo(CommandRejection.WeaponNotInLoadout));
            _constraints.AddExtraWeapon(WeaponIds.SeveredLimb);
            Assert.That(validator.Validate(club, audit, Loadout, _constraints).Accepted, Is.True);
        }

        [Test]
        public void TheValidator_RefusesABodyBlowPathPastItsReach()
        {
            Armless();
            var validator = new PlanValidator(_settings, new RulePolicies());
            var tooFar = new WeaponPath(new[] { Tip, new Vec2(250f, 100f) }); // inside its 200 ink, past its 160 reach
            TurnPlan plan = new TurnPlan(WeaponIds.BodyBlow, BodyMove.None, tooFar, default, false);

            CommandResult result = validator.Validate(plan, new PlanningAudit(DeadlineSeconds, null, 1.0, null), Loadout, _constraints);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.InvalidPath));
        }
    }
}
