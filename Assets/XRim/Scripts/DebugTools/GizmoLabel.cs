using UnityEngine;
using XRim.Core;

namespace XRim.DebugTools
{
    /// <summary>A debug label at an arena point (canonical arena units), in a colour; <see cref="GameplayGizmos"/> draws it on screen.</summary>
    internal readonly struct GizmoLabel
    {
        public GizmoLabel(Vec2 pointUnits, string text, Color color)
        {
            PointUnits = pointUnits;
            Text = text;
            Color = color;
        }

        public Vec2 PointUnits { get; }
        public string Text { get; }
        public Color Color { get; }
    }
}
