using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Arena
{
    /// <summary>D22 (GDD §13 stays TBD): the arena width comes from settings and each edge is a solid invisible stop.</summary>
    public sealed class ArenaEdgePolicyTests
    {
        [Test]
        public void Default_IsTheSolidStopPolicy()
        {
            Assert.That(new RulePolicies().ArenaEdge, Is.TypeOf<SolidStopArenaEdgePolicy>());
        }

        [Test]
        public void SolidStop_CentresTheTunableWidthOnZero()
        {
            var arena = new ArenaSettings { WidthUnits = 1600f };

            ArenaEdges edges = new SolidStopArenaEdgePolicy().EdgesFor(arena);

            Assert.That(edges.LeftXUnits, Is.EqualTo(-800f));
            Assert.That(edges.RightXUnits, Is.EqualTo(800f));
            Assert.That(edges.WidthUnits, Is.EqualTo(1600f));
            Assert.That(edges.IsSolid, Is.True);
        }

        [Test]
        public void DefaultWidth_LeavesRoomBehindBothStartingLines()
        {
            var arena = new ArenaSettings();

            ArenaEdges edges = new SolidStopArenaEdgePolicy().EdgesFor(arena);

            Assert.That(edges.RightXUnits, Is.GreaterThan(arena.StartingGapUnits * 0.5f));
        }

        [Test]
        public void SolidEdges_KeepAPositionTheMarginInside()
        {
            var edges = new ArenaEdges(-1000f, 1000f, isSolid: true);

            Assert.That(edges.ClampX(-990f, 30f), Is.EqualTo(-970f));
            Assert.That(edges.ClampX(1200f, 30f), Is.EqualTo(970f));
            Assert.That(edges.ClampX(400f, 30f), Is.EqualTo(400f));
        }

        [Test]
        public void OpenEdges_NeverMoveAPosition()
        {
            var edges = new ArenaEdges(-1000f, 1000f, isSolid: false);

            Assert.That(edges.ClampX(-1500f, 30f), Is.EqualTo(-1500f));
        }

        [Test]
        public void ArenaNarrowerThanTheMargins_KeepsThePositionAtTheCentre()
        {
            var edges = new ArenaEdges(100f, 140f, isSolid: true);

            Assert.That(edges.ClampX(0f, 30f), Is.EqualTo(120f));
        }
    }
}
