using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Audio;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The duel's sound in play (Session 13): a sound without a clip is silent, never an error; a set with a clip plays it; each dummy's
    /// own grunt pack (a cosmetic, GDD §16) is used for its voice; music needs a clip.
    /// </summary>
    public sealed class MatchAudioTests
    {
        private static readonly SimTime Time = SimTime.FromSeconds(0.3);
        private readonly List<Object> _created = new List<Object>();
        private AudioConfig _audio;
        private MatchAudio _player;

        [SetUp]
        public void SetUp()
        {
            _audio = Track(ScriptableObject.CreateInstance<AudioConfig>());
            FeelConfig feel = Track(ScriptableObject.CreateInstance<FeelConfig>());
            _player = Track(new GameObject("Audio")).AddComponent<MatchAudio>();
            _player.Configure(_audio, feel, () => 1f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void WithoutAnySound_EveryEventIsSilent_AndNothingBreaks()
        {
            foreach (MatchEvent matchEvent in EveryKindOfEvent())
            {
                Assert.DoesNotThrow(() => _player.OnMatchEvent(matchEvent), matchEvent.GetType().Name);
            }

            Assert.That(_player.PlayedCount, Is.EqualTo(0));
            Assert.That(_player.SilentCount, Is.GreaterThan(10), "every asked-for sound is counted as silent");
            _player.SetMusicPlaying(true);
            Assert.That(_player.IsMusicPlaying, Is.False, "no music clip, no music");
        }

        [Test]
        public void ASetWithAClip_PlaysIt_AndCuesItLacksStaySilent()
        {
            AudioClip clang = Clip("clang");
            _audio.SetSounds(Set(SoundCue.ClashRebound, clang), null, null);

            _player.OnMatchEvent(new WeaponClashEvent(Time, new ClashResult(ClashKind.BothRebound, null, 70f, 5f, 5f), Vec2.Zero));
            _player.OnMatchEvent(new FighterDiedEvent(Time, Side.Left));

            Assert.That(_player.PlayedCount, Is.EqualTo(1));
            Assert.That(_player.LastPlayedClip, Is.SameAs(clang));
            Assert.That(_player.SilentCount, Is.EqualTo(3), "the death's split, bolts and scream have no clips");
        }

        [Test]
        public void EachDummy_GruntsFromItsOwnPack()
        {
            AudioClip shared = Clip("shared");
            AudioClip rightOwn = Clip("right");
            _audio.SetSounds(null, Set(SoundCue.GruntKnockOut, shared), null);
            _player.SetGruntPack(Side.Right, Set(SoundCue.GruntKnockOut, rightOwn));

            _player.OnMatchEvent(new FighterDiedEvent(Time, Side.Right));
            Assert.That(_player.LastPlayedClip, Is.SameAs(rightOwn));

            _player.OnMatchEvent(new FighterDiedEvent(Time, Side.Left));
            Assert.That(_player.LastPlayedClip, Is.SameAs(shared), "the left has no pack of its own");
        }

        private IEnumerable<MatchEvent> EveryKindOfEvent()
        {
            yield return new AttackStartedEvent(SimTime.Zero, Side.Left, WeaponIds.Rapier, WeaponKind.Weapon);
            yield return new BodyMoveStartedEvent(SimTime.Zero, Side.Right, BodyMove.Lunge);
            yield return new HitLandedEvent(Time, new HitFacts(Side.Left, BodyPart.Head, WeaponIds.Rapier, Time, false),
                new DamageResult(20f, 20f, 0f, true, false));
            yield return new WeaponClashEvent(Time, new ClashResult(ClashKind.CrushThrough, Side.Left, 70f, 15f, 5f), Vec2.Zero);
            yield return new ShieldBlockEvent(Time, Side.Right, new BlockResult(true, 0f, false), Vec2.Zero, 80f, 0f);
            yield return new LimbSeveredEvent(Time, Side.Right, BodyPart.RightArm);
            yield return new WeaponDroppedEvent(Time, Side.Right, WeaponIds.Rapier);
            yield return new ElectricWallEvent(Time, Side.Left, ElectricWallPhase.Active, 10f, -450f);
            yield return new FighterDiedEvent(Time, Side.Right);
        }

        private AudioClip Clip(string name)
        {
            AudioClip clip = AudioClip.Create(name, 441, 1, 44100, false);
            return Track(clip);
        }

        private SoundSetDefinition Set(SoundCue cue, AudioClip clip)
        {
            SoundSetDefinition set = Track(ScriptableObject.CreateInstance<SoundSetDefinition>());
            set.SetCues(new[] { new SoundCueClips { Cue = cue, Clips = new List<AudioClip> { clip } } });
            return set;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
