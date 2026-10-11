using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Dummy;
using XRim.Rules;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// How the visible dummy draws lost limbs (GDD §11, §12): a severed limb where it lies on the floor, hidden while someone wields it as
    /// a club; a dropped weapon where it falls; the torso's two halves once it split. A dummy of plain transforms, ten bodies.
    /// </summary>
    public sealed class DummyViewTests
    {
        private const float Tolerance = 1e-3f;

        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly ArenaSpace _space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
        private DummyView _view;
        private Transform[] _parts;
        private Transform[] _lower;
        private Transform _torsoBottom;
        private Transform _torsoTop;
        private Transform _rapier;

        [SetUp]
        public void SetUp()
        {
            GameObject root = Make("Dummy", null);
            _parts = new Transform[BodyParts.Count];
            _lower = new Transform[BodyParts.Count];
            foreach (BodyPart part in BodyParts.All)
            {
                _parts[(int)part] = Make(part.ToString(), root.transform).transform;
                if (part.IsSeverable()) _lower[(int)part] = Make(part + "Lower", root.transform).transform;
            }

            _torsoBottom = Make("TorsoBottom", root.transform).transform;
            _torsoTop = Make("TorsoTop", root.transform).transform;
            _torsoBottom.gameObject.SetActive(false);
            _torsoTop.gameObject.SetActive(false);
            _rapier = Make("Rapier", root.transform).transform;
            _view = root.AddComponent<DummyView>();
            _view.Configure(_parts, _lower, _torsoBottom, _torsoTop);
            _view.AddHeldItem(WeaponIds.Rapier, _rapier);
            _view.ShowWeapon(WeaponIds.Rapier);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        private GameObject Make(string name, Transform parent)
        {
            var created = new GameObject(name);
            if (parent != null) created.transform.SetParent(parent, false);
            else _created.Add(created);
            return created;
        }

        private static FighterPose Pose()
        {
            var pose = new FighterPose { HasLowerSegments = true, HasHeldItem = true, HeldItem = new BodyPose(new Vec2(0f, 100f), 0f) };
            foreach (BodyPart part in BodyParts.All)
            {
                pose.Set(part, new BodyPose(new Vec2(0f, 180f), 0f));
                pose.SetLower(part, new BodyPose(new Vec2(0f, 90f), 0f));
            }

            return pose;
        }

        private Vector3 World(Vec2 units) => _space.ToWorld(units);

        [Test]
        public void ASeveredLimb_IsDrawnWhereItLiesOnTheFloor()
        {
            FighterPose pose = Pose();
            pose.MarkSevered(BodyPart.LeftLeg);
            var floor = new List<SeveredLimbPose>
            {
                new SeveredLimbPose(Side.Right, BodyPart.LeftLeg, new BodyPose(new Vec2(900f, 0f), 0f), new BodyPose(new Vec2(990f, 0f), 0f)),
                new SeveredLimbPose(Side.Left, BodyPart.LeftLeg, new BodyPose(new Vec2(300f, 20f), 90f), new BodyPose(new Vec2(390f, 20f), 90f)),
            };

            _view.ApplyPose(pose, Side.Left, floor, _space);

            Transform leg = _parts[(int)BodyPart.LeftLeg];
            Assert.That(leg.gameObject.activeSelf, Is.True);
            Assert.That(Vector3.Distance(leg.localPosition, World(new Vec2(300f, 20f))), Is.LessThan(Tolerance), "its own leg, not the opponent's");
            Assert.That(Vector3.Distance(_lower[(int)BodyPart.LeftLeg].localPosition, World(new Vec2(390f, 20f))), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(_parts[(int)BodyPart.RightLeg].localPosition, World(new Vec2(0f, 180f))), Is.LessThan(Tolerance),
                "the leg it kept is drawn on the dummy");
        }

        [Test]
        public void ASeveredLimbNotOnTheFloor_IsHidden_ItIsWieldedAsAClub()
        {
            FighterPose pose = Pose();
            pose.MarkSevered(BodyPart.RightArm);

            _view.ApplyPose(pose, Side.Left, new List<SeveredLimbPose>(), _space);

            Assert.That(_parts[(int)BodyPart.RightArm].gameObject.activeSelf, Is.False);
            Assert.That(_lower[(int)BodyPart.RightArm].gameObject.activeSelf, Is.False);

            _view.ApplyPose(Pose(), Side.Left, new List<SeveredLimbPose>(), _space);
            Assert.That(_parts[(int)BodyPart.RightArm].gameObject.activeSelf, Is.True, "shown again when the pose has it back (a new match)");
        }

        [Test]
        public void ADroppedWeapon_IsDrawnWhereItFalls()
        {
            FighterPose pose = Pose();
            pose.HasHeldItem = false;
            pose.HasDroppedItem = true;
            pose.DroppedItem = new BodyPose(new Vec2(150f, 10f), 30f);

            _view.ApplyPose(pose, Side.Left, new List<SeveredLimbPose>(), _space);

            Assert.That(_rapier.gameObject.activeSelf, Is.True);
            Assert.That(Vector3.Distance(_rapier.localPosition, World(new Vec2(150f, 10f))), Is.LessThan(Tolerance));
        }

        [Test]
        public void ASplitTorso_ShowsItsTwoHalves_InsteadOfTheTorso()
        {
            FighterPose pose = Pose();
            pose.IsTorsoSplit = true;
            pose.Set(BodyPart.Torso, new BodyPose(new Vec2(0f, 40f), 80f));
            pose.TorsoTop = new BodyPose(new Vec2(-70f, 30f), 120f);

            _view.ApplyPose(pose, Side.Left, new List<SeveredLimbPose>(), _space);

            Assert.That(_parts[(int)BodyPart.Torso].gameObject.activeSelf, Is.False);
            Assert.That(_torsoBottom.gameObject.activeSelf && _torsoTop.gameObject.activeSelf, Is.True);
            Assert.That(Vector3.Distance(_torsoTop.localPosition, World(new Vec2(-70f, 30f))), Is.LessThan(Tolerance));

            _view.ApplyPose(Pose(), Side.Left, new List<SeveredLimbPose>(), _space);
            Assert.That(_parts[(int)BodyPart.Torso].gameObject.activeSelf, Is.True, "whole again (S5)");
            Assert.That(_torsoTop.gameObject.activeSelf, Is.False);
        }
    }
}
