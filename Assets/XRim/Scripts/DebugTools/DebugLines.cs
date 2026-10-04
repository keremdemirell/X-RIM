using UnityEngine;
using XRim.Presentation.Arena;

namespace XRim.DebugTools
{
    /// <summary>The unlit material and line set-up the debug drawings share (contact gizmos); the same lines the path previews use.</summary>
    internal static class DebugLines
    {
        public static Material CreateMaterial() => ArenaLines.CreateMaterial();

        /// <summary>An empty line drawn in its parent's local space (the arena root, so it flips with the view).</summary>
        public static LineRenderer Create(Transform parent, string name, Material material, Color color, float widthWorld, int sortingOrder) =>
            ArenaLines.Create(parent, name, material, color, widthWorld, sortingOrder);
    }
}
