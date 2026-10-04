using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Presentation.Arena
{
    /// <summary>
    /// Draws one side's live path preview (GDD §6) in the arena root's space, so it lines up with the dummies and flips with the
    /// view. Paths are stored in the side's torso frame (§6, Decided) and positions only (§4):
    /// <list type="bullet">
    /// <item>the raw stroke, thin and white;</item>
    /// <item>the path that will execute, at the weapon's hit width (the lead-in from the tip, clipped at the reach limit and where
    /// the ink runs out);</item>
    /// <item>the part a too-sharp turn broke, in red;</item>
    /// <item>with a body move, a faint copy of the path where it will be once the move is at full extent (the path travels with
    /// the body);</item>
    /// <item>while drawing, a faint ring at the reach limit.</item>
    /// </list>
    /// </summary>
    public sealed class PathPreviewView
    {
        private const float ThinWidthWorld = 0.02f;
        private const int SortingOrder = 100;
        private const int RingSegments = 64;
        private const float FullTurnDegrees = 360f;

        private static readonly Color StrokeColor = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color PathColor = new Color(1f, 0.85f, 0.1f, 0.9f);
        private static readonly Color InvalidColor = new Color(0.95f, 0.15f, 0.1f, 0.95f);
        private static readonly Color GhostColor = new Color(1f, 0.85f, 0.1f, 0.3f);
        private static readonly Color ReachColor = new Color(1f, 1f, 1f, 0.18f);

        private readonly LineRenderer _strokeLine;
        private readonly LineRenderer _pathLine;
        private readonly LineRenderer _invalidLine;
        private readonly LineRenderer _ghostLine;
        private readonly LineRenderer _reachLine;
        private readonly List<Vec2> _ring = new List<Vec2>(RingSegments + 1);

        public PathPreviewView(Transform arenaRoot, Material material, string name)
        {
            _strokeLine = ArenaLines.Create(arenaRoot, name + "Stroke", material, StrokeColor, ThinWidthWorld, SortingOrder);
            _pathLine = ArenaLines.Create(arenaRoot, name + "ExecutedPath", material, PathColor, ThinWidthWorld, SortingOrder);
            _invalidLine = ArenaLines.Create(arenaRoot, name + "InvalidPath", material, InvalidColor, ThinWidthWorld, SortingOrder);
            _ghostLine = ArenaLines.Create(arenaRoot, name + "PathAtFullExtent", material, GhostColor, ThinWidthWorld, SortingOrder);
            _reachLine = ArenaLines.Create(arenaRoot, name + "ReachLimit", material, ReachColor, ThinWidthWorld, SortingOrder);
        }

        /// <param name="preview">Null clears everything.</param>
        /// <param name="root">The side's turn-start root: the frame the preview is in.</param>
        /// <param name="moveFrame">The path's frame once the body move is at full extent; null without a body move.</param>
        /// <param name="showReach">Draw the reach ring (while the finger is drawing).</param>
        public void Show(PathPreview preview, BodyPose root, Side side, ArenaSpace space, BodyPose? moveFrame, bool showReach)
        {
            if (preview == null)
            {
                Clear();
                return;
            }

            float width = space.ToWorldLength(preview.ThicknessUnits);
            _pathLine.widthMultiplier = width;
            _invalidLine.widthMultiplier = width;
            _ghostLine.widthMultiplier = width;

            ShowLocal(_strokeLine, preview.Stroke.Points, root, side, space);
            ShowLocal(_pathLine, preview.Path.Points, root, side, space);
            ShowLocal(_invalidLine, preview.InvalidTail.IsEmpty ? null : preview.InvalidTail.Points, root, side, space);
            ShowLocal(_ghostLine, moveFrame.HasValue && !preview.Path.IsEmpty ? preview.Path.Points : null, moveFrame ?? root, side, space);
            ShowLocal(_reachLine, showReach ? Ring(preview.Reach) : null, root, side, space);
        }

        public void Clear()
        {
            _strokeLine.positionCount = 0;
            _pathLine.positionCount = 0;
            _invalidLine.positionCount = 0;
            _ghostLine.positionCount = 0;
            _reachLine.positionCount = 0;
        }

        /// <summary>
        /// Removes the lines. They live under the scene's arena root, which leaving Play mode may already have destroyed, so
        /// each is checked first.
        /// </summary>
        public void Destroy()
        {
            DestroyLine(_strokeLine);
            DestroyLine(_pathLine);
            DestroyLine(_invalidLine);
            DestroyLine(_ghostLine);
            DestroyLine(_reachLine);
        }

        private static void DestroyLine(LineRenderer line)
        {
            if (line != null) Object.Destroy(line.gameObject);
        }

        /// <summary>The reach limit as a closed circle around the shoulder, in the torso frame.</summary>
        private IReadOnlyList<Vec2> Ring(ReachLimit reach)
        {
            _ring.Clear();
            for (int i = 0; i <= RingSegments; i++)
            {
                _ring.Add(reach.OriginUnits + Vec2.FromAngleDegrees(FullTurnDegrees * i / RingSegments) * reach.RadiusUnits);
            }

            return _ring;
        }

        private static void ShowLocal(LineRenderer line, IReadOnlyList<Vec2> pointsLocal, BodyPose root, Side side, ArenaSpace space)
        {
            int count = pointsLocal != null ? pointsLocal.Count : 0;
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                line.SetPosition(i, space.ToWorld(TorsoFrame.ToArena(pointsLocal[i], root, side)));
            }
        }
    }
}
