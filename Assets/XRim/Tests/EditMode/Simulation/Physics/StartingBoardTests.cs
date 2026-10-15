using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Tests.EditMode.Simulation.Physics
{
    /// <summary>The board a match starts from, built from settings alone (GDD §3): the guard stance at the starting gap.</summary>
    public sealed class StartingBoardTests
    {
        private const float Tolerance = 1e-3f;

        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private readonly AimFromShoulderModel _aim = new AimFromShoulderModel();

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
        }

        private MatchState State(WeaponId left, WeaponId right, Handedness rightHandedness = Handedness.Right) => new MatchState(
            new PerSide<FighterState>(new FighterState(Handedness.Right, _rules.Damage.MaxHp, left),
                new FighterState(rightHandedness, _rules.Damage.MaxHp, right)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        /// <summary>The held item's path point: a weapon's tip, the shield's centre.</summary>
        private Vec2 Tip(FighterPose pose, WeaponId weapon) => HeldItemShape.Of(_rules.FindWeapon(weapon)).PathPointAt(pose.HeldItem);

        /// <summary>The corners of the held item's box in the arena.</summary>
        private Vec2[] Corners(FighterPose pose, WeaponId weapon)
        {
            HeldItemShape shape = HeldItemShape.Of(_rules.FindWeapon(weapon));
            Vec2 half = shape.SizeUnits * 0.5f;
            var corners = new Vec2[4];
            int i = 0;
            foreach (float x in new[] { -half.X, half.X })
            {
                foreach (float y in new[] { -half.Y, half.Y })
                {
                    corners[i++] = HeldItemShape.ToArena(shape.CentreLocal + new Vec2(x, y), pose.HeldItem);
                }
            }

            return corners;
        }

        [Test]
        public void Dummies_StandTheStartingGapApart_MirroredAroundZero_OnTheirLegs()
        {
            PoseSnapshot board = StartingBoard.Create(State(WeaponIds.Rapier, WeaponIds.Mace), _rules, _simulation, _aim);

            float half = _rules.Arena.StartingGapUnits * 0.5f;
            Assert.That(board.Left.Get(BodyPart.Torso).PositionUnits, Is.EqualTo(new Vec2(-half, _simulation.Ragdoll.LegLengthUnits)));
            Assert.That(board.Right.Get(BodyPart.Torso).PositionUnits, Is.EqualTo(new Vec2(half, _simulation.Ragdoll.LegLengthUnits)));
            Assert.That(board.Left.Get(BodyPart.Torso).RotationDegrees, Is.EqualTo(TurnStartRoot.UprightDegrees));
            Assert.That(board.Left.HasLowerSegments, Is.True, "D2: ten bodies");
            Assert.That(board.SeveredLimbs, Is.Empty);
        }

        [Test]
        public void EachDummy_HoldsItsCurrentWeaponAtTheGuardTip()
        {
            PoseSnapshot board = StartingBoard.Create(State(WeaponIds.Rapier, WeaponIds.Mace), _rules, _simulation, _aim);

            foreach ((Side side, WeaponId weapon) in new[] { (Side.Left, WeaponIds.Rapier), (Side.Right, WeaponIds.Mace) })
            {
                FighterPose pose = board.Get(side);
                Vec2 expected = TorsoFrame.ToArena(GuardStance.TipLocal(_rules.FindWeapon(weapon), _simulation.Ragdoll, _rules.Paths),
                    TurnStartRoot.Of(pose), side);
                Assert.That(pose.HasHeldItem, Is.True);
                Assert.That(Vec2.Distance(Tip(pose, weapon), expected), Is.LessThan(Tolerance), $"{side} tip");
            }
        }

        [Test]
        public void ADummyStartingWithTheShield_HoldsItFaceOut_CentredOnTheGuardPoint()
        {
            PoseSnapshot board = StartingBoard.Create(State(WeaponIds.Shield, WeaponIds.Shield), _rules, _simulation, _aim);

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                FighterPose pose = board.Get(side);
                BodyPose root = TurnStartRoot.Of(pose);
                Vec2 guardLocal = GuardStance.TipLocal(_rules.FindWeapon(WeaponIds.Shield), _simulation.Ragdoll, _rules.Paths);
                Vec2 centre = TorsoFrame.ToArena(guardLocal, root, side);
                Assert.That(Vec2.Distance(pose.HeldItem.PositionUnits, centre), Is.LessThan(Tolerance), $"{side}: the grip is the shield's centre");
                Vec2 faceLocal = guardLocal - _rules.Paths.ShoulderOffsetUnits;
                float faceDegrees = TorsoFrame.AngleToArena(faceLocal.AngleDegrees, root, side);
                Assert.That(XMath.DeltaAngleDegrees(pose.HeldItem.RotationDegrees, faceDegrees), Is.EqualTo(0f).Within(Tolerance),
                    $"{side}: the face looks out from the shoulder (A3)");
            }
        }

        [Test]
        public void LeftHandedDummy_HoldsItsWeaponInItsLeftHand()
        {
            PoseSnapshot board = StartingBoard.Create(State(WeaponIds.Rapier, WeaponIds.Rapier, Handedness.Left), _rules, _simulation, _aim);

            Assert.That(board.Right.Get(BodyPart.LeftArm).RotationDegrees, Is.Not.EqualTo(0f), "the left arm reaches for the weapon");
            Assert.That(board.Right.Get(BodyPart.RightArm).RotationDegrees, Is.EqualTo(0f), "the right arm hangs");
        }

        [Test]
        public void GuardBlades_OfTheDefaultLoadout_StartApartAndAboveTheFloor()
        {
            // Designer, 2026-10-01: the blades start apart en garde, so every clash comes from a player's move.
            var loadout = new List<WeaponId>();
            foreach (string id in _rules.Loadout.DefaultLoadoutWeaponIds) loadout.Add(new WeaponId(id));

            foreach (WeaponId left in loadout)
            {
                foreach (WeaponId right in loadout)
                {
                    PoseSnapshot board = StartingBoard.Create(State(left, right), _rules, _simulation, _aim);
                    Vec2[] leftBox = Corners(board.Left, left);
                    Vec2[] rightBox = Corners(board.Right, right);
                    Assert.That(leftBox.Max(corner => corner.X), Is.LessThan(rightBox.Min(corner => corner.X)),
                        $"{left.Value} and {right.Value} start apart");
                    Assert.That(leftBox.Min(corner => corner.Y), Is.GreaterThan(0f), $"{left.Value} above the floor");
                }
            }
        }

        [Test]
        public void GuardBlades_OfEveryRosterWeapon_StartApartAndAboveTheFloor()
        {
            // Session 14: the sword and the spear are playable, so every pair of the five weapons must start apart and off the floor
            // (the spear at the plain -40° guard crossed its twin and sat below the floor).
            WeaponId[] roster = { WeaponIds.Rapier, WeaponIds.Sword, WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield };

            foreach (WeaponId left in roster)
            {
                foreach (WeaponId right in roster)
                {
                    PoseSnapshot board = StartingBoard.Create(State(left, right), _rules, _simulation, _aim);
                    Vec2[] leftBox = Corners(board.Left, left);
                    Vec2[] rightBox = Corners(board.Right, right);
                    Assert.That(leftBox.Max(corner => corner.X), Is.LessThan(rightBox.Min(corner => corner.X)),
                        $"{left.Value} and {right.Value} start apart");
                    Assert.That(leftBox.Min(corner => corner.Y), Is.GreaterThan(0f), $"{left.Value} (left) above the floor");
                    Assert.That(rightBox.Min(corner => corner.Y), Is.GreaterThan(0f), $"{right.Value} (right) above the floor");
                }
            }
        }

        [Test]
        public void TheSpearsTip_RestsAboveTheFloor_AtTheGuard()
        {
            // Known issue until Session 14: at -40° a 500-long spear's tip sat 41 units below the floor.
            WeaponStats spear = _rules.FindWeapon(WeaponIds.Spear);

            Vec2 tip = GuardStance.TipLocal(spear, _simulation.Ragdoll, _rules.Paths);

            Assert.That(tip.Y, Is.GreaterThan(-_simulation.Ragdoll.LegLengthUnits), "the floor is a leg length below the pelvis");
        }

        [Test]
        public void ALongWeapon_RestsWithItsTipAtTheCap_AndItsButtBehindTheShoulder()
        {
            WeaponStats spear = _rules.FindWeapon(WeaponIds.Spear);
            RagdollSettings body = _simulation.Ragdoll;
            Assert.That(spear.LengthUnits, Is.GreaterThan(body.GuardMaxTipDistanceUnits), "the spear is the long weapon");
            Vec2 shoulder = _rules.Paths.ShoulderOffsetUnits;

            Vec2 tipLocal = GuardStance.TipLocal(spear, body, _rules.Paths);

            Assert.That(Vec2.Distance(tipLocal, shoulder), Is.EqualTo(body.GuardMaxTipDistanceUnits).Within(Tolerance));

            var torso = new BodyPose(Vec2.Zero, 0f);
            FighterPose pose = GuardStance.Create(torso, Side.Left, BodyPart.RightArm, spear, RagdollSegmentation.TenBodies, body,
                _rules.Paths, _aim);
            Vec2 tip = HeldItemShape.Of(spear).PathPointAt(pose.HeldItem);
            Assert.That(Vec2.Distance(tip, TorsoFrame.ToArena(tipLocal, torso, Side.Left)), Is.LessThan(Tolerance), "the pose rests on the guard tip");
            Assert.That(pose.HeldItem.PositionUnits.X, Is.LessThan(TorsoFrame.ToArena(shoulder, torso, Side.Left).X),
                "the butt sticks out behind the shoulder");
            Assert.That(spear.ReachBeyondHandUnits, Is.EqualTo(spear.LengthUnits), "the cap never shortens the reach limit");
        }

        [TestCase("rapier")]
        [TestCase("sword")]
        [TestCase("mace")]
        [TestCase("shield")]
        public void AnItemWithinTheCap_RestsExactlyAsBeforeTheCap(string id)
        {
            WeaponStats item = _rules.FindWeapon(new WeaponId(id));
            RagdollSettings body = _simulation.Ragdoll;
            Vec2 asBefore = _rules.Paths.ShoulderOffsetUnits + Vec2.FromAngleDegrees(body.GuardAngleDegrees) *
                (_rules.Paths.ArmLengthUnits * body.GuardHandReachFraction + item.LengthUnits);

            Vec2 tip = GuardStance.TipLocal(item, body, _rules.Paths);

            Assert.That(Vec2.Distance(tip, asBefore), Is.LessThan(Tolerance));
        }

        [Test]
        public void StartingDummies_StandInsideTheArenaEdges()
        {
            ArenaEdges edges = new RulePolicies().ArenaEdge.EdgesFor(_rules.Arena);

            PoseSnapshot board = StartingBoard.Create(State(WeaponIds.Rapier, WeaponIds.Rapier), _rules, _simulation, _aim);

            Assert.That(board.Left.Get(BodyPart.Torso).PositionUnits.X, Is.GreaterThan(edges.LeftXUnits));
            Assert.That(board.Right.Get(BodyPart.Torso).PositionUnits.X, Is.LessThan(edges.RightXUnits));
        }

        [TestCase(RagdollSegmentation.SixBodies)]
        [TestCase(RagdollSegmentation.TenBodies)]
        public void GuardStance_WithoutAWeapon_HangsEveryLimbFromItsPivot(RagdollSegmentation segmentation)
        {
            var torso = new BodyPose(new Vec2(100f, 180f), 0f);

            FighterPose pose = GuardStance.Create(torso, Side.Right, BodyPart.RightArm, null, segmentation, _simulation.Ragdoll, _rules.Paths, _aim);

            Assert.That(pose.HasHeldItem, Is.False);
            Assert.That(pose.Get(BodyPart.Head).PositionUnits, Is.EqualTo(new Vec2(100f, 180f + _simulation.Ragdoll.TorsoHeightUnits)));
            Assert.That(pose.Get(BodyPart.LeftLeg).PositionUnits, Is.EqualTo(torso.PositionUnits));
            Vec2 shoulder = TorsoFrame.ToArena(_rules.Paths.ShoulderOffsetUnits, torso, Side.Right);
            Assert.That(Vec2.Distance(pose.Get(BodyPart.RightArm).PositionUnits, shoulder), Is.LessThan(Tolerance));
            Assert.That(pose.HasLowerSegments, Is.EqualTo(segmentation == RagdollSegmentation.TenBodies));
            if (segmentation == RagdollSegmentation.TenBodies)
            {
                float knee = _simulation.Ragdoll.LegLengthUnits * _simulation.Ragdoll.UpperSegmentFraction;
                Assert.That(pose.GetLower(BodyPart.LeftLeg).PositionUnits, Is.EqualTo(new Vec2(100f, 180f - knee)));
            }
        }

        [Test]
        public void GuardStance_TenBodies_HandStaysWithinTheArmsReach()
        {
            var torso = new BodyPose(Vec2.Zero, 0f);
            WeaponStats rapier = _rules.FindWeapon(WeaponIds.Rapier);
            RagdollSettings body = _simulation.Ragdoll;

            FighterPose pose = GuardStance.Create(torso, Side.Left, BodyPart.RightArm, rapier, RagdollSegmentation.TenBodies, body, _rules.Paths, _aim);

            float upper = _rules.Paths.ArmLengthUnits * body.UpperSegmentFraction;
            float lower = _rules.Paths.ArmLengthUnits - upper;
            BodyPose forearm = pose.GetLower(BodyPart.RightArm);
            Vec2 elbowFromUpper = pose.Get(BodyPart.RightArm).PositionUnits +
                                  Vec2.FromAngleDegrees(pose.Get(BodyPart.RightArm).RotationDegrees - 90f) * upper;
            Assert.That(Vec2.Distance(elbowFromUpper, forearm.PositionUnits), Is.LessThan(Tolerance), "upper arm and forearm meet at the elbow");
            Vec2 hand = forearm.PositionUnits + Vec2.FromAngleDegrees(forearm.RotationDegrees - 90f) * lower;
            Vec2 axis = Vec2.FromAngleDegrees(pose.HeldItem.RotationDegrees);
            Assert.That(System.Math.Abs(Vec2.Cross(axis, hand - pose.HeldItem.PositionUnits)), Is.LessThan(0.5f), "the hand is on the blade");
        }
    }
}
