using System;

namespace XRim.Profile
{
    /// <summary>
    /// Writes <see cref="ProfileData"/> as text and reads it back. The game's one is Config's <c>ProfileJson</c> (Unity's JsonUtility); the
    /// engine-free Profile code only knows this interface, so it can be tested without Unity.
    /// </summary>
    public interface IProfileTextCodec
    {
        string ToText(ProfileData data);

        /// <exception cref="FormatException">The text is empty, damaged or not a profile.</exception>
        ProfileData FromText(string text);
    }
}
