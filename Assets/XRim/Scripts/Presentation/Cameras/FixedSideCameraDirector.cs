using System;
using UnityEngine;
using XRim.Rules.Events;

namespace XRim.Presentation.Cameras
{
    /// <summary>Fixed side view with one fighter at each end of the screen (GDD §1, §2; assumed, TBD).</summary>
    public sealed class FixedSideCameraDirector : MonoBehaviour, ICameraDirector
    {
        private const float MirroredScaleX = -1f;
        private const float NormalScaleX = 1f;

        [Tooltip("Parent of every arena visual. Flipping its X scale mirrors the view without touching the simulation.")]
        [SerializeField] private Transform _arenaRoot;

        public void SetArenaFlipped(bool flipped)
        {
            if (_arenaRoot == null) return;
            Vector3 scale = _arenaRoot.localScale;
            scale.x = flipped ? MirroredScaleX : NormalScaleX;
            _arenaRoot.localScale = scale;
        }

        public void OnMatchEvent(MatchEvent matchEvent)
        {
            // Placeholder: architecture setup only. Camera reactions come later.
            throw new NotImplementedException("FixedSideCameraDirector.OnMatchEvent is not implemented yet.");
        }

        /// <summary>Serialized field names, for Editor tools that assign references through SerializedObject.</summary>
        public static class FieldNames
        {
            public const string ArenaRoot = nameof(_arenaRoot);
        }
    }
}
