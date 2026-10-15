using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Bots;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Tests.EditMode.Rules.Match;

namespace XRim.Tests.EditMode.Bots
{
    /// <summary>The debug bot's pre-match pick (GDD §6, D25): always a valid loadout from what it owns, in a random order.</summary>
    public sealed class RandomLoadoutPickerTests
    {
        private const int Seeds = 200;

        private readonly RandomLoadoutPicker _picker = new RandomLoadoutPicker();
        private RulesSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
        }

        private IReadOnlyList<WeaponId> Pick(IWeaponOwnership ownership, uint seed)
        {
            Assert.That(_picker.TryPick(_settings, ownership, new XorShiftRandom(seed), out IReadOnlyList<WeaponId> loadout), Is.True);
            return loadout;
        }

        [Test]
        public void EveryPick_IsAValidLoadout()
        {
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                IReadOnlyList<WeaponId> loadout = Pick(new AllWeaponsOwned(), seed);

                var issues = new List<string>();
                LoadoutValidator.Validate(loadout, _settings, issues);
                Assert.That(issues, Is.Empty, $"seed {seed}: {string.Join("; ", issues)}");
            }
        }

        [Test]
        public void WhenAShieldIsRequired_EveryPickHasOne()
        {
            _settings.Loadout.RequireShieldSlot = true;

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                IReadOnlyList<WeaponId> loadout = Pick(new AllWeaponsOwned(), seed);

                Assert.That(loadout, Contains.Item(WeaponIds.Shield), $"seed {seed}");
            }
        }

        [Test]
        public void EveryPick_IsFromTheOwnedWeaponsOnly()
        {
            var owned = new OwnedWeapons(WeaponIds.Sword, WeaponIds.Spear, WeaponIds.Mace, WeaponIds.Shield);

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                IReadOnlyList<WeaponId> loadout = Pick(owned, seed);

                Assert.That(loadout, Has.None.EqualTo(WeaponIds.Rapier), $"seed {seed}");
                var issues = new List<string>();
                LoadoutValidator.Validate(loadout, _settings, owned, issues);
                Assert.That(issues, Is.Empty);
            }
        }

        [Test]
        public void ThePicksVary_AndEveryWeaponOfTheRosterTurnsUp()
        {
            var seen = new HashSet<WeaponId>();
            var distinctLoadouts = new HashSet<string>();
            var firstWeapons = new HashSet<WeaponId>();
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                IReadOnlyList<WeaponId> loadout = Pick(new AllWeaponsOwned(), seed);
                seen.UnionWith(loadout);
                firstWeapons.Add(loadout[0]);
                distinctLoadouts.Add(string.Join(",", loadout.Select(id => id.Value)));
            }

            Assert.That(seen, Is.EquivalentTo(new[]
            {
                WeaponIds.Rapier, WeaponIds.Sword, WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield,
            }));
            Assert.That(distinctLoadouts.Count, Is.GreaterThan(10));
            Assert.That(firstWeapons.Count, Is.GreaterThan(2), "the order is random too: the bot does not always open with one weapon");
        }

        [Test]
        public void TheSameSeed_MakesTheSamePick()
        {
            Assert.That(Pick(new AllWeaponsOwned(), 7u), Is.EqualTo(Pick(new AllWeaponsOwned(), 7u)));
        }

        [Test]
        public void WithTooFewOwnedWeapons_NoPickIsMade()
        {
            bool picked = _picker.TryPick(_settings, new OwnedWeapons(WeaponIds.Rapier, WeaponIds.Mace), new XorShiftRandom(1u),
                out IReadOnlyList<WeaponId> loadout);

            Assert.That(picked, Is.False);
            Assert.That(loadout, Is.Null);
        }

        [Test]
        public void ARequiredShieldTheBotDoesNotOwn_MakesNoPick()
        {
            _settings.Loadout.RequireShieldSlot = true;

            bool picked = _picker.TryPick(_settings, new OwnedWeapons(WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Sword),
                new XorShiftRandom(1u), out IReadOnlyList<WeaponId> loadout);

            Assert.That(picked, Is.False);
        }
    }
}
