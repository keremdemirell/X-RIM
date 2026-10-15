using System.Collections.Generic;
using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>What the pre-match picker offers and starts with (GDD §6).</summary>
    public sealed class LoadoutOptionsTests
    {
        private RulesSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
        }

        private static WeaponId[] Ids(IEnumerable<WeaponId> ids) => new List<WeaponId>(ids).ToArray();

        [Test]
        public void EveryOwnedWeaponAndShield_IsAnOption_InTheOrderOfTheWeaponList()
        {
            List<WeaponId> options = LoadoutOptions.Pickable(_settings, new AllWeaponsOwned());

            Assert.That(options, Is.EqualTo(new[]
            {
                WeaponIds.Rapier, WeaponIds.Sword, WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield,
            }), "the five weapons of §6; the shield counts as one");
        }

        [Test]
        public void TheSeveredLimbClubAndTheBodyBlow_AreNeverOptions()
        {
            List<WeaponId> options = LoadoutOptions.Pickable(_settings, new AllWeaponsOwned());

            Assert.That(options, Has.No.Member(WeaponIds.SeveredLimb));
            Assert.That(options, Has.No.Member(WeaponIds.BodyBlow));
        }

        [Test]
        public void OnlyTheOwnedWeapons_AreOptions()
        {
            var owned = new OwnedWeapons(WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield);

            Assert.That(LoadoutOptions.Pickable(_settings, owned),
                Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }));
        }

        [Test]
        public void TheDefault_IsRapierMaceAndShield_AndValid()
        {
            List<WeaponId> loadout = LoadoutOptions.DefaultFor(_settings, new AllWeaponsOwned());

            Assert.That(loadout, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }), "D8");
            var issues = new List<string>();
            LoadoutValidator.Validate(loadout, _settings, issues);
            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void ADefaultWeaponThePlayerDoesNotOwn_IsReplacedFromTheOtherOptions()
        {
            var owned = new OwnedWeapons(WeaponIds.Rapier, WeaponIds.Sword, WeaponIds.Spear, WeaponIds.Shield);

            List<WeaponId> loadout = LoadoutOptions.DefaultFor(_settings, owned);

            Assert.That(loadout, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Shield, WeaponIds.Sword }),
                "the default's owned entries first, then the rest in list order");
            var issues = new List<string>();
            LoadoutValidator.Validate(loadout, _settings, owned, issues);
            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void WhenAShieldIsRequired_TheDefaultKeepsOne()
        {
            _settings.Loadout.RequireShieldSlot = true;
            _settings.Loadout.DefaultLoadoutWeaponIds = new List<string> { "rapier", "mace", "sword" };

            List<WeaponId> loadout = LoadoutOptions.DefaultFor(_settings, new AllWeaponsOwned());

            Assert.That(loadout, Has.Count.EqualTo(3));
            Assert.That(loadout, Contains.Item(WeaponIds.Shield));
            var issues = new List<string>();
            LoadoutValidator.Validate(loadout, _settings, issues);
            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void TooFewOwnedWeapons_CannotFormALoadout()
        {
            Assert.That(LoadoutOptions.CanFormALoadout(_settings, new AllWeaponsOwned()), Is.True);
            Assert.That(LoadoutOptions.CanFormALoadout(_settings, new OwnedWeapons(WeaponIds.Rapier, WeaponIds.Mace)), Is.False);
        }

        [Test]
        public void ARequiredShieldThePlayerDoesNotOwn_CannotFormALoadout()
        {
            var noShield = new OwnedWeapons(WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Sword);
            Assert.That(LoadoutOptions.CanFormALoadout(_settings, noShield), Is.True, "D8 default: no shield required");

            _settings.Loadout.RequireShieldSlot = true;

            Assert.That(LoadoutOptions.CanFormALoadout(_settings, noShield), Is.False);
        }
    }
}
