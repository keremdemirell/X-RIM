using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Vfx;
using XRim.Rules.Arena;
using XRim.Simulation.Physics;

namespace XRim.Presentation.Arena
{
    /// <summary>
    /// Placeholder drawing of each player's electric wall (GDD §13; art arrives in Session 17): a tall bar whose face stands where the
    /// pose puts it, behind its owner. A warning is a pale amber bar that flickers; a live wall a bright cyan one that pulses and crackles
    /// with arcs up its face (Session 13); no wall, nothing. A wall that springs up grows from the floor. It follows every pose playback
    /// shows (<c>TurnPlayback.PoseShown</c>), the frozen board included, and animates in real time, so a wall standing during planning
    /// keeps crackling. Drawing only: it never changes a turn.
    /// </summary>
    public sealed class ElectricWallView : MonoBehaviour
    {
        private const string RootName = "ElectricWalls";

        /// <summary>The bar's thickness and height on screen (technical: how the placeholder looks, not where the wall stands).</summary>
        internal const float BarWidthUnits = 14f;

        internal const float BarHeightUnits = 700f;

        /// <summary>A soft glow around the bar, this many times its width.</summary>
        internal const float GlowWidthMultiplier = 4f;

        private const float GlowAlphaShare = 0.25f;

        /// <summary>In front of the floor and the edge markers, behind the dummies.</summary>
        private const int SortingOrder = -60;

        private const int GlowSortingOrder = -61;

        /// <summary>A wall that springs up grows from the floor over this long (real seconds).</summary>
        private const float SpringUpSeconds = 0.14f;

        private const float WarningPulseHz = 1.6f;
        private const float WarningMinBrightness = 0.65f;
        private const float LivePulseHz = 7f;
        private const float LiveMinBrightness = 0.8f;

        private const int ArcsPerWall = 2;
        private const int ArcPoints = 10;
        private const float ArcJitterUnits = 12f;
        internal const float ArcWidthUnits = 3f;

        /// <summary>A live wall's arcs are redrawn with a new jag this often (real seconds): the crackle.</summary>
        private const float ArcRejagSeconds = 0.05f;

        /// <summary>A warning crackles only now and then: this share of its re-jags shows an arc.</summary>
        private const float WarningCrackleShare = 0.3f;

        public static readonly Color WarningColor = new Color(1f, 0.72f, 0.1f, 0.45f);
        public static readonly Color ActiveColor = new Color(0.35f, 0.9f, 1f, 0.9f);

        private readonly SpriteRenderer[] _bars = new SpriteRenderer[2];
        private readonly SpriteRenderer[] _glows = new SpriteRenderer[2];
        private readonly LineRenderer[,] _arcs = new LineRenderer[2, ArcsPerWall];
        private readonly ElectricWallPose[] _shown = new ElectricWallPose[2];
        private readonly float[] _upSinceSeconds = new float[2];
        private readonly Vec2[] _arcScratch = new Vec2[ArcPoints];
        private ArenaSpace _space;
        private Material _lineMaterial;

        /// <summary>The view under the arena root (so the camera director's flip mirrors it with the dummies); nothing drawn without a sprite.</summary>
        public static ElectricWallView Create(Transform parent, Sprite square, ArenaSpace space)
        {
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<ElectricWallView>();
            view._space = space;
            if (square == null) return view;

            view._lineMaterial = ArenaLines.CreateMaterial();
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                view._glows[(int)side] = CreateBar(root.transform, side + "WallGlow", square, space, BarWidthUnits * GlowWidthMultiplier, GlowSortingOrder);
                view._bars[(int)side] = CreateBar(root.transform, side + "Wall", square, space, BarWidthUnits, SortingOrder);
                for (int i = 0; i < ArcsPerWall; i++)
                {
                    if (view._lineMaterial == null) break;
                    view._arcs[(int)side, i] = ArenaLines.Create(root.transform, side + "WallArc" + i, view._lineMaterial, ActiveColor,
                        space.ToWorldLength(ArcWidthUnits), SortingOrder + 1);
                }
            }

            return view;
        }

        /// <summary>The bar drawing a side's wall; null without a sprite (tests read it).</summary>
        public SpriteRenderer BarOf(Side side) => _bars[(int)side];

        public void Show(PoseSnapshot pose)
        {
            Guard.NotNull(pose, nameof(pose));
            Show(Side.Left, pose.LeftWall);
            Show(Side.Right, pose.RightWall);
        }

        private void Show(Side side, ElectricWallPose wall)
        {
            int index = (int)side;
            SpriteRenderer bar = _bars[index];
            if (bar == null) return;
            if (wall.IsUp && !_shown[index].IsUp) _upSinceSeconds[index] = Time.unscaledTime;
            _shown[index] = wall;

            bar.enabled = wall.IsUp;
            _glows[index].enabled = wall.IsUp;
            if (!wall.IsUp)
            {
                HideArcs(index);
                return;
            }

            bar.color = wall.Phase == ElectricWallPhase.Active ? ActiveColor : WarningColor;
            float centreX = wall.XUnits - side.FacingSign() * BarWidthUnits * 0.5f;
            Vector2 centre = _space.ToWorld(new Vec2(centreX, BarHeightUnits * 0.5f));
            bar.transform.localPosition = centre;
            _glows[index].transform.localPosition = centre;
        }

        /// <summary>Real-time life: the spring up from the floor, the pulse, and a live wall's crackling arcs.</summary>
        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            for (int index = 0; index < _bars.Length; index++)
            {
                SpriteRenderer bar = _bars[index];
                if (bar == null || !bar.enabled) continue;
                ElectricWallPose wall = _shown[index];
                bool live = wall.Phase == ElectricWallPhase.Active;

                float grown = XMath.Clamp01((now - _upSinceSeconds[index]) / SpringUpSeconds);
                float height = BarHeightUnits * grown * grown * (3f - 2f * grown);
                Grow(bar, height, BarWidthUnits);
                Grow(_glows[index], height, BarWidthUnits * GlowWidthMultiplier);

                float wave = 0.5f + 0.5f * Mathf.Sin(now * (live ? LivePulseHz : WarningPulseHz) * 2f * Mathf.PI);
                float brightness = Mathf.Lerp(live ? LiveMinBrightness : WarningMinBrightness, 1f, wave);
                Color color = live ? ActiveColor : WarningColor;
                color.a *= brightness;
                bar.color = color;
                Color glow = color;
                glow.a *= GlowAlphaShare;
                _glows[index].color = glow;

                Crackle(index, wall, live, height, now);
            }
        }

        private void Crackle(int index, ElectricWallPose wall, bool live, float heightUnits, float now)
        {
            int jag = (int)(now / ArcRejagSeconds);
            for (int i = 0; i < ArcsPerWall; i++)
            {
                LineRenderer arc = _arcs[index, i];
                if (arc == null) continue;
                var random = new XorShiftRandom(unchecked((uint)(jag * 31 + index * 7 + i + 1)));
                bool shown = live || (i == 0 && random.NextFloat() < WarningCrackleShare);
                if (!shown || heightUnits <= 0f)
                {
                    arc.positionCount = 0;
                    continue;
                }

                LightningArc.Fill(new Vec2(wall.XUnits, 0f), new Vec2(wall.XUnits, heightUnits), ArcJitterUnits, (uint)random.NextInt(1, int.MaxValue), _arcScratch);
                arc.positionCount = ArcPoints;
                for (int p = 0; p < ArcPoints; p++)
                {
                    arc.SetPosition(p, _space.ToWorld(_arcScratch[p]));
                }

                Color color = live ? ActiveColor : WarningColor;
                arc.startColor = color;
                arc.endColor = color;
            }
        }

        private void HideArcs(int index)
        {
            for (int i = 0; i < ArcsPerWall; i++)
            {
                if (_arcs[index, i] != null) _arcs[index, i].positionCount = 0;
            }
        }

        /// <summary>The bar standing on the floor at this height (it grows from the floor as the wall springs up).</summary>
        private void Grow(SpriteRenderer renderer, float heightUnits, float widthUnits)
        {
            Transform bar = renderer.transform;
            Vector3 position = bar.localPosition;
            position.y = _space.ToWorldLength(heightUnits * 0.5f);
            bar.localPosition = position;
            bar.localScale = new Vector3(_space.ToWorldLength(widthUnits), _space.ToWorldLength(heightUnits), 1f);
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        private static SpriteRenderer CreateBar(Transform parent, string name, Sprite square, ArenaSpace space, float widthUnits, int sortingOrder)
        {
            var bar = new GameObject(name);
            bar.transform.SetParent(parent, false);
            bar.transform.localScale = new Vector3(space.ToWorldLength(widthUnits), space.ToWorldLength(BarHeightUnits), 1f);
            var renderer = bar.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            return renderer;
        }
    }
}
