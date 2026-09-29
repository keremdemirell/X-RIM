using UnityEngine;

namespace XRim.DebugTools
{
    /// <summary>
    /// Scene-view and in-game debug drawing. Planned: drawn vs resampled path with ink used, rigidity-invalid segments
    /// in red, hit zones colour-coded by zone, contact points with normals and clash angles, time-to-impact labels.
    /// Placeholder: architecture setup only.
    /// </summary>
    public sealed class GameplayGizmos : MonoBehaviour
    {
        [SerializeField] private bool _showPaths = true;
        [SerializeField] private bool _showHitZones = true;
        [SerializeField] private bool _showContacts = true;

        public bool ShowPaths => _showPaths;
        public bool ShowHitZones => _showHitZones;
        public bool ShowContacts => _showContacts;
    }
}
