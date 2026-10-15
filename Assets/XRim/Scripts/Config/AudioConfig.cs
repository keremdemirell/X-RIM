using UnityEngine;
using XRim.Core.Gdd;

namespace XRim.Config
{
    /// <summary>
    /// The duel's sound (GDD §2, Decided: tense duel music, metal clashes, impacts, joint snaps, bolt scatter, wall zaps and comic effort
    /// grunts). Client only: it never changes an outcome. The default effects set and grunt pack (each dummy can be given its own later,
    /// §16), the music loop, the volumes (the player's settings, Session 18), and how sounds behave. Without clips everything is silent.
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Client + "Audio", fileName = "Audio")]
    public sealed class AudioConfig : ScriptableObject
    {
        private const string AudioPlaceholder = "Session 13 audio: no GDD value, set by ear at PT5";

        [Header("Sounds")]
        [Tooltip("The effects every dummy uses until it has its own (clashes, impacts, snaps, zaps, whooshes).")]
        [SerializeField] private SoundSetDefinition _effects;

        [Tooltip("The grunt pack every dummy uses until it has its own (a cosmetic, GDD §16).")]
        [SerializeField] private SoundSetDefinition _grunts;

        [Tooltip("The tense duel loop (GDD §2). Adapting it to the board is TBD: it plays steadily.")]
        [SerializeField] private AudioClip _music;

        [SerializeField] private bool _playMusic = true;

        [Header("Volumes")]
        [Placeholder(AudioPlaceholder)]
        [Range(0f, 1f)]
        [SerializeField] private float _masterVolume = 1f;

        [Placeholder(AudioPlaceholder)]
        [Range(0f, 1f)]
        [SerializeField] private float _effectsVolume = 0.9f;

        [Placeholder(AudioPlaceholder)]
        [Range(0f, 1f)]
        [SerializeField] private float _voiceVolume = 0.85f;

        [Placeholder(AudioPlaceholder)]
        [Range(0f, 1f)]
        [SerializeField] private float _musicVolume = 0.45f;

        [Header("Behaviour")]
        [Tooltip("How many effects can sound at once (each dummy also has its own voice).")]
        [Placeholder(AudioPlaceholder)]
        [SerializeField] private int _maxEffectVoices = 16;

        [Tooltip("The same sound for the same dummy is not played again within this many real seconds (no machine-gun repeats).")]
        [Placeholder(AudioPlaceholder)]
        [SerializeField] private float _repeatCooldownSeconds = 0.04f;

        [Tooltip("Each sound's pitch varies by up to this share either way, so repeats do not sound identical.")]
        [Placeholder(AudioPlaceholder)]
        [Range(0f, 0.5f)]
        [SerializeField] private float _pitchJitter = 0.06f;

        [Tooltip("In slow motion sounds play deeper: their pitch moves this share of the way toward the playback speed.")]
        [Placeholder(AudioPlaceholder)]
        [Range(0f, 1f)]
        [SerializeField] private float _slowMotionPitchShare = 0.5f;

        [Placeholder(AudioPlaceholder)]
        [Range(0.1f, 1f)]
        [SerializeField] private float _minPitch = 0.5f;

        [Tooltip("A hit this strong (damage) plays at full volume; lighter hits are quieter, down to the least below.")]
        [Placeholder(AudioPlaceholder)]
        [SerializeField] private float _fullVolumeDamage = 25f;

        [Placeholder(AudioPlaceholder)]
        [Range(0f, 1f)]
        [SerializeField] private float _minHitVolume = 0.55f;

        public SoundSetDefinition Effects => _effects;
        public SoundSetDefinition Grunts => _grunts;
        public AudioClip Music => _music;
        public bool PlayMusic => _playMusic;
        public float MasterVolume => _masterVolume;
        public float EffectsVolume => _effectsVolume;
        public float VoiceVolume => _voiceVolume;
        public float MusicVolume => _musicVolume;
        public int MaxEffectVoices => _maxEffectVoices;
        public float RepeatCooldownSeconds => _repeatCooldownSeconds;
        public float PitchJitter => _pitchJitter;
        public float SlowMotionPitchShare => _slowMotionPitchShare;
        public float MinPitch => _minPitch;
        public float FullVolumeDamage => _fullVolumeDamage;
        public float MinHitVolume => _minHitVolume;

        /// <summary>Editor tools and tests: the sets and the music (the audio builder makes them from the incoming files).</summary>
        public void SetSounds(SoundSetDefinition effects, SoundSetDefinition grunts, AudioClip music)
        {
            _effects = effects;
            _grunts = grunts;
            _music = music;
        }
    }
}
