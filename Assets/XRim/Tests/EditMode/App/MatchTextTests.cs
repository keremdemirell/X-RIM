using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Rules.Match;

namespace XRim.Tests.EditMode.App
{
    /// <summary>The words the match scene shows: names per mode, the picker, the cover screen and the match-over screen.</summary>
    public sealed class MatchTextTests
    {
        [Test]
        public void ThePicker_OffersHotSeatBotAndWatch_InThatOrder()
        {
            Assert.That(MatchLabels.PickerModes(), Is.EqualTo(new[] { MatchMode.HotSeat, MatchMode.HumanVsBot, MatchMode.BotVsBot }));
            foreach (MatchMode mode in MatchLabels.PickerModes())
            {
                Assert.That(MatchLabels.Option(mode).Title, Is.Not.Empty);
                Assert.That(MatchLabels.Option(mode).Detail, Is.Not.Empty);
            }
        }

        [Test]
        public void FighterNames_SayWhoIsWho_InEachMode()
        {
            Assert.That(MatchLabels.FighterName(MatchMode.HumanVsBot, Side.Left, Side.Left), Is.EqualTo(MatchLabels.You));
            Assert.That(MatchLabels.FighterName(MatchMode.HumanVsBot, Side.Right, Side.Left), Is.EqualTo("BOT"));
            Assert.That(MatchLabels.FighterName(MatchMode.HumanVsBot, Side.Left, Side.Right), Is.EqualTo("BOT"));
            Assert.That(MatchLabels.FighterName(MatchMode.HotSeat, Side.Left, Side.Left), Is.EqualTo("PLAYER 1"));
            Assert.That(MatchLabels.FighterName(MatchMode.HotSeat, Side.Right, Side.Left), Is.EqualTo("PLAYER 2"));
            Assert.That(MatchLabels.FighterName(MatchMode.BotVsBot, Side.Right, Side.Left), Is.EqualTo("RIGHT BOT"));
        }

        [Test]
        public void TheCover_NamesThePlayerAndTheirTime()
        {
            Assert.That(MatchLabels.CoverPlayer(Side.Right), Is.EqualTo("PLAYER 2 (RIGHT)"));
            Assert.That(MatchLabels.CoverAction(12f), Is.EqualTo("TAP TO START PLANNING (12 s)"));
        }

        [Test]
        public void TheTitle_NamesTheWinner()
        {
            var names = new PerSide<string>(MatchLabels.You, "BOT");

            Assert.That(MatchOverText.Title(new MatchOutcome(Side.Left, MatchEndReason.KnockOut, 5), names), Is.EqualTo("YOU WIN!"));
            Assert.That(MatchOverText.Title(new MatchOutcome(Side.Right, MatchEndReason.KnockOut, 5), names), Is.EqualTo("BOT WINS!"));
        }

        [Test]
        public void TheReason_SaysHowTheMatchEnded()
        {
            var names = new PerSide<string>("PLAYER 1", "PLAYER 2");

            Assert.That(MatchOverText.Reason(new MatchOutcome(Side.Left, MatchEndReason.KnockOut, 5), names), Is.EqualTo("by knockout"));
            Assert.That(MatchOverText.Reason(new MatchOutcome(Side.Left, MatchEndReason.Forfeit, 5), names),
                Is.EqualTo("by forfeit: PLAYER 2 set no move for 3 turns in a row"));
            Assert.That(MatchOverText.Reason(new MatchOutcome(Side.Right, MatchEndReason.SuddenDeathHit, 31), names),
                Is.EqualTo("with the first hit in sudden death"));
            Assert.That(MatchOverText.Reason(new MatchOutcome(Side.Right, MatchEndReason.SuddenDeathMoreHp, 34), names),
                Is.EqualTo("sudden death ran out of tries: more HP when it began"));
            Assert.That(MatchOverText.Reason(new MatchOutcome(Side.Left, MatchEndReason.SuddenDeathCoinToss, 34), names),
                Is.EqualTo("sudden death ran out of tries, level on HP: a coin toss"));
        }

        [Test]
        public void EveryEndReason_HasWords()
        {
            var names = new PerSide<string>("PLAYER 1", "PLAYER 2");
            foreach (MatchEndReason reason in (MatchEndReason[])System.Enum.GetValues(typeof(MatchEndReason)))
            {
                Assert.That(MatchOverText.Reason(new MatchOutcome(Side.Left, reason, 10), names), Is.Not.Empty, reason.ToString());
            }
        }

        [Test]
        public void TheSummary_GivesTurnsAndRealTime()
        {
            Assert.That(MatchOverText.Summary(7, 125.4), Is.EqualTo("7 turns · 2:05"));
            Assert.That(MatchOverText.Summary(1, 9.0), Is.EqualTo("1 turn · 0:09"));
        }
    }
}
