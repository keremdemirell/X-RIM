using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.DebugTools.Spike
{
    /// <summary>
    /// Mouse drawing for the feel spike. The stroke is stored in the attacker's torso frame (GDD §6, Decided: paths are
    /// torso-relative) as positions only, never timestamps, so drawing speed cannot matter (§4). It shows the raw stroke
    /// thin and white, and the path that will execute (lead-in, resampled, reach-clamped, ink-limited) in yellow at the
    /// weapon's hit width.
    /// </summary>
    internal sealed class SpikePathDrawer
    {
        /// <summary>Raw points closer than this are skipped; the path rules resample anyway.</summary>
        private const float MinPointSpacingUnits = 1f;

        private const float StrokeWidthWorld = 0.02f;
        private const int LineSortingOrder = 100;
        private const int CapVertices = 4;

        private static readonly Color StrokeColor = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color PathColor = new Color(1f, 0.85f, 0.1f, 0.9f);

        private readonly List<Vec2> _strokeLocal = new List<Vec2>();
        private readonly LineRenderer _strokeLine;
        private readonly LineRenderer _pathLine;
        private bool _drawing;

        public SpikePathDrawer(Transform parent, Material material)
        {
            _strokeLine = CreateLine(parent, "Stroke", material, StrokeColor);
            _pathLine = CreateLine(parent, "ExecutedPath", material, PathColor);
        }

        public bool IsDrawing => _drawing;
        public bool HasStroke => _strokeLocal.Count >= 2;
        public WeaponPath Stroke => new WeaponPath(_strokeLocal);

        /// <summary>Feeds one frame of mouse state. Returns true when a stroke was just finished.</summary>
        public bool Track(bool pressed, bool startAllowed, Vector2 pointerWorld, BodyPose torso, ArenaSpace space)
        {
            Vec2 local = TorsoFrame.ToLocal(space.ToArena(pointerWorld), torso, Side.Left);
            if (pressed && !_drawing && startAllowed)
            {
                _drawing = true;
                _strokeLocal.Clear();
                _strokeLocal.Add(local);
            }
            else if (pressed && _drawing)
            {
                if (Vec2.Distance(_strokeLocal[_strokeLocal.Count - 1], local) >= MinPointSpacingUnits) _strokeLocal.Add(local);
            }
            else if (!pressed && _drawing)
            {
                _drawing = false;
                ShowLocal(_strokeLine, _strokeLocal, torso, space);
                return true;
            }

            if (_drawing) ShowLocal(_strokeLine, _strokeLocal, torso, space);
            return false;
        }

        /// <summary>Shows the path that will execute, at the weapon's hit width (ink thickness, GDD §6).</summary>
        public void ShowPath(WeaponPath path, BodyPose torso, ArenaSpace space, float widthUnits)
        {
            _pathLine.widthMultiplier = space.ToWorldLength(widthUnits);
            ShowLocal(_pathLine, path != null ? path.Points : null, torso, space);
        }

        public void ShowStroke(BodyPose torso, ArenaSpace space) => ShowLocal(_strokeLine, _strokeLocal, torso, space);

        public void SetVisible(bool visible)
        {
            _strokeLine.enabled = visible;
            _pathLine.enabled = visible;
        }

        public void Clear()
        {
            _drawing = false;
            _strokeLocal.Clear();
            _strokeLine.positionCount = 0;
            _pathLine.positionCount = 0;
        }

        private static void ShowLocal(LineRenderer line, IReadOnlyList<Vec2> pointsLocal, BodyPose torso, ArenaSpace space)
        {
            int count = pointsLocal != null ? pointsLocal.Count : 0;
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                line.SetPosition(i, space.ToWorld(TorsoFrame.ToArena(pointsLocal[i], torso, Side.Left)));
            }
        }

        private static LineRenderer CreateLine(Transform parent, string name, Material material, Color color)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
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
