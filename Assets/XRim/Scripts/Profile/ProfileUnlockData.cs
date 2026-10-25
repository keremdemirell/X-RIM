using System;

namespace XRim.Profile
{
    /// <summary>One owned weapon, shield or signature move in the saved file.</summary>
    [Serializable]
    public sealed class ProfileUnlockData
    {
        /// <summary>A <c>GameplayUnlockKind</c> number.</summary>
        public int Kind = 0;

        public string Id = string.Empty;

        public ProfileUnlockData()
        {
        }

        public ProfileUnlockData(int kind, string id)
        {
            Kind = kind;
            Id = id;
        }
    }
}
