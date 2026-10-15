using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRim.Config
{
    /// <summary>The clips of one cue in a sound set: one is picked at random each time it plays, at this volume.</summary>
    [Serializable]
    public sealed class SoundCueClips
    {
        public SoundCue Cue;
        public List<AudioClip> Clips = new List<AudioClip>();

        [Range(0f, 1f)]
        public float Volume = 1f;

        public bool HasClips
        {
            get
            {
                foreach (AudioClip clip in Clips)
                {
                    if (clip != null) return true;
                }

                return false;
            }
        }
    }
}
