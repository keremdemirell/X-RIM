using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Arena;

namespace XRim.Presentation.Arena
{
    /// <summary>
    /// Placeholder arena drawing (art arrives in Session 17): the floor between the edges, a faint marker where each solid edge stops a
    /// dummy (D22, an invisible stop in the game; the marker helps while tuning), and (Session 13) a neutral backdrop for contrast: dark
    /// bands growing lighter toward the floor, a faint calibration grid and a stripe along the floor's edge. The setting itself (a
    /// crash-test facility is proposed) is TBD, so this stays neutral.
    /// </summary>
    [GddTbd("§2", "Default arena setting", Proposal = "Crash-test facility")]
    public static class ArenaView
    {
        private const string RootName = "ArenaView";
        private const float FloorThicknessUnits = 100f;
        private const float EdgeMarkerWidthUnits = 8f;
        private const float EdgeMarkerHeightUnits = 600f;
        private const int FloorSortingOrder = -100;
        private const int EdgeSortingOrder = -90;
        private const int FloorStripeSortingOrder = -95;
        private const int BackdropSortingOrder = -200;
        private const int GridSortingOrder = -190;

        /// <summary>The backdrop reaches this far past each edge, below the floor and up (any framing stays inside it).</summary>
        private const float BackdropMarginUnits = 1500f;

        private const float BackdropTopUnits = 1600f;
        private const float BackdropBottomUnits = -400f;
        private const int BackdropBands = 7;
        private const float GridSpacingXUnits = 250f;
        private const float GridSpacingYUnits = 100f;
        private const float GridLineUnits = 3f;
        private const float FloorStripeUnits = 5f;

        private static readonly Color FloorColor = new Color(0.22f, 0.22f, 0.25f);
        private static readonly Color EdgeMarkerColor = new Color(1f, 0.85f, 0.1f, 0.25f);
        private static readonly Color BackdropNearFloorColor = new Color(0.16f, 0.17f, 0.2f);
        private static readonly Color BackdropTopColor = new Color(0.07f, 0.075f, 0.09f);
        private static readonly Color GridColor = new Color(1f, 1f, 1f, 0.035f);
        private static readonly Color FloorStripeColor = new Color(0.95f, 0.78f, 0.1f, 0.45f);

        public static GameObject Create(Transform parent, Sprite square, ArenaEdges edges, ArenaSpace space)
        {
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            if (square == null) return root;

            AddBackdrop(root.transform, square, edges, space);
            AddBox(root.transform, square, "Floor", new Vec2(edges.CentreXUnits, -FloorThicknessUnits * 0.5f), edges.WidthUnits,
                FloorThicknessUnits, FloorColor, FloorSortingOrder, space);
            AddBox(root.transform, square, "FloorStripe", new Vec2(edges.CentreXUnits, -FloorStripeUnits * 0.5f), edges.WidthUnits, FloorStripeUnits,
                FloorStripeColor, FloorStripeSortingOrder, space);
            if (!edges.IsSolid) return root;

            float markerY = EdgeMarkerHeightUnits * 0.5f;
            AddBox(root.transform, square, "EdgeLeft", new Vec2(edges.LeftXUnits, markerY), EdgeMarkerWidthUnits, EdgeMarkerHeightUnits,
                EdgeMarkerColor, EdgeSortingOrder, space);
            AddBox(root.transform, square, "EdgeRight", new Vec2(edges.RightXUnits, markerY), EdgeMarkerWidthUnits, EdgeMarkerHeightUnits,
                EdgeMarkerColor, EdgeSortingOrder, space);
            return root;
        }

        /// <summary>Stepped bands from dark at the top to lighter near the floor, and a faint grid like a test wall's calibration marks.</summary>
        private static void AddBackdrop(Transform parent, Sprite square, ArenaEdges edges, ArenaSpace space)
        {
            float left = edges.LeftXUnits - BackdropMarginUnits;
            float right = edges.RightXUnits + BackdropMarginUnits;
            float width = right - left;
            float centreX = (left + right) * 0.5f;
            float bandHeight = (BackdropTopUnits - BackdropBottomUnits) / BackdropBands;
            for (int band = 0; band < BackdropBands; band++)
            {
                float bottom = BackdropBottomUnits + band * bandHeight;
                Color color = Color.Lerp(BackdropNearFloorColor, BackdropTopColor, (float)band / (BackdropBands - 1));
                AddBox(parent, square, "Backdrop" + band, new Vec2(centreX, bottom + bandHeight * 0.5f), width, bandHeight, color, BackdropSortingOrder, space);
            }

            float gridHeight = BackdropTopUnits;
            for (float x = Mathf.Ceil(left / GridSpacingXUnits) * GridSpacingXUnits; x <= right; x += GridSpacingXUnits)
            {
                AddBox(parent, square, "GridX", new Vec2(x, gridHeight * 0.5f), GridLineUnits, gridHeight, GridColor, GridSortingOrder, space);
            }

            for (float y = GridSpacingYUnits; y <= BackdropTopUnits; y += GridSpacingYUnits)
            {
                AddBox(parent, square, "GridY", new Vec2(centreX, y), width, GridLineUnits, GridColor, GridSortingOrder, space);
            }
        }

        private static void AddBox(Transform parent, Sprite square, string name, Vec2 centreUnits, float widthUnits, float heightUnits, Color color,
            int sortingOrder, ArenaSpace space)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = space.ToWorld(centreUnits);
            box.transform.localScale = new Vector3(space.ToWorldLength(widthUnits), space.ToWorldLength(heightUnits), 1f);
            var renderer = box.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }
    }
}
