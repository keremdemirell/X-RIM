using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using XRim.Bots;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using XRim.Rules.Status;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Bots
{
    /// <summary>
    /// The heuristic bot (Session 16): every plan it makes is one a real planning session accepts, whatever the situation; it closes in,
    /// ducks under heavier weapons, hunts damaged limbs, guards when hurt, never steps back into its own wall, plays a signature move only
    /// when it reaches; and it plans from public information only. Boards are real starting boards (both right-handed, mirrored around
    /// x = 0) at the gap each test names.
    /// </summary>
    public sealed class HeuristicBotBrainTests
    {
        private const int Seeds = 60;
        private const double PlanningSeconds = 12.0;
        private const float StartingGapUnits = 700f;
        private const float CloseGapUnits = 460f;
        private const float FarGapUnits = 1400f;
        private const float LowHp = 10f;
        private const float Tolerance = 0.5f;

        private static readonly WeaponId[] Classic = { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield };
        private static readonly WeaponId[] Long = { WeaponIds.Spear, WeaponIds.Sword, WeaponIds.Shield };
        private static readonly WeaponId[] RapierOnly = { WeaponIds.Rapier };

        private RulesSettings _rules;
        private SimulationSettings _simulation;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
        }

        // --- Building windows and sessions --------------------------------------------------------------

        private PlanningWindow Window(float gapUnits, WeaponId left, WeaponId right, Action<MatchState> edit = null,
            PlanningConstraints leftConstraints = null, SignatureOptions leftSignatures = null, bool suddenDeath = false)
        {
            var state = new MatchState(
                new PerSide<FighterState>(
                    new FighterState(Handedness.Right, _rules.Damage.MaxHp, left),
                    new FighterState(Handedness.Right, _rules.Damage.MaxHp, right)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            float savedGap = _rules.Arena.StartingGapUnits;
            _rules.Arena.StartingGapUnits = gapUnits;
            PoseSnapshot pose = StartingBoard.Create(state, _rules, _simulation, new AimFromShoulderModel());
            _rules.Arena.StartingGapUnits = savedGap;
            edit?.Invoke(state); // after the pose: a change of hand or a lost limb is a rules fact here
            var board = new BoardSnapshot(state, pose);
            var locator = new PoseWeaponTipLocator(_rules, _simulation);
            return new PlanningWindow(0, PlanningSeconds,
                new PerSide<PlanningConstraints>(leftConstraints ?? Constraints(), Constraints()), suddenDeath, board,
                PerSide<IWeaponTipSource>.Create(side => locator.ForSide(board, side)),
                new PerSide<SignatureOptions>(leftSignatures ?? SignatureOptions.None, SignatureOptions.None));
        }

        private PlanningConstraints Constraints() => PlanningConstraints.CreateDefault(_rules.Match);

        private SignatureOptions Charged() => new SignatureOptions(_rules.SignatureMoves, new SignatureChargeState(true, 1f), true);

        private PlanningSession Session(PlanningWindow window, IReadOnlyList<WeaponId> loadout)
        {
            SignatureOptions options = window.Signatures.Left;
            return new PlanningSession(Side.Left, window.Board.State.Fighters.Left.CurrentWeapon, loadout, window.WeaponTips.Left,
                window.Constraints.Left, PlanningSeconds, _rules, new RulePolicies(), options.Charge, options.Moves);
        }

        private BotContext Context(PlanningWindow window, IReadOnlyList<WeaponId> loadout, uint seed) =>
            new BotContext(Side.Left, window, loadout, _rules, new XorShiftRandom(seed));

        /// <summary>Every chance at 1 and no mistakes or aim error, so a test sees one behaviour.</summary>
        private static HeuristicBotSettings Eager() => new HeuristicBotSettings
        {
            AimErrorUnits = 0f, MistakeChance = 0f, SignatureChance = 1f, DuckChance = 1f, SwingChance = 0f, HeadAimChance = 0f,
            ShieldGuardChance = 1f, RetreatChance = 1f, JumpChance = 0f, LowHpFraction = 0.35f, DamagedLimbFocusFraction = 0.5f,
        };

        private static HeuristicBotSettings Never(Action<HeuristicBotSettings> enable = null)
        {
            var settings = new HeuristicBotSettings
            {
                AimErrorUnits = 0f, MistakeChance = 0f, SignatureChance = 0f, DuckChance = 0f, SwingChance = 0f, HeadAimChance = 0f,
                ShieldGuardChance = 0f, RetreatChance = 0f, JumpChance = 0f,
            };
            enable?.Invoke(settings);
            return settings;
        }

        private static Vec2 End(WeaponPath path) => path.Points[path.Points.Count - 1];

        private Vec2 OpponentLocal(PlanningWindow window, Vec2 arena) =>
            TorsoFrame.ToLocal(arena, TurnStartRoot.Of(window.Board.Pose.Left), Side.Left);

        // --- Every plan is accepted -----------------------------------------------------------------

        private IEnumerable<(string Name, PlanningWindow Window, IReadOnlyList<WeaponId> Loadout)> Situations()
        {
            yield return ("starting gap", Window(StartingGapUnits, WeaponIds.Rapier, WeaponIds.Rapier), Classic);
            yield return ("close, against a mace", Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace), Classic);
            yield return ("very close", Window(200f, WeaponIds.Mace, WeaponIds.Rapier), Classic);
            yield return ("far apart", Window(FarGapUnits, WeaponIds.Rapier, WeaponIds.Mace), Classic);
            yield return ("spear, sword and shield", Window(CloseGapUnits, WeaponIds.Spear, WeaponIds.Sword), Long);

            PlanningConstraints stunned = Constraints();
            stunned.ForbidAllBodyMoves();
            stunned.AddStatus(StatusKind.Stunned);
            yield return ("stunned", Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace, leftConstraints: stunned), Classic);

            PlanningConstraints locked = Constraints();
            locked.CanSwitchWeapon = false;
            yield return ("no switching", Window(CloseGapUnits, WeaponIds.Mace, WeaponIds.Rapier, leftConstraints: locked), Classic);

            PlanningConstraints oneLeg = Constraints();
            oneLeg.ForbidBodyMove(BodyMove.Jump);
            oneLeg.ForbidBodyMove(BodyMove.Lunge);
            yield return ("one leg", Window(StartingGapUnits, WeaponIds.Rapier, WeaponIds.Mace, s => s.Fighters.Left.MarkSevered(BodyPart.LeftLeg), oneLeg),
                Classic);

            yield return ("low on HP with its wall behind it", Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace, s =>
            {
                s.Fighters.Left.Hp = LowHp;
                s.Walls.Left.Phase = ElectricWallPhase.Warning;
                s.Walls.Left.IsUp = true;
                s.Walls.Left.PositionUnits = -CloseGapUnits * 0.5f - 60f;
                s.Walls.Left.ConsecutiveRetreats = 1;
            }), Classic);

            PlanningConstraints armless = Constraints();
            armless.CanUseLoadoutWeapons = false;
            armless.AddExtraWeapon(WeaponIds.BodyBlow);
            yield return ("armless", Window(200f, WeaponIds.Rapier, WeaponIds.Mace, s =>
            {
                s.Fighters.Left.MarkSevered(BodyPart.LeftArm);
                s.Fighters.Left.MarkSevered(BodyPart.RightArm);
                s.Fighters.Left.CurrentWeapon = WeaponIds.BodyBlow;
            }, armless), Classic);

            PlanningConstraints club = Constraints();
            club.AddExtraWeapon(WeaponIds.SeveredLimb);
            yield return ("a club on offer", Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Rapier, leftConstraints: club), Classic);

            yield return ("a charged signature move", Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace, leftSignatures: Charged()), Classic);
            yield return ("the opponent's arm nearly severed", Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Rapier,
                s => s.Fighters.Right.SetLimbDamage(BodyPart.RightArm, _rules.Damage.ArmDurability * 0.8f)), Classic);
            yield return ("sudden death", Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace, s =>
            {
                s.IsSuddenDeath = true;
                s.Fighters.Left.Hp = 1f;
                s.Fighters.Right.Hp = 1f;
            }, suddenDeath: true), Classic);

            PlanningConstraints lessInk = Constraints();
            lessInk.InkLengthMultiplier = 0.5f;
            yield return ("half the ink", Window(StartingGapUnits, WeaponIds.Rapier, WeaponIds.Mace, leftConstraints: lessInk), Classic);
        }

        [Test]
        public void EveryPlan_InEverySituation_IsOneTheRulesAccept()
        {
            var validator = new PlanValidator(_rules, new RulePolicies());
            foreach (HeuristicBotSettings settings in new[] { new HeuristicBotSettings(), Eager() })
            {
                foreach ((string name, PlanningWindow window, IReadOnlyList<WeaponId> loadout) in Situations())
                {
                    for (uint seed = 1; seed <= Seeds; seed++)
                    {
                        PlanningSession session = Session(window, loadout);
                        var results = new List<CommandResult>();
                        new BotPlanSource(Side.Left, new HeuristicBotBrain(settings), loadout, _rules, new XorShiftRandom(seed))
                            .OnPlanningStarted(window, new SessionSink(session, results));

                        foreach (CommandResult result in results)
                            Assert.That(result.Accepted, Is.True, $"{name}, seed {seed}: {result.Rejection}");
                        Assert.That(validator.Validate(session).Accepted, Is.True, $"{name}, seed {seed}: the plan validates");
                        Assert.That(new AnyMoveIdleTurnPolicy().IsIdle(Side.Left, session.CurrentPlan), Is.False, $"{name}, seed {seed}: never idle");
                        if (!session.CurrentPlan.Signature.IsEmpty) continue;
                        Assert.That(session.CurrentPath.WasClampedByReach, Is.False, $"{name}, seed {seed}: inside its own reach");
                        Assert.That(session.CurrentPath.Inked.WasCut, Is.False, $"{name}, seed {seed}: within its ink");
                    }
                }
            }
        }

        [Test]
        public void TheSameSeed_GivesTheSamePlan()
        {
            PlanningWindow window = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace, leftSignatures: Charged());
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                TurnPlan first = new HeuristicBotBrain().PlanTurn(Context(window, Classic, seed));
                TurnPlan second = new HeuristicBotBrain().PlanTurn(Context(window, Classic, seed));
                Assert.That(second.Weapon, Is.EqualTo(first.Weapon), $"seed {seed}");
                Assert.That(second.BodyMove, Is.EqualTo(first.BodyMove), $"seed {seed}");
                Assert.That(second.Signature, Is.EqualTo(first.Signature), $"seed {seed}");
                Assert.That(second.Path.Points, Is.EqualTo(first.Path.Points), $"seed {seed}");
            }
        }

        // --- What it does ---------------------------------------------------------------------------

        [Test]
        public void OutOfReach_ButALungeReaches_ItLunges()
        {
            PlanningWindow window = Window(StartingGapUnits, WeaponIds.Rapier, WeaponIds.Rapier);
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Assert.That(new HeuristicBotBrain(Never()).PlanTurn(Context(window, RapierOnly, seed)).BodyMove, Is.EqualTo(BodyMove.Lunge), $"seed {seed}");
            }
        }

        [Test]
        public void NothingReaches_EvenWithALunge_ItLungesToCloseIn()
        {
            PlanningWindow window = Window(StartingGapUnits, WeaponIds.Mace, WeaponIds.Mace);
            WeaponId[] maceOnly = { WeaponIds.Mace };
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Assert.That(new HeuristicBotBrain(Never()).PlanTurn(Context(window, maceOnly, seed)).BodyMove, Is.EqualTo(BodyMove.Lunge),
                    $"seed {seed}: a mace at the starting gap steps in rather than waiting for the turn cap");
            }

            PlanningConstraints oneLeg = Constraints();
            oneLeg.ForbidBodyMove(BodyMove.Lunge);
            PlanningWindow noLunge = Window(StartingGapUnits, WeaponIds.Mace, WeaponIds.Mace, leftConstraints: oneLeg);
            Assert.That(new HeuristicBotBrain(Never()).PlanTurn(Context(noLunge, maceOnly, 1u)).BodyMove, Is.Not.EqualTo(BodyMove.Lunge),
                "never a move it may not make");
        }

        [Test]
        public void AHeavierWeaponInReach_ItDucksAndThrustsJustAboveThePelvis()
        {
            PlanningWindow window = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace);
            TurnPlan plan = new HeuristicBotBrain(Never(s => s.DuckChance = 1f)).PlanTurn(Context(window, RapierOnly, 1u));

            Assert.That(plan.BodyMove, Is.EqualTo(BodyMove.Crouch), "the GDD §1 moment: under the mace");
            Vec2 pelvis = OpponentLocal(window, window.Board.Pose.Right.Get(BodyPart.Torso).PositionUnits);
            Vec2 neck = OpponentLocal(window, window.Board.Pose.Right.Get(BodyPart.Head).PositionUnits);
            Vec2 low = pelvis + (neck - pelvis) * 0.25f;
            float crouchUnits = -_rules.FindBodyMove(BodyMove.Crouch).DisplacementUnits.Y;
            Vec2 drawnAt = low + new Vec2(0f, crouchUnits); // the path sinks with the crouch
            Vec2 end = End(plan.Path);
            Assert.That(Math.Abs(end.Y - drawnAt.Y), Is.LessThan(Tolerance), $"a level low thrust: ends at {end}, aimed at {drawnAt}");
            Assert.That(end.X, Is.GreaterThan(drawnAt.X), "it carries on through the target");

            Assert.That(new HeuristicBotBrain(Never()).PlanTurn(Context(window, RapierOnly, 1u)).BodyMove, Is.Not.EqualTo(BodyMove.Crouch),
                "never without the chance");
        }

        [Test]
        public void ADamagedLimbInReach_IsWhatItAimsAt()
        {
            PlanningWindow window = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Rapier,
                s => s.Fighters.Right.SetLimbDamage(BodyPart.RightArm, _rules.Damage.ArmDurability * 0.75f));
            Vec2 elbow = OpponentLocal(window, window.Board.Pose.Right.GetLower(BodyPart.RightArm).PositionUnits);

            TurnPlan plan = new HeuristicBotBrain(Never()).PlanTurn(Context(window, RapierOnly, 1u));

            Vec2 end = End(plan.Path);
            Assert.That(Vec2.Distance(end, elbow), Is.EqualTo(40f).Within(Tolerance), $"through the elbow: ends at {end}, elbow {elbow}");

            PlanningWindow whole = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Rapier);
            Assert.That(Vec2.Distance(End(new HeuristicBotBrain(Never()).PlanTurn(Context(whole, RapierOnly, 1u)).Path), elbow),
                Is.GreaterThan(40f + Tolerance), "an undamaged arm is not the target");
        }

        [Test]
        public void LowOnHp_AndThreatened_ItStepsBack_ButNeverIntoItsOwnWall()
        {
            PlanningWindow open = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Rapier, s => s.Fighters.Left.Hp = LowHp);
            PlanningWindow walled = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Rapier, s =>
            {
                s.Fighters.Left.Hp = LowHp;
                s.Walls.Left.Phase = ElectricWallPhase.Warning;
                s.Walls.Left.IsUp = true;
                s.Walls.Left.ConsecutiveRetreats = 1;
            });
            var retreats = Never(s => s.RetreatChance = 1f);

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Assert.That(new HeuristicBotBrain(retreats).PlanTurn(Context(open, RapierOnly, seed)).BodyMove, Is.EqualTo(BodyMove.StepBack), $"seed {seed}");
                Assert.That(new HeuristicBotBrain(Eager()).PlanTurn(Context(walled, Classic, seed)).BodyMove, Is.Not.EqualTo(BodyMove.StepBack),
                    $"seed {seed}: one more step back would zap it (§13)");
            }
        }

        [Test]
        public void LowOnHp_AndThreatened_ItRaisesItsShield()
        {
            PlanningWindow window = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace, s => s.Fighters.Left.Hp = LowHp);
            TurnPlan plan = new HeuristicBotBrain(Never(s => s.ShieldGuardChance = 1f)).PlanTurn(Context(window, Classic, 1u));

            Assert.That(plan.Weapon, Is.EqualTo(WeaponIds.Shield));
            Assert.That(End(plan.Path).X, Is.EqualTo(110f).Within(Tolerance), "face out in front of the chest");
            Assert.That(End(plan.Path).Y, Is.EqualTo(100f).Within(Tolerance));

            PlanningWindow healthy = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace);
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Assert.That(new HeuristicBotBrain(Never(s => s.ShieldGuardChance = 1f)).PlanTurn(Context(healthy, Classic, seed)).Weapon,
                    Is.Not.EqualTo(WeaponIds.Shield), $"seed {seed}: at full HP it attacks");
            }
        }

        [Test]
        public void AChargedMove_IsPlayed_OnlyWhenItsPatternReaches()
        {
            PlanningWindow close = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace, leftSignatures: Charged());
            PlanningWindow far = Window(FarGapUnits, WeaponIds.Rapier, WeaponIds.Mace, leftSignatures: Charged());
            var eager = Never(s => s.SignatureChance = 1f);

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                Assert.That(new HeuristicBotBrain(eager).PlanTurn(Context(close, RapierOnly, seed)).Signature.IsEmpty, Is.False, $"seed {seed}: in range");
                Assert.That(new HeuristicBotBrain(eager).PlanTurn(Context(far, RapierOnly, seed)).Signature.IsEmpty, Is.True, $"seed {seed}: out of range");
            }
        }

        [Test]
        public void ItsWeapons_AreAllUsed_OverManyTurns()
        {
            PlanningWindow window = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Rapier);
            var used = new HashSet<WeaponId>();
            for (uint seed = 1; seed <= Seeds * 3; seed++)
            {
                used.Add(new HeuristicBotBrain(Never()).PlanTurn(Context(window, new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Sword }, seed)).Weapon);
            }

            Assert.That(used, Has.Count.EqualTo(3), "a weighted pick, not always the same weapon: a batch measures them all");
        }

        // --- Public information only ------------------------------------------------------------------

        [Test]
        public void TheContext_HoldsNoHiddenState()
        {
            Type[] hidden = { typeof(PlanningWindow), typeof(MatchState), typeof(FighterState), typeof(BoardSnapshot), typeof(ElectricWallState), typeof(TurnPlan) };
            foreach (PropertyInfo property in typeof(BotContext).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.That(hidden, Has.No.Member(property.PropertyType), $"BotContext.{property.Name}");
            }

            string[] secret = { "SignatureCharge", "SignatureUses", "Statuses", "Plan" };
            foreach (MemberInfo member in typeof(PublicFighterView).GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.That(secret, Has.No.Member(member.Name), $"PublicFighterView.{member.Name}");
            }
        }

        [Test]
        public void TheOpponentsHiddenState_ChangesNothingItPlans()
        {
            PlanningWindow plain = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace);
            PlanningWindow secretive = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Mace, s =>
            {
                s.Fighters.Right.SignatureCharge = 1f;
                s.Fighters.Right.SignatureUses = 0;
                s.Fighters.Right.Statuses.Add(new NoBodyMoveStatus(StatusKind.Stunned, 1));
                s.Fighters.Right.LifestealHitCount = 2;
            });
            var opponentOptions = new PerSide<SignatureOptions>(SignatureOptions.None, Charged());
            var opponentLimits = Constraints();
            opponentLimits.ForbidAllBodyMoves();
            secretive = new PlanningWindow(secretive.TurnIndex, secretive.DeadlineSeconds,
                new PerSide<PlanningConstraints>(secretive.Constraints.Left, opponentLimits), false, secretive.Board, secretive.WeaponTips, opponentOptions);

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                TurnPlan a = new HeuristicBotBrain().PlanTurn(Context(plain, Classic, seed));
                TurnPlan b = new HeuristicBotBrain().PlanTurn(Context(secretive, Classic, seed));
                Assert.That(b.Weapon, Is.EqualTo(a.Weapon), $"seed {seed}");
                Assert.That(b.BodyMove, Is.EqualTo(a.BodyMove), $"seed {seed}");
                Assert.That(b.Path.Points, Is.EqualTo(a.Path.Points), $"seed {seed}");
            }
        }

        [Test]
        public void TheOpponentsPublicWeapon_IsTheOneItReads()
        {
            PlanningWindow rapier = Window(CloseGapUnits, WeaponIds.Rapier, WeaponIds.Rapier);
            var maceShown = new PerSide<PublicPlanningState>(new PublicPlanningState(WeaponIds.Rapier, false, false),
                new PublicPlanningState(WeaponIds.Mace, true, false));
            PlanningWindow mace = new PlanningWindow(rapier.TurnIndex, rapier.DeadlineSeconds, rapier.Constraints, false, rapier.Board,
                rapier.WeaponTips, rapier.Signatures, maceShown);
            var ducks = Never(s => s.DuckChance = 1f);

            Assert.That(new BotContext(Side.Left, mace, RapierOnly, _rules, new XorShiftRandom(1u)).OpponentPublicState.Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(new HeuristicBotBrain(ducks).PlanTurn(Context(mace, RapierOnly, 1u)).BodyMove, Is.EqualTo(BodyMove.Crouch),
                "it ducks under the mace the opponent shows");
        }

        // --- Whole matches -----------------------------------------------------------------------------

        [Test]
        public void AMatch_HeuristicAgainstRandom_RunsToItsEnd_EveryCommandAccepted()
        {
            var results = new List<CommandResult>();
            var simulator = new XRim.Tests.EditMode.Networking.ScriptedTurnSimulator((turn, state) => state.Fighters[Side.Right].Hp -= 9f);
            MatchSetup setup = TestData.CreateMatchSetup();
            var authority = new LocalTurnAuthority(setup, _rules, _simulation, new RulePolicies(), simulator, new ManualClock(0.0));
            MatchOutcome outcome = null;
            authority.MatchEnded += o => outcome = o;
            var sources = new IPlanSource[]
            {
                new RecordedBot(new BotPlanSource(Side.Left, new HeuristicBotBrain(), setup.Fighters.Left.Loadout, _rules, new XorShiftRandom(5u)), results),
                new RecordedBot(new BotPlanSource(Side.Right, new RandomBotBrain(), setup.Fighters.Right.Loadout, _rules, new XorShiftRandom(9u)), results),
            };
            using (new PlanSourceBinder(authority, sources))
            {
                authority.Start();
                for (int tick = 0; tick < 200 && outcome == null; tick++) authority.Tick();
            }

            Assert.That(outcome, Is.Not.Null, "the match ended");
            Assert.That(results, Is.Not.Empty);
            foreach (CommandResult result in results) Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
        }

        private sealed class RecordedBot : IPlanSource
        {
            private readonly BotPlanSource _inner;
            private readonly List<CommandResult> _results;

            public RecordedBot(BotPlanSource inner, List<CommandResult> results)
            {
                _inner = inner;
                _results = results;
            }

            public Side Side => _inner.Side;

            public void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink) => _inner.OnPlanningStarted(window, new SessionlessSink(sink, _results));

            public void OnPlanningEnded() => _inner.OnPlanningEnded();
        }

        private sealed class SessionlessSink : IPlanningCommandSink
        {
            private readonly IPlanningCommandSink _inner;
            private readonly List<CommandResult> _results;

            public SessionlessSink(IPlanningCommandSink inner, List<CommandResult> results)
            {
                _inner = inner;
                _results = results;
            }

            public CommandResult Send(PlanningCommand command)
            {
                CommandResult result = _inner.Send(command);
                _results.Add(result);
                return result;
            }
        }

        private sealed class SessionSink : IPlanningCommandSink
        {
            private const double PlanningAtSeconds = 1.0;

            private readonly PlanningSession _session;
            private readonly List<CommandResult> _results;

            public SessionSink(PlanningSession session, List<CommandResult> results)
            {
                _session = session;
                _results = results;
            }

            public CommandResult Send(PlanningCommand command)
            {
                CommandResult result = _session.Apply(command, PlanningAtSeconds);
                _results.Add(result);
                return result;
            }
        }
    }
}
