using NUnit.Framework;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Networking
{
    /// <summary>D3: a stroke's lead-in starts at the weapon tip, read from where the last turn left the weapon (GDD §3).</summary>
    public sealed class PoseWeaponTipLocatorTests
    {
        private const float Tolerance = 1e-3f;

        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private PoseWeaponTipLocator _locator;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _locator = new PoseWeaponTipLocator(_rules, _simulation);
        }

        private BoardSnapshot Board(BodyPose leftTorso, BodyPose leftGrip, BodyPose rightTorso, BodyPose rightGrip)
        {
            var pose = new PoseSnapshot();
            pose.Left.Set(BodyPart.Torso, leftTorso);
            pose.Left.HeldItem = leftGrip;
            pose.Right.Set(BodyPart.Torso, rightTorso);
            pose.Right.HeldItem = rightGrip;
            return new BoardSnapshot(TestData.CreateInitialBoard(_rules).State, pose);
        }

        private static void AssertNear(Vec2 actual, Vec2 expected, string what = "tip")
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance), what + " x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance), what + " y");
        }

        [Test]
        public void Tip_IsTheGripPlusTheWeaponsLength_InTheTorsoFrame()
        {
            // Left pelvis at (-350, 180); the grip 100 above it, pointing straight at the opponent.
            BoardSnapshot board = Board(new BodyPose(new Vec2(-350f, 180f), 0f), new BodyPose(new Vec2(-350f, 280f), 0f),
                new BodyPose(new Vec2(350f, 180f), 0f), new BodyPose(new Vec2(350f, 280f), 180f));

            IWeaponTipSource left = _locator.ForSide(board, Side.Left);
            IWeaponTipSource right = _locator.ForSide(board, Side.Right);

            float rapier = _rules.FindWeapon(WeaponIds.Rapier).LengthUnits;
            AssertNear(left.TipLocal(WeaponIds.Rapier), new Vec2(rapier, 100f));
            AssertNear(right.TipLocal(WeaponIds.Rapier), new Vec2(rapier, 100f), "mirrored: +X still points at the opponent");
        }

        [Test]
        public void SwitchedWeapon_SitsInTheSameGripWithItsOwnLength()
        {
            BoardSnapshot board = Board(new BodyPose(Vec2.Zero, 0f), new BodyPose(new Vec2(0f, 100f), -90f),
                new BodyPose(new Vec2(700f, 0f), 0f), new BodyPose(new Vec2(700f, 100f), 180f));

            IWeaponTipSource tips = _locator.ForSide(board, Side.Left);

            float mace = _rules.FindWeapon(WeaponIds.Mace).LengthUnits;
            AssertNear(tips.TipLocal(WeaponIds.Mace), new Vec2(0f, 100f - mace));
        }

        [Test]
        public void TiltedTorso_TipIsInTheUprightTurnStartFrame()
        {
            BoardSnapshot board = Board(new BodyPose(Vec2.Zero, 25f), new BodyPose(new Vec2(0f, 100f), 0f),
                new BodyPose(new Vec2(700f, 0f), 0f), new BodyPose(new Vec2(700f, 100f), 180f));

            Vec2 tip = _locator.ForSide(board, Side.Left).TipLocal(WeaponIds.Rapier);

            AssertNear(tip, new Vec2(_rules.FindWeapon(WeaponIds.Rapier).LengthUnits, 100f));
        }

        [Test]
        public void SideHoldingNothing_PlansFromTheGuardTip()
        {
            BoardSnapshot board = Board(new BodyPose(Vec2.Zero, 0f), default, new BodyPose(new Vec2(700f, 0f), 0f), default);
            board.Pose.Left.HasHeldItem = false;

            Vec2 tip = _locator.ForSide(board, Side.Left).TipLocal(WeaponIds.Rapier);

            AssertNear(tip, GuardStance.TipLocal(_rules.FindWeapon(WeaponIds.Rapier), _simulation.Ragdoll, _rules.Paths));
        }

        [Test]
        public void LaterChangesToTheBoard_DoNotMoveATipAlreadyHandedOut()
        {
            BoardSnapshot board = Board(new BodyPose(Vec2.Zero, 0f), new BodyPose(new Vec2(0f, 100f), 0f),
                new BodyPose(new Vec2(700f, 0f), 0f), new BodyPose(new Vec2(700f, 100f), 180f));
            IWeaponTipSource tips = _locator.ForSide(board, Side.Left);

            board.Pose.Left.HeldItem = new BodyPose(new Vec2(50f, 50f), 45f);

            AssertNear(tips.TipLocal(WeaponIds.Rapier), new Vec2(_rules.FindWeapon(WeaponIds.Rapier).LengthUnits, 100f));
        }

        [Test]
        public void UnknownWeapon_IsAProgrammerError()
        {
            BoardSnapshot board = Board(new BodyPose(Vec2.Zero, 0f), new BodyPose(new Vec2(0f, 100f), 0f),
                new BodyPose(new Vec2(700f, 0f), 0f), new BodyPose(new Vec2(700f, 100f), 180f));

            Assert.Throws<System.InvalidOperationException>(() => _locator.ForSide(board, Side.Left).TipLocal(new WeaponId("trebuchet")));
        }
    }
}
