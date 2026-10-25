using System;

namespace XRim.Profile
{
    /// <summary>One owned cosmetic in the saved file.</summary>
    [Serializable]
    public sealed class ProfileCosmeticData
    {
        /// <summary>A <c>CosmeticKind</c> number.</summary>
        public int Kind = 0;

        public string Id = string.Empty;

        public ProfileCosmeticData()
        {
        }

        public ProfileCosmeticData(int kind, string id)
        {
            Kind = kind;
            Id = id;
        }
    }
}
