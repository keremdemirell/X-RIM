using System;
using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules.Events;
using XRim.Rules.Match;

namespace XRim.Presentation.Audio
{
    /// <summary>
    /// The duel's sound (GDD §2, Decided; Session 13): metal clashes, impacts, joint snaps, bolt scatter, wall zaps and comic effort
    /// grunts on attacks and impacts, as <see cref="SoundCueMapper"/> says for each played event, and one tense music loop.
    /// <list type="bullet">
    /// <item>A cue without a clip plays nothing and is only counted (<see cref="SilentCount"/>), never an error, so the game is whole
    /// before any sound exists.</item>
    /// <item>Sets are swappable (grunt packs and hit effects are cosmetics, §16): each dummy can have its own effects set and grunt pack;
    /// the Audio asset's are the default.</item>
    /// <item>Effects share a pool of sources; each dummy speaks one grunt at a time, a more important one (a KO scream) cutting off a
    /// lesser one; the same cue for the same dummy does not repeat within a few hundredths of a second; pitch varies a little, and in
    /// slow motion sounds play deeper.</item>
    /// <item>Adapting the music to the board is TBD: it goes through <see cref="IMusicIntensityDriver"/> (a steady loop by default).</item>
    /// </list>
    /// Presentation only: it never changes a turn.
    /// </summary>
    public sealed class MatchAudio : MonoBehaviour, IMatchEventReactor
    {
        private const string EffectSourceName = "SoundEffect";
        private const string VoiceSourceName = "SoundVoice";
        private const string MusicSourceName = "SoundMusic";
        private const int SideCount = 2;

        private readonly List<SoundRequest> _requests = new List<SoundRequest>();
        private readonly Dictionary<long, float> _lastPlayedRealSeconds = new Dictionary<long, float>();
        private readonly SoundSetDefinition[] _effectSets = new SoundSetDefinition[SideCount];
        private readonly SoundSetDefinition[] _gruntPacks = new SoundSetDefinition[SideCount];
        private readonly AudioSource[] _voices = new AudioSource[SideCount];
        private readonly SoundCue[] _speaking = new SoundCue[SideCount];
        private AudioSource[] _effects = Array.Empty<AudioSource>();
        private AudioSource _music;
        private AudioConfig _audio;
        private FeelConfig _feel;
        private Func<float> _playbackSpeed;
        private IMusicIntensityDriver _musicIntensity;
        private XorShiftRandom _random;
        private int _nextEffect;
        private float _playerMaster = PlayerVolumeAsMixed;
        private float _playerMusic = PlayerVolumeAsMixed;
        private float _playerEffects = PlayerVolumeAsMixed;
        private float _playerVoices = PlayerVolumeAsMixed;

        /// <summary>The player's sliders at their top: the Audio asset's mix exactly as the designer set it.</summary>
        public const float PlayerVolumeAsMixed = 1f;

        /// <summary>How many sounds have played (tests and debug tools read it).</summary>
        public int PlayedCount { get; private set; }

        /// <summary>How many sounds were asked for that have no clip yet: they played nothing.</summary>
        public int SilentCount { get; private set; }

        /// <summary>The last clip played (tests read it).</summary>
        public AudioClip LastPlayedClip { get; private set; }

        public bool IsMusicPlaying => _music != null && _music.isPlaying;

        /// <param name="playbackSpeed">The slow motion's playback speed now (1 = real time): sounds deepen with it.</param>
        public void Configure(AudioConfig audio, FeelConfig feel, Func<float> playbackSpeed, IMusicIntensityDriver musicIntensity = null)
        {
            _audio = Guard.NotNull(audio, nameof(audio));
            _feel = Guard.NotNull(feel, nameof(feel));
            _playbackSpeed = playbackSpeed;
            _musicIntensity = musicIntensity ?? new SteadyMusicIntensity();
            _random = new XorShiftRandom((uint)Environment.TickCount | 1u);
            CreateSources();
        }

        /// <summary>
        /// The player's volume sliders (Settings, Session 18), each 0 to 1 of the Audio asset's mix, so they never fight the designer's balance
        /// between music, effects and voices. Applies from the next sound and from the next time the music starts.
        /// </summary>
        public void SetPlayerVolumes(float master, float music, float effects, float voices)
        {
            _playerMaster = Mathf.Clamp01(master);
            _playerMusic = Mathf.Clamp01(music);
            _playerEffects = Mathf.Clamp01(effects);
            _playerVoices = Mathf.Clamp01(voices);
            if (_music != null && _audio != null) _music.volume = _audio.MusicVolume * _audio.MasterVolume * _playerMusic * _playerMaster;
        }

        /// <summary>A dummy's effects set (a cosmetic, GDD §16); null goes back to the Audio asset's.</summary>
        public void SetEffectsSet(Side side, SoundSetDefinition set) => _effectSets[(int)side] = set;

        /// <summary>A dummy's grunt pack (a cosmetic, GDD §16); null goes back to the Audio asset's.</summary>
        public void SetGruntPack(Side side, SoundSetDefinition pack) => _gruntPacks[(int)side] = pack;

        public void OnMatchEvent(MatchEvent matchEvent)
        {
            if (_audio == null || matchEvent == null) return;
            _requests.Clear();
            SoundCueMapper.Add(matchEvent, SoundMappingSettings.From(_audio, _feel), _requests);
            foreach (SoundRequest request in _requests)
            {
                Play(request);
            }
        }

        /// <summary>The music loop plays during a match (it stops at the end, so the finishing blow rings out alone).</summary>
        public void SetMusicPlaying(bool playing)
        {
            if (_music == null) return;
            bool wanted = playing && _audio.PlayMusic && _audio.Music != null;
            if (!wanted)
            {
                if (_music.isPlaying) _music.Stop();
                return;
            }

            _music.volume = _audio.MusicVolume * _audio.MasterVolume * _playerMusic * _playerMaster;
            if (_music.clip != _audio.Music) _music.clip = _audio.Music;
            if (!_music.isPlaying) _music.Play();
        }

        /// <summary>A board is shown (a turn's start): the adaptive-music seam hears it (TBD: a steady loop by default).</summary>
        public void OnBoardShown(MatchState state)
        {
            if (state != null) _musicIntensity?.OnBoardChanged(state);
        }

        private void Play(SoundRequest request)
        {
            bool voice = request.IsVoice;
            SoundSetDefinition set = voice ? PackOf(request.Side) : EffectsOf(request.Side);
            if (set == null || !set.TryGetClips(request.Cue, out SoundCueClips clips))
            {
                SilentCount++;
                return;
            }

            float now = Time.unscaledTime;
            long key = (long)request.Cue * SideCount + (request.Side.HasValue ? (int)request.Side.Value : SideCount) + 1;
            if (_lastPlayedRealSeconds.TryGetValue(key, out float last) && now - last < _audio.RepeatCooldownSeconds) return;

            AudioSource source = voice ? VoiceSource(request) : NextEffectSource();
            if (source == null) return;

            AudioClip clip = Pick(clips);
            if (clip == null)
            {
                SilentCount++;
                return;
            }

            _lastPlayedRealSeconds[key] = now;
            float category = voice ? _audio.VoiceVolume * _playerVoices : _audio.EffectsVolume * _playerEffects;
            source.clip = clip;
            source.volume = Mathf.Clamp01(request.Volume * clips.Volume * category * _audio.MasterVolume * _playerMaster);
            source.pitch = Pitch();
            source.Play();
            PlayedCount++;
            LastPlayedClip = clip;
        }

        /// <summary>The dummy's own voice, if its new grunt may cut off what it is saying; null when it must not.</summary>
        private AudioSource VoiceSource(SoundRequest request)
        {
            int side = request.Side.HasValue ? (int)request.Side.Value : 0;
            AudioSource source = _voices[side];
            if (source == null || !SoundCues.VoiceMayInterrupt(source.isPlaying, _speaking[side], request.Cue)) return null;
            _speaking[side] = request.Cue;
            return source;
        }

        /// <summary>An idle effect source, or the one that started longest ago.</summary>
        private AudioSource NextEffectSource()
        {
            if (_effects.Length == 0) return null;
            for (int i = 0; i < _effects.Length; i++)
            {
                AudioSource candidate = _effects[(_nextEffect + i) % _effects.Length];
                if (candidate.isPlaying) continue;
                _nextEffect = (_nextEffect + i + 1) % _effects.Length;
                return candidate;
            }

            AudioSource oldest = _effects[_nextEffect];
            _nextEffect = (_nextEffect + 1) % _effects.Length;
            return oldest;
        }

        private AudioClip Pick(SoundCueClips clips)
        {
            int count = clips.Clips.Count;
            int start = _random.NextInt(0, count);
            for (int i = 0; i < count; i++)
            {
                AudioClip clip = clips.Clips[(start + i) % count];
                if (clip != null) return clip;
            }

            return null;
        }

        /// <summary>A little random variation, and deeper in slow motion.</summary>
        private float Pitch()
        {
            float jitter = (_random.NextFloat() * 2f - 1f) * _audio.PitchJitter;
            float speed = _playbackSpeed != null ? Mathf.Clamp01(_playbackSpeed()) : 1f;
            float slowed = Mathf.Lerp(1f, speed, _audio.SlowMotionPitchShare);
            return Mathf.Max(_audio.MinPitch, (1f + jitter) * slowed);
        }

        private SoundSetDefinition EffectsOf(Side? side)
        {
            SoundSetDefinition own = side.HasValue ? _effectSets[(int)side.Value] : null;
            return own != null ? own : _audio.Effects;
        }

        private SoundSetDefinition PackOf(Side? side)
        {
            SoundSetDefinition own = side.HasValue ? _gruntPacks[(int)side.Value] : null;
            return own != null ? own : _audio.Grunts;
        }

        private void CreateSources()
        {
            if (_effects.Length > 0) return;
            int count = Math.Max(1, _audio.MaxEffectVoices);
            _effects = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                _effects[i] = CreateSource(EffectSourceName + i, false);
            }

            for (int side = 0; side < SideCount; side++)
            {
                _voices[side] = CreateSource(VoiceSourceName + (Side)side, false);
            }

            _music = CreateSource(MusicSourceName, true);
        }

        private AudioSource CreateSource(string name, bool loop)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(transform, false);
            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f; // 2D: the side view is heard the same everywhere
            return source;
        }
    }
}
