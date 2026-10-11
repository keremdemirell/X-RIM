using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Combat
{
    /// <summary>
    /// Severing through the hit rules (GDD §11, §12). Left holds the rapier, right the mace; both are right-handed, so each one's
    /// weapon arm is its right arm. Arm durability 40 and a 35% cap: a mace arm hit (16) adds 14, so the third capped hit severs.
    /// Each turn is a fresh resolver over the same match state.
    /// </summary>
    public sealed class HitResolverSeverTests
    {
        private const float Tolerance = 1e-4f;

        private RulesSettings _settings;
        private RulePolicies _policies;
        private MatchState _state;
        private HitResolver _resolver;

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
            NewTurn();
        }

        private WeaponStats Rapier => _settings.FindWeapon(WeaponIds.Rapier);
        private FighterState Left => _state.Fighters[Side.Left];
        private FighterState Right => _state.Fighters[Side.Right];

        /// <summary>Two capped mace hits on a limb: one more severs it.</summary>
        private float TwoCappedHits => 2f * _settings.Damage.ArmDurability * _settings.Damage.PerHitLimbCapFraction;

        private void NewTurn() =>
            _resolver = new HitResolver(_state, _settings, _policies,
                new PerSide<TurnAttack>(new TurnAttack(Rapier), new TurnAttack(_settings.FindWeapon(WeaponIds.Mace))));

        private static HitFacts Hit(Side attacker, BodyPart part, double milliseconds, bool offHand = false) =>
            new HitFacts(attacker, part, attacker == Side.Left ? WeaponIds.Rapier : WeaponIds.Mace, SimTime.FromMilliseconds(milliseconds), offHand);

        private HitResolution Resolve(params HitFacts[] sameInstant) => _resolver.Resolve(sameInstant, new PerSide<bool>(true, true));

        [Test]
        public void ALimb_IsSeveredOnlyByTheThirdCappedHit()
        {
            for (int turn = 1; turn <= 2; turn++)
            {
                HitResolution capped = Resolve(Hit(Side.Right, BodyPart.LeftArm, 200));
                Assert.That(capped.Severed, Is.Empty, $"turn {turn}");
                Assert.That(capped.Events.OfType<LimbSeveredEvent>(), Is.Empty, $"turn {turn}");
                Assert.That(Left.IsSevered(BodyPart.LeftArm), Is.False, $"turn {turn}");
                NewTurn();
            }

            HitResolution third = Resolve(Hit(Side.Right, BodyPart.LeftArm, 200));

            Assert.That(Left.GetLimbDamage(BodyPart.LeftArm), Is.EqualTo(TwoCappedHits * 1.5f).Within(Tolerance), "three capped hits");
            Assert.That(Left.IsSevered(BodyPart.LeftArm), Is.True, "§11: at least 3 hits on the same limb");
            SeverOutcome sever = third.Severed.Single();
            Assert.That(sever.Limb, Is.EqualTo(new SeveredLimbRef(Side.Left, BodyPart.LeftArm)));
            Assert.That(sever.DropsHeldItem, Is.False, "the off arm holds nothing");
            Assert.That(third.Events.Select(e => e.GetType()), Is.EqualTo(new[] { typeof(HitLandedEvent), typeof(LimbSeveredEvent) }));
        }

        [Test]
        public void TheDominantArmSevered_DropsTheWeapon_AndEndsTheAttack()
        {
            Left.SetLimbDamage(BodyPart.RightArm, TwoCappedHits);

            HitResolution sever = Resolve(Hit(Side.Right, BodyPart.RightArm, 200));
            HitResolution later = Resolve(Hit(Side.Left, BodyPart.Torso, 300));

            SeverOutcome outcome = sever.Severed.Single();
            Assert.That(outcome.DropsHeldItem, Is.True, "§12: the weapon drops");
            Assert.That(outcome.DroppedItem, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(sever.Events.Select(e => e.GetType()),
                Is.EqualTo(new[] { typeof(HitLandedEvent), typeof(LimbSeveredEvent), typeof(WeaponDroppedEvent) }));
            Assert.That(_resolver.Attacks[Side.Left].Stop, Is.EqualTo(AttackStop.WeaponDropped));
            Assert.That(sever.Interrupted, Is.Empty, "a dropped weapon is not an interrupt");
            Assert.That(sever.Events.OfType<AttackInterruptedEvent>(), Is.Empty);
            Assert.That(later.Landed, Is.Empty, "a dropped weapon lands nothing");
        }

        [Test]
        public void AWeaponDroppedAtTheSameInstant_StillLandsItsOwnHit()
        {
            Left.SetLimbDamage(BodyPart.RightArm, TwoCappedHits);

            HitResolution result = Resolve(Hit(Side.Right, BodyPart.RightArm, 200), Hit(Side.Left, BodyPart.Torso, 200));

            Assert.That(result.Landed, Has.Count.EqualTo(2), "a trade: neither was hit before its own attack landed");
            Assert.That(_settings.Damage.MaxHp - Right.Hp, Is.EqualTo(Rapier.BaseDamage).Within(Tolerance));
        }

        [Test]
        public void TheSecondArmLostInTheSameTurn_DropsNothingAgain()
        {
            Left.SetLimbDamage(BodyPart.RightArm, TwoCappedHits);
            Left.SetLimbDamage(BodyPart.LeftArm, _settings.Damage.ArmDurability - 1f);

            Resolve(Hit(Side.Right, BodyPart.RightArm, 200));
            HitResolution second = Resolve(Hit(Side.Right, BodyPart.LeftArm, 250));

            Assert.That(Left.RemainingArms, Is.EqualTo(0));
            Assert.That(second.Severed.Single().DropsHeldItem, Is.False, "the weapon already dropped");
            Assert.That(second.Events.OfType<WeaponDroppedEvent>(), Is.Empty);
        }

        [Test]
        public void TheOffHand_HitsAtTheOffHandShare()
        {
            Left.MarkSevered(BodyPart.RightArm);

            Resolve(Hit(Side.Left, BodyPart.Torso, 200, offHand: true));

            Assert.That(_settings.Damage.MaxHp - Right.Hp,
                Is.EqualTo(Rapier.BaseDamage * _settings.Damage.OffHandDamageMultiplier).Within(Tolerance), "§12: 80% damage");
        }

        [Test]
        public void ASeveredLimb_TakesNoFurtherHits()
        {
            Left.SetLimbDamage(BodyPart.LeftLeg, 2f * _settings.Damage.LegDurability * _settings.Damage.PerHitLimbCapFraction);
            _settings.FindWeapon(WeaponIds.Mace).BaseDamage = 100f;

            Assert.That(Resolve(Hit(Side.Right, BodyPart.LeftLeg, 200)).Severed, Has.Count.EqualTo(1));
            NewTurn();
            Assert.That(Resolve(Hit(Side.Right, BodyPart.LeftLeg, 300)).Landed, Is.Empty);
        }
    }
}
