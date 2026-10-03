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
        public void Validation_RejectsADoubledBodyMoveAndANegativeDuration()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.BodyMoves.Add(new BodyMoveStats { Move = BodyMove.Jump, DurationSeconds = -0.1f });
            var issues = new List<string>();
            settings.Validate(issues);
            Assert.That(issues, Has.Some.Contains("'Jump' is listed twice"));
            Assert.That(issues, Has.Some.Contains("'Jump' duration must not be negative"));
        }

        [Test]
        public void WithBodyMoves_SwapsOnlyTheBodyMoves_AndLeavesTheOriginalAlone()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            List<BodyMoveStats> original = settings.BodyMoves;
            List<BodyMoveStats> other = GddStartingValues.CreateBodyMoves();

            RulesSettings copy = settings.WithBodyMoves(other);

            Assert.That(copy.BodyMoves, Is.SameAs(other));
            Assert.That(copy.Paths, Is.SameAs(settings.Paths));
            Assert.That(settings.BodyMoves, Is.SameAs(original));
        }

        [Test]
        public void OnlyTheBackwardSwipe_IsBackward()
        {
            Assert.That(BodyMove.StepBack.IsBackward(), Is.True, "§13: the backward swipe triggers the wall, a lean in place included");
            foreach (BodyMove move in new[] { BodyMove.None, BodyMove.Crouch, BodyMove.Lunge, BodyMove.Jump })
            {
                Assert.That(move.IsBackward(), Is.False, move.ToString());
            }
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
