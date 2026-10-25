using System;

namespace XRim.Profile
{
    /// <summary>The cosmetic equipped for one kind, in the saved file.</summary>
    [Serializable]
    public sealed class ProfileEquippedData
    {
        /// <summary>A <c>CosmeticKind</c> number.</summary>
        public int Kind = 0;

        public string Id = string.Empty;

        public ProfileEquippedData()
        {
        }

        public ProfileEquippedData(int kind, string id)
        {
            Kind = kind;
            Id = id;
        }
    }
}
