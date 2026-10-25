using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Economy;
using XRim.Profile;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Profile
{
    /// <summary>
    /// What the profile brings into a match (Session 18): the first-launch handedness reaches the fighter setup, the weapons and moves come
    /// from what the account owns.
    /// </summary>
    public sealed class ProfileSetupsTests
    {
        private static readonly WeaponId[] Loadout = { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield };

        private readonly ShopCatalogue _catalogue = ShopCatalogue.From(new EconomySettings());
        private PlayerProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = PlayerProfile.CreateNew(_catalogue);
        }

        [Test]
        public void TheFirstLaunchPick_FlowsIntoTheFighterSetup()
        {
            _profile.ChooseHandedness(Handedness.Left);

            FighterSetup fighter = ProfileSetups.ForPlayer(_profile, Loadout, _catalogue);

            Assert.That(fighter.Handedness, Is.EqualTo(Handedness.Left));
            Assert.That(fighter.Loadout, Is.EqualTo(Loadout));
        }

        [Test]
        public void ChangingHandednessLaterInSettings_ReachesTheNextMatch()
        {
            _profile.ChooseHandedness(Handedness.Left);
            Assert.That(ProfileSetups.ForPlayer(_profile, Loadout, _catalogue).Handedness, Is.EqualTo(Handedness.Left));

            _profile.ChooseHandedness(Handedness.Right);

            Assert.That(ProfileSetups.ForPlayer(_profile, Loadout, _catalogue).Handedness, Is.EqualTo(Handedness.Right));
        }

        [Test]
        public void BeforeThePickIsMade_TheDummyHoldsItsWeaponInTheRight()
        {
            Assert.That(_profile.HasChosenHandedness, Is.False);
            Assert.That(ProfileSetups.ForPlayer(_profile, Loadout, _catalogue).Handedness, Is.EqualTo(Handedness.Right));
        }

        [Test]
        public void ABot_IsAlwaysRightHanded_WhateverThePlayerPicked()
        {
            _profile.ChooseHandedness(Handedness.Left);

            Assert.That(ProfileSetups.ForBot(_profile, Loadout, _catalogue).Handedness, Is.EqualTo(Handedness.Right));
        }

        [Test]
        public void ThePick_SurvivesQuittingTheGame()
        {
            string folder = Path.Combine(Path.GetTempPath(), "XRimProfileTests-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var codec = new XmlProfileTextCodec();
                _profile.ChooseHandedness(Handedness.Left);
                new ProfileStore(folder, codec, _catalogue).Save(_profile);

                PlayerProfile next = new ProfileStore(folder, codec, _catalogue).Load().Profile;

                Assert.That(next.HasChosenHandedness, Is.True, "the pick screen is shown only once");
                Assert.That(ProfileSetups.ForPlayer(next, Loadout, _catalogue).Handedness, Is.EqualTo(Handedness.Left));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void ANewPlayer_BringsOnlyTheStarterMove()
        {
            Assert.That(ProfileSetups.OwnedSignatureMoves(_profile, _catalogue), Is.EqualTo(new[] { SignatureMoveIds.FiveFingerExplodingTouch }));
            Assert.That(ProfileSetups.ForPlayer(_profile, Loadout, _catalogue).Signatures, Is.EqualTo(new[] { SignatureMoveIds.FiveFingerExplodingTouch }),
                "an owned list, never null: null would mean the whole catalogue");
        }

        [Test]
        public void BoughtMoves_AreBroughtInTheShopsOrder_NotTheOrderTheyWereBought()
        {
            _profile.Inventory.Grant(GameplayUnlockKind.SignatureMove, SignatureMoveIds.WhiplashCalibrationSpin.Value);
            _profile.Inventory.Grant(GameplayUnlockKind.SignatureMove, SignatureMoveIds.HeadImpactSimulation404.Value);

            Assert.That(ProfileSetups.OwnedSignatureMoves(_profile, _catalogue), Is.EqualTo(new[]
            {
                SignatureMoveIds.FiveFingerExplodingTouch, SignatureMoveIds.HeadImpactSimulation404, SignatureMoveIds.WhiplashCalibrationSpin,
            }));
        }

        [Test]
        public void ABot_BringsTheSameMovesTheAccountOwns()
        {
            _profile.Inventory.Grant(GameplayUnlockKind.SignatureMove, SignatureMoveIds.HeadImpactSimulation404.Value);

            Assert.That(ProfileSetups.ForBot(_profile, Loadout, _catalogue).Signatures,
                Is.EqualTo(ProfileSetups.ForPlayer(_profile, Loadout, _catalogue).Signatures));
        }

        [Test]
        public void AMoveSomeoneElseOwns_IsNotBroughtJustBecauseItIsInTheGame()
        {
            Assert.That(ProfileSetups.OwnedSignatureMoves(_profile, _catalogue), Has.No.Member(SignatureMoveIds.WhiplashCalibrationSpin));
        }

        [Test]
        public void ANewPlayer_OwnsTheRapierMaceAndShield_NotTheSwordOrSpear()
        {
            var ownership = new ProfileWeaponOwnership(_profile.Inventory);

            Assert.That(ownership.Owns(WeaponIds.Rapier), Is.True);
            Assert.That(ownership.Owns(WeaponIds.Mace), Is.True);
            Assert.That(ownership.Owns(WeaponIds.Shield), Is.True, "the shield is its own kind of unlock but a weapon to the rules");
            Assert.That(ownership.Owns(WeaponIds.Sword), Is.False);
            Assert.That(ownership.Owns(WeaponIds.Spear), Is.False);
        }

        [Test]
        public void ANewPlayersPicker_OffersTheThreeStartingWeapons_AndABoughtWeaponJoinsThem()
        {
            RulesSettings rules = GddStartingValues.CreateRulesSettings();
            var ownership = new ProfileWeaponOwnership(_profile.Inventory);

            Assert.That(LoadoutOptions.Pickable(rules, ownership), Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }));
            Assert.That(LoadoutOptions.CanFormALoadout(rules, ownership), Is.True, "three weapons fill the three slots from the first launch");

            _profile.Wallet.Add(new EarnedAmount(300));
            new Shop(_profile.Wallet, _profile.Inventory).Buy(_catalogue.FindUnlock(GameplayUnlockKind.Weapon, "sword"));

            Assert.That(LoadoutOptions.Pickable(rules, ownership), Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Sword, WeaponIds.Mace, WeaponIds.Shield }));
        }

        [Test]
        public void TheClubAndTheBodyBlow_AreNeverOwned()
        {
            var ownership = new ProfileWeaponOwnership(_profile.Inventory);

            Assert.That(ownership.Owns(WeaponIds.SeveredLimb), Is.False);
            Assert.That(ownership.Owns(WeaponIds.BodyBlow), Is.False);
        }

        [Test]
        public void AFighterSetup_HoldsNoCosmetics()
        {
            // Cosmetics never reach the rules (GDD §16): the setup type has nowhere to put one.
            var names = typeof(FighterSetup).GetProperties().Select(property => property.Name).ToList();

            Assert.That(names, Is.EquivalentTo(new List<string> { "Handedness", "Loadout", "Signatures" }));
        }
    }
}
