using System.Collections.Generic;
using NUnit.Framework;
using XRim.Bots;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using XRim.Tests.EditMode.Networking;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.Bots
{
    /// <summary>The random bot emits only plans a real planning session accepts, and does it from its seed alone.</summary>
    public sealed class RandomBotBrainTests
    {
        private const int Seeds = 200;

        private RulesSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
        }

        private BotContext Context(PlanningWindow window, uint seed, IReadOnlyList<WeaponId> loadout = null) =>
            new BotContext(Side.Left, window, loadout ?? PlanningTestData.Loadout, _settings, new XorShiftRandom(seed));

        private TurnPlan Plan(PlanningWindow window, uint seed) => new RandomBotBrain().PlanTurn(Context(window, seed));

        /// <summary>Sends the bot's commands for a seed into a real session, the way an authority would.</summary>
        private static List<CommandResult> PlayInto(PlanningSession session, PlanningWindow window, RulesSettings settings, uint seed)
        {
            var results = new List<CommandResult>();
            var bot = new BotPlanSource(Side.Left, new RandomBotBrain(), PlanningTestData.Loadout, settings, new XorShiftRandom(seed));
            bot.OnPlanningStarted(window, new SessionSink(session, results));
            return results;
        }

        [Test]
        public void TheWeapon_AlwaysComesFromTheLoadout_AndEveryOneGetsPicked()
        {
            PlanningWindow window = NetworkingSanityTests.CreateWindow(_settings);
            var picked = new HashSet<WeaponId>();

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                TurnPlan plan = Plan(window, seed);
                Assert.That(PlanningTestData.Loadout, Contains.Item(plan.Weapon), $"seed {seed}");
                picked.Add(plan.Weapon);
            }

            Assert.That(picked, Has.Count.EqualTo(PlanningTestData.Loadout.Length));
        }

        [Test]
        public void WhenSwitchingIsNotAllowed_TheBotKeepsItsWeapon()
        {
            PlanningWindow window = NetworkingSanityTests.CreateWindow(_settings);
            window.Constraints[Side.Left].CanSwitchWeapon = false;

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Assert.That(Plan(window, seed).Weapon, Is.EqualTo(WeaponIds.Rapier), $"seed {seed}");
            }
        }

        [Test]
        public void TheBodyMove_IsRandom_AndAlwaysAllowed()
        {
            PlanningWindow window = NetworkingSanityTests.CreateWindow(_settings);
            window.Constraints[Side.Left].ForbidBodyMove(BodyMove.StepBack);
            var seen = new HashSet<BodyMove>();

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                BodyMove move = Plan(window, seed).BodyMove;
                Assert.That(move, Is.Not.EqualTo(BodyMove.StepBack), $"seed {seed}");
                seen.Add(move);
            }

            Assert.That(seen, Has.Count.EqualTo(4), "none, crouch, lunge and jump all show up");
        }

        [Test]
        public void EveryPlan_IsAcceptedByARealSession_WithThePathInsideTheInkAndReach()
        {
            PlanningWindow window = NetworkingSanityTests.CreateWindow(_settings);
            var validator = new PlanValidator(_settings, new RulePolicies());

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                PlanningSession session = PlanningTestData.CreateSession(_settings, window.Constraints[Side.Left]);

                List<CommandResult> results = PlayInto(session, window, _settings, seed);

                Assert.That(results, Is.Not.Empty);
                foreach (CommandResult result in results) Assert.That(result.Accepted, Is.True, $"seed {seed}: {result.Rejection}");
                Assert.That(session.IsReady, Is.True, $"seed {seed}");
                Assert.That(validator.Validate(session).Accepted, Is.True, $"seed {seed}");
                BuiltPath built = session.CurrentPath;
                Assert.That(built, Is.Not.Null, $"seed {seed}: the bot always draws");
                Assert.That(built.Inked.WasCut, Is.False, $"seed {seed}: inside the ink");
                Assert.That(built.WasClampedByReach, Is.False, $"seed {seed}: inside the reach");
                Assert.That(built.WasCutAtBreak, Is.False, $"seed {seed}");
                Assert.That(session.CurrentPlan.Path.IsEmpty, Is.False, $"seed {seed}");
            }
        }

        [Test]
        public void TheStroke_StillFitsAHalvedInkBudget()
        {
            PlanningWindow window = NetworkingSanityTests.CreateWindow(_settings);
            window.Constraints[Side.Left].InkLengthMultiplier = 0.5f;

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                PlanningSession session = PlanningTestData.CreateSession(_settings, window.Constraints[Side.Left]);

                PlayInto(session, window, _settings, seed);

                Assert.That(session.CurrentPath.Inked.WasCut, Is.False, $"seed {seed}");
                WeaponStats weapon = _settings.FindWeapon(session.Weapon);
                Assert.That(session.CurrentPlan.Path.LengthUnits, Is.LessThanOrEqualTo(weapon.InkLengthUnits * 0.5f), $"seed {seed}");
            }
        }

        [Test]
        public void TheSameSeed_MakesTheSamePlan_AndOtherSeedsDiffer()
        {
            PlanningWindow window = NetworkingSanityTests.CreateWindow(_settings);

            TurnPlan first = Plan(window, 77u);
            TurnPlan again = Plan(window, 77u);

            Assert.That(again.Weapon, Is.EqualTo(first.Weapon));
            Assert.That(again.BodyMove, Is.EqualTo(first.BodyMove));
            Assert.That(again.Path.Points, Is.EqualTo(first.Path.Points));

            var distinctEnds = new HashSet<Vec2>();
            for (uint seed = 1; seed <= 30; seed++)
            {
                WeaponPath path = Plan(window, seed).Path;
                distinctEnds.Add(path.Points[path.Points.Count - 1]);
            }

            Assert.That(distinctEnds.Count, Is.GreaterThan(20), "the strokes vary with the seed");
        }

        [Test]
        public void AnOfferedClub_GetsPicked_AndItsPlansStayValid()
        {
            PlanningWindow window = NetworkingSanityTests.CreateWindow(_settings);
            window.Constraints[Side.Left].AddExtraWeapon(WeaponIds.SeveredLimb);
            var validator = new PlanValidator(_settings, new RulePolicies());
            var picked = new HashSet<WeaponId>();

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                PlanningSession session = PlanningTestData.CreateSession(_settings, window.Constraints[Side.Left]);
                foreach (CommandResult result in PlayInto(session, window, _settings, seed))
                    Assert.That(result.Accepted, Is.True, $"seed {seed}: {result.Rejection}");
                Assert.That(validator.Validate(session).Accepted, Is.True, $"seed {seed}");
                picked.Add(session.Weapon);
            }

            Assert.That(picked, Contains.Item(WeaponIds.SeveredLimb), "§12: the club is a fourth option");
            Assert.That(picked, Has.Count.EqualTo(PlanningTestData.Loadout.Length + 1));
        }

        [Test]
        public void AnArmlessBot_AlwaysBodyBlows_WithinItsReach()
        {
            PlanningWindow window = NetworkingSanityTests.CreateWindow(_settings);
            window.Constraints[Side.Left].CanUseLoadoutWeapons = false;
            window.Constraints[Side.Left].AddExtraWeapon(WeaponIds.BodyBlow);
            window.Board.State.Fighters[Side.Left].CurrentWeapon = WeaponIds.BodyBlow;

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                // As in a match: the session opens on the body blow the rules made the armless dummy's weapon.
                var session = new PlanningSession(Side.Left, WeaponIds.BodyBlow, PlanningTestData.Loadout,
                    new FixedWeaponTipSource(PlanningTestData.Tip), window.Constraints[Side.Left], PlanningTestData.DeadlineSeconds, _settings,
                    new RulePolicies());
                foreach (CommandResult result in PlayInto(session, window, _settings, seed))
                    Assert.That(result.Accepted, Is.True, $"seed {seed}: {result.Rejection}");
                Assert.That(session.Weapon, Is.EqualTo(WeaponIds.BodyBlow), $"seed {seed}");
                Assert.That(session.CurrentPath.WasClampedByReach, Is.False, $"seed {seed}: inside its own reach");
                Assert.That(session.CurrentPath.Inked.WasCut, Is.False, $"seed {seed}");
            }
        }

        // --- Signature moves (GDD §8, Session 15) ------------------------------------------------------

        /// <summary>The usual test window, offering the left bot the placeholder moves with this charge.</summary>
        private PlanningWindow WindowWithSignatures(bool canUse, System.Collections.Generic.IReadOnlyList<SignatureMoveStats> moves = null)
        {
            PlanningWindow plain = NetworkingSanityTests.CreateWindow(_settings);
            var options = new SignatureOptions(moves ?? _settings.SignatureMoves, new SignatureChargeState(canUse, canUse ? 1f : 0f), canUse);
            return new PlanningWindow(plain.TurnIndex, plain.DeadlineSeconds, plain.Constraints, plain.IsSuddenDeath, plain.Board,
                plain.WeaponTips, new PerSide<SignatureOptions>(options, SignatureOptions.None));
        }

        private PlanningSession ChargedSession(PlanningWindow window) =>
            PlanningTestData.CreateSession(_settings, window.Constraints[Side.Left], signatureCharge: new SignatureChargeState(true, 1f));

        [Test]
        public void WhenAMoveCanBeUsed_SomePlansUseOne_AndEveryPlanIsAccepted()
        {
            PlanningWindow window = WindowWithSignatures(true);
            var validator = new PlanValidator(_settings, new RulePolicies());
            var used = new HashSet<SignatureMoveId>();
            int signaturePlans = 0;

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                PlanningSession session = ChargedSession(window);
                var results = new List<CommandResult>();
                new BotPlanSource(Side.Left, new RandomBotBrain(), PlanningTestData.Loadout, _settings, new XorShiftRandom(seed))
                    .OnPlanningStarted(window, new SessionSink(session, results));

                foreach (CommandResult result in results) Assert.That(result.Accepted, Is.True, $"seed {seed}: {result.Rejection}");
                Assert.That(validator.Validate(session).Accepted, Is.True, $"seed {seed}");
                Assert.That(new AnyMoveIdleTurnPolicy().IsIdle(Side.Left, session.CurrentPlan), Is.False, $"seed {seed}: never idle");
                if (session.CurrentPlan.Signature.IsEmpty) continue;
                signaturePlans++;
                used.Add(session.CurrentPlan.Signature);
                Assert.That(session.CurrentPlan.Path.IsEmpty, Is.False, $"seed {seed}: the rules drew the pattern");
            }

            Assert.That(signaturePlans, Is.InRange(Seeds / 10, Seeds / 2), "about one turn in four");
            Assert.That(used, Has.Count.EqualTo(3), "every placeholder move gets played");
        }

        [Test]
        public void WhenNoMoveCanBeUsed_TheBotAlwaysDraws_ExactlyAsBefore()
        {
            PlanningWindow offered = WindowWithSignatures(false);
            PlanningWindow plain = NetworkingSanityTests.CreateWindow(_settings);

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                TurnPlan withOptions = Plan(offered, seed);
                TurnPlan without = Plan(plain, seed);

                Assert.That(withOptions.Signature.IsEmpty, Is.True, $"seed {seed}");
                Assert.That(withOptions.Path.Points, Is.EqualTo(without.Path.Points), $"seed {seed}: no random number spent on it");
            }
        }

        [Test]
        public void ATiedMove_IsNeverPlayed_WithAnotherWeapon()
        {
            SignatureMoveStats maceOnly = GddStartingValues.CreateSignatureMoves()[0];
            maceOnly.AllowedWeaponIds.Add(WeaponIds.Mace.Value);
            PlanningWindow window = WindowWithSignatures(true, new[] { maceOnly });

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                TurnPlan plan = new RandomBotBrain(1f).PlanTurn(Context(window, seed));

                if (plan.Weapon == WeaponIds.Mace) Assert.That(plan.Signature, Is.EqualTo(maceOnly.MoveId), $"seed {seed}: chance 1 with the mace");
                else Assert.That(plan.Signature.IsEmpty, Is.True, $"seed {seed}: {plan.Weapon} cannot play it");
            }
        }

        [Test]
        public void TheChance_IsTheBotsOwnSetting()
        {
            PlanningWindow window = WindowWithSignatures(true);

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Assert.That(new RandomBotBrain(0f).PlanTurn(Context(window, seed)).Signature.IsEmpty, Is.True, $"seed {seed}: never");
                Assert.That(new RandomBotBrain(1f).PlanTurn(Context(window, seed)).Signature.IsEmpty, Is.False, $"seed {seed}: always");
            }
        }

        private sealed class SessionSink : IPlanningCommandSink
        {
            private const double PlanningSeconds = 1.0;

            private readonly PlanningSession _session;
            private readonly List<CommandResult> _results;

            public SessionSink(PlanningSession session, List<CommandResult> results)
            {
                _session = session;
                _results = results;
            }

            public CommandResult Send(PlanningCommand command)
            {
                CommandResult result = _session.Apply(command, PlanningSeconds);
                _results.Add(result);
                return result;
            }
        }
    }
}
