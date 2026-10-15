using NUnit.Framework;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Cameras;
using XRim.Rules.Arena;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The duel framing (Session 13, designer 2026-10-11, F3; GDD §2 camera TBD): a set height centred between the fighters, the floor near
    /// the bottom, widened only when the fighters stand too far apart, never past the whole-arena view, and kept from showing far past the
    /// arena's edges.
    /// </summary>
    public sealed class CameraFramingTests
    {
        private const float Tolerance = 1e-3f;
        private const float Aspect = 2f;
        private static readonly ArenaEdges Edges = new ArenaEdges(-1000f, 1000f, true);

        private static CameraFeelSettings Settings() => new CameraFeelSettings
        {
            Framing = CameraFramingMode.Duel, DuelViewHeightUnits = 700f, FramingMarginBehindUnits = 160f, FramingFloorMarginUnits = 90f,
            FramingEdgeMarginUnits = 60f, MaxViewHeightUnits = 1150f,
        };

        [Test]
        public void TheStartingGap_IsFramedAtTheSetHeight_CentredBetweenTheFighters_FloorNearTheBottom()
        {
            ViewFraming framing = DuelFraming.Compute(-350f, 350f, Aspect, Settings(), Edges);

            Assert.That(framing.HeightUnits, Is.EqualTo(700f).Within(Tolerance));
            Assert.That(framing.CentreUnits.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(framing.CentreUnits.Y - framing.HeightUnits * 0.5f, Is.EqualTo(-90f).Within(Tolerance), "the screen's bottom edge");
        }

        [Test]
        public void FightersTooFarApart_WidenTheView_SoBothFitWithRoomBehind()
        {
            // 1500 apart plus 160 behind each needs 1820 across: 910 tall at 2:1.
            ViewFraming framing = DuelFraming.Compute(-750f, 750f, Aspect, Settings(), Edges);

            Assert.That(framing.HeightUnits, Is.EqualTo(910f).Within(Tolerance));
        }

        [Test]
        public void TheView_NeverGrowsPastTheWholeArenaView()
        {
            ViewFraming framing = DuelFraming.Compute(-990f, 990f, 1.5f, Settings(), Edges);

            Assert.That(framing.HeightUnits, Is.EqualTo(1150f).Within(Tolerance));
        }

        [Test]
        public void FightersNearAnEdge_AreFramedWithoutShowingFarPastIt()
        {
            ViewFraming framing = DuelFraming.Compute(-950f, -500f, Aspect, Settings(), Edges);

            float left = framing.CentreUnits.X - framing.HeightUnits * Aspect * 0.5f;
            Assert.That(left, Is.EqualTo(-1060f).Within(Tolerance), "60 past the edge at most");
            Assert.That(left, Is.LessThan(-950f - 0f), "the fighter near the edge stays in view");
        }

        [Test]
        public void WithoutEdges_TheViewIsCentredBetweenTheFighters()
        {
            ViewFraming framing = DuelFraming.Compute(100f, 500f, Aspect, Settings(), null);

            Assert.That(framing.CentreUnits.X, Is.EqualTo(300f).Within(Tolerance));
        }

        [Test]
        public void Framings_LerpAndCompare()
        {
            var from = new ViewFraming(new Vec2(0f, 100f), 700f);
            var to = new ViewFraming(new Vec2(100f, 300f), 900f);

            ViewFraming half = from.Lerp(to, 0.5f);

            Assert.That(half.CentreUnits.X, Is.EqualTo(50f).Within(Tolerance));
            Assert.That(half.HeightUnits, Is.EqualTo(800f).Within(Tolerance));
            Assert.That(from.IsCloseTo(new ViewFraming(new Vec2(0.2f, 100f), 700.1f)), Is.True);
            Assert.That(from.IsCloseTo(to), Is.False);
        }
    }
}
