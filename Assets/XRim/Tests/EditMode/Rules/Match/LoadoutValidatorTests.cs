using System.Collections.Generic;
using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>The picked-loadout rules of GDD §6, with D8's shield flag.</summary>
    public sealed class LoadoutValidatorTests
    {
        private RulesSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
        }

        private List<string> Check(params WeaponId[] loadout)
        {
            var issues = new List<string>();
            LoadoutValidator.Validate(loadout, _settings, issues);
            return issues;
        }

        [Test]
        public void ThePrototypeLoadout_IsValid()
        {
            Assert.That(Check(WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield), Is.Empty, "D8: rapier, mace, shield");
        }

        [Test]
        public void TheDefaultLoadoutInTheSettings_IsValid()
        {
            var ids = new List<WeaponId>();
            foreach (string id in _settings.Loadout.DefaultLoadoutWeaponIds) ids.Add(new WeaponId(id));

            Assert.That(Check(ids.ToArray()), Is.Empty);
        }

        [Test]
        public void TheSlotCount_IsExactlyThree()
        {
            Assert.That(Check(WeaponIds.Rapier, WeaponIds.Mace), Has.Count.EqualTo(1));
            Assert.That(Check(WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield, WeaponIds.Sword), Has.Count.EqualTo(1));
            Assert.That(Check(), Has.Count.EqualTo(1));
        }

        [Test]
        public void AnUnknownWeapon_IsReported()
        {
            Assert.That(Check(WeaponIds.Rapier, WeaponIds.Mace, new WeaponId("bogus")), Has.Some.Contains("bogus"));
        }

        [Test]
        public void AWeaponPickedTwice_IsReported()
        {
            Assert.That(Check(WeaponIds.Rapier, WeaponIds.Rapier, WeaponIds.Mace), Has.Some.Contains("twice"));
        }

        [Test]
        public void TheShieldSlot_IsOnlyRequiredWhenTheFlagSaysSo()
        {
            WeaponId[] noShield = { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Sword };

            Assert.That(Check(noShield), Is.Empty, "D8 default: no required shield slot");

            _settings.Loadout.RequireShieldSlot = true;
            Assert.That(Check(noShield), Has.Some.Contains("shield"));
            Assert.That(Check(WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield), Is.Empty);
        }
    }
}
