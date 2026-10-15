using System.Collections.Generic;
using UnityEngine;

namespace XRim.Config
{
    /// <summary>
    /// A swappable set of sounds (GDD §16: grunt packs and hit and impact effects are cosmetics): an effects set (clashes, impacts, snaps,
    /// zaps, whooshes) or a grunt pack (the <c>Grunt…</c> voices). Each dummy can have its own. A cue the set has no clip for plays
    /// nothing, never an error. Built from the files in <c>Assets/XRim/Audio/Incoming/</c> by XRim → Setup → Build Audio Sets From
    /// Incoming, or filled by hand in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Client + "Sound Set", fileName = "SoundSet")]
    public sealed class SoundSetDefinition : ScriptableObject
    {
        [Tooltip("What the set is shown as (a cosmetic's name later).")]
        [SerializeField] private string _displayName = "Default";

        [SerializeField] private List<SoundCueClips> _cues = new List<SoundCueClips>();

        public string DisplayName => _displayName;

        public IReadOnlyList<SoundCueClips> Cues => _cues;

        /// <summary>The cue's clips, when the set has at least one for it.</summary>
        public bool TryGetClips(SoundCue cue, out SoundCueClips clips)
        {
            foreach (SoundCueClips entry in _cues)
            {
                if (entry == null || entry.Cue != cue || !entry.HasClips) continue;
                clips = entry;
                return true;
            }

            clips = null;
            return false;
        }

        /// <summary>Editor tools and tests: replaces every cue's clips (a cue keeps the volume it had).</summary>
        public void SetCues(IEnumerable<SoundCueClips> cues)
        {
            var volumes = new Dictionary<SoundCue, float>();
            foreach (SoundCueClips entry in _cues)
            {
                if (entry != null) volumes[entry.Cue] = entry.Volume;
            }

            _cues = new List<SoundCueClips>();
            foreach (SoundCueClips entry in cues)
            {
                if (entry == null) continue;
                if (volumes.TryGetValue(entry.Cue, out float volume)) entry.Volume = volume;
                _cues.Add(entry);
            }
        }
    }
}
