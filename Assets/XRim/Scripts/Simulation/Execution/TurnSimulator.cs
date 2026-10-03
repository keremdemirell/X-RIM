using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// Resolves one turn up front, faster than real time, as a pure function of the frozen board, both plans and the
    /// settings (ARCHITECTURE §1, §6). It loads the board at zero velocity (GDD §3 stance persistence) and steps the physics
    /// world at a fixed rate on <see cref="SimClock"/>. Each step:
    /// <list type="number">
    /// <item>every dummy's body-move driver (§5) sets its root target, shifted by any knockback it took (E1) and kept inside
    /// solid arena edges (<see cref="IArenaEdgePolicy"/>, §13), and bends its legs so the soles land where the move wants them
    /// (<see cref="LegPoser"/>); its weapon driver moves the weapon along the path at the weapon's own speed, in a frame that
    /// travels with that root (§6, §9);</item>
    /// <item>the world steps and the pose is recorded;</item>
    /// <item>the step's new contacts get a time-to-impact refined from the blade's motion (d = v·t) and wait until no earlier or
    /// equally early contact can still be reported (one or two more steps), then go to the contact handler in <see cref="TurnContactOrder"/>:
    /// the rules' seam (Simulation detects, Rules decide, Simulation applies through <see cref="ITurnActions"/>). So the first
    /// weapon to reach a hitbox is always handled first (§9).</item>
    /// </list>
    /// The turn ends once both paths, body moves and knockbacks are done and physics has stayed settled for
    /// <see cref="SimulationSettings.SettleStepsRequired"/> steps, or at the execution hard cap (§3). The last pose is
    /// the next turn's frozen board.
    /// </summary>
    public sealed class TurnSimulator : ITurnSimulator
    {
        /// <summary>Box2D reports a new contact one step after the motion that made it, so two steps are searched.</summary>
        private const int RefinementWindowSteps = 2;

        private readonly IPhysicsWorld _world;
        private readonly IWeaponAimModel _aim;
        private readonly IBodyMoveDriverFactory _bodyMoves;
        private readonly ITurnContactHandler _contactHandler;
        private readonly List<ContactFacts> _drained = new List<ContactFacts>();
        private readonly List<TurnContact> _pendingContacts = new List<TurnContact>();
        private readonly List<TurnContact> _releasedContacts = new List<TurnContact>();
        private ArenaEdges _edges;
        private float _rootMarginUnits;
        private float _soleMarginUnits;
        private bool _pathTiltsWithLean;
        private RagdollSegmentation _segmentation;
        private RagdollSettings _body;

        private RulePolicies Policies { get; }

        public TurnSimulator(IPhysicsWorld world, RulePolicies policies, TurnSimulatorOptions options = null)
        {
            _world = Guard.NotNull(world, nameof(world));
            Policies = Guard.NotNull(policies, nameof(policies));
            options = options ?? new TurnSimulatorOptions();
            _aim = options.Aim ?? new AimFromShoulderModel();
            _bodyMoves = options.BodyMoves ?? new StanceBodyMoveDriverFactory();
            _contactHandler = options.Contacts ?? new HitContactHandler();
        }

        public TurnResult Simulate(TurnInput input)
        {
            Guard.NotNull(input, nameof(input));
            RulesSettings rules = input.Rules;
            SimulationSettings simulation = input.Simulation;
            MatchState state = input.Board.State.Clone();
            IArenaEdgePolicy edgePolicy = Guard.NotNull(Policies.ArenaEdge, nameof(RulePolicies.ArenaEdge));
            _edges = edgePolicy.EdgesFor(rules.Arena);
            _rootMarginUnits = simulation.Ragdoll.TorsoWidthUnits * 0.5f;
            _soleMarginUnits = simulation.Ragdoll.LegWidthUnits * 0.5f;
            _pathTiltsWithLean = rules.Paths.PathTiltsWithTorsoLean;
            _segmentation = simulation.Segmentation;
            _body = simulation.Ragdoll;
            _pendingContacts.Clear();
            _world.Load(input.Board.Pose, state, rules, simulation, _edges);

            var clock = new SimClock(simulation.StepRateHz);
            var recorder = new TimelineRecorder();
            PerSide<BodyMove> moves = PerSide<BodyMove>.Create(side => PlannedMove(input.Plans[side]));
            PerSide<Fighter> fighters = PerSide<Fighter>.Create(side => CreateFighter(side, moves[side], input, state));
            var actions = new FighterActions(_world, fighters, clock, simulation.HitReaction);
            var context = new TurnContactContext(state, rules, Policies, simulation,
                PerSide<BodyMoveStats>.Create(side => rules.FindBodyMove(moves[side])), actions, recorder);
            _contactHandler.BeginTurn(context);
            RecordBodyMoves(moves, recorder);

            PoseSnapshot pose = _world.CapturePose();
            recorder.RecordFrame(clock.Step, clock.Now, pose);
            int lastRecordedStep = clock.Step;
            Remember(fighters, clock.Now, pose);

            int maxSteps = clock.StepsFor(rules.Match.ExecutionHardCapSeconds);
            int settledSteps = 0;
            TurnEndReason endReason = TurnEndReason.HardCap;
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

                HandleContacts(clock, fighters, context);

                bool movesDone = fighters.Left.IsDone(clock.Now) && fighters.Right.IsDone(clock.Now);
                settledSteps = movesDone &&
                               _world.IsSettled(simulation.SettleLinearSpeedUnitsPerSecond, simulation.SettleAngularSpeedDegreesPerSecond)
                    ? settledSteps + 1
                    : 0;
                if (settledSteps >= simulation.SettleStepsRequired)
                {
                    endReason = TurnEndReason.Settled;
                    break;
                }
            }

            ReleaseContacts(null, context);
            if (lastRecordedStep != clock.Step) recorder.RecordFrame(clock.Step, clock.Now, pose);

            var finalBoard = new BoardSnapshot(state.Clone(), pose.Clone());
            PerSide<SimTime?> firstHits = context.FirstValidHitTime;
            var report = new ExecutionReport(state, new PerSide<SimTime?>(firstHits.Left, firstHits.Right), OnTheSameStep(firstHits, clock));
            return new TurnResult(state.TurnIndex, recorder.Build(), finalBoard, report, endReason);
        }

        private static BodyMove PlannedMove(TurnPlan plan) => plan != null ? plan.BodyMove : BodyMove.None;

        /// <summary>Both first hits landed during the motion of one simulation step (the §14 exact-tie case).</summary>
        private static bool OnTheSameStep(PerSide<SimTime?> firstHits, SimClock clock) =>
            firstHits.Left.HasValue && firstHits.Right.HasValue &&
            clock.StepContaining(firstHits.Left.Value) == clock.StepContaining(firstHits.Right.Value);

        /// <summary>
        /// Each chosen body move is a fact of the turn's result from its first instant: the player's input, which is what the
        /// electric wall reads (§13), never what happened to the body.
        /// </summary>
        private static void RecordBodyMoves(PerSide<BodyMove> moves, TimelineRecorder recorder)
        {
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                if (moves[side] != BodyMove.None) recorder.RecordEvent(new BodyMoveStartedEvent(SimTime.Zero, side, moves[side]));
            }
        }

        private Fighter CreateFighter(Side side, BodyMove move, TurnInput input, MatchState state)
        {
            FighterPose start = input.Board.Pose.Get(side);
            TurnPlan plan = input.Plans[side];
            FighterState fighterState = state.Fighters[side];
            BodyMoveStats moveStats = input.Rules.FindBodyMove(move);

            IBodyMoveDriver bodyMove = _bodyMoves.Create(move, input.Rules, input.Simulation);
            var moveStart = BodyMoveStart.Of(side, start, fighterState.Handedness, input.Simulation.Ragdoll);
            bodyMove.Begin(moveStats, moveStart);
            var legs = new Legs(fighterState, LegGeometry.FrontLeg(fighterState.Handedness), LegGeometry.BackLeg(fighterState.Handedness));
            float startRotation = moveStart.Root.RotationDegrees;

            // The world loads the weapon the rules state holds (the plan's weapon, set when planning locked). A side
            // without a path still drives its weapon: an empty path holds it, so a free (motor) weapon does not drop.
            WeaponStats weapon = input.Rules.FindWeapon(fighterState.CurrentWeapon);
            if (weapon == null || !start.HasHeldItem) return new Fighter(side, bodyMove, legs, startRotation, null, 0f, null);

            // D13 (TBD §5, default 0): a body move may speed the weapon along its path for the whole turn.
            float speedMultiplier = 1f + (moveStats != null ? moveStats.WeaponSpeedBonusFraction : 0f);
            WeaponStats pathWeapon = weapon.WithSpeedMultiplier(speedMultiplier);
            IWeaponDriver driver = WeaponDriverFactory.Create(input.Simulation, input.Rules.Paths, _aim);
            driver.Begin(side, plan != null ? plan.Path : WeaponPath.Empty, pathWeapon);
            return new Fighter(side, bodyMove, legs, startRotation, weapon, pathWeapon.SpeedUnitsPerSecond, driver);
        }

        private void Drive(Fighter fighter, SimTime stepStart, SimTime stepEnd)
        {
            BodyMoveFrame frame = fighter.BodyMove.Evaluate(stepEnd);
            Vec2 knock = fighter.KnockOffsetAt(stepEnd);
            BodyPose root = KeepInside(Shifted(frame.Root, knock));
            _world.SetRootTarget(fighter.Side, root);
            PoseLegs(fighter, root, frame, knock);
            if (fighter.WeaponDriver == null) return;

            HeldItemCommand command = fighter.WeaponDriver.Drive(stepStart, stepEnd, PathFrameAt(fighter, stepStart),
                PathFrame(fighter, root), _world.GetHeldItemState(fighter.Side));
            switch (command.Kind)
            {
                case HeldItemCommandKind.MoveTo:
                    _world.SetHeldItemTarget(fighter.Side, command.Target);
                    break;
                case HeldItemCommandKind.Release:
                    _world.ReleaseHeldItem(fighter.Side, command.VelocityUnitsPerSecond, command.AngularVelocityDegreesPerSecond);
                    break;
                default:
                    _world.PushHeldItem(fighter.Side, command.AccelerationUnitsPerSecondSquared,
                        command.AngularAccelerationDegreesPerSecondSquared);
                    break;
            }
        }

        private static BodyPose Shifted(BodyPose pose, Vec2 offsetUnits) => new BodyPose(pose.PositionUnits + offsetUnits, pose.RotationDegrees);

        /// <summary>Never planned past a solid edge: the torso stays half its width inside.</summary>
        private BodyPose KeepInside(BodyPose root) =>
            new BodyPose(new Vec2(_edges.ClampX(root.PositionUnits.X, _rootMarginUnits), root.PositionUnits.Y), root.RotationDegrees);

        /// <summary>
        /// The frame the path is drawn in, at a time: it travels with the root, knockback included (§6, Decided), and keeps the
        /// angle the path was drawn at, unless <see cref="PathSettings.PathTiltsWithTorsoLean"/> lets a move's lean tilt it.
        /// </summary>
        private BodyPose PathFrameAt(Fighter fighter, SimTime time) =>
            PathFrame(fighter, KeepInside(Shifted(fighter.BodyMove.Evaluate(time).Root, fighter.KnockOffsetAt(time))));

        private BodyPose PathFrame(Fighter fighter, BodyPose root) =>
            _pathTiltsWithLean ? root : new BodyPose(root.PositionUnits, fighter.StartRotationDegrees);

        /// <summary>
        /// Bends each leg the dummy still has so its sole lands on the move's target, carried along by any knockback, kept inside
        /// solid edges.
        /// </summary>
        private void PoseLegs(Fighter fighter, BodyPose root, BodyMoveFrame frame, Vec2 knock)
        {
            Legs legs = fighter.Legs;
            if (legs.HasFront) PoseLeg(fighter.Side, legs.Front, true, root, frame.FrontSoleUnits + knock);
            if (legs.HasBack) PoseLeg(fighter.Side, legs.Back, false, root, frame.BackSoleUnits + knock);
        }

        private void PoseLeg(Side side, BodyPart leg, bool isFront, BodyPose root, Vec2 soleUnits)
        {
            var sole = new Vec2(_edges.ClampX(soleUnits.X, _soleMarginUnits), soleUnits.Y);
            _world.SetLimbTarget(side, leg, LegPoser.Solve(root, sole, side, isFront, _segmentation, _body));
        }

        private static void Remember(PerSide<Fighter> fighters, SimTime now, PoseSnapshot pose)
        {
            fighters.Left.Remember(now, pose.Left);
            fighters.Right.Remember(now, pose.Right);
        }

        /// <summary>
        /// Measures the step's new contacts and hands over every contact that is now final. A contact reported after a later step
        /// is refined at the earliest to this step's start time, so everything strictly before it is settled; contacts at exactly
        /// that time wait, so an instant's contacts are always handed over together.
        /// </summary>
        private void HandleContacts(SimClock clock, PerSide<Fighter> fighters, TurnContactContext context)
        {
            _drained.Clear();
            _world.DrainContacts(_drained);
            foreach (ContactFacts facts in _drained)
            {
                _pendingContacts.Add(Measure(facts, clock, fighters));
            }

            ReleaseContacts(clock.TimeAtStep(clock.Step - RefinementWindowSteps + 1), context);
        }

        /// <param name="before">Hand over the contacts earlier than this; null hands over all of them (the turn is over).</param>
        private void ReleaseContacts(SimTime? before, TurnContactContext context)
        {
            if (_pendingContacts.Count == 0) return;
            TurnContactOrder.Sort(_pendingContacts);

            int count = 0;
            while (count < _pendingContacts.Count && (!before.HasValue || _pendingContacts[count].Time < before.Value)) count++;
            if (count == 0) return;

            _releasedContacts.Clear();
            for (int i = 0; i < count; i++)
            {
                _releasedContacts.Add(_pendingContacts[i]);
            }

            _pendingContacts.RemoveRange(0, count);
            _contactHandler.Handle(_releasedContacts, context);
        }

        /// <summary>When the contact happened (refined from the blade's motion for a weapon) and how far along its path.</summary>
        private TurnContact Measure(ContactFacts facts, SimClock clock, PerSide<Fighter> fighters)
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

                distance = weapon.WeaponDriver.DistanceAlongPathUnits(time);
            }

            return new TurnContact(facts, clock.Step, time, weapon?.Side, distance,
                ContactAngle.Degrees(facts.Normal, facts.RelativeVelocityUnitsPerSecond));
        }

        private static Fighter WeaponOf(BodyTag tag, PerSide<Fighter> fighters)
        {
            if (tag.Role != BodyRole.HeldItem || !tag.Owner.HasValue) return null;
            Fighter fighter = fighters[tag.Owner.Value];
            return fighter.WeaponDriver != null ? fighter : null;
        }

        /// <summary>Which leg leads a stride, and which legs the dummy still has (a severed leg is not posed).</summary>
        private readonly struct Legs
        {
            public BodyPart Front { get; }
            public BodyPart Back { get; }
            public bool HasFront { get; }
            public bool HasBack { get; }

            public Legs(FighterState fighter, BodyPart front, BodyPart back)
            {
                Front = front;
                Back = back;
                HasFront = !fighter.IsSevered(front);
                HasBack = !fighter.IsSevered(back);
            }
        }

        /// <summary>
        /// One side during the turn: its body move and legs, its weapon and driver, its blade's recent poses and the knockbacks
        /// it has taken.
        /// </summary>
        private sealed class Fighter
        {
            private readonly List<PoseSample> _bladeHistory = new List<PoseSample>(RefinementWindowSteps + 1);
            private readonly List<Knock> _knocks = new List<Knock>();

            public Side Side { get; }
            public IBodyMoveDriver BodyMove { get; }
            public Legs Legs { get; }

            /// <summary>The turn-start root's angle: the angle the path was drawn at.</summary>
            public float StartRotationDegrees { get; }

            /// <summary>The weapon's own stats; null when the dummy holds nothing.</summary>
            public WeaponStats Weapon { get; }

            /// <summary>Its path speed this turn, with a body move's speed bonus (D13).</summary>
            public float PathSpeedUnitsPerSecond { get; }

            public IWeaponDriver WeaponDriver { get; }

            public Fighter(Side side, IBodyMoveDriver bodyMove, Legs legs, float startRotationDegrees, WeaponStats weapon,
                float pathSpeedUnitsPerSecond, IWeaponDriver weaponDriver)
            {
                Side = side;
                BodyMove = bodyMove;
                Legs = legs;
                StartRotationDegrees = startRotationDegrees;
                Weapon = weapon;
                PathSpeedUnitsPerSecond = pathSpeedUnitsPerSecond;
                WeaponDriver = weaponDriver;
            }

            public bool IsDone(SimTime time) =>
                BodyMove.IsComplete(time) && (WeaponDriver == null || WeaponDriver.IsComplete(time)) && AreKnocksOver(time);

            public void Remember(SimTime time, FighterPose pose)
            {
                if (Weapon == null) return;
                if (_bladeHistory.Count > RefinementWindowSteps) _bladeHistory.RemoveAt(0);
                _bladeHistory.Add(new PoseSample(time, pose.HeldItem));
            }

            public SimTime RefineImpactTime(Vec2 pointUnits, float touchDistanceUnits) =>
                ImpactTimeRefiner.Refine(pointUnits, new BladeShape(Weapon.LengthUnits, Weapon.InkThicknessUnits), touchDistanceUnits,
                    _bladeHistory);

            public void KnockBack(SimTime start, Vec2 displacementUnits, float seconds) => _knocks.Add(new Knock(start, displacementUnits, seconds));

            /// <summary>How far every knockback so far has moved the standing point by this time; each eases out over its time.</summary>
            public Vec2 KnockOffsetAt(SimTime time)
            {
                Vec2 offset = Vec2.Zero;
                foreach (Knock knock in _knocks)
                {
                    offset += knock.OffsetAt(time);
                }

                return offset;
            }

            private bool AreKnocksOver(SimTime time)
            {
                foreach (Knock knock in _knocks)
                {
                    if (!knock.IsOver(time)) return false;
                }

                return true;
            }
        }

        /// <summary>One knockback (E1): the standing point moves by a displacement, fast first, then easing to a stop.</summary>
        private readonly struct Knock
        {
            private readonly SimTime _start;
            private readonly Vec2 _displacementUnits;
            private readonly float _seconds;

            public Knock(SimTime start, Vec2 displacementUnits, float seconds)
            {
                _start = start;
                _displacementUnits = displacementUnits;
                _seconds = seconds;
            }

            public Vec2 OffsetAt(SimTime time)
            {
                if (time <= _start) return Vec2.Zero;
                float progress = _seconds > 0f ? XMath.Clamp01((float)((time - _start).Seconds / _seconds)) : 1f;
                float easedOut = 1f - (1f - progress) * (1f - progress);
                return _displacementUnits * easedOut;
            }

            public bool IsOver(SimTime time) => (time - _start).Seconds >= _seconds;
        }

        /// <summary>What the contact handler does to the fighters, from the start of the next step.</summary>
        private sealed class FighterActions : ITurnActions
        {
            private readonly IPhysicsWorld _world;
            private readonly PerSide<Fighter> _fighters;
            private readonly SimClock _clock;
            private readonly HitReactionSettings _reaction;

            public FighterActions(IPhysicsWorld world, PerSide<Fighter> fighters, SimClock clock, HitReactionSettings reaction)
            {
                _world = world;
                _fighters = fighters;
                _clock = clock;
                _reaction = Guard.NotNull(reaction, nameof(reaction));
            }

            public SimTime Now => _clock.Now;

            public WeaponStats HeldWeapon(Side side) => _fighters[side].Weapon;

            public float WeaponSpeedUnitsPerSecond(Side side) => _fighters[side].PathSpeedUnitsPerSecond;

            public bool IsWeaponTravelling(Side side, SimTime time)
            {
                IWeaponDriver driver = _fighters[side].WeaponDriver;
                return driver != null && driver.IsTravelling(time);
            }

            public void SlowWeapon(Side side, float speedFraction) => _fighters[side].WeaponDriver?.SlowTo(Now, speedFraction);

            public void StopWeapon(Side side, WeaponStopKind kind) => _fighters[side].WeaponDriver?.Stop(Now, kind);

            public void ApplyImpulse(Side side, BodyPart part, Vec2 impulse) => _world.ApplyImpulse(side, part, impulse);

            public void KnockBack(Side side, Vec2 displacementUnits) =>
                _fighters[side].KnockBack(Now, displacementUnits, _reaction.KnockbackSeconds);
        }
    }
}
