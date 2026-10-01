using UnityEngine;

namespace XRim.DebugTools
{
    /// <summary>Helpers for debug tools that edit tuning assets live.</summary>
    internal static class DebugAssets
    {
        /// <summary>In the Editor, marks an edited asset so the change is saved and survives leaving Play mode.</summary>
        public static void MarkDirty(ScriptableObject asset)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(asset);
#endif
        }
    }
}
