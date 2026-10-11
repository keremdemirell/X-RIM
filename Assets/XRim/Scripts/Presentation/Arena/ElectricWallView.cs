using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules.Arena;
using XRim.Simulation.Physics;

namespace XRim.Presentation.Arena
{
    /// <summary>
    /// Placeholder drawing of each player's electric wall (GDD §13; art arrives in Session 13 or 17): a tall bar whose face stands where the
    /// pose puts it, behind its owner. A warning is a pale amber bar, a live wall a bright cyan one; no wall, nothing. It follows every pose
    /// playback shows (<c>TurnPlayback.PoseShown</c>), the frozen board included, so a wall standing during planning is always in view.
    /// Drawing only: it never changes a turn.
    /// </summary>
    public sealed class ElectricWallView : MonoBehaviour
    {
        private const string RootName = "ElectricWalls";

        /// <summary>The bar's thickness and height on screen (technical: how the placeholder looks, not where the wall stands).</summary>
        private const float BarWidthUnits = 14f;

        private const float BarHeightUnits = 700f;

        /// <summary>In front of the floor and the edge markers, behind the dummies.</summary>
        private const int SortingOrder = -60;

        public static readonly Color WarningColor = new Color(1f, 0.72f, 0.1f, 0.45f);
        public static readonly Color ActiveColor = new Color(0.35f, 0.9f, 1f, 0.9f);

        private readonly SpriteRenderer[] _bars = new SpriteRenderer[2];
        private ArenaSpace _space;

        /// <summary>The view under the arena root (so the camera director's flip mirrors it with the dummies); nothing drawn without a sprite.</summary>
        public static ElectricWallView Create(Transform parent, Sprite square, ArenaSpace space)
        {
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<ElectricWallView>();
            view._space = space;
            if (square == null) return view;

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                var bar = new GameObject(side + "Wall");
                bar.transform.SetParent(root.transform, false);
                bar.transform.localScale = new Vector3(space.ToWorldLength(BarWidthUnits), space.ToWorldLength(BarHeightUnits), 1f);
                var renderer = bar.AddComponent<SpriteRenderer>();
                renderer.sprite = square;
                renderer.sortingOrder = SortingOrder;
                renderer.enabled = false;
                view._bars[(int)side] = renderer;
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
            SpriteRenderer bar = _bars[(int)side];
            if (bar == null) return;
            bar.enabled = wall.IsUp;
            if (!wall.IsUp) return;

            bar.color = wall.Phase == ElectricWallPhase.Active ? ActiveColor : WarningColor;
            float centreX = wall.XUnits - side.FacingSign() * BarWidthUnits * 0.5f;
            bar.transform.localPosition = _space.ToWorld(new Vec2(centreX, BarHeightUnits * 0.5f));
        }
    }
}
