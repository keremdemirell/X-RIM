using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.DebugTools.Sandbox
{
    /// <summary>
    /// Shows one side's plan in the sandbox, in the arena root's space so it lines up with the dummies: the raw stroke thin
    /// and white, and the path that will execute (lead-in from the weapon tip, resampled, reach-clamped, ink-limited, GDD §6)
    /// at the weapon's hit width. Paths are stored in the side's torso frame (§6, Decided), positions only (§4).
    /// </summary>
    internal sealed class SandboxPathDrawer
    {
        private const float StrokeWidthWorld = 0.02f;
        private const int LineSortingOrder = 100;
        private const int CapVertices = 4;

        private static readonly Color StrokeColor = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color PathColor = new Color(1f, 0.85f, 0.1f, 0.9f);

        private readonly LineRenderer _strokeLine;
        private readonly LineRenderer _pathLine;
        private readonly Side _side;

        public SandboxPathDrawer(Transform arenaRoot, Material material, Side side)
        {
            _side = side;
            _strokeLine = CreateLine(arenaRoot, side + "Stroke", material, StrokeColor);
            _pathLine = CreateLine(arenaRoot, side + "ExecutedPath", material, PathColor);
        }

        public void Show(IReadOnlyList<Vec2> strokeLocal, WeaponPath path, float widthUnits, BodyPose root, ArenaSpace space)
        {
            ShowLocal(_strokeLine, strokeLocal, root, space);
            _pathLine.widthMultiplier = space.ToWorldLength(widthUnits);
            ShowLocal(_pathLine, path != null ? path.Points : null, root, space);
        }

        public void Clear()
        {
            _strokeLine.positionCount = 0;
            _pathLine.positionCount = 0;
        }

        /// <summary>
        /// Removes the lines. They live under the scene's arena root, which leaving Play mode may already have destroyed
        /// (this drawer belongs to the debug tools, which outlive the scene), so each is checked first.
        /// </summary>
        public void Destroy()
        {
            if (_strokeLine != null) Object.Destroy(_strokeLine.gameObject);
            if (_pathLine != null) Object.Destroy(_pathLine.gameObject);
        }

        private void ShowLocal(LineRenderer line, IReadOnlyList<Vec2> pointsLocal, BodyPose root, ArenaSpace space)
        {
            int count = pointsLocal != null ? pointsLocal.Count : 0;
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                line.SetPosition(i, space.ToWorld(TorsoFrame.ToArena(pointsLocal[i], root, _side)));
            }
        }

        private static LineRenderer CreateLine(Transform parent, string name, Material material, Color color)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = StrokeWidthWorld;
            line.numCapVertices = CapVertices;
            line.numCornerVertices = CapVertices;
            line.sortingOrder = LineSortingOrder;
            line.positionCount = 0;
            return line;
        }
    }
}
