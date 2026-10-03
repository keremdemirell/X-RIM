using UnityEngine;

namespace XRim.DebugTools
{
    /// <summary>The unlit material and line set-up the debug drawings share (sandbox paths, contact gizmos).</summary>
    internal static class DebugLines
    {
        private const string SpriteShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const string FallbackShaderName = "Sprites/Default";
        private const int CapVertices = 4;

        public static Material CreateMaterial()
        {
            Shader shader = Shader.Find(SpriteShaderName);
            if (shader == null) shader = Shader.Find(FallbackShaderName);
            return shader != null ? new Material(shader) : null;
        }

        /// <summary>An empty line drawn in its parent's local space (the arena root, so it flips with the view).</summary>
        public static LineRenderer Create(Transform parent, string name, Material material, Color color, float widthWorld, int sortingOrder)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = widthWorld;
            line.numCapVertices = CapVertices;
            line.numCornerVertices = CapVertices;
            line.sortingOrder = sortingOrder;
            line.positionCount = 0;
            return line;
        }
    }
}
