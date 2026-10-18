using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Limbs;
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

namespace XRim.Tests.EditMode.Simulation.Scenarios
{
    /// <summary>
    /// Scenario inputs that use every part of the save format: a sudden-death board with damaged and severed limbs, a held club, all three
    /// status effects, lost weapons, both walls up, a dropped item, a split torso and limbs on the floor, and two plans (a drawn path with a
    /// crouch; a signature move).
    /// </summary>
    internal static class ScenarioTestData
    {
        public const float WorldUnitsPerArenaUnit = 0.01f;

        public static Scenario RichScenario(ScenarioOutcome expected = null)
        {
            RulesSettings rules = GddStartingValues.CreateRulesSettings();
            var simulation = new SimulationSettings();
            var input = new TurnInput(RichBoard(rules, simulation), RichPlans(), rules, simulation);
            return new Scenario("Rich board, turn 18", "every part of the format", input, WorldUnitsPerArenaUnit, expected);
        }

        public static BoardSnapshot RichBoard(RulesSettings rules, SimulationSettings simulation)
        {
            MatchState state = MatchState.CreateInitial(TestData.CreateMatchSetup(), rules);
            PoseSnapshot pose = StartingBoard.Create(state, rules, simulation, new AimFromShoulderModel());

            state.TurnIndex = 17;
            state.IsSuddenDeath = true;
            state.SuddenDeathRepeats = 2;

            FighterState left = state.Fighters.Left;
            left.Hp = 1f;
            left.HpBeforeSuddenDeath = 37.5f;
            left.CurrentWeapon = WeaponIds.SeveredLimb;
            left.HpDamageTaken = 63.25f;
            left.LifestealHitCount = 2;
            left.LifestealDamageDealt = 14.5f;
            left.HitLandedThisTurn = true;
            left.SignatureCharge = 0.625f;
            left.SignatureUses = 1;
            left.ConsecutiveIdleTurns = 2;
            left.SetLimbDamage(BodyPart.LeftLeg, 12.3f);
            left.SetLimbDamage(BodyPart.RightArm, 40f);
            left.MarkSevered(BodyPart.RightArm);
            left.Statuses.Add(new NoBodyMoveStatus(StatusKind.Stunned, 1));
            left.Statuses.Add(new ShorterPlanningStatus(StatusKind.Staggered, 2, 0.5f));
            left.HeldLimb = new SeveredLimbRef(Side.Right, BodyPart.LeftLeg);
            left.LostWeapons.Add(WeaponIds.Mace); // ordinal order, as the file stores them
            left.LostWeapons.Add(WeaponIds.Rapier);

            FighterState right = state.Fighters.Right;
            right.Hp = 1f;
            right.Statuses.Add(new LessInkStatus(StatusKind.Stunned, 1, 0.75f));
            right.SetLimbDamage(BodyPart.LeftLeg, 50f);
            right.MarkSevered(BodyPart.LeftLeg);

            ElectricWallState leftWall = state.Walls.Left;
            leftWall.Phase = ElectricWallPhase.Active;
            leftWall.IsUp = true;
            leftWall.PositionUnits = -812.5f;
            leftWall.ConsecutiveRetreats = 3;
            ElectricWallState rightWall = state.Walls.Right;
            rightWall.Phase = ElectricWallPhase.Warning;
            rightWall.IsUp = true;
            rightWall.PositionUnits = 905f;
            rightWall.ConsecutiveRetreats = 1;

            pose.Left.MarkSevered(BodyPart.RightArm);
            pose.Left.HasDroppedItem = true;
            pose.Left.DroppedItem = new BodyPose(new Vec2(-200.5f, 12f), 33f);
            pose.Right.MarkSevered(BodyPart.LeftLeg);
            pose.Right.IsTorsoSplit = true;
            pose.Right.TorsoTop = new BodyPose(new Vec2(300f, 260f), -12.5f);
            pose.SeveredLimbs.Add(new SeveredLimbPose(Side.Left, BodyPart.RightArm, new BodyPose(new Vec2(-90f, 8f), -45f)));
            pose.LeftWall = ElectricWallPose.Of(leftWall);
            pose.RightWall = ElectricWallPose.Of(rightWall);
            return new BoardSnapshot(state, pose);
        }

        public static PerSide<TurnPlan> RichPlans() => new PerSide<TurnPlan>(
            new TurnPlan(WeaponIds.SeveredLimb, BodyMove.Crouch,
                new WeaponPath(new[] { new Vec2(120.25f, 40f), new Vec2(300f, 95.5f), new Vec2(480.125f, 101f) }), default, true),
            new TurnPlan(WeaponIds.Mace, BodyMove.None, WeaponPath.Empty, SignatureMoveIds.HeadImpactSimulation404, false));

        /// <summary>A turn with one event of every kind the scenario keeps, and one raw contact it leaves out.</summary>
        public static TurnResult TurnWithEveryEvent()
        {
            RulesSettings rules = GddStartingValues.CreateRulesSettings();
            var simulation = new SimulationSettings();
            BoardSnapshot final = RichBoard(rules, simulation);
            var tag = new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso);
            var events = new List<MatchEvent>
            {
                new BodyMoveStartedEvent(Ms(0), Side.Left, BodyMove.Crouch),
                new AttackStartedEvent(Ms(0), Side.Right, WeaponIds.Mace, WeaponKind.Weapon),
                new SignatureUsedEvent(Ms(0), Side.Right, SignatureMoveIds.HeadImpactSimulation404, "Head Impact Simulation #404"),
                new ContactEvent(new TurnContact(new ContactFacts(tag, tag, Vec2.Zero, Vec2.UnitX, Vec2.Zero), 10, Ms(40), Side.Left, 50f, 80f)),
                new WeaponClashEvent(Ms(80), new ClashResult(ClashKind.CrushThrough, Side.Right, 75f, 6.5f, 11.25f), new Vec2(0f, 200f)),
                new StatusAppliedEvent(Ms(80), Side.Left, StatusKind.Staggered),
                new ShieldBlockEvent(Ms(90), Side.Left, new BlockResult(false, 0.5f, false), new Vec2(10f, 210f), 42f, 0.9f),
                new HitLandedEvent(Ms(120.5), new HitFacts(Side.Left, BodyPart.RightArm, WeaponIds.Rapier, Ms(120.5), false),
                    new DamageResult(6.4f, 6.4f, 6.4f, false, true)),
                new AttackInterruptedEvent(Ms(120.5), Side.Right),
                new LimbSeveredEvent(Ms(120.5), Side.Right, BodyPart.RightArm),
                new WeaponDroppedEvent(Ms(120.5), Side.Right, WeaponIds.Mace),
                new ElectricWallEvent(Ms(300), Side.Left, ElectricWallPhase.Active, 10f, -762.5f),
                new WeaponTraitHealEvent(Ms(310), Side.Left, WeaponTraitIds.Lifesteal, 4.5f),
                new FighterDiedEvent(Ms(400), Side.Right),
            };
            var frames = new List<TimelineFrame> { new TimelineFrame(0, Ms(0), final.Pose), new TimelineFrame(240, Ms(1000), final.Pose) };
            var report = new ExecutionReport(final.State, new PerSide<SimTime?>(null, null), false);
            return new TurnResult(17, new TurnTimeline(frames, events), final, report, TurnEndReason.HardCap);
        }

        public static SimTime Ms(double milliseconds) => SimTime.FromMilliseconds(milliseconds);
    }
}
