using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Paths;
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
    /// The Unity 2D physics world in its hidden, manually stepped scene (Session 02 feel spike). The left dummy stands
    /// at x = 0 with its pelvis one leg length above the floor; its shoulder is at (0, 100) in its torso frame.
    /// </summary>
    public sealed class Unity2DPhysicsWorldTests
    {
        private const int StepCount = 30;
        private const float StepSeconds = 1f / 240f;
        private const float FarAwayX = 900f;
        private const float StandingSeconds = 2f;

        private SimulationMode2D _previousMode;
        private ArenaSpace _space;
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private RagdollSettings _body;
        private WeaponStats _rapier;
        private Ragdoll _sixBodies;
        private Ragdoll _tenBodies;
        private readonly AimFromShoulderModel _aim = new AimFromShoulderModel();

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
            _sixBodies = BuildTemplate(RagdollSegmentation.SixBodies);
            _tenBodies = BuildTemplate(RagdollSegmentation.TenBodies);
        }

        [TearDown]
        public void TearDown()
        {
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

        private Unity2DPhysicsWorld CreateWorld() => new Unity2DPhysicsWorld(_space, new RagdollPrefabSet(_sixBodies, _tenBodies));

        private Ragdoll Template => _simulation.Segmentation == RagdollSegmentation.TenBodies ? _tenBodies : _sixBodies;

        private BodyPose TorsoAt(float x) => new BodyPose(new Vec2(x, _body.LegLengthUnits), 0f);

        private PoseSnapshot Board(float rightX, bool leftHolds, bool rightHolds) => new PoseSnapshot
        {
            Left = Template.CreateRestPose(TorsoAt(0f), Side.Left, _space, BodyPart.RightArm, leftHolds ? _rapier : null, _body, _aim),
            Right = Template.CreateRestPose(TorsoAt(rightX), Side.Right, _space, BodyPart.RightArm, rightHolds ? _rapier : null, _body, _aim),
        };

        private MatchState State() => new MatchState(
            PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        private Vec2 Tip(BodyPose grip) => grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * _rapier.LengthUnits;

        private SwingResult Swing(Unity2DPhysicsWorld world, PoseSnapshot board, WeaponPath leftPath) =>
            new SwingSimulator(world, _aim).Run(new SwingInput(board, State(), new PerSide<WeaponPath>(leftPath, null), _rules, _simulation));

        /// <summary>A straight thrust from the guard tip, outward from the shoulder, in the torso frame.</summary>
        private WeaponPath ThrustFromGuard(PoseSnapshot board, float lengthUnits)
        {
            Vec2 start = TorsoFrame.ToLocal(Tip(board.Left.HeldItem), TorsoAt(0f), Side.Left);
            Vec2 outward = (start - _rules.Paths.ShoulderOffsetUnits).Normalized;
            return new WeaponPath(new[] { start, start + outward * lengthUnits });
        }

        private static IEnumerator DisposeAndWait(Unity2DPhysicsWorld world)
        {
            Scene scene = world.Scene;
            world.Dispose();
            yield return new WaitUntil(() => !scene.isLoaded);
        }

        [UnityTest]
        public IEnumerator IsolatedPhysicsScene_MovesOnlyWhenStepped()
        {
            var world = new Unity2DPhysicsWorld(_space);
            var probe = new GameObject("SimulationProbe", typeof(Rigidbody2D));
            SceneManager.MoveGameObjectToScene(probe, world.Scene);
            var probeBody = probe.GetComponent<Rigidbody2D>();
            var control = new GameObject("VisualSceneControl", typeof(Rigidbody2D));
            var controlBody = control.GetComponent<Rigidbody2D>();
            float probeStartY = probeBody.position.y;
            float controlStartY = controlBody.position.y;

            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(probeBody.position.y, Is.EqualTo(probeStartY), "Nothing may step the simulation scene automatically");

            for (int i = 0; i < StepCount; i++)
            {
                world.Step(StepSeconds);
            }

            Assert.That(probeBody.position.y, Is.LessThan(probeStartY), "Gravity acts when the world is stepped");
            Assert.That(controlBody.position.y, Is.EqualTo(controlStartY), "Stepping the simulation never moves the visual scene");

            Object.Destroy(control);
            yield return DisposeAndWait(world);
        }

        [UnityTest]
        public IEnumerator LoadedSwing_NeverMovesTheVisualScene()
        {
            var control = new GameObject("VisualSceneControl", typeof(Rigidbody2D));
            var controlBody = control.GetComponent<Rigidbody2D>();
            Vector2 controlStart = controlBody.position;
            Vector3 templateTorso = _sixBodies.GetBody(BodyPart.Torso).transform.position;
            Unity2DPhysicsWorld world = CreateWorld();
            PoseSnapshot board = Board(FarAwayX, true, false);

            Swing(world, board, ThrustFromGuard(board, 100f));

            Assert.That(controlBody.position, Is.EqualTo(controlStart), "the visual scene's bodies never move");
            Assert.That(_sixBodies.GetBody(BodyPart.Torso).transform.position, Is.EqualTo(templateTorso), "the prefab is never touched");
            Assert.That(world.GetRagdoll(Side.Left).gameObject.scene, Is.EqualTo(world.Scene), "dummies live in the hidden scene");
            Object.Destroy(control);
            yield return DisposeAndWait(world);
        }

        [UnityTest]
        public IEnumerator Standing_SixBodies_SettlesWithNoInput() => StandsStill(RagdollSegmentation.SixBodies);

        [UnityTest]
        public IEnumerator Standing_TenBodies_SettlesWithNoInput() => StandsStill(RagdollSegmentation.TenBodies);

        private IEnumerator StandsStill(RagdollSegmentation segmentation)
        {
            _simulation.Segmentation = segmentation;
            Unity2DPhysicsWorld world = CreateWorld();
            PoseSnapshot board = Board(FarAwayX, true, false);
            world.Load(board, State(), _rules, _simulation);
            var clock = new SimClock(_simulation.StepRateHz);
            int steps = clock.StepsFor(StandingSeconds);
            int settledInARow = 0;
            for (int i = 0; i < steps; i++)
            {
                world.Step(clock.StepSeconds);
                settledInARow = world.IsSettled(_simulation.SettleLinearSpeedUnitsPerSecond, _simulation.SettleAngularSpeedDegreesPerSecond)
                    ? settledInARow + 1
                    : 0;
            }

            BodyPose torso = world.GetPose(Side.Left, BodyPart.Torso);
            BodyPose head = world.GetPose(Side.Left, BodyPart.Head);
            Assert.That(settledInARow, Is.GreaterThanOrEqualTo(_simulation.SettleStepsRequired), "physics settles while standing");
            Assert.That(Vec2.Distance(torso.PositionUnits, TorsoAt(0f).PositionUnits), Is.LessThan(2f), "the pelvis stays put");
            Assert.That(Mathf.Abs(XMath.DeltaAngleDegrees(0f, torso.RotationDegrees)), Is.LessThan(2f), "the torso stays upright");
            Assert.That(head.PositionUnits.Y, Is.GreaterThan(torso.PositionUnits.Y + _body.TorsoHeightUnits * 0.9f), "the head stays on top");
            yield return DisposeAndWait(world);
        }

        [UnityTest]
        public IEnumerator KinematicSwing_TipReachesThePathEndAtLengthOverSpeed()
        {
            _simulation.WeaponDriver = WeaponDriverKind.Kinematic;
            Unity2DPhysicsWorld world = CreateWorld();
            PoseSnapshot board = Board(FarAwayX, true, false);
            WeaponPath path = ThrustFromGuard(board, 100f);
            Vec2 end = TorsoFrame.ToArena(path.Points[1], TorsoAt(0f), Side.Left);

            SwingResult result = Swing(world, board, path);

            var clock = new SimClock(_simulation.StepRateHz);
            int expectedStep = clock.StepsFor(path.LengthUnits / _rapier.SpeedUnitsPerSecond);
            int arrivalStep = -1;
            foreach (TimelineFrame frame in result.Timeline.Frames)
            {
                if (Vec2.Distance(Tip(frame.Pose.Left.HeldItem), end) >= 0.5f) continue;
                arrivalStep = frame.Step;
                break;
            }

            Assert.That(arrivalStep, Is.InRange(expectedStep - 1, expectedStep + 1), "t = length / speed, ± one step");
            Assert.That(result.EndReason, Is.EqualTo(SwingEndReason.Settled));
            yield return DisposeAndWait(world);
        }

        [UnityTest]
        public IEnumerator MotorSwing_EndsAtThePathEnd()
        {
            _simulation.WeaponDriver = WeaponDriverKind.Motor;
            Unity2DPhysicsWorld world = CreateWorld();
            PoseSnapshot board = Board(FarAwayX, true, false);
            WeaponPath path = ThrustFromGuard(board, 100f);
            Vec2 end = TorsoFrame.ToArena(path.Points[1], TorsoAt(0f), Side.Left);

            SwingResult result = Swing(world, board, path);

            Assert.That(Vec2.Distance(Tip(result.FinalPose.Left.HeldItem), end), Is.LessThan(10f), "the motor brings the tip to the path end");
            yield return DisposeAndWait(world);
        }

        [UnityTest]
        public IEnumerator ThrustIntoTorso_ReportsATimedContactFromWeaponToTorso()
        {
            // The right dummy's torso faces the thrust at x = 550 (its pivot at 580, half-width 30), at shoulder height.
            const float rightTorsoX = 580f;
            const float thrustHeight = 100f;
            float faceX = rightTorsoX - _body.TorsoWidthUnits * 0.5f;
            Unity2DPhysicsWorld world = CreateWorld();
            PoseSnapshot board = Board(rightTorsoX, true, false);
            Vec2 guardTip = TorsoFrame.ToLocal(Tip(board.Left.HeldItem), TorsoAt(0f), Side.Left);
            var path = new WeaponPath(new[] { guardTip, new Vec2(450f, thrustHeight), new Vec2(620f, thrustHeight) });

            SwingResult result = Swing(world, board, path);

            SwingContact hit = null;
            foreach (SwingContact contact in result.Contacts)
            {
                if (contact.WeaponSide == Side.Left && contact.Facts.B.Owner == Side.Right && contact.Facts.B.Part == BodyPart.Torso)
                {
                    hit = contact;
                    break;
                }
            }

            Assert.That(hit, Is.Not.Null, "the rapier hits the right dummy's torso (no tunnelling at 240 Hz)");
            Assert.That(hit.Facts.A.Role, Is.EqualTo(BodyRole.HeldItem));
            Assert.That(hit.Facts.Normal.X, Is.GreaterThan(0.9f), "the normal points from A (the rapier) to B (the torso)");
            Assert.That(hit.Facts.RelativeVelocityUnitsPerSecond.X, Is.LessThan(0f), "the torso comes at the rapier");
            Assert.That(hit.ContactAngleDegrees, Is.GreaterThan(60f), "a thrust meets the torso nearly square");

            var clock = new SimClock(_simulation.StepRateHz);
            Assert.That(hit.Time.Microseconds,
                Is.InRange(clock.TimeAtStep(hit.ReportedStep - 2).Microseconds, clock.TimeAtStep(hit.ReportedStep).Microseconds),
                "the refined time lies in the step whose motion made the contact");
            float leadIn = Vec2.Distance(guardTip, path.Points[1]);
            float touchX = faceX - _rapier.InkThicknessUnits * 0.5f - world.TouchDistanceUnits;
            float expectedDistance = leadIn + (touchX - 450f);
            float oneStepOfTravel = _rapier.SpeedUnitsPerSecond * clock.StepSeconds;
            Assert.That(hit.PathDistanceUnits, Is.EqualTo(expectedDistance).Within(oneStepOfTravel), "time-to-impact d = v·t, within one step");
            yield return DisposeAndWait(world);
        }

        [UnityTest]
        public IEnumerator JointAngle_GrowsWhenALimbSwingsForward()
        {
            const float forwardSwingDegrees = 30f;
            Unity2DPhysicsWorld world = CreateWorld();
            PoseSnapshot board = Board(FarAwayX, false, false);
            BodyPose leg = board.Left.Get(BodyPart.LeftLeg);
            board.Left.Set(BodyPart.LeftLeg, new BodyPose(leg.PositionUnits, forwardSwingDegrees));

            world.Load(board, State(), _rules, _simulation);

            float angle = world.GetRagdoll(Side.Left).GetBody(BodyPart.LeftLeg).GetComponent<HingeJoint2D>().jointAngle;
            Assert.That(angle, Is.EqualTo(forwardSwingDegrees).Within(1f),
                "if this is −30, flip Ragdoll.JointAngleGrowsCounterClockwise so the joint limits mean what RagdollSettings says");
            yield return DisposeAndWait(world);
        }
    }
}
