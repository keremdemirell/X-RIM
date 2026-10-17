using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using XRim.Rules.Status;

namespace XRim.Tests.EditMode.Rules.Signature
{
    /// <summary>
    /// The special properties of the placeholder signature moves (GDD §8; designer 2026-10-15): knockback burst ×3, stun on hit, damage
    /// boost ×1.5. Each touches only the hits its own move lands. Left holds the rapier (torso hit 8), right the mace (torso 20).
    /// </summary>
    public sealed class SignatureEffectTests
    {
        private const float Tolerance = 1e-4f;
        private const float RapierTorsoDamage = 8f;

        private RulesSettings _settings;
        private RulePolicies _policies;
        private MatchState _state;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _policies = new RulePolicies();
            _state = new MatchState(
                new PerSide<FighterState>(
                    new FighterState(Handedness.Right, _settings.Damage.MaxHp, WeaponIds.Rapier),
                    new FighterState(Handedness.Right, _settings.Damage.MaxHp, WeaponIds.Mace)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
        }

        private SignatureAttackEffects EffectsOf(SignatureMoveId move) => SignatureAttackEffects.For(move, _settings, _policies.SignatureEffects);

        /// <summary>One instant: the given side hits the other on a part, the left attack carrying <paramref name="leftMove"/>'s effects.</summary>
        private HitResolution Hit(Side attacker, BodyPart part, SignatureMoveId leftMove = default)
        {
            var attacks = new PerSide<TurnAttack>(
                new TurnAttack(_settings.FindWeapon(WeaponIds.Rapier), null, EffectsOf(leftMove)),
                new TurnAttack(_settings.FindWeapon(WeaponIds.Mace)));
            var resolver = new HitResolver(_state, _settings, _policies, attacks);
            var hit = new HitFacts(attacker, part, attacker == Side.Left ? WeaponIds.Rapier : WeaponIds.Mace, SimTime.FromMilliseconds(100), false);
            return resolver.Resolve(new[] { hit }, new PerSide<bool>(true, true));
        }

        // --- The catalogue -------------------------------------------------------------------------------

        [Test]
        public void TheCatalogue_HasTheThreePlaceholderEffects()
        {
            Assert.That(new RulePolicies().SignatureEffects.Select(effect => effect.Id),
                Is.EquivalentTo(new[] { SignatureEffectIds.KnockbackBurst, SignatureEffectIds.StunOnHit, SignatureEffectIds.DamageBoost }));
        }

        [Test]
        public void TheStartingNumbers_AreThePlaceholders()
        {
            var settings = new SignatureSettings();

            Assert.That(settings.KnockbackBurstMultiplier, Is.EqualTo(3f));
            Assert.That(settings.DamageBoostMultiplier, Is.EqualTo(1.5f));
        }

        [Test]
        public void NoMove_HasNoEffect()
        {
            SignatureAttackEffects none = EffectsOf(default);

            Assert.That(none, Is.SameAs(SignatureAttackEffects.None));
            Assert.That((none.DamageMultiplier, none.KnockbackMultiplier, none.StunsOnHit), Is.EqualTo((1f, 1f, false)));
        }

        [Test]
        public void AMoveTheCatalogueDoesNotKnow_HasNoEffect()
        {
            Assert.That(EffectsOf(new SignatureMoveId("no_such_move")), Is.SameAs(SignatureAttackEffects.None));
        }

        [Test]
        public void AMoveWhoseEffectIsUnknown_PlaysWithoutASpecialProperty()
        {
            _settings.FindSignatureMove(SignatureMoveIds.WhiplashCalibrationSpin).SpecialEffectId = "nothing_by_that_name";

            SignatureAttackEffects effects = EffectsOf(SignatureMoveIds.WhiplashCalibrationSpin);

            Assert.That(effects.Move, Is.EqualTo(SignatureMoveIds.WhiplashCalibrationSpin));
            Assert.That((effects.DamageMultiplier, effects.KnockbackMultiplier, effects.StunsOnHit), Is.EqualTo((1f, 1f, false)));
        }

        [Test]
        public void EachPlaceholderMove_HasItsOwnProperty_ReadFromTheSettings()
        {
            _settings.Signature.KnockbackBurstMultiplier = 4f;
            _settings.Signature.DamageBoostMultiplier = 2f;

            SignatureAttackEffects touch = EffectsOf(SignatureMoveIds.FiveFingerExplodingTouch);
            SignatureAttackEffects impact = EffectsOf(SignatureMoveIds.HeadImpactSimulation404);
            SignatureAttackEffects whiplash = EffectsOf(SignatureMoveIds.WhiplashCalibrationSpin);

            Assert.That((touch.DamageMultiplier, touch.KnockbackMultiplier, touch.StunsOnHit), Is.EqualTo((1f, 4f, false)));
            Assert.That((impact.DamageMultiplier, impact.KnockbackMultiplier, impact.StunsOnHit), Is.EqualTo((1f, 1f, true)));
            Assert.That((whiplash.DamageMultiplier, whiplash.KnockbackMultiplier, whiplash.StunsOnHit), Is.EqualTo((2f, 1f, false)));
        }

        // --- Damage boost ("Whiplash Calibration Spin") ---------------------------------------------------

        [Test]
        public void DamageBoost_MultipliesTheMovesHits()
        {
            Hit(Side.Left, BodyPart.Torso, SignatureMoveIds.WhiplashCalibrationSpin);

            Assert.That(_state.Fighters[Side.Right].Hp, Is.EqualTo(_settings.Damage.MaxHp - RapierTorsoDamage * 1.5f).Within(Tolerance));
        }

        [Test]
        public void DamageBoost_StillRespectsTheNoInstantKoRule()
        {
            _settings.Signature.DamageBoostMultiplier = 100f;

            Hit(Side.Left, BodyPart.Head, SignatureMoveIds.WhiplashCalibrationSpin);

            Assert.That(_state.Fighters[Side.Right].Hp, Is.EqualTo(1f), "D27: a single hit from full HP leaves 1 HP, boost or not");
        }

        [Test]
        public void AnEffect_NeverTouchesTheOpponentsHits()
        {
            Hit(Side.Right, BodyPart.Torso, SignatureMoveIds.WhiplashCalibrationSpin);

            Assert.That(_state.Fighters[Side.Left].Hp, Is.EqualTo(_settings.Damage.MaxHp - 20f).Within(Tolerance), "the mace hits as ever");
        }

        [Test]
        public void ADrawnPath_HitsAsEver()
        {
            Hit(Side.Left, BodyPart.Torso);

            Assert.That(_state.Fighters[Side.Right].Hp, Is.EqualTo(_settings.Damage.MaxHp - RapierTorsoDamage).Within(Tolerance));
        }

        // --- Stun on hit ("Head Impact Simulation #404") ----------------------------------------------------

        [Test]
        public void StunOnHit_AnyLandedHitStuns_EvenALightTorsoHit()
        {
            HitResolution resolution = Hit(Side.Left, BodyPart.Torso, SignatureMoveIds.HeadImpactSimulation404);

            Assert.That(resolution.Landed[0].Damage.Stuns, Is.True);
            Assert.That(resolution.Events.OfType<StatusAppliedEvent>().Single().Kind, Is.EqualTo(StatusKind.Stunned));
            Assert.That(_state.Fighters[Side.Right].Statuses, Has.Count.EqualTo(1));
        }

        [Test]
        public void WithoutTheMove_ALightTorsoHitDoesNotStun()
        {
            HitResolution resolution = Hit(Side.Left, BodyPart.Torso);

            Assert.That(resolution.Landed[0].Damage.Stuns, Is.False);
            Assert.That(_state.Fighters[Side.Right].Statuses, Is.Empty);
        }

        [Test]
        public void StunOnHit_AKillingHitDoesNotStun()
        {
            _state.Fighters[Side.Right].Hp = 3f;

            HitResolution resolution = Hit(Side.Left, BodyPart.Torso, SignatureMoveIds.HeadImpactSimulation404);

            Assert.That(resolution.Landed[0].KillsVictim, Is.True);
            Assert.That(_state.Fighters[Side.Right].Statuses, Is.Empty);
        }

        // --- The placeholder catalogue (designer 2026-10-15) ---------------------------------------------------

        [Test]
        public void TheStartingCatalogue_IsTheThreeApprovedPlaceholders()
        {
            var moves = GddStartingValues.CreateSignatureMoves();

            Assert.That(moves.Select(move => move.DisplayName),
                Is.EqualTo(new[] { "5 Finger Exploding Touch", "Head Impact Simulation #404", "Whiplash Calibration Spin" }));
            Assert.That(moves.Select(move => move.SpecialEffectId),
                Is.EqualTo(new[] { SignatureEffectIds.KnockbackBurst, SignatureEffectIds.StunOnHit, SignatureEffectIds.DamageBoost }));
            foreach (SignatureMoveStats move in moves)
            {
                Assert.That(move.DesignIsTbd, Is.True, move.Id);
                Assert.That(move.ConsumesInk, Is.False, $"{move.Id}: ignores ink (designer 2026-10-15)");
                Assert.That(move.AllowedWeaponIds, Is.Empty, $"{move.Id}: any weapon");
                Assert.That(move.OverridesBodyMove, Is.False, $"{move.Id}: the body swipe stays");
            }
        }

        [Test]
        public void EveryStartingMove_NamesAnEffectTheCatalogueHas()
        {
            var effects = new RulePolicies().SignatureEffects.Select(effect => effect.Id).ToList();

            foreach (SignatureMoveStats move in GddStartingValues.CreateSignatureMoves())
            {
                Assert.That(effects, Does.Contain(move.SpecialEffectId), move.Id);
            }
        }

        [Test]
        public void EveryStartingPattern_StaysInARapiersReach_AndAboveTheFloor()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            Vec2 shoulder = settings.Paths.ShoulderOffsetUnits;
            float reach = settings.Paths.ArmLengthUnits + settings.FindWeapon(WeaponIds.Rapier).LengthUnits;
            const float FloorInTorsoFrameUnits = -180f;

            foreach (SignatureMoveStats move in settings.SignatureMoves)
            {
                foreach (Vec2 point in move.PresetPath)
                {
                    Assert.That(Vec2.Distance(point, shoulder), Is.LessThanOrEqualTo(reach), $"{move.Id} {point}");
                    Assert.That(point.Y, Is.GreaterThan(FloorInTorsoFrameUnits), $"{move.Id} {point}");
                    Assert.That(point.X, Is.GreaterThan(0f), $"{move.Id}: toward the opponent");
                }
            }
        }

        [Test]
        public void EveryStartingPattern_EndsWithinTheTurn_WithARapierFromItsGuard()
        {
            // The rapier rests in the low guard: its tip 400 from the shoulder at −40° (Session 04 guard, Session 14 tip cap).
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            WeaponStats rapier = settings.FindWeapon(WeaponIds.Rapier);
            Vec2 guardTip = settings.Paths.ShoulderOffsetUnits + Vec2.FromAngleDegrees(-40f) * 400f;
            var builder = new SignaturePathBuilder(new RulePolicies());

            foreach (SignatureMoveStats move in settings.SignatureMoves)
            {
                float seconds = builder.Build(move, guardTip, rapier, settings.Paths).Path.LengthUnits / rapier.SpeedUnitsPerSecond;

                Assert.That(seconds, Is.LessThan(settings.Match.ExecutionHardCapSeconds * 0.75f),
                    $"{move.Id} takes {seconds:0.00} s: it must land well inside the {settings.Match.ExecutionHardCapSeconds} s turn");
            }
        }

        [Test]
        public void TheStartingCatalogue_IsValid_AndInTheStartingSettings()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var issues = new System.Collections.Generic.List<string>();

            settings.Validate(issues);

            Assert.That(issues, Is.Empty);
            Assert.That(settings.SignatureMoves, Has.Count.EqualTo(3));
            Assert.That(settings.FindSignatureMove(SignatureMoveIds.HeadImpactSimulation404).DisplayName, Is.EqualTo("Head Impact Simulation #404"));
        }

        [Test]
        public void TheCatalogueValidation_ReportsBrokenMoves()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.SignatureMoves.Add(new SignatureMoveStats { Id = SignatureMoveIds.FiveFingerExplodingTouch.Value });
            settings.SignatureMoves.Add(new SignatureMoveStats { Id = "tied", PresetPath = { new Vec2(100f, 100f) }, AllowedWeaponIds = { "banana" } });
            settings.Signature.KnockbackBurstMultiplier = -1f;
            var issues = new System.Collections.Generic.List<string>();

            settings.Validate(issues);

            Assert.That(issues, Has.Some.Contains("duplicate id"));
            Assert.That(issues, Has.Some.Contains("has no pattern"));
            Assert.That(issues, Has.Some.Contains("unknown weapon 'banana'"));
            Assert.That(issues, Has.Some.Contains("effect multipliers"));
        }
    }
}
