using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XRim.Core;
using XRim.Presentation.Moments;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Scenarios;
using XRim.Simulation.Settings;

namespace XRim.Editor.Scenarios
{
    /// <summary>
    /// The five seed regression scenarios (Session 16), each one turn on the code's starting values (<see cref="GddStartingValues"/>, a default
    /// <see cref="SimulationSettings"/>), both dummies right-handed and mirrored around x = 0. Heights in the torso frame (pelvis = 0):
    /// shoulder 100, torso 0–120 and 60 wide, head centre 145 (radius 25); a crouch lowers the pelvis 70 and leans 10° in. The boards of the
    /// crush, the block and the sever are the ones the PlayMode tests already prove (<c>ClashPlayModeTests</c>, <c>DismembermentPlayModeTests</c>).
    /// </summary>
    internal static class SeedScenarios
    {
        public const string FilePrefix = "seed-";

        // --- §1 reference moment: duck under a mace swing, rapier into the pelvis ---

        /// <summary>Pelvis to pelvis 460: the left's standing head (front 435 from the right's shoulder) is inside the mace's reach (460).</summary>
        private const float ReferenceHalfGapUnits = 230f;

        /// <summary>The mace raised 40°: its head rests at (168, 241), above everything the rapier does.</summary>
        private const float RaisedMaceGuardDegrees = 40f;

        /// <summary>The swing comes down and forward to head height, ending where the left's standing head is (arena about (−240, 330)).</summary>
        private static readonly Vec2 MaceSwingMiddleLocal = new Vec2(300f, 160f);
        private static readonly Vec2 MaceSwingEndLocal = new Vec2(470f, 150f);

        /// <summary>The rapier rises from its low guard, then thrusts level: with the crouch (pelvis at 110) it runs 30 above the opponent's pelvis.</summary>
        private static readonly Vec2 RapierRiseLocal = new Vec2(380f, 100f);
        private static readonly Vec2 RapierPelvisEndLocal = new Vec2(480f, 100f);

        // --- A mace crushing a rapier (ClashPlayModeTests) ---

        /// <summary>Pelvis to pelvis 700, level guards: the tips meet square on, on one line.</summary>
        private const float HeadOnHalfGapUnits = 350f;
        private const float LevelGuardDegrees = 0f;
        private static readonly Vec2 RapierThrustEndLocal = new Vec2(600f, 100f);
        private static readonly Vec2 MaceThrustEndLocal = new Vec2(420f, 100f);

        // --- A shield's full block (ClashPlayModeTests) ---

        /// <summary>Pelvis to pelvis 640: a level rapier thrust would end 30 inside the chest.</summary>
        private const float ShieldHalfGapUnits = 320f;
        private static readonly Vec2 BlockedRiseLocal = new Vec2(450f, 100f);
        private static readonly Vec2 BlockedChestEndLocal = new Vec2(640f, 100f);
        private static readonly Vec2 ShieldArcMiddleLocal = new Vec2(150f, 50f);
        private static readonly Vec2 ShieldRaisedLocal = new Vec2(110f, 100f);

        // --- A sever (DismembermentPlayModeTests' chop, the arm one hit from its durability) ---

        /// <summary>Pelvis to pelvis 600: the rapier reaches the opponent's outstretched arm.</summary>
        private const float SeverHalfGapUnits = 300f;

        /// <summary>The chopping rapier held 30° up with the hand at full reach; its blade only comes down onto the opponent's arm.</summary>
        private const float HighGuardDegrees = 30f;

        private const float FullHandReachFraction = 1f;

        /// <summary>
        /// No guard tip cap: the chop was built (Session 11) before Session 14's cap, which would rest both full-reach rapiers 400 from the
        /// shoulder and put the target's blade along its arm, in the chop's way.
        /// </summary>
        private const float UncappedGuardTipUnits = 10000f;

        private static readonly Vec2 ChopEndLocal = new Vec2(480f, -40f);

        /// <summary>The target arm's damage so far, short of its durability by this: any landed hit severs it.</summary>
        private const float OneHitFromSeveredUnits = 1f;

        public static IReadOnlyList<SeedScenario> All() => new[] { ReferenceMoment(), MaceCrushesRapier(), ShieldBlock(), Sever(), WallZap() };

        private static SeedScenario ReferenceMoment() => new SeedScenario(FilePrefix + "reference-duck-under-mace", "§1 reference moment",
            "GDD §1: the left dummy ducks under a mace swing aimed at its head and, in the same turn, thrusts its rapier into the opponent just above the pelvis.",
            _ =>
            {
                RulesSettings rules = GddStartingValues.CreateRulesSettings();
                var simulation = new SimulationSettings();
                WeaponStats rapier = rules.FindWeapon(WeaponIds.Rapier);
                WeaponStats mace = rules.FindWeapon(WeaponIds.Mace);
                var pose = new PoseSnapshot
                {
                    Left = Fighter(Side.Left, -ReferenceHalfGapUnits, rapier, new RagdollSettings(), simulation, rules),
                    Right = Fighter(Side.Right, ReferenceHalfGapUnits, mace, new RagdollSettings { GuardAngleDegrees = RaisedMaceGuardDegrees }, simulation, rules),
                };
                var plans = new PerSide<TurnPlan>(
                    Plan(rapier, BodyMove.Crouch, PathFrom(pose, Side.Left, rapier, RapierRiseLocal, RapierPelvisEndLocal)),
                    Plan(mace, BodyMove.None, PathFrom(pose, Side.Right, mace, MaceSwingMiddleLocal, MaceSwingEndLocal)));
                return new TurnInput(new BoardSnapshot(State(rules, rapier, mace), pose), plans, rules, simulation);
            },
            (input, result) =>
            {
                if (!new HeuristicMomentDetector().TryDetect(result, out LegendaryMoment moment)) return "no legendary moment at all: " + Describe(result);
                if (moment.Kind != MomentKind.DodgeCounter || moment.Hero != Side.Left)
                    return $"the turn's moment is {moment}, not the left dummy's dodge and counter: {Describe(result)}";
                HitLandedEvent counter = Hits(result).First(hit => hit.Hit.Attacker == Side.Left);
                if (counter.Hit.Part != BodyPart.Torso) return $"the counter hit the {counter.Hit.Part}, not the torso just above the pelvis: {Describe(result)}";
                return MaceAimedAtTheHead(input);
            });

        /// <summary>Null when the mace's swing ends on the left dummy's standing head, so the crouch is what saved it.</summary>
        private static string MaceAimedAtTheHead(TurnInput input)
        {
            RagdollSettings body = input.Simulation.Ragdoll;
            FighterPose left = input.Board.Pose.Get(Side.Left);
            FighterPose right = input.Board.Pose.Get(Side.Right);
            Vec2 head = TurnStartRoot.Of(left).PositionUnits + new Vec2(0f, body.TorsoHeightUnits + body.HeadDiameterUnits * 0.5f);
            Vec2 swingEnd = TorsoFrame.ToArena(MaceSwingEndLocal, TurnStartRoot.Of(right), Side.Right);
            float reach = body.HeadDiameterUnits * 0.5f + input.Rules.FindWeapon(WeaponIds.Mace).InkThicknessUnits * 0.5f;
            float distance = Vec2.Distance(head, swingEnd);
            return distance <= reach
                ? null
                : string.Format(CultureInfo.InvariantCulture, "the mace's swing ends {0:0} from the standing head, so it was never aimed at it", distance);
        }

        private static SeedScenario MaceCrushesRapier() => new SeedScenario(FilePrefix + "mace-crushes-rapier", "Mace crushes rapier",
            "GDD §10: two level thrusts meet tip to tip, square on; the mace (power 11.25) crushes the rapier (6.5): the rapier is knocked off and its dummy staggered.",
            _ =>
            {
                RulesSettings rules = GddStartingValues.CreateRulesSettings();
                var simulation = new SimulationSettings();
                WeaponStats rapier = rules.FindWeapon(WeaponIds.Rapier);
                WeaponStats mace = rules.FindWeapon(WeaponIds.Mace);
                var level = new RagdollSettings { GuardAngleDegrees = LevelGuardDegrees };
                var pose = new PoseSnapshot
                {
                    Left = Fighter(Side.Left, -HeadOnHalfGapUnits, rapier, level, simulation, rules),
                    Right = Fighter(Side.Right, HeadOnHalfGapUnits, mace, level, simulation, rules),
                };
                var plans = new PerSide<TurnPlan>(
                    Plan(rapier, BodyMove.None, PathFrom(pose, Side.Left, rapier, RapierThrustEndLocal)),
                    Plan(mace, BodyMove.None, PathFrom(pose, Side.Right, mace, MaceThrustEndLocal)));
                return new TurnInput(new BoardSnapshot(State(rules, rapier, mace), pose), plans, rules, simulation);
            },
            (input, result) =>
            {
                List<WeaponClashEvent> clashes = result.Timeline.Events.OfType<WeaponClashEvent>().ToList();
                if (clashes.Count != 1) return $"{clashes.Count} clashes, expected exactly one: {Describe(result)}";
                ClashResult clash = clashes[0].Result;
                if (clash.Kind != ClashKind.CrushThrough || clash.Winner != Side.Right) return $"the clash was {clash.Kind}, not the mace crushing through: {Describe(result)}";
                if (!result.Timeline.Events.OfType<StatusAppliedEvent>().Any(e => e.Side == Side.Left && e.Kind == StatusKind.Staggered))
                    return "the rapier's dummy was not staggered: " + Describe(result);
                return Hits(result).Count == 0 ? null : "a hit landed as well: " + Describe(result);
            });

        private static SeedScenario ShieldBlock() => new SeedScenario(FilePrefix + "shield-full-block", "Shield full block",
            "GDD §7: the shield sweeps up to stand face out in front of the chest and fully blocks a level rapier thrust square on its face.",
            _ =>
            {
                RulesSettings rules = GddStartingValues.CreateRulesSettings();
                var simulation = new SimulationSettings();
                WeaponStats rapier = rules.FindWeapon(WeaponIds.Rapier);
                WeaponStats shield = rules.FindWeapon(WeaponIds.Shield);
                var pose = new PoseSnapshot
                {
                    Left = Fighter(Side.Left, -ShieldHalfGapUnits, rapier, new RagdollSettings(), simulation, rules),
                    Right = Fighter(Side.Right, ShieldHalfGapUnits, shield, new RagdollSettings(), simulation, rules),
                };
                var plans = new PerSide<TurnPlan>(
                    Plan(rapier, BodyMove.None, PathFrom(pose, Side.Left, rapier, BlockedRiseLocal, BlockedChestEndLocal)),
                    Plan(shield, BodyMove.None, PathFrom(pose, Side.Right, shield, ShieldArcMiddleLocal, ShieldRaisedLocal)));
                return new TurnInput(new BoardSnapshot(State(rules, rapier, shield), pose), plans, rules, simulation);
            },
            (input, result) =>
            {
                List<ShieldBlockEvent> blocks = result.Timeline.Events.OfType<ShieldBlockEvent>().ToList();
                if (blocks.Count != 1) return $"{blocks.Count} blocks, expected exactly one: {Describe(result)}";
                if (blocks[0].Blocker != Side.Right || !blocks[0].Result.AttackStopped) return "not a full block by the shield: " + Describe(result);
                return Hits(result).Count == 0 ? null : "a hit landed as well: " + Describe(result);
            });

        private static SeedScenario Sever() => new SeedScenario(FilePrefix + "sever-arm", "Sever",
            "GDD §11, §12: the right dummy's weapon arm is one hit from its durability; the left's chop lands on it: the arm is severed and the rapier drops.",
            _ =>
            {
                RulesSettings rules = GddStartingValues.CreateRulesSettings();
                var simulation = new SimulationSettings();
                simulation.Ragdoll.GuardHandReachFraction = FullHandReachFraction;
                simulation.Ragdoll.GuardMaxTipDistanceUnits = UncappedGuardTipUnits;
                WeaponStats rapier = rules.FindWeapon(WeaponIds.Rapier);
                var highGuard = new RagdollSettings
                {
                    GuardAngleDegrees = HighGuardDegrees, GuardHandReachFraction = FullHandReachFraction, GuardMaxTipDistanceUnits = UncappedGuardTipUnits,
                };
                var pose = new PoseSnapshot
                {
                    Left = Fighter(Side.Left, -SeverHalfGapUnits, rapier, highGuard, simulation, rules),
                    Right = Fighter(Side.Right, SeverHalfGapUnits, rapier, simulation.Ragdoll, simulation, rules),
                };
                MatchState state = State(rules, rapier, rapier);
                state.Fighters.Right.SetLimbDamage(BodyPart.RightArm, rules.Damage.ArmDurability - OneHitFromSeveredUnits);
                var plans = new PerSide<TurnPlan>(
                    Plan(rapier, BodyMove.None, PathFrom(pose, Side.Left, rapier, ChopEndLocal)),
                    Plan(rapier, BodyMove.None, WeaponPath.Empty));
                return new TurnInput(new BoardSnapshot(state, pose), plans, rules, simulation);
            },
            (input, result) =>
            {
                if (!result.Timeline.Events.OfType<LimbSeveredEvent>().Any(e => e.Side == Side.Right && e.Part == BodyPart.RightArm))
                    return "the right arm was not severed: " + Describe(result);
                return result.Timeline.Events.OfType<WeaponDroppedEvent>().Any(e => e.Side == Side.Right) ? null : "the rapier did not drop: " + Describe(result);
            });

        private static SeedScenario WallZap() => new SeedScenario(FilePrefix + "wall-zap", "Electric wall zap",
            "GDD §13: the second backward swipe in a row runs the left dummy into its electric wall (raised by the first): damage, a forward throw, the wall advances.",
            simulate =>
            {
                RulesSettings rules = GddStartingValues.CreateRulesSettings();
                var simulation = new SimulationSettings();
                WeaponStats rapier = rules.FindWeapon(WeaponIds.Rapier);
                var wallRules = new ElectricWallRules();
                MatchState start = State(rules, rapier, rapier);
                var plans = new PerSide<TurnPlan>(Plan(rapier, BodyMove.StepBack, WeaponPath.Empty), Plan(rapier, BodyMove.None, WeaponPath.Empty));

                // Turn 1, locked the way the match locks it: the first step back raises the warning behind the dummy.
                MatchState first = start.Clone();
                Lock(wallRules, first, plans, rules);
                var firstPose = StartingBoard.Create(start, rules, simulation, new AimFromShoulderModel());
                TurnResult warning = simulate(new TurnInput(new BoardSnapshot(first, firstPose), plans, rules, simulation));

                // Turn 2, the one saved: the second step back makes the wall live.
                MatchState second = warning.FinalBoard.State.Clone();
                second.TurnIndex = 1;
                Lock(wallRules, second, plans, rules);
                return new TurnInput(new BoardSnapshot(second, warning.FinalBoard.Pose.Clone()), plans, rules, simulation);
            },
            (input, result) =>
            {
                bool zapped = result.Timeline.Events.OfType<ElectricWallEvent>()
                    .Any(e => e.Side == Side.Left && e.Phase == ElectricWallPhase.Active && e.HpDamage > 0f);
                return zapped ? null : "the left dummy was not zapped by its wall: " + Describe(result);
            });

        // --- Building blocks ---------------------------------------------------------------------

        private static void Lock(ElectricWallRules wallRules, MatchState state, PerSide<TurnPlan> plans, RulesSettings rules)
        {
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                wallRules.OnPlansLocked(state.Walls[side], plans[side].BodyMove, state.IsSuddenDeath, rules.ElectricWall);
            }
        }

        private static FighterPose Fighter(Side side, float pelvisX, WeaponStats item, RagdollSettings guard, SimulationSettings simulation,
            RulesSettings rules)
        {
            var torso = new BodyPose(new Vec2(pelvisX, LegGeometry.StandingPelvisHeightUnits(simulation.Ragdoll)), TurnStartRoot.UprightDegrees);
            return GuardStance.Create(torso, side, BodyParts.DominantArm(Handedness.Right), item, simulation.Segmentation, guard, rules.Paths,
                new AimFromShoulderModel());
        }

        private static MatchState State(RulesSettings rules, WeaponStats left, WeaponStats right) => new MatchState(
            new PerSide<FighterState>(
                new FighterState(Handedness.Right, rules.Damage.MaxHp, left.WeaponId),
                new FighterState(Handedness.Right, rules.Damage.MaxHp, right.WeaponId)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        private static TurnPlan Plan(WeaponStats item, BodyMove move, WeaponPath path) => new TurnPlan(item.WeaponId, move, path, default, true);

        /// <summary>A path from where the held item rests (its tip, or the shield's centre: the D3 lead-in costs nothing) through these points.</summary>
        private static WeaponPath PathFrom(PoseSnapshot pose, Side side, WeaponStats item, params Vec2[] pointsLocal)
        {
            FighterPose fighter = pose.Get(side);
            Vec2 start = TorsoFrame.ToLocal(HeldItemShape.Of(item).PathPointAt(fighter.HeldItem), TurnStartRoot.Of(fighter), side);
            var points = new List<Vec2> { start };
            points.AddRange(pointsLocal);
            return new WeaponPath(points);
        }

        private static List<HitLandedEvent> Hits(TurnResult result) => result.Timeline.Events.OfType<HitLandedEvent>().ToList();

        /// <summary>What the turn did, in words (the moves and swings that started left out).</summary>
        private static string Describe(TurnResult result)
        {
            List<string> events = ScenarioOutcome.Of(result).Events
                .Where(e => e.Kind != ScenarioEventKind.BodyMoveStarted && e.Kind != ScenarioEventKind.AttackStarted)
                .Select(e => e.Summary).ToList();
            return events.Count == 0 ? "nothing happened" : string.Join("; ", events);
        }
    }
}
