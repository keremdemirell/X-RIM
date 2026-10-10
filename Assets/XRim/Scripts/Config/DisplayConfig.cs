using UnityEngine;
using XRim.Core.Gdd;

namespace XRim.Config
{
    /// <summary>
    /// How the client draws, on this device. Nothing here may change an outcome: turns are simulated on their own fixed 240 Hz steps
    /// and played back in real time, so the display's refresh rate and the frame rate never matter (GDD §4 input fairness, §18).
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Client + "Display", fileName = "Display")]
    public sealed class DisplayConfig : ScriptableObject
    {
        /// <summary>The frame rate target value that leaves the platform's own default (usually 30 on a phone).</summary>
        public const int PlatformDefaultFrameRate = -1;

        [Tooltip("Frames per second the game aims for (Application.targetFrameRate; vertical sync is switched off so it applies). " +
                 "-1 leaves the platform's own default, which is usually 30 on a phone. A 90 or 120 Hz phone can be tried by raising it. " +
                 "Never changes an outcome.")]
        [GddTbd("§18", "Target devices and performance budget")]
        [Placeholder("§18 target devices and frame rate are TBD; 60 suits every phone screen (the test phones run 60 Hz, one also 90 Hz)")]
        [Range(PlatformDefaultFrameRate, 240)]
        [SerializeField] private int _targetFrameRate = 60;

        public int TargetFrameRate => _targetFrameRate;
    }
}
