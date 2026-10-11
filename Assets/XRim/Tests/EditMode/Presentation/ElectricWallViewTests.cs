using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Arena;
using XRim.Rules.Arena;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The placeholder electric wall on the board (GDD §13, Session 12): drawn where the pose puts it, behind its owner, amber while it is a
    /// warning, cyan once live, gone when the pose has none.
    /// </summary>
    public sealed class ElectricWallViewTests
    {
        private readonly ArenaSpace _space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
        private GameObject _root;
        private Texture2D _texture;
        private ElectricWallView _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ArenaRoot");
            _texture = new Texture2D(4, 4);
            Sprite square = Sprite.Create(_texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            _view = ElectricWallView.Create(_root.transform, square, _space);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void NoWall_DrawsNothing()
        {
            _view.Show(new PoseSnapshot());

            Assert.That(_view.BarOf(Side.Left).enabled, Is.False);
            Assert.That(_view.BarOf(Side.Right).enabled, Is.False);
        }

        [Test]
        public void AWarning_IsAmber_AndALiveWallCyan()
        {
            _view.Show(new PoseSnapshot { LeftWall = new ElectricWallPose(ElectricWallPhase.Warning, -510f) });
            Assert.That(_view.BarOf(Side.Left).enabled, Is.True);
            Assert.That(_view.BarOf(Side.Left).color, Is.EqualTo(ElectricWallView.WarningColor));

            _view.Show(new PoseSnapshot { LeftWall = new ElectricWallPose(ElectricWallPhase.Active, -460f) });
            Assert.That(_view.BarOf(Side.Left).color, Is.EqualTo(ElectricWallView.ActiveColor));
        }

        [Test]
        public void TheBar_StandsBehindItsFace_AwayFromTheOpponent()
        {
            _view.Show(new PoseSnapshot
            {
                LeftWall = new ElectricWallPose(ElectricWallPhase.Active, -500f),
                RightWall = new ElectricWallPose(ElectricWallPhase.Warning, 500f),
            });

            float leftX = _view.BarOf(Side.Left).transform.localPosition.x;
            float rightX = _view.BarOf(Side.Right).transform.localPosition.x;
            Assert.That(leftX, Is.LessThan(_space.ToWorld(new Vec2(-500f, 0f)).x), "left's bar lies at lower x than its face");
            Assert.That(rightX, Is.GreaterThan(_space.ToWorld(new Vec2(500f, 0f)).x));
        }

        [Test]
        public void AWallThatGoesAway_IsNoLongerDrawn()
        {
            _view.Show(new PoseSnapshot { RightWall = new ElectricWallPose(ElectricWallPhase.Warning, 500f) });

            _view.Show(new PoseSnapshot());

            Assert.That(_view.BarOf(Side.Right).enabled, Is.False);
        }
    }
}
