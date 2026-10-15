using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Damage
{
    /// <summary>
    /// Weapon traits (GDD §15, Session 14; designer 2026-10-15: seams only, off). Every trait needs its flag on and a weapon that lists
    /// it. Left holds the rapier (torso hit 8), right the mace (torso 20). Lifesteal: after 3 hits, 30% of the damage those hits dealt;
    /// build-up: +0.5% damage per HP of damage taken, up to +50% (placeholders).
    /// </summary>
    public sealed class WeaponTraitTests
    {
        private const float Tolerance = 1e-3f;
        private const float RapierTorsoDamage = 8f;
        private const float MaceTorsoDamage = 20f;

        private RulesSettings _settings;
        private RulePolicies _policies;
        private MatchState _state;
        private int _milliseconds;

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
            _milliseconds = 100;
        }

        private WeaponStats Rapier => _settings.FindWeapon(WeaponIds.Rapier);
        private FighterState Left => _state.Fighters[Side.Left];
        private FighterState Right => _state.Fighters[Side.Right];

        private void LifestealOn(bool carriedByTheRapier = true)
        {
            _settings.Traits.LifestealEnabled = true;
            if (carriedByTheRapier) Rapier.TraitIds.Add(WeaponTraitIds.Lifesteal);
        }

        private void BuildUpOn(bool carriedByTheRapier = true)
        {
            _settings.Traits.BuildUpEnabled = true;
            if (carriedByTheRapier) Rapier.TraitIds.Add(WeaponTraitIds.BuildUp);
        }

        private static HitFacts Hit(Side attacker, BodyPart part, int milliseconds) =>
            new HitFacts(attacker, part, attacker == Side.Left ? WeaponIds.Rapier : WeaponIds.Mace, SimTime.FromMilliseconds(milliseconds), false);

        /// <summary>One turn: a fresh resolver, the hits (each its own instant, in order), then the end of the turn. Returns every event.</summary>
        private System.Collections.Generic.List<MatchEvent> PlayTurn(params (Side Attacker, BodyPart Part)[] hits)
        {
            var resolver = new HitResolver(_state, _settings, _policies,
                new PerSide<TurnAttack>(new TurnAttack(Rapier), new TurnAttack(_settings.FindWeapon(WeaponIds.Mace))));
            var events = new System.Collections.Generic.List<MatchEvent>();
            foreach ((Side attacker, BodyPart part) in hits)
            {
                _milliseconds += 50;
                events.AddRange(resolver.Resolve(new[] { Hit(attacker, part, _milliseconds) }, new PerSide<bool>(true, true)).Events);
            }

            EndTurn(Left, Rapier);
            EndTurn(Right, null);
            return events;
        }

        private void EndTurn(FighterState fighter, WeaponStats attackedWith) =>
            WeaponTraitRules.EndTurn(fighter, attackedWith, _settings.Traits);

        /// <summary>A turn in which the left dummy hit the right one once on the torso.</summary>
        private System.Collections.Generic.List<MatchEvent> LeftHitsOnce() => PlayTurn((Side.Left, BodyPart.Torso));

        private static float Healed(System.Collections.Generic.IEnumerable<MatchEvent> events) =>
            events.OfType<WeaponTraitHealEvent>().Sum(heal => heal.HpRecovered);

        // --- Off by default, never on a default dummy ----------------------------------------------

        [Test]
        public void ByDefault_BothTraitsAreOff_AndNoWeaponCarriesOne()
        {
            var settings = GddStartingValues.CreateRulesSettings();

            Assert.That(settings.Traits.LifestealEnabled, Is.False);
            Assert.That(settings.Traits.BuildUpEnabled, Is.False);
            Assert.That(settings.Weapons.SelectMany(weapon => weapon.TraitIds), Is.Empty, "§15: no dummy has these effects by default");
        }

        [Test]
        public void TheStartingValues_AreTheGddsThreeHits_AndPlaceholdersForTheRest()
        {
            var traits = new TraitSettings();

            Assert.That(traits.LifestealHitsNeeded, Is.EqualTo(3), "§15: after 3 successful hits");
            Assert.That(traits.LifestealHitCounting, Is.EqualTo(LifestealHitCounting.Total));
            Assert.That(traits.LifestealRecoveredFraction, Is.EqualTo(0.3f));
            Assert.That(traits.BuildUpGrowthPerHpDamageTaken, Is.EqualTo(0.005f));
            Assert.That(traits.BuildUpMaxBonusFraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void ThePolicies_HoldBothTraits_ByTheirIds()
        {
            Assert.That(_policies.WeaponTraits.Select(trait => trait.Id), Is.EquivalentTo(new[] { "lifesteal", "build_up" }));
        }

        [Test]
        public void ATraitDoesNothing_WithItsFlagOff_EvenOnAWeaponThatCarriesIt()
        {
            Rapier.TraitIds.Add(WeaponTraitIds.Lifesteal);
            Rapier.TraitIds.Add(WeaponTraitIds.BuildUp);
            Left.Hp = 50f;
            Left.HpDamageTaken = 40f;

            float lostBefore = _settings.Damage.MaxHp - Right.Hp;
            for (int i = 0; i < 3; i++) LeftHitsOnce();

            Assert.That(Left.Hp, Is.EqualTo(50f));
            Assert.That(Left.LifestealHitCount, Is.EqualTo(0));
            Assert.That(lostBefore + (_settings.Damage.MaxHp - Right.Hp), Is.EqualTo(3 * RapierTorsoDamage).Within(Tolerance),
                "no build-up bonus either");
        }

        [Test]
        public void ATraitDoesNothing_WithItsFlagOn_WhenNoWeaponCarriesIt()
        {
            LifestealOn(carriedByTheRapier: false);
            BuildUpOn(carriedByTheRapier: false);
            Left.Hp = 50f;
            Left.HpDamageTaken = 40f;

            for (int i = 0; i < 3; i++) LeftHitsOnce();

            Assert.That(Left.Hp, Is.EqualTo(50f));
            Assert.That(_settings.Damage.MaxHp - Right.Hp, Is.EqualTo(3 * RapierTorsoDamage).Within(Tolerance));
        }

        // --- Lifesteal ------------------------------------------------------------------------------

        [Test]
        public void Lifesteal_AfterThreeHits_RecoversAShareOfTheDamageThoseHitsDealt()
        {
            LifestealOn();
            Left.Hp = 50f;

            System.Collections.Generic.List<MatchEvent> first = LeftHitsOnce();
            System.Collections.Generic.List<MatchEvent> second = LeftHitsOnce();
            Assert.That(Left.Hp, Is.EqualTo(50f), "nothing before the third hit");
            System.Collections.Generic.List<MatchEvent> third = LeftHitsOnce();

            Assert.That(Healed(first) + Healed(second), Is.EqualTo(0f));
            Assert.That(Healed(third), Is.EqualTo(0.3f * 3f * RapierTorsoDamage).Within(Tolerance), "30% of 24");
            Assert.That(Left.Hp, Is.EqualTo(50f + 7.2f).Within(Tolerance));
            WeaponTraitHealEvent heal = third.OfType<WeaponTraitHealEvent>().Single();
            Assert.That(heal.Side, Is.EqualTo(Side.Left));
            Assert.That(heal.TraitId, Is.EqualTo("lifesteal"));
            Assert.That(Left.LifestealHitCount, Is.EqualTo(0), "the count starts again");
            Assert.That(Left.LifestealDamageDealt, Is.EqualTo(0f));
        }

        [Test]
        public void Lifesteal_PaysOutAgainAfterEveryThreeMoreHits()
        {
            LifestealOn();
            Left.Hp = 40f;

            float healed = 0f;
            for (int i = 0; i < 6; i++) healed += Healed(LeftHitsOnce());

            Assert.That(healed, Is.EqualTo(2f * 7.2f).Within(Tolerance));
        }

        [Test]
        public void Lifesteal_NeverRaisesHpAboveTheMaximum()
        {
            LifestealOn();
            Left.Hp = _settings.Damage.MaxHp - 2f;

            float healed = 0f;
            for (int i = 0; i < 3; i++) healed += Healed(LeftHitsOnce());

            Assert.That(Left.Hp, Is.EqualTo(_settings.Damage.MaxHp));
            Assert.That(healed, Is.EqualTo(2f).Within(Tolerance), "the event says what was actually gained");
        }

        [Test]
        public void Lifesteal_ADummyAtFullHp_GainsNothing_AndMakesNoEvent()
        {
            LifestealOn();

            float healed = 0f;
            for (int i = 0; i < 3; i++) healed += Healed(LeftHitsOnce());

            Assert.That(healed, Is.EqualTo(0f));
            Assert.That(Left.Hp, Is.EqualTo(_settings.Damage.MaxHp));
        }

        [Test]
        public void Lifesteal_NeverChangesTheDamageOfAHit()
        {
            LifestealOn();

            for (int i = 0; i < 3; i++) LeftHitsOnce();

            Assert.That(_settings.Damage.MaxHp - Right.Hp, Is.EqualTo(3 * RapierTorsoDamage).Within(Tolerance));
        }

        [Test]
        public void Lifesteal_Total_KeepsItsCount_AcrossATurnThatMissed()
        {
            LifestealOn();
            Left.Hp = 50f;
            LeftHitsOnce();
            LeftHitsOnce();

            PlayTurn(); // an attack that landed nothing (the end of the turn says the rapier was swung)
            float healed = Healed(LeftHitsOnce());

            Assert.That(healed, Is.EqualTo(7.2f).Within(Tolerance), "total hits: the miss does not matter");
        }

        [Test]
        public void Lifesteal_Consecutive_AMissedAttackStartsTheCountAgain()
        {
            LifestealOn();
            _settings.Traits.LifestealHitCounting = LifestealHitCounting.Consecutive;
            Left.Hp = 50f;
            LeftHitsOnce();
            LeftHitsOnce();

            PlayTurn(); // swung and missed
            Assert.That(Left.LifestealHitCount, Is.EqualTo(0));
            float healed = Healed(LeftHitsOnce());
            healed += Healed(LeftHitsOnce());

            Assert.That(healed, Is.EqualTo(0f), "two hits after the miss are not three");
            healed += Healed(LeftHitsOnce());
            Assert.That(healed, Is.EqualTo(7.2f).Within(Tolerance), "the third in a row pays");
        }

        [Test]
        public void Lifesteal_Consecutive_ATurnWithAnotherWeaponOrNoAttack_KeepsTheCount()
        {
            LifestealOn();
            _settings.Traits.LifestealHitCounting = LifestealHitCounting.Consecutive;
            LeftHitsOnce();
            LeftHitsOnce();

            EndTurn(Left, null); // no attack this turn
            EndTurn(Left, _settings.FindWeapon(WeaponIds.Mace)); // another weapon that does not carry it, no hit

            Assert.That(Left.LifestealHitCount, Is.EqualTo(2));
        }

        [Test]
        public void Lifesteal_IsRecoveredAfterTheInstant_SoATradeDoesNotDependOnTheOrderOfTheHits()
        {
            // Left is one hit from the payout at 50 HP; at the same instant the mace hits it for 20.
            LifestealOn();
            Left.LifestealHitCount = 2;
            Left.LifestealDamageDealt = 16f;
            Left.Hp = 50f;
            var resolver = new HitResolver(_state, _settings, _policies,
                new PerSide<TurnAttack>(new TurnAttack(Rapier), new TurnAttack(_settings.FindWeapon(WeaponIds.Mace))));

            HitResolution trade = resolver.Resolve(new[] { Hit(Side.Right, BodyPart.Torso, 200), Hit(Side.Left, BodyPart.Torso, 200) },
                new PerSide<bool>(true, true));

            Assert.That(Left.Hp, Is.EqualTo(50f - MaceTorsoDamage + 0.3f * (16f + RapierTorsoDamage)).Within(Tolerance));
            Assert.That(Healed(trade.Events), Is.EqualTo(7.2f).Within(Tolerance));
        }

        [Test]
        public void Lifesteal_NeverBringsADummyBackFromTheDead()
        {
            // The mace kills left at the same instant as its third hit; the recovery would have saved it if it came first.
            LifestealOn();
            Left.LifestealHitCount = 2;
            Left.LifestealDamageDealt = 16f;
            Left.Hp = 18f;
            var resolver = new HitResolver(_state, _settings, _policies,
                new PerSide<TurnAttack>(new TurnAttack(Rapier), new TurnAttack(_settings.FindWeapon(WeaponIds.Mace))));

            HitResolution trade = resolver.Resolve(new[] { Hit(Side.Left, BodyPart.Torso, 200), Hit(Side.Right, BodyPart.Torso, 200) },
                new PerSide<bool>(true, true));

            Assert.That(Left.IsDead, Is.True, "18 HP, a 20 damage hit: the recovery cannot undo a death");
            Assert.That(trade.Died, Contains.Item(Side.Left));
            Assert.That(Healed(trade.Events), Is.EqualTo(0f));
        }

        [Test]
        public void Lifesteal_RecoversNothingInSuddenDeath_WhereOneHitHoldsTheWinAnd1HpStays()
        {
            LifestealOn();
            _state.IsSuddenDeath = true;
            Left.Hp = 1f;
            Left.LifestealHitCount = 2;
            Left.LifestealDamageDealt = 16f;

            System.Collections.Generic.List<MatchEvent> events = LeftHitsOnce();

            Assert.That(Left.Hp, Is.EqualTo(1f));
            Assert.That(Healed(events), Is.EqualTo(0f));
        }

        // --- Build-up -------------------------------------------------------------------------------

        [Test]
        public void BuildUp_RaisesDamage_WithTheDamageTheDummyHasTaken()
        {
            BuildUpOn();
            Left.HpDamageTaken = 40f;

            LeftHitsOnce();

            Assert.That(_settings.Damage.MaxHp - Right.Hp, Is.EqualTo(RapierTorsoDamage * (1f + 0.005f * 40f)).Within(Tolerance));
        }

        [Test]
        public void BuildUp_StopsGrowingAtItsCap()
        {
            BuildUpOn();
            Left.HpDamageTaken = 5000f;

            LeftHitsOnce();

            Assert.That(_settings.Damage.MaxHp - Right.Hp, Is.EqualTo(RapierTorsoDamage * 1.5f).Within(Tolerance));
        }

        [Test]
        public void BuildUp_ADummyThatHasTakenNothing_HitsForItsBaseDamage()
        {
            BuildUpOn();

            LeftHitsOnce();

            Assert.That(_settings.Damage.MaxHp - Right.Hp, Is.EqualTo(RapierTorsoDamage).Within(Tolerance));
        }

        [Test]
        public void BuildUp_ScalesWithDamageTaken_NotWithTheHpLeft()
        {
            // A low-HP rage is Rejected (§15, §20): HP 10 with nothing taken (it never was hit) gains nothing...
            BuildUpOn();
            Left.Hp = 10f;
            LeftHitsOnce();
            float lowHpDamage = _settings.Damage.MaxHp - Right.Hp;
            Assert.That(lowHpDamage, Is.EqualTo(RapierTorsoDamage).Within(Tolerance));

            // ...while full HP after being hit and healed up still carries what it took.
            SetUp();
            BuildUpOn();
            Left.Hp = _settings.Damage.MaxHp;
            Left.HpDamageTaken = 40f;
            LeftHitsOnce();
            Assert.That(_settings.Damage.MaxHp - Right.Hp, Is.GreaterThan(RapierTorsoDamage));
        }

        [Test]
        public void BuildUp_ReadsTheDamageTakenBeforeTheInstant_SoATradeDoesNotDependOnTheOrderOfTheHits()
        {
            float TradeAndMeasure(bool leftFirst)
            {
                SetUp();
                BuildUpOn();
                var resolver = new HitResolver(_state, _settings, _policies,
                    new PerSide<TurnAttack>(new TurnAttack(Rapier), new TurnAttack(_settings.FindWeapon(WeaponIds.Mace))));
                HitFacts left = Hit(Side.Left, BodyPart.Torso, 200);
                HitFacts right = Hit(Side.Right, BodyPart.Torso, 200);
                resolver.Resolve(leftFirst ? new[] { left, right } : new[] { right, left }, new PerSide<bool>(true, true));
                return _settings.Damage.MaxHp - Right.Hp;
            }

            float leftFirst = TradeAndMeasure(true);
            float rightFirst = TradeAndMeasure(false);

            Assert.That(rightFirst, Is.EqualTo(leftFirst).Within(Tolerance));
            Assert.That(leftFirst, Is.EqualTo(RapierTorsoDamage).Within(Tolerance), "left had taken nothing before the instant");
        }

        // --- What counts as damage taken --------------------------------------------------------------

        [Test]
        public void DamageTaken_CountsTheHpAHitRemoved_AfterTheNoInstantKoRule()
        {
            Right.Hp = 20f; // the mace torso hit (20) would kill: it removes 20 here

            PlayTurn((Side.Left, BodyPart.Torso), (Side.Left, BodyPart.Head));

            Assert.That(Right.HpDamageTaken, Is.EqualTo(20f - Right.Hp).Within(Tolerance));
            Assert.That(Left.HpDamageTaken, Is.EqualTo(0f));
        }

        [Test]
        public void DamageTaken_IsCopiedWithTheState_AndOtherCountersToo()
        {
            Left.HpDamageTaken = 12f;
            Left.LifestealHitCount = 2;
            Left.LifestealDamageDealt = 9f;
            Left.HitLandedThisTurn = true;

            FighterState copy = Left.Clone();
            copy.HpDamageTaken = 99f;

            Assert.That(copy.LifestealHitCount, Is.EqualTo(2));
            Assert.That(copy.LifestealDamageDealt, Is.EqualTo(9f));
            Assert.That(copy.HitLandedThisTurn, Is.True);
            Assert.That(Left.HpDamageTaken, Is.EqualTo(12f), "the copy is independent");
        }

        [Test]
        public void TheEndOfATurn_ClearsTheHitMark()
        {
            LeftHitsOnce();

            Assert.That(Left.HitLandedThisTurn, Is.False, "PlayTurn ends the turn");
        }

        // --- Validation -----------------------------------------------------------------------------

        [Test]
        public void ValidationReportsUnworkableTraitNumbers()
        {
            _settings.Traits.LifestealHitsNeeded = 0;
            _settings.Traits.LifestealRecoveredFraction = 1.5f;
            _settings.Traits.BuildUpGrowthPerHpDamageTaken = -0.1f;
            var issues = new System.Collections.Generic.List<string>();

            _settings.Validate(issues);

            Assert.That(issues, Has.Some.Contains("at least 1 hit"));
            Assert.That(issues, Has.Some.Contains("recovery must be a share"));
            Assert.That(issues, Has.Some.Contains("build-up growth"));
        }

        [Test]
        public void TheStartingTraitSettings_PassValidation()
        {
            var issues = new System.Collections.Generic.List<string>();

            _settings.Validate(issues);

            Assert.That(issues, Is.Empty);
        }
    }
}
