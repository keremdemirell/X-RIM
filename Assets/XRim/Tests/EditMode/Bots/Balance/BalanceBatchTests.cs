using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Bots;
using XRim.Bots.Balance;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Tests.EditMode.Networking;
using XRim.Tests.EditMode.Simulation.Scenarios;

namespace XRim.Tests.EditMode.Bots.Balance
{
    /// <summary>
    /// The bot-vs-bot balance batch (Session 16) without physics: which runs a spec makes, what a match record counts from a turn's events,
    /// whole batches on a scripted simulator (sides swap, durability scaled, random valid loadouts), and the report's verdicts and weapon flags.
    /// </summary>
    public sealed class BalanceBatchTests
    {
        private RulesSettings _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
        }

        private static readonly BalanceTargets Targets = new BalanceTargets(0.1f, 0.2f, 0f, 6f);

        // --- The spec ----------------------------------------------------------------------------

        [Test]
        public void TheDefaultSpec_PlaysEachMatchup_ThenSweepsTheMirrorMatchup()
        {
            List<BalanceRun> runs = new BalanceBatchSpec().Runs();

            Assert.That(runs.Select(r => r.Label), Is.EqualTo(new[]
            {
                "Heuristic vs Random", "Heuristic vs Heuristic", "Heuristic vs Heuristic, limb durability ×0.6",
                "Heuristic vs Heuristic, limb durability ×0.8", "Heuristic vs Heuristic, limb durability ×1.25",
            }));
            Assert.That(new BalanceBatchSpec().TotalMatches, Is.EqualTo(5 * BalanceBatchSpec.DefaultMatchesPerRun));
        }

        [Test]
        public void WithoutTheSweep_OrWithFactorOne_NoExtraRunIsPlayed()
        {
            Assert.That(new BalanceBatchSpec { SweepDurability = false }.Runs(), Has.Count.EqualTo(2));
            Assert.That(new BalanceBatchSpec { DurabilityFactors = new List<float> { 1f } }.Runs(), Has.Count.EqualTo(2));
            Assert.That(new BalanceBatchSpec { Matchups = new List<BalanceMatchup>() }.Runs(), Is.Empty);
        }

        // --- One match's record ------------------------------------------------------------------

        private TurnResult Turn(params MatchEvent[] events)
        {
            BoardSnapshot board = TestData.CreateInitialBoard(_rules);
            var frames = new List<TimelineFrame> { new TimelineFrame(0, SimTime.Zero, board.Pose), new TimelineFrame(240, SimTime.FromSeconds(1.0), board.Pose) };
            var report = new ExecutionReport(board.State, new PerSide<SimTime?>(null, null), false);
            return new TurnResult(0, new TurnTimeline(frames, new List<MatchEvent>(events)), board, report);
        }

        private static HitLandedEvent Hit(Side attacker, WeaponId weapon, BodyPart part, float damage, double milliseconds, bool severs = false) =>
            new HitLandedEvent(SimTime.FromMilliseconds(milliseconds), new HitFacts(attacker, part, weapon, SimTime.FromMilliseconds(milliseconds), false),
                new DamageResult(damage, damage, part.IsSeverable() ? damage : 0f, false, severs));

        private BalanceMatchRecord Record(int seed = 1, BalanceRun run = null) => new BalanceMatchRecord(seed,
            run ?? new BalanceRun(new BalanceMatchup(BotBrainKind.Heuristic, BotBrainKind.Random), 1f),
            new PerSide<BotBrainKind>(BotBrainKind.Heuristic, BotBrainKind.Random),
            new PerSide<IReadOnlyList<WeaponId>>(new[] { WeaponIds.Rapier, WeaponIds.Sword, WeaponIds.Shield }, new[] { WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield }));

        [Test]
        public void ATurn_IsCounted_SwingsHitsDamageSeversZapsAndSignatures()
        {
            BalanceMatchRecord record = Record();

            record.Add(ScenarioTestData.TurnWithEveryEvent(), 12.5, 1.25);

            WeaponTally rapier = record.WeaponsOf(Side.Left)[WeaponIds.Rapier];
            Assert.That(rapier.Hits, Is.EqualTo(1));
            Assert.That(rapier.Damage, Is.EqualTo(6.4f));
            Assert.That(rapier.Severs, Is.EqualTo(1), "the hit that severed the arm");
            WeaponTally mace = record.WeaponsOf(Side.Right)[WeaponIds.Mace];
            Assert.That(mace.Swings, Is.EqualTo(1));
            Assert.That(mace.SignatureSwings, Is.EqualTo(1), "the right played its signature move with the mace");
            Assert.That(mace.SignatureHitTurns, Is.EqualTo(0), "and landed nothing");
            Assert.That(record.SignatureUses[SignatureMoveIds.HeadImpactSimulation404], Is.EqualTo(1));
            Assert.That(record.SignatureHitTurns.ContainsKey(SignatureMoveIds.HeadImpactSimulation404), Is.False);
            Assert.That(record.ArmSevers, Is.EqualTo(1));
            Assert.That(record.WallZaps, Is.EqualTo(1));
            Assert.That(record.ReachedSuddenDeath, Is.True, "the rich board is in sudden death");
            Assert.That(record.Turns, Is.EqualTo(1));
            Assert.That(record.SimulationMilliseconds, Is.EqualTo(new[] { 12.5 }));
            Assert.That(record.OnScreenSeconds, Is.EqualTo(1.25));
            Assert.That(record.ExecutedSeconds, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void TheWinningBlow_IsTheWinnersLastHitBeforeTheDeath()
        {
            BalanceMatchRecord record = Record();
            TurnResult last = Turn(
                Hit(Side.Left, WeaponIds.Sword, BodyPart.Torso, 12f, 100.0),
                Hit(Side.Left, WeaponIds.Rapier, BodyPart.Head, 20f, 300.0),
                new FighterDiedEvent(SimTime.FromMilliseconds(300.0), Side.Right),
                Hit(Side.Left, WeaponIds.Sword, BodyPart.Torso, 12f, 500.0));
            record.Add(last, 1.0, 1.0);

            record.Finish(new MatchOutcome(Side.Left, MatchEndReason.KnockOut, 1), last);

            Assert.That(record.WeaponsOf(Side.Left)[WeaponIds.Rapier].Kos, Is.EqualTo(1));
            Assert.That(record.WeaponsOf(Side.Left)[WeaponIds.Sword].Kos, Is.EqualTo(0));
            Assert.That(record.WinningBrain, Is.EqualTo(BotBrainKind.Heuristic));
            Assert.That(record.EndedByWall, Is.False);
        }

        [Test]
        public void ADeathWithoutAHit_IsAWallEnding()
        {
            BalanceMatchRecord record = Record();
            TurnResult last = Turn(new FighterDiedEvent(SimTime.FromMilliseconds(200.0), Side.Right));
            record.Add(last, 1.0, 1.0);

            record.Finish(new MatchOutcome(Side.Left, MatchEndReason.KnockOut, 1), last);

            Assert.That(record.EndedByWall, Is.True);
        }

        // --- Whole batches on a scripted simulator ----------------------------------------------------

        private BalanceBatch Batch(BalanceBatchSpec spec, float rightLosesPerTurn = 40f) => new BalanceBatch(spec, GddStartingValues.CreateRulesSettings,
            new SimulationSettings(), new BotSettings(), new ScriptedTurnSimulator((turn, state) => state.Fighters[Side.Right].Hp -= rightLosesPerTurn));

        private static void PlayAll(BalanceBatch batch)
        {
            for (int step = 0; step < 10000 && batch.Step(); step++)
            {
            }
        }

        [Test]
        public void ABatch_PlaysEveryRun_WithTheSameSeeds_AndScalesDurability()
        {
            var spec = new BalanceBatchSpec { MatchesPerRun = 3, DurabilityFactors = new List<float> { 0.5f } };
            using (BalanceBatch batch = Batch(spec))
            {
                PlayAll(batch);

                Assert.That(batch.IsDone, Is.True);
                Assert.That(batch.Step(), Is.False);
                Assert.That(batch.MatchesDone, Is.EqualTo(9));
                Assert.That(batch.Progress, Is.EqualTo(1f));
                Assert.That(batch.Results, Has.Count.EqualTo(3));
                foreach (BalanceRunResult result in batch.Results)
                {
                    Assert.That(result.Records.Select(r => r.Seed), Is.EqualTo(new[] { 1, 2, 3 }), result.Run.Label);
                    Assert.That(result.Records.All(r => r.Finished), Is.True, result.Run.Label);
                }

                Assert.That(batch.Results[2].ArmDurability, Is.EqualTo(_rules.Damage.ArmDurability * 0.5f));
                Assert.That(batch.Results[2].LegDurability, Is.EqualTo(_rules.Damage.LegDurability * 0.5f));
                Assert.That(batch.Results[0].ArmDurability, Is.EqualTo(_rules.Damage.ArmDurability));
            }
        }

        [Test]
        public void TheBrains_SwapSidesEveryMatch()
        {
            var spec = new BalanceBatchSpec { MatchesPerRun = 4, SweepDurability = false, Matchups = new List<BalanceMatchup> { new BalanceMatchup() } };
            using (BalanceBatch batch = Batch(spec))
            {
                PlayAll(batch);

                IReadOnlyList<BalanceMatchRecord> records = batch.Results[0].Records;
                Assert.That(records.Select(r => r.Brains.Left), Is.EqualTo(new[]
                {
                    BotBrainKind.Heuristic, BotBrainKind.Random, BotBrainKind.Heuristic, BotBrainKind.Random,
                }));
                Assert.That(records.All(r => r.Outcome.Winner == Side.Left), Is.True, "the script only hurts the right");
                Assert.That(records.Count(r => r.WinningBrain == BotBrainKind.Heuristic), Is.EqualTo(2));
            }
        }

        [Test]
        public void RandomLoadouts_AreValid_AndDiffer_TheDefaultOnesAreTheDefault()
        {
            var spec = new BalanceBatchSpec { MatchesPerRun = 12, SweepDurability = false, Matchups = new List<BalanceMatchup> { new BalanceMatchup() } };
            using (BalanceBatch batch = Batch(spec))
            {
                PlayAll(batch);

                var seen = new HashSet<string>();
                foreach (BalanceMatchRecord record in batch.Results[0].Records)
                {
                    foreach (Side side in new[] { Side.Left, Side.Right })
                    {
                        IReadOnlyList<WeaponId> loadout = record.Loadouts[side];
                        Assert.That(loadout, Has.Count.EqualTo(RuleConstants.LoadoutSlotCount));
                        Assert.That(loadout.Distinct().Count(), Is.EqualTo(loadout.Count));
                        Assert.That(loadout.All(id => _rules.FindWeapon(id) != null && id != WeaponIds.SeveredLimb && id != WeaponIds.BodyBlow), Is.True);
                        seen.Add(string.Join(",", loadout));
                    }
                }

                Assert.That(seen.Count, Is.GreaterThan(3), "random picks vary from match to match");
            }

            var fixedSpec = new BalanceBatchSpec { MatchesPerRun = 2, SweepDurability = false, RandomLoadouts = false, Matchups = new List<BalanceMatchup> { new BalanceMatchup() } };
            using (BalanceBatch batch = Batch(fixedSpec))
            {
                PlayAll(batch);
                Assert.That(batch.Results[0].Records[0].Loadouts.Left.Select(id => id.Value), Is.EqualTo(_rules.Loadout.DefaultLoadoutWeaponIds));
            }
        }

        [Test]
        public void AMatchThatNeverEnds_StopsAtTheTurnLimit()
        {
            var spec = new BalanceBatchSpec
            {
                MatchesPerRun = 1, TurnLimit = 5, SweepDurability = false, Matchups = new List<BalanceMatchup> { new BalanceMatchup() },
            };
            using (BalanceBatch batch = Batch(spec, 0f))
            {
                PlayAll(batch);

                BalanceMatchRecord record = batch.Results[0].Records[0];
                Assert.That(record.Turns, Is.LessThanOrEqualTo(5));
                Assert.That(batch.IsDone, Is.True);
            }
        }

        // --- The report ------------------------------------------------------------------------------

        [Test]
        public void TheReport_JudgesTheTargets_AndShowsTheSweep()
        {
            var spec = new BalanceBatchSpec { MatchesPerRun = 3, DurabilityFactors = new List<float> { 0.5f } };
            using (BalanceBatch batch = Batch(spec))
            {
                PlayAll(batch);

                string report = BalanceReport.Write(batch.Results, spec, Targets, _rules, new[] { "a test heading" });

                Assert.That(report, Does.Contain("# X-RIM bot-vs-bot balance batch"));
                Assert.That(report, Does.Contain("- a test heading"));
                Assert.That(report, Does.Contain("**Heuristic against Random:**"));
                Assert.That(report, Does.Contain("0 of 3 matches had a sever (0%)"));
                Assert.That(report, Does.Contain("below the target"));
                Assert.That(report, Does.Contain("not decided yet"), "§1 stays TBD: measured, not judged");
                Assert.That(report, Does.Contain("Limb durability sweep"));
                Assert.That(report, Does.Contain("| ×0.5 |"));
                Assert.That(report, Does.Contain("### Weapons"));
                Assert.That(report, Does.Contain("### Matches"));
                Assert.That(report, Does.Not.Contain("**Partial:**"));
                Assert.That(BalanceReport.Write(batch.Results, spec, Targets, _rules, null, true), Does.Contain("**Partial:**"));
            }
        }

        [Test]
        public void ASeverRateInsideTheRange_IsWithinTheTarget()
        {
            var run = new BalanceRun(new BalanceMatchup(BotBrainKind.Heuristic, BotBrainKind.Heuristic), 1f);
            var result = new BalanceRunResult(run, 40f, 50f);
            for (int i = 0; i < 10; i++)
            {
                BalanceMatchRecord record = Record(i + 1, run);
                record.Add(i < 2 ? ScenarioTestData.TurnWithEveryEvent() : Turn(), 1.0, 1.0);
                record.Finish(new MatchOutcome(Side.Left, MatchEndReason.KnockOut, 1), null);
                result.Add(record);
            }

            string report = BalanceReport.Write(new[] { result }, new BalanceBatchSpec { MatchesPerRun = 10 }, Targets, _rules, null);

            Assert.That(report, Does.Contain("2 of 10 matches had a sever (20%)"));
            Assert.That(report, Does.Contain("within the target"));
        }

        [Test]
        public void AWeaponThatAlwaysWins_AndHitsHardest_IsFlaggedStrictlyStronger()
        {
            var run = new BalanceRun(new BalanceMatchup(BotBrainKind.Heuristic, BotBrainKind.Heuristic), 1f);
            var result = new BalanceRunResult(run, 40f, 50f);
            for (int i = 0; i < 40; i++)
            {
                BalanceMatchRecord record = Record(i + 1, run);
                TurnResult turn = Turn(
                    new AttackStartedEvent(SimTime.Zero, Side.Left, WeaponIds.Rapier, WeaponKind.Weapon),
                    new AttackStartedEvent(SimTime.Zero, Side.Right, WeaponIds.Mace, WeaponKind.Weapon),
                    Hit(Side.Left, WeaponIds.Rapier, BodyPart.Torso, 8f, 200.0));
                record.Add(turn, 1.0, 1.0);
                record.Finish(new MatchOutcome(Side.Left, MatchEndReason.KnockOut, 1), turn);
                result.Add(record);
            }

            string report = BalanceReport.Write(new[] { result }, new BalanceBatchSpec { MatchesPerRun = 40 }, Targets, _rules, null);

            Assert.That(report, Does.Contain("**rapier looks strictly stronger**"), report);
            Assert.That(report, Does.Contain("sword wins more often when carried"), "it wins too, but deals no damage: strong, not strictly stronger");
            Assert.That(report, Does.Contain("mace looks weak"));
        }
    }
}
