using System;
using UnityEngine;
using XRim.Profile;

namespace XRim.Config
{
    /// <summary>
    /// The saved profile as JSON, through Unity's JsonUtility over <see cref="ProfileData"/> (Session 18). It lives here because the
    /// engine-free <c>XRim.Profile</c> cannot use UnityEngine; the profile code only sees <see cref="IProfileTextCodec"/>. Fields the file
    /// lacks keep the values <see cref="ProfileData"/> starts with, and fields this game does not know are ignored, so a file written by an
    /// earlier or a later game still reads.
    /// </summary>
    public sealed class ProfileJson : IProfileTextCodec
    {
        public string ToText(ProfileData data) => JsonUtility.ToJson(data ?? throw new ArgumentNullException(nameof(data)), true);

        public ProfileData FromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("the file is empty");
            try
            {
                ProfileData data = JsonUtility.FromJson<ProfileData>(text);
                if (data == null) throw new FormatException("the file holds no profile");
                return data;
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("the file is not valid JSON (" + exception.Message + ")", exception);
            }
        }
    }
}
