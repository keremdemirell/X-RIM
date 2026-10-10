using XRim.Config;

namespace XRim.Tests.PlayMode.App
{
    /// <summary>
    /// The project's own tuning profile (XRim/Setup/Create Default Tuning Assets), so match-level tests play with the values the Match
    /// scene uses. Only the Editor can load it by path; elsewhere it is null and the tests that need it are ignored.
    /// </summary>
    internal static class ProjectTuning
    {
        private const string ProfilePath = "Assets/XRim/Data/Tuning/TuningProfile.asset";

        public static TuningProfile Load()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<TuningProfile>(ProfilePath);
#else
            return null;
#endif
        }
    }
}
