using System.Collections;
using System.Collections.Generic;
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

namespace XRim.Tests.PlayMode.Simulation
{
    /// <summary>
    /// The real turn loop on Unity 2D physics (Session 04): two right-handed dummies with rapiers in the guard stance,
    /// mirrored around x = 0, both able to plan.
    /// </summary>
    public sealed class TurnSimulatorPlayModeTests
    {
        private const float PoseToleranceUnits = 0.01f;
        private const float AngleToleranceDegrees = 0.01f;

        /// <summary>Pelvis to pelvis 600: each rapier can reach the other's chest (arm 240 + rapier 400 from the shoulder).</summary>
        private const float CloseHalfGapUnits = 300f;

        /// <summary>Pelvis to pelvis 1200: the guard tips are well apart.</summary>
        private const float WideHalfGapUnits = 600f;

        /// <summary>
        /// Pelvis to pelvis 1000: the guard tips start 388 apart, and a forward thrust at full reach carries each rapier tip
        /// about 96 past the middle, so the two blades cross.
        /// </summary>
        private const float CrossingHalfGapUnits = 500f;

        /// <summary>A thrust ending here (torso frame) goes into the chest of an opponent standing 600 away.</summary>
        private static readonly Vec2 ChestThrustEndLocal = new Vec2(620f, 100f);

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

        private Ragdoll BuildTemplate(RagdollSegmentation segmentation)
        {
            Ragdoll ragdoll = PlaceholderRagdollBuilder.Build(new RagdollBuildSpec(_body, _rules.Paths, segmentation, _space, _rules.Weapons, null));
            ragdoll.gameObject.SetActive(false);
            return ragdoll;
        }

        private Unity2DPhysicsWorld CreateWorld()
        {
            var world = new Unity2DPhysicsWorld(_space, new RagdollPrefabSet(_sixBodies, _tenBodies));
            _worlds.Add(world);
            return world;
        }

        private ArenaEdges Edges => _policies.ArenaEdge.EdgesFor(_rules.Arena);

        private BodyPose TorsoAt(float x) => new BodyPose(new Vec2(x, _body.LegLengthUnits), 0f);

        private PoseSnapshot Board(float leftX, float rightX) => new PoseSnapshot
        {
            Left = GuardStance.Create(TorsoAt(leftX), Side.Left, BodyPart.RightArm, _rapier, _simulation.Segmentation, _body, _rules.Paths, _aim),
            Right = GuardStance.Create(TorsoAt(rightX), Side.Right, BodyPart.RightArm, _rapier, _simulation.Segmentation, _body, _rules.Paths, _aim),
        };

        private MatchState State() => new MatchState(
            PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        private static TurnPlan Plan(WeaponPath path) => new TurnPlan(WeaponIds.Rapier, BodyMove.None, path ?? WeaponPath.Empty, default, true);

        private TurnInput Input(PoseSnapshot board, WeaponPath leftPath, WeaponPath rightPath) =>
            new TurnInput(new BoardSnapshot(State(), board), new PerSide<TurnPlan>(Plan(leftPath), Plan(rightPath)), _rules, _simulation);

        private TurnResult Simulate(Unity2DPhysicsWorld world, PoseSnapshot board, WeaponPath leftPath, WeaponPath rightPath) =>
            new TurnSimulator(world, _policies).Simulate(Input(board, leftPath, rightPath));

        private Vec2 Tip(BodyPose grip) => grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * _rapier.LengthUnits;

        /// <summary>The weapon tip on the frozen board, in the torso frame the turn will use.</summary>
        private Vec2 TipLocal(PoseSnapshot board, Side side) =>
            TorsoFrame.ToLocal(Tip(board.Get(side).HeldItem), TurnStartRoot.Of(board.Get(side)), side);

        private WeaponPath ThrustTo(PoseSnapshot board, Side side, Vec2 endLocal) => new WeaponPath(new[] { TipLocal(board, side), endLocal });

        /// <summary>A straight thrust from the guard tip toward the opponent (+X in the torso frame), level with the tip.</summary>
        private WeaponPath ThrustForward(PoseSnapshot board, Side side, float lengthUnits)
        {
            Vec2 start = TipLocal(board, side);
            return new WeaponPath(new[] { start, start + Vec2.UnitX * lengthUnits });
        }

        private static List<ContactEvent> Contacts(TurnResult result)
        {
            var contacts = new List<ContactEvent>();
            foreach (MatchEvent matchEvent in result.Timeline.Events)
            {
                if (matchEvent is ContactEvent contact) contacts.Add(contact);
            }

            return contacts;
        }

        private static int StepsSimulated(TurnResult result) => result.Timeline.Frames[result.Timeline.Frames.Count - 1].Step;

        private static void AssertSamePose(BodyPose actual, BodyPose expected, string what)
        {
            Assert.That(Vec2.Distance(actual.PositionUnits, expected.PositionUnits), Is.LessThan(PoseToleranceUnits), what + " position");
            Assert.That(Mathf.Abs(XMath.DeltaAngleDegrees(expected.RotationDegrees, actual.RotationDegrees)), Is.LessThan(AngleToleranceDegrees),
                what + " rotation");
        }

        private static void AssertSamePose(PoseSnapshot actual, PoseSnapshot expected)
        {
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                FighterPose a = actual.Get(side);
                FighterPose e = expected.Get(side);
                foreach (BodyPart part in BodyParts.All)
                {
                    AssertSamePose(a.Get(part), e.Get(part), $"{side} {part}");
                    if (e.HasLowerSegments && (part.IsArm() || part.IsLeg())) AssertSamePose(a.GetLower(part), e.GetLower(part), $"{side} lower {part}");
                }

                Assert.That(a.HasHeldItem, Is.EqualTo(e.HasHeldItem), $"{side} holds its weapon");
                if (e.HasHeldItem) AssertSamePose(a.HeldItem, e.HeldItem, $"{side} weapon");
            }
        }

        [UnityTest]
        public IEnumerator SameBoardAndPlansTwice_GiveTheSameEventOrderAndEndPoses()
        {
            PoseSnapshot board = Board(-CloseHalfGapUnits, CloseHalfGapUnits);
            WeaponPath left = ThrustTo(board, Side.Left, ChestThrustEndLocal);
            WeaponPath right = ThrustTo(board, Side.Right, ChestThrustEndLocal);
            Unity2DPhysicsWorld world = CreateWorld();

            TurnResult first = Simulate(world, board, left, right);
            TurnResult again = Simulate(world, board, left, right);
            TurnResult freshWorld = Simulate(CreateWorld(), board, left, right);

            List<ContactEvent> expected = Contacts(first);
            Assert.That(expected, Is.Not.Empty, "both thrusts reach a chest");
            foreach (TurnResult other in new[] { again, freshWorld })
            {
                List<ContactEvent> actual = Contacts(other);
                Assert.That(actual.Count, Is.EqualTo(expected.Count), "same number of contacts");
                for (int i = 0; i < expected.Count; i++)
                {
                    Assert.That(actual[i].Time, Is.EqualTo(expected[i].Time), $"contact {i} time");
                    Assert.That(actual[i].Contact.Facts.A, Is.EqualTo(expected[i].Contact.Facts.A), $"contact {i} first body");
                    Assert.That(actual[i].Contact.Facts.B, Is.EqualTo(expected[i].Contact.Facts.B), $"contact {i} second body");
                }

                Assert.That(StepsSimulated(other), Is.EqualTo(StepsSimulated(first)), "same length");
                AssertSamePose(other.FinalBoard.Pose, first.FinalBoard.Pose);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator HardCap_EndsATurnThatNeverSettles()
        {
            PoseSnapshot board = Board(-WideHalfGapUnits, WideHalfGapUnits);
            Unity2DPhysicsWorld world = CreateWorld();
            TurnResult idle = Simulate(world, board, null, null);
            Assert.That(idle.EndReason, Is.EqualTo(TurnEndReason.Settled), "control: standing still settles");

            _simulation.SettleLinearSpeedUnitsPerSecond = 0f; // nothing ever counts as still
            TurnResult capped = Simulate(world, board, null, null);

            var clock = new SimClock(_simulation.StepRateHz);
            Assert.That(capped.EndReason, Is.EqualTo(TurnEndReason.HardCap));
            Assert.That(StepsSimulated(capped), Is.EqualTo(clock.StepsFor(_rules.Match.ExecutionHardCapSeconds)));
            Assert.That(capped.Timeline.Duration, Is.EqualTo(SimTime.FromSeconds(_rules.Match.ExecutionHardCapSeconds)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadingACapturedSnapshot_ReproducesThePoseAtZeroVelocity()
        {
            PoseSnapshot board = Board(-CloseHalfGapUnits, CloseHalfGapUnits);
            Unity2DPhysicsWorld world = CreateWorld();
            TurnResult turn = Simulate(world, board, ThrustTo(board, Side.Left, ChestThrustEndLocal), null);
            IReadOnlyList<TimelineFrame> frames = turn.Timeline.Frames;

            foreach (PoseSnapshot captured in new[] { frames[frames.Count / 3].Pose, turn.FinalBoard.Pose })
            {
                world.Load(captured, State(), _rules, _simulation, Edges);

                AssertSamePose(world.CapturePose(), captured);
                foreach (Side side in new[] { Side.Left, Side.Right })
                {
                    Ragdoll ragdoll = world.GetRagdoll(side);
                    var bodies = new List<Rigidbody2D>();
                    ragdoll.GetPartBodies(bodies);
                    bodies.Add(ragdoll.ActiveHeldItem);
                    foreach (Rigidbody2D body in bodies)
                    {
                        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero), $"{side} {body.name} is at rest");
                        Assert.That(body.angularVelocity, Is.EqualTo(0f), $"{side} {body.name} does not spin");
                    }
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator NextTurn_StartsFromTheLastTurnsEndPose()
        {
            PoseSnapshot board = Board(-CloseHalfGapUnits, CloseHalfGapUnits);
            Unity2DPhysicsWorld world = CreateWorld();
            TurnResult first = Simulate(world, board, ThrustTo(board, Side.Left, ChestThrustEndLocal), null);

            TurnResult second = Simulate(world, first.FinalBoard.Pose, null, null);

            AssertSamePose(second.Timeline.Frames[0].Pose, first.FinalBoard.Pose);
            Assert.That(Vec2.Distance(Tip(second.FinalBoard.Pose.Left.HeldItem), Tip(first.FinalBoard.Pose.Left.HeldItem)),
                Is.LessThan(1f), "an idle turn holds the weapon where the last turn left it");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeveredLimbs_AreRebuiltLooseAndLeftOffTheirOwner()
        {
            PoseSnapshot board = Board(-WideHalfGapUnits, WideHalfGapUnits);
            var limbPose = new BodyPose(new Vec2(0f, 40f), 90f);
            board.SeveredLimbs.Add(new SeveredLimbPose(Side.Right, BodyPart.LeftArm, limbPose));
            MatchState state = State();
            state.Fighters.Right.MarkSevered(BodyPart.LeftArm);
            Unity2DPhysicsWorld world = CreateWorld();

            world.Load(board, state, _rules, _simulation, Edges);

            PoseSnapshot loaded = world.CapturePose();
            Assert.That(loaded.SeveredLimbs.Count, Is.EqualTo(1));
            Assert.That(loaded.SeveredLimbs[0].Owner, Is.EqualTo(Side.Right));
            Assert.That(loaded.SeveredLimbs[0].Part, Is.EqualTo(BodyPart.LeftArm));
            AssertSamePose(loaded.SeveredLimbs[0].Pose, limbPose, "severed limb");
            Assert.That(world.GetRagdoll(Side.Right).GetBody(BodyPart.LeftArm).gameObject.activeSelf, Is.False, "the lost arm is not on the dummy");

            var clock = new SimClock(_simulation.StepRateHz);
            for (int i = 0; i < clock.StepsFor(1.0); i++)
            {
                world.Step(clock.StepSeconds);
            }

            float restingY = world.CapturePose().SeveredLimbs[0].Pose.PositionUnits.Y;
            Assert.That(restingY, Is.InRange(0f, limbPose.PositionUnits.Y), "the limb falls and lies on the floor");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SolidEdge_StopsADummyKnockedIntoIt()
        {
            const float startInsideUnits = 150f;
            const float knockSpeedUnitsPerSecond = 8000f;
            ArenaEdges solid = Edges;
            var open = new ArenaEdges(solid.LeftXUnits, solid.RightXUnits, isSolid: false);

            float solidReach = FurthestLeftPelvis(solid, startInsideUnits, knockSpeedUnitsPerSecond);
            float openReach = FurthestLeftPelvis(open, startInsideUnits, knockSpeedUnitsPerSecond);

            Assert.That(openReach, Is.LessThan(solid.LeftXUnits), "control: without the edge the knock carries the dummy out");
            Assert.That(solidReach, Is.GreaterThan(solid.LeftXUnits), "D22: the solid edge stops it");
            yield return null;
        }

        /// <summary>Knocks the left dummy toward the left edge and returns the furthest left its pelvis got.</summary>
        private float FurthestLeftPelvis(ArenaEdges edges, float startInsideUnits, float knockSpeedUnitsPerSecond)
        {
            PoseSnapshot board = Board(edges.LeftXUnits + startInsideUnits, WideHalfGapUnits);
            Unity2DPhysicsWorld world = CreateWorld();
            world.Load(board, State(), _rules, _simulation, edges);
            world.ApplyImpulse(Side.Left, BodyPart.Torso, new Vec2(-knockSpeedUnitsPerSecond * _body.TorsoMass, 0f));

            var clock = new SimClock(_simulation.StepRateHz);
            float furthest = float.MaxValue;
            for (int i = 0; i < clock.StepsFor(0.5); i++)
            {
                world.Step(clock.StepSeconds);
                furthest = Mathf.Min(furthest, world.GetPose(Side.Left, BodyPart.Torso).PositionUnits.X);
            }

            return furthest;
        }

        [UnityTest]
        public IEnumerator CrossingBlades_ReportAContactBetweenTheTwoKinematicWeapons()
        {
            PoseSnapshot board = Board(-CrossingHalfGapUnits, CrossingHalfGapUnits);
            Assert.That(BladeGapUnits(board), Is.GreaterThan(0f), "the guard blades start apart");

            TurnResult result = Simulate(CreateWorld(), board, ThrustForward(board, Side.Left, 350f), ThrustForward(board, Side.Right, 350f));

            // Precondition: the recording shows the blades overlapping, so a missing contact is the physics, not the setup.
            float closest = float.MaxValue;
            foreach (TimelineFrame frame in result.Timeline.Frames)
            {
                closest = Mathf.Min(closest, BladeGapUnits(frame.Pose));
            }

            Assert.That(closest, Is.LessThan(0f), "test setup: the two blades must actually cross during the turn");

            ContactEvent clash = null;
            foreach (ContactEvent contact in Contacts(result))
            {
                ContactFacts facts = contact.Contact.Facts;
                if (facts.A.Role == BodyRole.HeldItem && facts.B.Role == BodyRole.HeldItem) clash = contact;
            }

            Assert.That(clash, Is.Not.Null, "two kinematic blades meeting are reported (D1 kinematic, for Session 07's clashes)");
            Assert.That(clash.Contact.Facts.A.Owner, Is.EqualTo(Side.Left));
            Assert.That(clash.Contact.Facts.B.Owner, Is.EqualTo(Side.Right));
            Assert.That(clash.Contact.WeaponSide, Is.Not.Null);
            Assert.That(clash.Time, Is.GreaterThan(SimTime.Zero));
            yield return null;
        }

        /// <summary>The gap between the two rapier blades (negative = overlapping), from their centre lines and hit width.</summary>
        private float BladeGapUnits(PoseSnapshot pose)
        {
            Vec2 a0 = pose.Left.HeldItem.PositionUnits;
            Vec2 a1 = Tip(pose.Left.HeldItem);
            Vec2 b0 = pose.Right.HeldItem.PositionUnits;
            Vec2 b1 = Tip(pose.Right.HeldItem);
            float centreLines = SegmentsCross(a0, a1, b0, b1)
                ? 0f
                : Mathf.Min(Mathf.Min(DistanceToSegment(a0, b0, b1), DistanceToSegment(a1, b0, b1)),
                    Mathf.Min(DistanceToSegment(b0, a0, a1), DistanceToSegment(b1, a0, a1)));
            return centreLines - _rapier.InkThicknessUnits;
        }

        private static bool SegmentsCross(Vec2 a0, Vec2 a1, Vec2 b0, Vec2 b1)
        {
            float d1 = Vec2.Cross(b1 - b0, a0 - b0);
            float d2 = Vec2.Cross(b1 - b0, a1 - b0);
            float d3 = Vec2.Cross(a1 - a0, b0 - a0);
            float d4 = Vec2.Cross(a1 - a0, b1 - a0);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        private static float DistanceToSegment(Vec2 point, Vec2 start, Vec2 end)
        {
            Vec2 segment = end - start;
            float t = segment.LengthSquared > 0f ? Mathf.Clamp01(Vec2.Dot(point - start, segment) / segment.LengthSquared) : 0f;
            return Vec2.Distance(point, start + segment * t);
        }
    }
}
