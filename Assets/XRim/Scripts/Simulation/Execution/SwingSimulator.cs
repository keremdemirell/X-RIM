using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// Simulates a swing up front, faster than real time, then hands back a recording to play (ARCHITECTURE §1, §6).
    /// Each fixed step: every dummy's root holds its place, each attacking weapon's driver sets its target, the world
    /// steps, the pose is recorded, and new contacts get a refined time-to-impact (GDD §9) and a contact angle (§10).
    /// It stops when every path is done and physics has settled, or at the execution hard cap (§3).
    /// This is the feel spike's loop (Session 02): no rules decide anything yet. Session 04 folds it into
    /// <see cref="TurnSimulator"/>, adding body moves and the rules' hit, clash and block decisions.
    /// </summary>
    public sealed class SwingSimulator
    {
        /// <summary>Box2D reports a new contact one step after the motion that made it, so two steps are searched.</summary>
        private const int RefinementWindowSteps = 2;

        private readonly IPhysicsWorld _world;
        private readonly IWeaponAimModel _aim;
        private readonly List<ContactFacts> _drained = new List<ContactFacts>();

        public SwingSimulator(IPhysicsWorld world, IWeaponAimModel aim)
        {
            _world = Guard.NotNull(world, nameof(world));
            _aim = Guard.NotNull(aim, nameof(aim));
        }

        public SwingResult Run(SwingInput input)
        {
            Guard.NotNull(input, nameof(input));
            SimulationSettings simulation = input.Simulation;
            _world.Load(input.StartPose, input.State, input.Rules, simulation);

            var clock = new SimClock(simulation.StepRateHz);
            PerSide<Fighter> fighters = PerSide<Fighter>.Create(side => CreateFighter(side, input));
            var recorder = new TimelineRecorder();
            var contacts = new List<SwingContact>();

            PoseSnapshot pose = _world.CapturePose();
            recorder.RecordFrame(clock.Step, clock.Now, pose);
            int lastRecordedStep = clock.Step;
            Remember(fighters, clock.Now, pose);

            int maxSteps = clock.StepsFor(input.Rules.Match.ExecutionHardCapSeconds);
            int settledSteps = 0;
            SwingEndReason endReason = SwingEndReason.HardCap;
            while (clock.Step < maxSteps)
            {
                SimTime stepStart = clock.Now;
                SimTime stepEnd = clock.TimeAtStep(clock.Step + 1);
                Drive(fighters.Left, stepStart, stepEnd);
                Drive(fighters.Right, stepStart, stepEnd);

                _world.Step(clock.StepSeconds);
                clock.Advance();

                pose = _world.CapturePose();
                Remember(fighters, clock.Now, pose);
                if (clock.Step % simulation.RecordEveryNthStep == 0)
                {
                    recorder.RecordFrame(clock.Step, clock.Now, pose);
                    lastRecordedStep = clock.Step;
                }

                CollectContacts(clock, fighters, contacts);

                bool pathsDone = IsDone(fighters.Left, clock.Now) && IsDone(fighters.Right, clock.Now);
                settledSteps = pathsDone &&
                               _world.IsSettled(simulation.SettleLinearSpeedUnitsPerSecond, simulation.SettleAngularSpeedDegreesPerSecond)
                    ? settledSteps + 1
                    : 0;
                if (settledSteps >= simulation.SettleStepsRequired)
                {
                    endReason = SwingEndReason.Settled;
                    break;
                }
            }

            if (lastRecordedStep != clock.Step) recorder.RecordFrame(clock.Step, clock.Now, pose);
            return new SwingResult(recorder.Build(), contacts, clock.Step, endReason, pose);
        }

        private Fighter CreateFighter(Side side, SwingInput input)
        {
            WeaponStats weapon = input.Rules.FindWeapon(input.State.Fighters[side].CurrentWeapon);
            bool holdsWeapon = weapon != null && input.StartPose.Get(side).HasHeldItem;
            var fighter = new Fighter(side, input.StartPose.Get(side).Get(BodyPart.Torso), holdsWeapon ? weapon : null);

            // A side without a path still drives its weapon: an empty path holds it where it is, so a free (motor)
            // weapon does not drop.
            if (holdsWeapon)
            {
                fighter.Driver = WeaponDriverFactory.Create(input.Simulation, input.Rules.Paths, _aim);
                fighter.Driver.Begin(side, input.Paths[side] ?? WeaponPath.Empty, weapon);
            }

            return fighter;
        }

        private void Drive(Fighter fighter, SimTime stepStart, SimTime stepEnd)
        {
            _world.SetRootTarget(fighter.Side, fighter.RootTarget);
            if (fighter.Driver == null) return;

            HeldItemCommand command = fighter.Driver.Drive(stepStart, stepEnd, fighter.RootTarget, _world.GetHeldItemState(fighter.Side));
            if (command.Kind == HeldItemCommandKind.MoveTo)
            {
                _world.SetHeldItemTarget(fighter.Side, command.Target);
            }
            else
            {
                _world.PushHeldItem(fighter.Side, command.AccelerationUnitsPerSecondSquared,
                    command.AngularAccelerationDegreesPerSecondSquared);
            }
        }

        private static bool IsDone(Fighter fighter, SimTime now) => fighter.Driver == null || fighter.Driver.IsComplete(now);

        private static void Remember(PerSide<Fighter> fighters, SimTime now, PoseSnapshot pose)
        {
            fighters.Left.Remember(now, pose.Left);
            fighters.Right.Remember(now, pose.Right);
        }

        private void CollectContacts(SimClock clock, PerSide<Fighter> fighters, List<SwingContact> contacts)
        {
            _drained.Clear();
            _world.DrainContacts(_drained);
            int firstOfStep = contacts.Count;
            foreach (ContactFacts facts in _drained)
            {
                Fighter weaponA = WeaponOf(facts.A, fighters);
                Fighter weaponB = WeaponOf(facts.B, fighters);
                Fighter weapon = weaponA ?? weaponB;
                SimTime time = clock.Now;
                float distance = 0f;
                if (weapon != null)
                {
                    time = weapon.RefineImpactTime(facts.PointUnits, _world.TouchDistanceUnits);
                    if (weaponA != null && weaponB != null)
                    {
                        SimTime other = weaponB.RefineImpactTime(facts.PointUnits, _world.TouchDistanceUnits);
                        if (other < time) time = other;
                    }

                    distance = weapon.Driver != null ? weapon.Driver.DistanceAlongPathUnits(time) : 0f;
                }

                contacts.Add(new SwingContact(facts, clock.Step, time, weapon?.Side, distance,
                    ContactAngle.Degrees(facts.Normal, facts.RelativeVelocityUnitsPerSecond)));
            }

            SortByTime(contacts, firstOfStep);
        }

        private static Fighter WeaponOf(BodyTag tag, PerSide<Fighter> fighters)
        {
            if (tag.Role != BodyRole.HeldItem || !tag.Owner.HasValue) return null;
            Fighter fighter = fighters[tag.Owner.Value];
            return fighter.Weapon != null ? fighter : null;
        }

        /// <summary>Stable insertion sort of one step's contacts by time; equal times keep the engine's order.</summary>
        private static void SortByTime(List<SwingContact> contacts, int start)
        {
            for (int i = start + 1; i < contacts.Count; i++)
            {
                SwingContact current = contacts[i];
                int j = i - 1;
                while (j >= start && contacts[j].Time > current.Time)
                {
                    contacts[j + 1] = contacts[j];
                    j--;
                }

                contacts[j + 1] = current;
            }
        }

        /// <summary>One side during the swing: where its root stands, its weapon, its driver and its blade's recent poses.</summary>
        private sealed class Fighter
        {
            private readonly List<PoseSample> _bladeHistory = new List<PoseSample>(RefinementWindowSteps + 1);

            public Side Side { get; }
            public BodyPose RootTarget { get; }
            public WeaponStats Weapon { get; }
            public IWeaponDriver Driver { get; set; }

            public Fighter(Side side, BodyPose rootTarget, WeaponStats weapon)
            {
                Side = side;
                RootTarget = rootTarget;
                Weapon = weapon;
            }

            public void Remember(SimTime time, FighterPose pose)
            {
                if (Weapon == null) return;
                if (_bladeHistory.Count > RefinementWindowSteps) _bladeHistory.RemoveAt(0);
                _bladeHistory.Add(new PoseSample(time, pose.HeldItem));
            }

            public SimTime RefineImpactTime(Vec2 pointUnits, float touchDistanceUnits) =>
                ImpactTimeRefiner.Refine(pointUnits, new BladeShape(Weapon.LengthUnits, Weapon.InkThicknessUnits), touchDistanceUnits,
                    _bladeHistory);
        }
    }
}
