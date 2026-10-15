using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation
{
    /// <summary>
    /// One scripted turn on the fake world with the game's real hit rules, rich in big moments: both right-handed dummies thrust with
    /// rapiers; the left blade reaches the right dummy's weapon arm first, which cancels the right attack (D15: an interrupt
    /// counter-hit) and kills it (the right dummy starts nearly dead): a landed hit, an interrupt, a death and the torso split. Every
    /// call builds a fresh world, so simulating it again from the same inputs is a true repeat.
    /// </summary>
    internal static class ScriptedDuelTurn
    {
        public const float ShoulderHeight = 100f;
        public const float RightX = 600f;
        public const float RightStartingHp = 5f;

        /// <summary>When the left blade reaches the right dummy's weapon arm.</summary>
        public const double HitSeconds = 0.0505;

        public static TurnResult Simulate()
        {
            RulesSettings rules = GddStartingValues.CreateRulesSettings();
            var simulation = new SimulationSettings();
            WeaponStats rapier = rules.FindWeapon(WeaponIds.Rapier);
            var clock = new SimClock(simulation.StepRateHz);

            var state = new MatchState(
                PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, rules.Damage.MaxHp, WeaponIds.Rapier)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            state.Fighters[Side.Right].Hp = RightStartingHp;

            var pose = new PoseSnapshot();
            pose.Left.Set(BodyPart.Torso, new BodyPose(Vec2.Zero, 0f));
            pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(RightX, 0f), 0f));
            pose.Left.Set(BodyPart.Head, new BodyPose(new Vec2(0f, 120f), 0f));
            pose.Right.Set(BodyPart.Head, new BodyPose(new Vec2(RightX, 120f), 0f));
            pose.Left.HeldItem = new BodyPose(new Vec2(0f, ShoulderHeight), 0f);
            pose.Right.HeldItem = new BodyPose(new Vec2(RightX, ShoulderHeight), 180f);

            var leftThrust = new WeaponPath(new[] { new Vec2(400f, ShoulderHeight), new Vec2(640f, ShoulderHeight) });
            var rightThrust = new WeaponPath(new[] { new Vec2(400f, ShoulderHeight), new Vec2(640f, ShoulderHeight) });
            var plans = new PerSide<TurnPlan>(Plan(leftThrust), Plan(rightThrust));

            using (var world = new FakePhysicsWorld())
            {
                // The left tip at (400 + v·t, 100); its blade touches half its width ahead of the tip; reported a step later.
                float touchX = 400f + (float)(rapier.SpeedUnitsPerSecond * HitSeconds) + rapier.InkThicknessUnits * 0.5f;
                world.ScheduleContacts(clock.StepsFor(HitSeconds) + 1,
                    new ContactFacts(new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso), new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.RightArm),
                        new Vec2(touchX, ShoulderHeight), Vec2.UnitX, new Vec2(-rapier.SpeedUnitsPerSecond, 0f)));
                return new TurnSimulator(world, new RulePolicies()).Simulate(new TurnInput(new BoardSnapshot(state, pose), plans, rules, simulation));
            }
        }

        private static TurnPlan Plan(WeaponPath path) => new TurnPlan(WeaponIds.Rapier, BodyMove.None, path, default, true);
    }
}
