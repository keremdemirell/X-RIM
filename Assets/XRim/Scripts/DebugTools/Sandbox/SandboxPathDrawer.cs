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
    /// at the weapon's hit width. Paths are stored in the side's torso frame (§6, Decided), positions only (§4). With a body
    /// move, a faint copy shows where the path will be once the move is at full extent (the path travels with the body).
    /// </summary>
    internal sealed class SandboxPathDrawer
    {
        private const float StrokeWidthWorld = 0.02f;
        private const int LineSortingOrder = 100;

        private static readonly Color StrokeColor = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color PathColor = new Color(1f, 0.85f, 0.1f, 0.9f);
        private static readonly Color GhostColor = new Color(1f, 0.85f, 0.1f, 0.3f);

        private readonly LineRenderer _strokeLine;
        private readonly LineRenderer _pathLine;
        private readonly LineRenderer _ghostLine;
        private readonly Side _side;

        public SandboxPathDrawer(Transform arenaRoot, Material material, Side side)
        {
            _side = side;
            _strokeLine = DebugLines.Create(arenaRoot, side + "Stroke", material, StrokeColor, StrokeWidthWorld, LineSortingOrder);
            _pathLine = DebugLines.Create(arenaRoot, side + "ExecutedPath", material, PathColor, StrokeWidthWorld, LineSortingOrder);
            _ghostLine = DebugLines.Create(arenaRoot, side + "PathAtFullExtent", material, GhostColor, StrokeWidthWorld, LineSortingOrder);
        }

        /// <param name="moveFrame">The path's frame once the body move is at full extent; null without a body move.</param>
        public void Show(IReadOnlyList<Vec2> strokeLocal, WeaponPath path, float widthUnits, BodyPose root, BodyPose? moveFrame, ArenaSpace space)
        {
            ShowLocal(_strokeLine, strokeLocal, root, space);
            _pathLine.widthMultiplier = space.ToWorldLength(widthUnits);
            ShowLocal(_pathLine, path != null ? path.Points : null, root, space);
            ShowLocal(_ghostLine, path != null && moveFrame.HasValue ? path.Points : null, moveFrame ?? root, space);
        }

        public void Clear()
        {
            _strokeLine.positionCount = 0;
            _pathLine.positionCount = 0;
            _ghostLine.positionCount = 0;
        }

        /// <summary>
        /// Removes the lines. They live under the scene's arena root, which leaving Play mode may already have destroyed
        /// (this drawer belongs to the debug tools, which outlive the scene), so each is checked first.
        /// </summary>
        public void Destroy()
        {
            if (_strokeLine != null) Object.Destroy(_strokeLine.gameObject);
            if (_pathLine != null) Object.Destroy(_pathLine.gameObject);
            if (_ghostLine != null) Object.Destroy(_ghostLine.gameObject);
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

    }
}
