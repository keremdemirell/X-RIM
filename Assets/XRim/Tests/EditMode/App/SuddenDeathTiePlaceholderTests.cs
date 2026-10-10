using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Recording;
using XRim.Tests.EditMode.Presentation;

namespace XRim.Tests.EditMode.App
{
    /// <summary>
    /// Only a sudden-death turn that both dummies end dead stops the first playable (the rules would repeat it forever at 0 HP until
    /// Session 12 decides the tie, §14). Every other turn plays on.
    /// </summary>
    public sealed class SuddenDeathTiePlaceholderTests
    {
        private RulesSettings _settings;

        [SetUp]
        public void SetUp() => _settings = GddStartingValues.CreateRulesSettings();

        private TurnResult Turn(bool suddenDeath, float leftHp, float rightHp)
        {
            TurnResult turn = PlayedTurn.Create(_settings, 31);
            turn.Report.ResolvedState.IsSuddenDeath = suddenDeath;
            turn.Report.ResolvedState.Fighters[Side.Left].Hp = leftHp;
            turn.Report.ResolvedState.Fighters[Side.Right].Hp = rightHp;
            return turn;
        }

        [Test]
        public void BothFallingInSuddenDeath_StopsTheMatch()
        {
            Assert.That(SuddenDeathTiePlaceholder.StopsTheMatch(Turn(true, 0f, 0f)), Is.True);
        }

        [Test]
        public void OneFallingInSuddenDeath_IsAWin_NotAStop()
        {
            Assert.That(SuddenDeathTiePlaceholder.StopsTheMatch(Turn(true, 0f, 1f)), Is.False);
        }

        [Test]
        public void NobodyHittingInSuddenDeath_PlaysOn()
        {
            Assert.That(SuddenDeathTiePlaceholder.StopsTheMatch(Turn(true, 1f, 1f)), Is.False);
        }

        [Test]
        public void ADoubleKoInANormalTurn_StartsSuddenDeath_NotAStop()
        {
            Assert.That(SuddenDeathTiePlaceholder.StopsTheMatch(Turn(false, 0f, 0f)), Is.False);
        }

        [Test]
        public void NoTurnYet_StopsNothing()
        {
            Assert.That(SuddenDeathTiePlaceholder.StopsTheMatch(null), Is.False);
        }
    }
}
