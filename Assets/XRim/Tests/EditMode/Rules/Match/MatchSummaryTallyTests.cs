using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Match;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>The facts about a finished match that Economy may use (GDD §16): hits landed and shield blocks, counted from events.</summary>
    public sealed class MatchSummaryTallyTests
    {
        private static MatchEvent Hit(Side attacker) => new HitLandedEvent(SimTime.Zero,
            new HitFacts(attacker, BodyPart.Torso, WeaponIds.Rapier, SimTime.Zero, false), new DamageResult(8f, 8f, 0f, false, false));

        private static MatchEvent Block(Side blocker, bool full = true) => new ShieldBlockEvent(SimTime.Zero, blocker,
            new BlockResult(full, full ? 0f : 0.5f, false), Vec2.Zero, 90f, 0f);

        private static readonly MatchOutcome Outcome = new MatchOutcome(Side.Left, MatchEndReason.KnockOut, 7);

        [Test]
        public void NewTally_IsAllZero()
        {
            MatchSummary summary = new MatchSummaryTally().ToSummary(Outcome);

            Assert.That(summary.HitsLanded.Left + summary.HitsLanded.Right, Is.EqualTo(0));
            Assert.That(summary.SuccessfulParries.Left + summary.SuccessfulParries.Right, Is.EqualTo(0));
            Assert.That(summary.Outcome, Is.SameAs(Outcome));
        }

        [Test]
        public void AHit_CountsForTheSideThatStruck()
        {
            var tally = new MatchSummaryTally();

            tally.Add(Hit(Side.Left));
            tally.Add(Hit(Side.Left));
            tally.Add(Hit(Side.Right));

            Assert.That(tally.HitsLanded.Left, Is.EqualTo(2));
            Assert.That(tally.HitsLanded.Right, Is.EqualTo(1));
        }

        [Test]
        public void AShieldBlock_CountsForTheSideHoldingTheShield_FullOrPartial()
        {
            var tally = new MatchSummaryTally();

            tally.Add(Block(Side.Right));
            tally.Add(Block(Side.Right, full: false));
            tally.Add(Block(Side.Left));

            Assert.That(tally.SuccessfulParries.Right, Is.EqualTo(2));
            Assert.That(tally.SuccessfulParries.Left, Is.EqualTo(1));
            Assert.That(tally.HitsLanded.Left + tally.HitsLanded.Right, Is.EqualTo(0), "A block is not a hit");
        }

        [Test]
        public void OtherEvents_AreIgnored()
        {
            var tally = new MatchSummaryTally();

            tally.Add(new FighterDiedEvent(SimTime.Zero, Side.Right));

            Assert.That(tally.HitsLanded.Left + tally.HitsLanded.Right + tally.SuccessfulParries.Left + tally.SuccessfulParries.Right, Is.EqualTo(0));
        }

        [Test]
        public void AddTurn_AddsEveryEventOfAResolvedTurn()
        {
            var tally = new MatchSummaryTally();

            tally.AddTurn(new[] { Hit(Side.Left), Block(Side.Right), Hit(Side.Right) });
            tally.AddTurn(new[] { Hit(Side.Left) });

            MatchSummary summary = tally.ToSummary(Outcome);
            Assert.That(summary.HitsLanded.Left, Is.EqualTo(2));
            Assert.That(summary.HitsLanded.Right, Is.EqualTo(1));
            Assert.That(summary.SuccessfulParries.Right, Is.EqualTo(1));
        }

        [Test]
        public void ASummaryAlreadyMade_IsNotChangedByLaterEvents()
        {
            var tally = new MatchSummaryTally();
            tally.Add(Hit(Side.Left));
            MatchSummary summary = tally.ToSummary(Outcome);

            tally.Add(Hit(Side.Left));

            Assert.That(summary.HitsLanded.Left, Is.EqualTo(1));
            Assert.That(tally.HitsLanded.Left, Is.EqualTo(2));
        }
    }
}
