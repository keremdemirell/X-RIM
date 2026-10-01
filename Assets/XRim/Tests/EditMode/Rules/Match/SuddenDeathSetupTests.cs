using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.SuddenDeath;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>The transition into sudden death: only what §14 decides (1 HP); the rest is Session 12.</summary>
    public sealed class SuddenDeathSetupTests
    {
        private static MatchState NewState() =>
            MatchState.CreateInitial(TestData.CreateMatchSetup(), GddStartingValues.CreateRulesSettings());

        [Test]
        public void BothDummies_AreSetToOneHp_AndTheStateIsMarked()
        {
            MatchState state = NewState();
            state.Fighters[Side.Left].Hp = 0f; // a double KO leaves both at 0
            state.Fighters[Side.Right].Hp = 0f;

            new SuddenDeathSetup().Apply(state);

            Assert.That(state.IsSuddenDeath, Is.True);
            Assert.That(state.Fighters[Side.Left].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            Assert.That(state.Fighters[Side.Right].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
        }

        [Test]
        public void TheTurnCapCase_AlsoSetsBothToOneHp()
        {
            MatchState state = NewState();
            state.Fighters[Side.Left].Hp = 70f;
            state.Fighters[Side.Right].Hp = 12f;

            new SuddenDeathSetup().Apply(state);

            Assert.That(state.Fighters[Side.Left].Hp, Is.EqualTo(1f));
            Assert.That(state.Fighters[Side.Right].Hp, Is.EqualTo(1f));
        }

        [Test]
        public void TheBoardCarriesOver_LimbsWeaponsAndTurnCount()
        {
            MatchState state = NewState();
            state.TurnIndex = 29;
            state.Fighters[Side.Left].SetLimbDamage(BodyPart.LeftArm, 25f);
            state.Fighters[Side.Right].MarkSevered(BodyPart.RightLeg);
            state.Fighters[Side.Right].CurrentWeapon = WeaponIds.Shield;

            new SuddenDeathSetup().Apply(state);

            Assert.That(state.TurnIndex, Is.EqualTo(29), "the machine advances the turn counter, not the setup");
            Assert.That(state.Fighters[Side.Left].GetLimbDamage(BodyPart.LeftArm), Is.EqualTo(25f));
            Assert.That(state.Fighters[Side.Right].IsSevered(BodyPart.RightLeg), Is.True);
            Assert.That(state.Fighters[Side.Right].CurrentWeapon, Is.EqualTo(WeaponIds.Shield));
        }

        [Test]
        public void TheIdleCounters_RestartForTheSuddenDeathTurn()
        {
            MatchState state = NewState();
            state.Fighters[Side.Left].ConsecutiveIdleTurns = RuleConstants.IdleTurnsBeforeForfeit;
            state.Fighters[Side.Right].ConsecutiveIdleTurns = 2;

            new SuddenDeathSetup().Apply(state);

            Assert.That(state.Fighters[Side.Left].ConsecutiveIdleTurns, Is.EqualTo(0));
            Assert.That(state.Fighters[Side.Right].ConsecutiveIdleTurns, Is.EqualTo(0));
        }

        [Test]
        public void AfterTheSetup_ADoubleKoDoesNotTriggerSuddenDeathAgain()
        {
            // The loop the 1 HP reset prevents: both at 0 HP would be a double KO on every later turn.
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            MatchState state = NewState();
            state.Fighters[Side.Left].Hp = 0f;
            state.Fighters[Side.Right].Hp = 0f;
            new SuddenDeathSetup().Apply(state);

            EndCheckResult next = new EndConditionEvaluator().Evaluate(
                new ExecutionReport(state, new PerSide<SimTime?>(null, null), false), settings);

            Assert.That(next.Resolution, Is.Not.EqualTo(TurnResolution.EnterSuddenDeath));
            Assert.That(next.Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn), "nobody has hit yet");
        }
    }
}
