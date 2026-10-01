using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules.Arena;

namespace XRim.Presentation.Arena
{
    /// <summary>
    /// Placeholder arena drawing (art arrives in Session 17): the floor between the edges, and a faint marker where each
    /// solid edge stops a dummy (D22, an invisible stop in the game; the marker helps while tuning).
    /// </summary>
    public static class ArenaView
    {
        private const string RootName = "ArenaView";
        private const float FloorThicknessUnits = 100f;
        private const float EdgeMarkerWidthUnits = 8f;
        private const float EdgeMarkerHeightUnits = 600f;
        private const int FloorSortingOrder = -100;
        private const int EdgeSortingOrder = -90;

        private static readonly Color FloorColor = new Color(0.22f, 0.22f, 0.25f);
        private static readonly Color EdgeMarkerColor = new Color(1f, 0.85f, 0.1f, 0.25f);

        public static GameObject Create(Transform parent, Sprite square, ArenaEdges edges, ArenaSpace space)
        {
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            if (square == null) return root;

            AddBox(root.transform, square, "Floor", new Vec2(edges.CentreXUnits, -FloorThicknessUnits * 0.5f), edges.WidthUnits,
                FloorThicknessUnits, FloorColor, FloorSortingOrder, space);
            if (!edges.IsSolid) return root;

            float markerY = EdgeMarkerHeightUnits * 0.5f;
            AddBox(root.transform, square, "EdgeLeft", new Vec2(edges.LeftXUnits, markerY), EdgeMarkerWidthUnits, EdgeMarkerHeightUnits,
                EdgeMarkerColor, EdgeSortingOrder, space);
            AddBox(root.transform, square, "EdgeRight", new Vec2(edges.RightXUnits, markerY), EdgeMarkerWidthUnits, EdgeMarkerHeightUnits,
                EdgeMarkerColor, EdgeSortingOrder, space);
            return root;
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
