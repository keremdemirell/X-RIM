using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using Object = UnityEngine.Object;

namespace XRim.Tests.PlayMode.Simulation
{
    /// <summary>
    /// Body moves on real Unity 2D physics (GDD §5, Session 05): two right-handed dummies with rapiers in the guard stance,
    /// mirrored around x = 0. The intended effects are physical outcomes, not special rules: a crouch drops the head under a
    /// high strike, a jump lifts the feet over a low sweep, a lunge and a step move the body, and the drawn path travels with
    /// the torso. Every test logs what it measured ("[XRim body moves]") so a near miss can be tuned from the numbers.
    /// </summary>
    public sealed class BodyMovePlayModeTests
    {
        /// <summary>Pelvis to pelvis 1200: nothing touches unless a test makes it.</summary>
        private const float WideHalfGapUnits = 600f;

        /// <summary>Pelvis to pelvis 600: each rapier reaches the other's body (arm 240 + rapier 400 from the shoulder).</summary>
        private const float CloseHalfGapUnits = 300f;

        /// <summary>
        /// The root target is at full extent exactly when the duration ends (EditMode tests); the physical torso, pulled by a
        /// strength-limited joint, a moment later. It is measured this long after the duration, within <see cref="LagToleranceUnits"/>.
        /// </summary>
        private const double ReachGraceSeconds = 0.05;

        private const float LagToleranceUnits = 12f;

        /// <summary>How close the torso must be to the move's full extent once the turn has settled.</summary>
        private const float SettledToleranceUnits = 5f;

        /// <summary>How far below the configured height the torso may peak in a jump (it is dragging its own limbs up).</summary>
        private const float JumpPeakToleranceUnits = 15f;

        /// <summary>
        /// While a move is fast, the physical pelvis may trail its root target by up to this much: a strength-limited joint pulls
        /// it, so the exactly driven weapon leads the body a little. A bound that says body and weapon never come apart.
        /// </summary>
        private const float MaxBodyLagUnits = 50f;

        /// <summary>The kinematic blade sits on the path in the frame its root target defines (allowing Unity's one-step delay).</summary>
        private const float DrivenFrameToleranceUnits = 0.5f;

        /// <summary>A crouched knee must stand at least this far ahead of the pelvis, toward the opponent.</summary>
        private const float KneeAheadMinUnits = 10f;

        /// <summary>A lean in place must carry the head at least this far away from the opponent (25° moves it about 60).</summary>
        private const float LeanHeadShiftMinUnits = 30f;

        /// <summary>
        /// The thrust each path test draws, straight at the opponent from the guard tip. It ends 568 from the shoulder, inside
        /// the reach (arm 240 + rapier 400), so the weapon can follow it all the way.
        /// </summary>
        private const float ThrustLengthUnits = 200f;

        /// <summary>How much lower than drawn the crouching thrust must land for "a duck plus a straight line is a low thrust".</summary>
        private const float LowThrustMinDropUnits = 40f;

        private static readonly BodyMove[] Swipes = { BodyMove.Crouch, BodyMove.Lunge, BodyMove.StepBack, BodyMove.Jump };

        private SimulationMode2D _previousMode;
        private ArenaSpace _space;
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private RagdollSettings _body;
        private WeaponStats _rapier;
        private RulePolicies _policies;
        private Ragdoll _sixBodies;
        private Ragdoll _tenBodies;
        private readonly AimFromShoulderModel _aim = new AimFromShoulderModel();
        private readonly List<Unity2DPhysicsWorld> _worlds = new List<Unity2DPhysicsWorld>();

        [SetUp]
        public void SetUp()
        {
            _previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            _space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _body = _simulation.Ragdoll;
            _rapier = _rules.FindWeapon(WeaponIds.Rapier);
            _policies = new RulePolicies();
            _sixBodies = BuildTemplate(RagdollSegmentation.SixBodies);
            _tenBodies = BuildTemplate(RagdollSegmentation.TenBodies);
            _worlds.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Unity2DPhysicsWorld world in _worlds)
            {
                Scene scene = world.Scene;
                world.Dispose();
                yield return new WaitUntil(() => !scene.isLoaded);
            }

            Object.DestroyImmediate(_sixBodies.gameObject);
            Object.DestroyImmediate(_tenBodies.gameObject);
            Physics2D.simulationMode = _previousMode;
        }

        // --- Set-up and measuring ---------------------------------------------------------------------------------------

        private Ragdoll BuildTemplate(RagdollSegmentation segmentation)
        {
            Ragdoll ragdoll = PlaceholderRagdollBuilder.Build(new RagdollBuildSpec(_body, _rules.Paths, segmentation, _space, _rules.Weapons, null));
            ragdoll.gameObject.SetActive(false);
            return ragdoll;
        }

        private float StandingHeight => LegGeometry.StandingPelvisHeightUnits(_body);

        private PoseSnapshot Board(float halfGapUnits) => new PoseSnapshot
        {
            Left = Guard(Side.Left, -halfGapUnits),
            Right = Guard(Side.Right, halfGapUnits),
        };

        private FighterPose Guard(Side side, float x) => GuardStance.Create(new BodyPose(new Vec2(x, StandingHeight), 0f), side, BodyPart.RightArm,
            _rapier, _simulation.Segmentation, _body, _rules.Paths, _aim);

        private MatchState State() => new MatchState(
            PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        private static TurnPlan Plan(BodyMove move, WeaponPath path = null) =>
            new TurnPlan(WeaponIds.Rapier, move, path ?? WeaponPath.Empty, default, true);

        private TurnResult Simulate(PoseSnapshot board, TurnPlan left, TurnPlan right)
        {
            var world = new Unity2DPhysicsWorld(_space, new RagdollPrefabSet(_sixBodies, _tenBodies));
            _worlds.Add(world);
            var input = new TurnInput(new BoardSnapshot(State(), board), new PerSide<TurnPlan>(left, right), _rules, _simulation);
            // Body-move geometry: raw contacts, without the hit rules (Session 06), which would slow, stop or knock the dummies.
            return new TurnSimulator(world, _policies, new TurnSimulatorOptions { Contacts = new RecordContactsHandler() }).Simulate(input);
        }

        private Vec2 Tip(BodyPose grip) => grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * _rapier.LengthUnits;

        /// <summary>The weapon tip on the frozen board, in the frame the turn starts in.</summary>
        private Vec2 TipLocal(PoseSnapshot board, Side side) =>
            TorsoFrame.ToLocal(Tip(board.Get(side).HeldItem), TurnStartRoot.Of(board.Get(side)), side);

        private WeaponPath ThrustForward(PoseSnapshot board, Side side)
        {
            Vec2 start = TipLocal(board, side);
            return new WeaponPath(new[] { start, start + Vec2.UnitX * ThrustLengthUnits });
        }

        private static TimelineFrame FrameAt(TurnResult result, double seconds)
        {
            foreach (TimelineFrame frame in result.Timeline.Frames)
            {
                if (frame.Time.Seconds >= seconds - 1e-9) return frame;
            }

            return LastFrame(result);
        }

        private static TimelineFrame LastFrame(TurnResult result) => result.Timeline.Frames[result.Timeline.Frames.Count - 1];

        private static Vec2 Pelvis(PoseSnapshot pose, Side side) => pose.Get(side).Get(BodyPart.Torso).PositionUnits;

        /// <summary>The head's centre: its body pivots at the neck and the circle sits on top of it.</summary>
        private Vec2 HeadCentre(PoseSnapshot pose, Side side)
        {
            BodyPose head = pose.Get(side).Get(BodyPart.Head);
            return head.PositionUnits + new Vec2(0f, _body.HeadDiameterUnits * 0.5f).Rotated(head.RotationDegrees);
        }

        private float HeadTop(PoseSnapshot pose, Side side) => HeadCentre(pose, side).Y + _body.HeadDiameterUnits * 0.5f;

        /// <summary>The lowest point of a dummy's two feet (the lower corner of each leg's flat end).</summary>
        private float LowestFoot(PoseSnapshot pose, Side side)
        {
            FighterPose fighter = pose.Get(side);
            float lowest = float.MaxValue;
            foreach (BodyPart leg in new[] { BodyPart.LeftLeg, BodyPart.RightLeg })
            {
                float tilt = (fighter.HasLowerSegments ? fighter.GetLower(leg) : fighter.Get(leg)).RotationDegrees;
                float corner = _body.LegWidthUnits * 0.5f * Mathf.Abs(Mathf.Sin(tilt * Mathf.Deg2Rad));
                lowest = Mathf.Min(lowest, LegGeometry.SoleOf(fighter, leg, _body).Y - corner);
            }

            return lowest;
        }

        /// <summary>Where the move puts the pelvis once it is at full extent (a hop: back on its feet).</summary>
        private Vec2 FullExtent(BodyMoveStats stats, Side side, Vec2 start)
        {
            if (stats.IsLeanInPlace) return start;
            float y = stats.DisplacementUnits.Y > 0f ? StandingHeight : StandingHeight + stats.DisplacementUnits.Y;
            return new Vec2(start.X + side.FacingSign() * stats.DisplacementUnits.X, y);
        }

        private static void Report(string format, params object[] args) =>
            Debug.Log("[XRim body moves] " + string.Format(CultureInfo.InvariantCulture, format, args));

        private static bool IsTagged(BodyTag tag, Side owner, BodyRole role, BodyPart? part = null) =>
            tag.Owner == owner && tag.Role == role && (!part.HasValue || tag.Part == part.Value);

        private static List<ContactEvent> Contacts(TurnResult result, Side weaponOwner, Side bodyOwner, BodyPart? part)
        {
            var found = new List<ContactEvent>();
            foreach (MatchEvent matchEvent in result.Timeline.Events)
            {
                if (!(matchEvent is ContactEvent contact)) continue;
                ContactFacts facts = contact.Contact.Facts;
                bool weaponOnBody = (IsTagged(facts.A, weaponOwner, BodyRole.HeldItem) && IsTagged(facts.B, bodyOwner, BodyRole.BodyPart, part)) ||
                                    (IsTagged(facts.B, weaponOwner, BodyRole.HeldItem) && IsTagged(facts.A, bodyOwner, BodyRole.BodyPart, part));
                if (weaponOnBody) found.Add(contact);
            }

            return found;
        }

        /// <summary>
        /// Both dummies make the same move. Each side's pelvis must be at the move's full extent a moment after its duration
        /// and once the turn settles, and the turn must settle (not hit the 1.5 s cap). A hop's full extent is its top, which the
        /// jump test measures; here it only has to be back on its feet once the turn settles (it lands with speed).
        /// </summary>
        private TurnResult AssertMoveReachesItsExtentAndSettles(BodyMove move)
        {
            BodyMoveStats stats = _rules.FindBodyMove(move);
            PoseSnapshot board = Board(WideHalfGapUnits);
            TurnResult result = Simulate(board, Plan(move), Plan(move));

            Assert.That(result.EndReason, Is.EqualTo(TurnEndReason.Settled), $"{move}: the turn settles");
            TimelineFrame reached = FrameAt(result, stats.DurationSeconds + ReachGraceSeconds);
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                Vec2 goal = FullExtent(stats, side, Pelvis(board, side));
                Vec2 atDuration = Pelvis(reached.Pose, side);
                Vec2 atEnd = Pelvis(result.FinalBoard.Pose, side);
                Report("{0} {1}: full extent ({2:0.0}, {3:0.0}); pelvis at {4:0.00} s ({5:0.0}, {6:0.0}), at the end ({7:0.0}, {8:0.0}); " +
                       "settled after {9:0.000} s", move, side, goal.X, goal.Y, reached.Time.Seconds, atDuration.X, atDuration.Y, atEnd.X, atEnd.Y,
                    LastFrame(result).Time.Seconds);
                bool isHop = stats.DisplacementUnits.Y > 0f && !stats.IsLeanInPlace;
                if (!isHop)
                    Assert.That(Vec2.Distance(atDuration, goal), Is.LessThan(LagToleranceUnits), $"{move} {side}: at full extent within its duration");
                Assert.That(Vec2.Distance(atEnd, goal), Is.LessThan(SettledToleranceUnits), $"{move} {side}: settled at full extent");
            }

            return result;
        }

        // --- Each move reaches its extent and settles -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Crouch_SinksToItsDepthWithinItsDuration_AndSettles()
        {
            AssertMoveReachesItsExtentAndSettles(BodyMove.Crouch);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Lunge_StepsTowardTheOpponentWithinItsDuration_AndSettles()
        {
            AssertMoveReachesItsExtentAndSettles(BodyMove.Lunge);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StepBack_D12Default_StepsAwayWithinItsDuration_AndSettles()
        {
            Assert.That(_rules.FindBodyMove(BodyMove.StepBack).IsLeanInPlace, Is.False, "D12 default: a real step");
            AssertMoveReachesItsExtentAndSettles(BodyMove.StepBack);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Jump_PeaksAtItsHeightWithinItsDuration_AndLandsStanding()
        {
            BodyMoveStats jump = _rules.FindBodyMove(BodyMove.Jump);
            TurnResult result = AssertMoveReachesItsExtentAndSettles(BodyMove.Jump);

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                float peak = float.MinValue;
                double peakSeconds = 0.0;
                foreach (TimelineFrame frame in result.Timeline.Frames)
                {
                    if (frame.Time.Seconds > jump.DurationSeconds) break;
                    float height = Pelvis(frame.Pose, side).Y - StandingHeight;
                    if (height <= peak) continue;
                    peak = height;
                    peakSeconds = frame.Time.Seconds;
                }

                Report("Jump {0}: pelvis peaks {1:0.0} above standing (configured {2:0.0}) at {3:0.000} s of {4:0.00} s", side, peak,
                    jump.DisplacementUnits.Y, peakSeconds, jump.DurationSeconds);
                Assert.That(peak, Is.GreaterThan(jump.DisplacementUnits.Y - JumpPeakToleranceUnits), $"{side}: the jump reaches its height");
            }

            yield return null;
        }

        // --- The intended effects, as physical outcomes -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator Crouch_DropsTheHeadUnderTheHighStrikeLine()
        {
            // A high strike: a swing at a standing dummy's head, which sits on top of its torso.
            float highStrikeLine = StandingHeight + _body.TorsoHeightUnits;
            BodyMoveStats crouch = _rules.FindBodyMove(BodyMove.Crouch);
            PoseSnapshot board = Board(WideHalfGapUnits);

            TurnResult result = Simulate(board, Plan(BodyMove.Crouch), Plan(BodyMove.Crouch));

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                float standing = HeadTop(board, side);
                float crouched = HeadTop(FrameAt(result, crouch.DurationSeconds + ReachGraceSeconds).Pose, side);
                float atEnd = HeadTop(result.FinalBoard.Pose, side);
                Report("Crouch {0}: head top standing {1:0.0}, crouched {2:0.0}, at the end {3:0.0}; high-strike line {4:0.0}", side, standing,
                    crouched, atEnd, highStrikeLine);
                Assert.That(standing, Is.GreaterThan(highStrikeLine), $"{side}: a standing head is in the strike's way");
                Assert.That(crouched, Is.LessThan(highStrikeLine), $"{side}: the crouch takes it under, within the duration");
                Assert.That(atEnd, Is.LessThan(highStrikeLine), $"{side}: and holds it there");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Crouch_BendsTheKneesTowardTheOpponent_OnBothSides()
        {
            TurnResult result = Simulate(Board(WideHalfGapUnits), Plan(BodyMove.Crouch), Plan(BodyMove.Crouch));

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                FighterPose fighter = result.FinalBoard.Pose.Get(side);
                float pelvisX = fighter.Get(BodyPart.Torso).PositionUnits.X;
                foreach (BodyPart leg in new[] { BodyPart.LeftLeg, BodyPart.RightLeg })
                {
                    float kneeAhead = (fighter.GetLower(leg).PositionUnits.X - pelvisX) * side.FacingSign();
                    Report("Crouch {0} {1}: knee {2:0.0} ahead of the pelvis", side, leg, kneeAhead);
                    Assert.That(kneeAhead, Is.GreaterThan(KneeAheadMinUnits), $"{side} {leg}: the knee points toward the opponent");
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Jump_LiftsTheFeetAboveTheLowSweepLine()
        {
            // A low sweep: a swing at shin height, a third of the way up a standing leg.
            float lowSweepLine = LegGeometry.FloorYUnits + _body.LegLengthUnits / 3f;
            TurnResult result = Simulate(Board(WideHalfGapUnits), Plan(BodyMove.Jump), Plan(BodyMove.Jump));

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                float highest = float.MinValue;
                double when = 0.0;
                foreach (TimelineFrame frame in result.Timeline.Frames)
                {
                    float feet = LowestFoot(frame.Pose, side);
                    if (feet <= highest) continue;
                    highest = feet;
                    when = frame.Time.Seconds;
                }

                Report("Jump {0}: lowest foot rises to {1:0.0} at {2:0.000} s; low-sweep line {3:0.0}; at the start {4:0.0}", side, highest, when,
                    lowSweepLine, LowestFoot(result.Timeline.Frames[0].Pose, side));
                Assert.That(LowestFoot(result.Timeline.Frames[0].Pose, side), Is.LessThan(lowSweepLine), $"{side}: standing feet are swept");
                Assert.That(highest, Is.GreaterThan(lowSweepLine), $"{side}: both feet clear the sweep at the top of the jump");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator StepBack_LeanInPlace_D12Flag_SwaysTheHeadAway_WithoutStepping()
        {
            _rules.FindBodyMove(BodyMove.StepBack).IsLeanInPlace = true;
            PoseSnapshot board = Board(WideHalfGapUnits);

            TurnResult result = AssertMoveReachesItsExtentAndSettles(BodyMove.StepBack);

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                float headAway = (HeadCentre(result.FinalBoard.Pose, side).X - HeadCentre(board, side).X) * -side.FacingSign();
                Report("Lean in place {0}: head {1:0.0} further from the opponent", side, headAway);
                Assert.That(headAway, Is.GreaterThan(LeanHeadShiftMinUnits), $"{side}: the upper body sways back");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Path_TravelsWithTheTorso_DuringEveryMove()
        {
            var moves = new List<(BodyMove Move, bool Lean)>();
            foreach (BodyMove move in Swipes) moves.Add((move, false));
            moves.Add((BodyMove.StepBack, true));
            var failures = new List<string>();

            foreach ((BodyMove move, bool lean) in moves)
            {
                _rules = GddStartingValues.CreateRulesSettings();
                BodyMoveStats stats = _rules.FindBodyMove(move);
                stats.IsLeanInPlace = lean;
                PoseSnapshot board = Board(WideHalfGapUnits);
                WeaponPath thrust = ThrustForward(board, Side.Left);
                TurnResult result = Simulate(board, Plan(move, thrust), Plan(BodyMove.None));

                // The frame the path is driven in: the move's root, upright (the path keeps the angle it was drawn at).
                var driver = new StanceBodyMoveDriver();
                driver.Begin(stats, BodyMoveStart.Of(Side.Left, board.Left, Handedness.Right, _body));
                float worstDriven = 0f;
                float worstLag = 0f;
                double worstLagSeconds = 0.0;
                double stepSeconds = 1.0 / _simulation.StepRateHz;
                foreach (TimelineFrame frame in result.Timeline.Frames)
                {
                    // Unity moves a kinematic body onto its target up to one step late (Session 02 measured it: 3.7 units for the
                    // rapier, one step of its travel), so the tip counts as on the path where it is now or was one step earlier.
                    Vec2 tip = Tip(frame.Pose.Left.HeldItem);
                    double earlier = Math.Max(0.0, frame.Time.Seconds - stepSeconds);
                    float offPath = Math.Min(OffPath(tip, thrust, driver, frame.Time.Seconds), OffPath(tip, thrust, driver, earlier));
                    worstDriven = Math.Max(worstDriven, offPath);
                    Vec2 root = driver.Evaluate(frame.Time).Root.PositionUnits;
                    float lag = Vec2.Distance(Pelvis(frame.Pose, Side.Left), root);
                    if (lag <= worstLag) continue;
                    worstLag = lag;
                    worstLagSeconds = frame.Time.Seconds;
                }

                string name = lean ? "StepBack (lean in place)" : move.ToString();
                Report("Path with {0}: tip off the path by at most {1:0.00} in the move's frame; the pelvis trails its root by at most " +
                       "{2:0.0} (at {3:0.000} s)", name, worstDriven, worstLag, worstLagSeconds);
                if (worstDriven >= DrivenFrameToleranceUnits) failures.Add($"{name}: the tip is {worstDriven:0.00} off the path in the move's frame");
                if (worstLag >= MaxBodyLagUnits) failures.Add($"{name}: the pelvis trails its root by {worstLag:0.0}");
            }

            Assert.That(failures, Is.Empty, "the path rides every move exactly, and the body stays with it");

            yield return null;
        }

        /// <summary>
        /// The reference legendary moment (GDD §1, §6): one dummy ducks under a high swing and, in the same turn, its straight
        /// thrust lands low on the opponent. Control run: the same swing hits a dummy that does not duck.
        /// </summary>
        [UnityTest]
        public IEnumerator DuckUnderAHighSwing_WhileAStraightThrustLandsLow()
        {
            PoseSnapshot board = Board(CloseHalfGapUnits);

            // Left: up from the guard, then a level thrust at chest height (standing), into the opponent's body.
            float chest = _rules.Paths.ShoulderOffsetUnits.Y;
            var thrust = new WeaponPath(new[] { TipLocal(board, Side.Left), new Vec2(450f, chest), new Vec2(620f, chest) });

            // Right: up from the guard, then a level swing across where the standing left dummy's head is.
            float headHeight = _body.TorsoHeightUnits + _body.HeadDiameterUnits * 0.5f;
            var swing = new WeaponPath(new[] { TipLocal(board, Side.Right), new Vec2(350f, headHeight), new Vec2(640f, headHeight) });

            TurnResult standing = Simulate(board, Plan(BodyMove.None, thrust), Plan(BodyMove.None, swing));
            TurnResult ducking = Simulate(board, Plan(BodyMove.Crouch, thrust), Plan(BodyMove.None, swing));

            List<ContactEvent> headHitStanding = Contacts(standing, Side.Right, Side.Left, BodyPart.Head);
            List<ContactEvent> headHitDucking = Contacts(ducking, Side.Right, Side.Left, BodyPart.Head);
            List<ContactEvent> thrustStanding = Contacts(standing, Side.Left, Side.Right, BodyPart.Torso);
            List<ContactEvent> thrustDucking = Contacts(ducking, Side.Left, Side.Right, BodyPart.Torso);
            Report("Duck: swing hits the head standing {0} time(s), ducking {1}; thrust meets the torso standing at y {2}, ducking at y {3}",
                headHitStanding.Count, headHitDucking.Count, FirstY(thrustStanding), FirstY(thrustDucking));

            Assert.That(headHitStanding, Is.Not.Empty, "control: the high swing hits a dummy that stands");
            Assert.That(headHitDucking, Is.Empty, "the crouch takes the head under the swing");
            Assert.That(thrustStanding, Is.Not.Empty, "control: the thrust reaches the torso");
            Assert.That(thrustDucking, Is.Not.Empty, "the ducking dummy's thrust still lands");
            Assert.That(thrustDucking[0].Contact.Facts.PointUnits.Y,
                Is.LessThan(thrustStanding[0].Contact.Facts.PointUnits.Y - LowThrustMinDropUnits),
                "a duck plus a straight line is a low thrust (§6)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SixBodies_CrouchAndJump_StillSettleOnTheirFeet()
        {
            _simulation.Segmentation = RagdollSegmentation.SixBodies;
            BodyMoveStats crouch = _rules.FindBodyMove(BodyMove.Crouch);
            PoseSnapshot board = Board(WideHalfGapUnits);

            TurnResult result = Simulate(board, Plan(BodyMove.Crouch), Plan(BodyMove.Jump));

            Vec2 left = Pelvis(result.FinalBoard.Pose, Side.Left);
            Vec2 right = Pelvis(result.FinalBoard.Pose, Side.Right);
            Report("Six bodies: {0}; crouched pelvis y {1:0.0} (goal {2:0.0}), landed pelvis y {3:0.0}, lowest feet {4:0.0} / {5:0.0}",
                result.EndReason, left.Y, StandingHeight + crouch.DisplacementUnits.Y, right.Y, LowestFoot(result.FinalBoard.Pose, Side.Left),
                LowestFoot(result.FinalBoard.Pose, Side.Right));
            Assert.That(result.EndReason, Is.EqualTo(TurnEndReason.Settled));
            Assert.That(left.Y, Is.EqualTo(StandingHeight + crouch.DisplacementUnits.Y).Within(SettledToleranceUnits));
            Assert.That(right.Y, Is.EqualTo(StandingHeight).Within(SettledToleranceUnits));
            yield return null;
        }

        /// <summary>How far the tip is from where the path puts it at a time, in the frame the move drives the path in (upright).</summary>
        private float OffPath(Vec2 tip, WeaponPath path, StanceBodyMoveDriver driver, double seconds)
        {
            var frame = new BodyPose(driver.Evaluate(SimTime.FromSeconds(seconds)).Root.PositionUnits, TurnStartRoot.UprightDegrees);
            Vec2 pathPoint = PointAlong(path, _rapier.SpeedUnitsPerSecond * (float)seconds);
            return Vec2.Distance(TorsoFrame.ToLocal(tip, frame, Side.Left), pathPoint);
        }

        private static Vec2 PointAlong(WeaponPath path, float distanceUnits)
        {
            IReadOnlyList<Vec2> points = path.Points;
            float left = distanceUnits;
            for (int i = 1; i < points.Count; i++)
            {
                float segment = Vec2.Distance(points[i - 1], points[i]);
                if (left <= segment) return Vec2.Lerp(points[i - 1], points[i], segment > 0f ? left / segment : 1f);
                left -= segment;
            }

            return points[points.Count - 1];
        }

        private static string FirstY(List<ContactEvent> contacts) =>
            contacts.Count > 0 ? contacts[0].Contact.Facts.PointUnits.Y.ToString("0.0", CultureInfo.InvariantCulture) : "none";
    }
}
