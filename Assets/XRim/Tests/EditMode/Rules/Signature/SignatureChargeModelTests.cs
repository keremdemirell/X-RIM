using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.Signature;

namespace XRim.Tests.EditMode.Rules.Signature
{
    /// <summary>
    /// The two charge options of GDD §8 (TBD; designer 2026-10-15: once per match by default, the bar built and switchable), as stateless
    /// strategies over a <see cref="FighterState"/>. Starting values (placeholders): 1 move per match; the bar gains 0.5% per HP dealt and
    /// 1% per HP taken.
    /// </summary>
    public sealed class SignatureChargeModelTests
    {
        private const float Tolerance = 1e-5f;

        private SignatureSettings _settings;
        private FighterState _fighter;

        [SetUp]
        public void SetUp()
        {
            _settings = new SignatureSettings();
            _fighter = new FighterState(Handedness.Right, 100f, WeaponIds.Rapier);
        }

        // --- Starting values ---------------------------------------------------------------------

        [Test]
        public void TheStartingValues_AreTheDesignersDefaults()
        {
            var settings = new SignatureSettings();

            Assert.That(settings.ChargeMode, Is.EqualTo(SignatureChargeMode.OncePerMatch));
            Assert.That(settings.MovesPerMatch, Is.EqualTo(1));
            Assert.That(settings.OpponentSeesCharged, Is.False);
            Assert.That(settings.BarChargePerHpDamageDealt, Is.EqualTo(0.005f));
            Assert.That(settings.BarChargePerHpDamageTaken, Is.EqualTo(0.01f));
            Assert.That(settings.BarChargePerHpDamageTaken, Is.GreaterThan(settings.BarChargePerHpDamageDealt),
                "being hurt charges faster: comeback swings");
        }

        // --- Option 1: once per match --------------------------------------------------------------

        [Test]
        public void OncePerMatch_IsAvailableFromTheFirstTurn_AndFullyCharged()
        {
            var model = new OncePerMatchChargeModel();

            Assert.That(model.IsAvailable(_fighter, _settings), Is.True);
            Assert.That(model.ChargeFraction(_fighter, _settings), Is.EqualTo(1f));
        }

        [Test]
        public void OncePerMatch_UsingTheMove_SpendsIt()
        {
            var model = new OncePerMatchChargeModel();

            model.OnUsed(_fighter, _settings);

            Assert.That(model.IsAvailable(_fighter, _settings), Is.False);
            Assert.That(model.ChargeFraction(_fighter, _settings), Is.EqualTo(0f));
            Assert.That(_fighter.SignatureUses, Is.EqualTo(1));
        }

        [Test]
        public void OncePerMatch_MovesPerMatchSetsHowManyUses()
        {
            var model = new OncePerMatchChargeModel();
            _settings.MovesPerMatch = 2;

            model.OnUsed(_fighter, _settings);
            Assert.That(model.IsAvailable(_fighter, _settings), Is.True, "one of two used");

            model.OnUsed(_fighter, _settings);
            Assert.That(model.IsAvailable(_fighter, _settings), Is.False);
        }

        [Test]
        public void OncePerMatch_ZeroMovesPerMatch_TurnsSignatureMovesOff()
        {
            var model = new OncePerMatchChargeModel();
            _settings.MovesPerMatch = 0;

            Assert.That(model.IsAvailable(_fighter, _settings), Is.False);
        }

        [Test]
        public void OncePerMatch_DamageDealtAndTakenChangeNothing()
        {
            var model = new OncePerMatchChargeModel();
            model.OnUsed(_fighter, _settings);

            model.OnDamageDealt(_fighter, 90f, _settings);
            model.OnDamageTaken(_fighter, 90f, _settings);

            Assert.That(model.IsAvailable(_fighter, _settings), Is.False, "a spent move is not refilled by damage");
            Assert.That(_fighter.SignatureCharge, Is.EqualTo(0f));
        }

        // --- Option 2: the Kinetic Energy Bar -------------------------------------------------------

        [Test]
        public void Bar_StartsEmpty_AndIsNotAvailable()
        {
            var model = new KineticEnergyBarChargeModel();

            Assert.That(model.IsAvailable(_fighter, _settings), Is.False);
            Assert.That(model.ChargeFraction(_fighter, _settings), Is.EqualTo(0f));
        }

        [Test]
        public void Bar_DamageDealtFillsHalfAPercentPerHp()
        {
            var model = new KineticEnergyBarChargeModel();

            model.OnDamageDealt(_fighter, 20f, _settings);

            Assert.That(_fighter.SignatureCharge, Is.EqualTo(0.1f).Within(Tolerance));
            Assert.That(model.ChargeFraction(_fighter, _settings), Is.EqualTo(0.1f).Within(Tolerance));
        }

        [Test]
        public void Bar_DamageTakenFillsAPercentPerHp_SoBeingHurtChargesFaster()
        {
            var model = new KineticEnergyBarChargeModel();
            var other = new FighterState(Handedness.Right, 100f, WeaponIds.Rapier);

            model.OnDamageTaken(_fighter, 20f, _settings);
            model.OnDamageDealt(other, 20f, _settings);

            Assert.That(_fighter.SignatureCharge, Is.EqualTo(0.2f).Within(Tolerance));
            Assert.That(_fighter.SignatureCharge, Is.GreaterThan(other.SignatureCharge));
        }

        [Test]
        public void Bar_IsAvailableOnceFull_AndNeverOverflows()
        {
            var model = new KineticEnergyBarChargeModel();

            model.OnDamageTaken(_fighter, 99f, _settings);
            Assert.That(model.IsAvailable(_fighter, _settings), Is.False, "99% is not full");

            model.OnDamageTaken(_fighter, 1f, _settings);
            Assert.That(model.IsAvailable(_fighter, _settings), Is.True);

            model.OnDamageTaken(_fighter, 500f, _settings);
            Assert.That(_fighter.SignatureCharge, Is.EqualTo(KineticEnergyBarChargeModel.FullBar));
            Assert.That(model.ChargeFraction(_fighter, _settings), Is.EqualTo(1f));
        }

        [Test]
        public void Bar_ManySmallFills_AddUpToFull_DespiteFloatError()
        {
            var model = new KineticEnergyBarChargeModel();
            _settings.BarChargePerHpDamageDealt = 0.05f;

            for (int i = 0; i < 20; i++)
            {
                model.OnDamageDealt(_fighter, 1f, _settings);
            }

            Assert.That(model.IsAvailable(_fighter, _settings), Is.True, "twenty fills of 5% fill the bar");
        }

        [Test]
        public void Bar_UsingTheMove_EmptiesTheBar_AndCountsTheUse()
        {
            var model = new KineticEnergyBarChargeModel();
            model.OnDamageTaken(_fighter, 100f, _settings);

            model.OnUsed(_fighter, _settings);

            Assert.That(_fighter.SignatureCharge, Is.EqualTo(0f));
            Assert.That(_fighter.SignatureUses, Is.EqualTo(1));
            Assert.That(model.IsAvailable(_fighter, _settings), Is.False);
        }

        [Test]
        public void Bar_CanBeRefilled_AndUsedAgain_WithNoUseCap()
        {
            var model = new KineticEnergyBarChargeModel();
            _settings.MovesPerMatch = 1;

            for (int use = 0; use < 3; use++)
            {
                model.OnDamageTaken(_fighter, 100f, _settings);
                Assert.That(model.IsAvailable(_fighter, _settings), Is.True, $"use {use}");
                model.OnUsed(_fighter, _settings);
            }

            Assert.That(_fighter.SignatureUses, Is.EqualTo(3), "the bar alone limits uses; MovesPerMatch is for the once-per-match option");
        }

        [Test]
        public void Bar_IgnoresNoDamageAndNegativeDamage()
        {
            var model = new KineticEnergyBarChargeModel();

            model.OnDamageDealt(_fighter, 0f, _settings);
            model.OnDamageTaken(_fighter, -5f, _settings);

            Assert.That(_fighter.SignatureCharge, Is.EqualTo(0f));
        }

        [Test]
        public void Bar_ReadsItsRatesFromTheSettings()
        {
            var model = new KineticEnergyBarChargeModel();
            _settings.BarChargePerHpDamageDealt = 0.02f;
            _settings.BarChargePerHpDamageTaken = 0.03f;

            model.OnDamageDealt(_fighter, 10f, _settings);
            model.OnDamageTaken(_fighter, 10f, _settings);

            Assert.That(_fighter.SignatureCharge, Is.EqualTo(0.5f).Within(Tolerance));
        }

        // --- The flag picks the option ----------------------------------------------------------------

        [Test]
        public void TheSettingsModel_FollowsTheChargeModeFlag()
        {
            var model = new SettingsSignatureChargeModel();

            _settings.ChargeMode = SignatureChargeMode.OncePerMatch;
            Assert.That(model.IsAvailable(_fighter, _settings), Is.True, "once per match: there from turn 1");

            _settings.ChargeMode = SignatureChargeMode.KineticEnergyBar;
            Assert.That(model.IsAvailable(_fighter, _settings), Is.False, "the bar: empty at the start");
        }

        [Test]
        public void TheSettingsModel_SwitchingTheFlagSwitchesTheRule_WithNothingRememberedInTheModel()
        {
            var model = new SettingsSignatureChargeModel();
            var other = new FighterState(Handedness.Right, 100f, WeaponIds.Rapier);
            _settings.ChargeMode = SignatureChargeMode.KineticEnergyBar;

            model.OnDamageTaken(_fighter, 50f, _settings);

            Assert.That(_fighter.SignatureCharge, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(other.SignatureCharge, Is.EqualTo(0f), "the charge lives on each fighter's state, not in the model");
            Assert.That(model.ChargeFraction(other, _settings), Is.EqualTo(0f));
        }

        [Test]
        public void TheSettingsModel_OncePerMatch_SpendsOneUse()
        {
            var model = new SettingsSignatureChargeModel();

            model.OnUsed(_fighter, _settings);

            Assert.That(model.IsAvailable(_fighter, _settings), Is.False);
            Assert.That(model.ChargeFraction(_fighter, _settings), Is.EqualTo(0f));
        }

        // --- The state and its copy --------------------------------------------------------------------

        [Test]
        public void TheChargeAndTheUses_AreCopiedWithTheFighterState()
        {
            _fighter.SignatureCharge = 0.35f;
            _fighter.SignatureUses = 2;

            FighterState copy = _fighter.Clone();
            copy.SignatureCharge = 1f;
            copy.SignatureUses = 5;

            Assert.That(_fighter.SignatureCharge, Is.EqualTo(0.35f), "a simulated turn never changes the board it started from");
            Assert.That(_fighter.SignatureUses, Is.EqualTo(2));
            Assert.That(_fighter.Clone().SignatureCharge, Is.EqualTo(0.35f));
            Assert.That(_fighter.Clone().SignatureUses, Is.EqualTo(2));
        }

        [Test]
        public void TheChargeState_ReadsTheModel_AndIsNotChargedWithoutOne()
        {
            _fighter.SignatureCharge = 0.4f;

            SignatureChargeState bar = SignatureChargeState.Of(new KineticEnergyBarChargeModel(), _fighter, _settings);
            SignatureChargeState once = SignatureChargeState.Of(new OncePerMatchChargeModel(), _fighter, _settings);
            SignatureChargeState none = SignatureChargeState.Of(null, _fighter, _settings);

            Assert.That(bar.IsAvailable, Is.False);
            Assert.That(bar.ChargeFraction, Is.EqualTo(0.4f).Within(Tolerance));
            Assert.That(once.IsAvailable, Is.True);
            Assert.That(once.ChargeFraction, Is.EqualTo(1f));
            Assert.That(none.IsAvailable, Is.False);
            Assert.That(none.ChargeFraction, Is.EqualTo(0f));
        }

        // --- Validation ---------------------------------------------------------------------------------

        [Test]
        public void TheStartingSettings_AreValid_AndBadBarRatesAreReported()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var issues = new System.Collections.Generic.List<string>();
            settings.Validate(issues);
            Assert.That(issues, Is.Empty);

            settings.Signature.BarChargePerHpDamageTaken = -0.1f;
            settings.Signature.MovesPerMatch = -1;
            settings.Validate(issues);

            Assert.That(issues, Has.Some.Contains("fill rates"));
            Assert.That(issues, Has.Some.Contains("moves per match"));
        }
    }
}
