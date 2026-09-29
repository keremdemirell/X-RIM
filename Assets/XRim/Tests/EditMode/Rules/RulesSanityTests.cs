using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules
{
    public sealed class RulesSanityTests
    {
        [Test]
        public void StartingValues_PassValidation()
        {
            var issues = new List<string>();
            GddStartingValues.CreateRulesSettings().Validate(issues);
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        [Test]
        public void Validation_RejectsEqualClashWeights()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.Clash.SpeedWeight = settings.Clash.MassWeight;
            var issues = new List<string>();
            settings.Validate(issues);
            Assert.That(issues, Has.Some.Contains("§10"));
        }

        [Test]
        public void OnlyArmsAndLegs_CanBeSevered()
        {
            Assert.That(BodyPart.Head.IsSeverable(), Is.False);
            Assert.That(BodyPart.Torso.IsSeverable(), Is.False);
            Assert.That(BodyPart.LeftArm.IsSeverable(), Is.True);
            Assert.That(BodyPart.RightLeg.IsSeverable(), Is.True);
        }

        [Test]
        public void InitialMatchState_StartsAtFullHpWithFirstLoadoutWeapon()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            MatchState state = MatchState.CreateInitial(TestData.CreateMatchSetup(), settings);

            Assert.That(state.TurnIndex, Is.EqualTo(0));
            Assert.That(state.Fighters[Side.Left].Hp, Is.EqualTo(settings.Damage.MaxHp));
            Assert.That(state.Fighters[Side.Left].CurrentWeapon, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(state.Fighters[Side.Right].CurrentWeapon, Is.EqualTo(WeaponIds.Mace));
        }

        [Test]
        public void MatchStateClone_IsIndependent()
        {
            MatchState original = MatchState.CreateInitial(TestData.CreateMatchSetup(), GddStartingValues.CreateRulesSettings());
            MatchState copy = original.Clone();
            copy.Fighters[Side.Left].Hp = 1f;
            copy.Fighters[Side.Left].MarkSevered(BodyPart.LeftArm);

            Assert.That(original.Fighters[Side.Left].Hp, Is.Not.EqualTo(1f));
            Assert.That(original.Fighters[Side.Left].IsSevered(BodyPart.LeftArm), Is.False);
        }
    }
}
