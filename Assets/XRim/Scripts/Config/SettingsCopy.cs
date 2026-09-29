using UnityEngine;

namespace XRim.Config
{
    public static class SettingsCopy
    {
        /// <summary>Deep copy through Unity's serializer, so the copy has exactly the fields an asset saves.</summary>
        public static T Clone<T>(T source) where T : class => JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
    }
}
