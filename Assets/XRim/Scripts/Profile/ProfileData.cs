using System;
using System.Collections.Generic;

namespace XRim.Profile
{
    /// <summary>
    /// The shape of the saved profile file (<see cref="ProfileFormat.CurrentVersion"/>). Plain public fields, so any serializer can write it
    /// (Config's <c>ProfileJson</c> uses Unity's JsonUtility). Kept apart from <see cref="PlayerProfile"/> on purpose: the file's shape only
    /// changes together with a version number and an <see cref="IProfileUpgrade"/>, while the profile in memory can be refactored freely.
    /// Enums are stored as their numbers.
    /// </summary>
    [Serializable]
    public sealed class ProfileData
    {
        /// <summary>0 when the file has no version (it was not written by this game).</summary>
        public int Version = 0;

        public bool HandednessChosen = false;

        /// <summary>A <c>Handedness</c> number; only meaningful when <see cref="HandednessChosen"/>.</summary>
        public int Handedness = 0;

        public float VolumeMaster = AudioVolumes.AsMixed;
        public float VolumeMusic = AudioVolumes.AsMixed;
        public float VolumeEffects = AudioVolumes.AsMixed;
        public float VolumeVoices = AudioVolumes.AsMixed;

        public long Earned = 0;
        public long Premium = 0;

        public List<ProfileUnlockData> Unlocks = new List<ProfileUnlockData>();
        public List<ProfileCosmeticData> Cosmetics = new List<ProfileCosmeticData>();
        public List<ProfileEquippedData> Equipped = new List<ProfileEquippedData>();
    }
}
