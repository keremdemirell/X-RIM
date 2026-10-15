using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Audio;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Status;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// Which sounds each event makes (Session 13; GDD §2, Decided: clashes, impacts, joint snaps, bolt scatter, wall zaps, and comic effort
    /// grunts on attacks and impacts), how sound files are named, and which grunt may cut off another.
    /// </summary>
    public sealed class SoundMappingTests
    {
        private static readonly SimTime Time = SimTime.FromSeconds(0.3);
        private readonly List<SoundRequest> _requests = new List<SoundRequest>();

        private static SoundMappingSettings Settings() => new SoundMappingSettings { HeavyHitMinDamage = 15f, FullVolumeDamage = 25f, MinHitVolume = 0.5f };

        [SetUp]
        public void SetUp() => _requests.Clear();

        // --- Events to sounds ---------------------------------------------------------------------

        [Test]
        public void ASwing_Whooshes_AndTheAttackerGrunts_ButNotForAShield()
        {
            Add(new AttackStartedEvent(SimTime.Zero, Side.Left, WeaponIds.Mace, WeaponKind.Weapon));
            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.SwingWhoosh, SoundCue.GruntAttack }));
            Assert.That(_requests.All(request => request.Side == Side.Left), Is.True);

            SetUp();
            Add(new AttackStartedEvent(SimTime.Zero, Side.Right, WeaponIds.Shield, WeaponKind.Shield));
            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.SwingWhoosh }));
        }

        [Test]
        public void ABodyMove_Sounds_AndALungeOrJumpTakesEffort()
        {
            Add(new BodyMoveStartedEvent(SimTime.Zero, Side.Left, BodyMove.Jump));
            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.BodyMove, SoundCue.GruntEffort }));

            SetUp();
            Add(new BodyMoveStartedEvent(SimTime.Zero, Side.Left, BodyMove.Crouch));
            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.BodyMove }));
        }

        [Test]
        public void AHit_Impacts_InTheAttackersSet_AndTheVictimGrunts()
        {
            Add(Hit(BodyPart.Torso, 8f));

            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.HitLight, SoundCue.GruntHurt }));
            Assert.That(_requests[0].Side, Is.EqualTo(Side.Left), "the impact comes from the attacker's effects set");
            Assert.That(_requests[1].Side, Is.EqualTo(Side.Right), "the victim grunts");
            Assert.That(_requests[1].IsVoice, Is.True);
        }

        [Test]
        public void AHeavyHit_ThudsLouder_AndTheVictimCriesOut_AHeadHitClangs()
        {
            Add(Hit(BodyPart.Torso, 20f));
            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.HitHeavy, SoundCue.GruntBigHurt }));
            Assert.That(_requests[0].Volume, Is.EqualTo(0.9f).Within(1e-4f), "half volume plus half of 20/25");

            SetUp();
            Add(Hit(BodyPart.Head, 8f));
            Assert.That(_requests[0].Cue, Is.EqualTo(SoundCue.HitHead));
        }

        [Test]
        public void HitVolume_GrowsWithDamage_UpToFull()
        {
            Assert.That(SoundCueMapper.HitVolume(0f, Settings()), Is.EqualTo(0.5f));
            Assert.That(SoundCueMapper.HitVolume(25f, Settings()), Is.EqualTo(1f));
            Assert.That(SoundCueMapper.HitVolume(100f, Settings()), Is.EqualTo(1f));
        }

        [Test]
        public void ClashesAndBlocks_SoundByWhatHappened()
        {
            Add(Clash(ClashKind.BothRebound, null));
            Add(Clash(ClashKind.CrushThrough, Side.Left));
            Add(Clash(ClashKind.BothSlidePast, null));
            Add(Block(true));
            Add(Block(false));

            Assert.That(Cues(), Is.EqualTo(new[]
            {
                SoundCue.ClashRebound, SoundCue.ClashCrush, SoundCue.ClashSlide, SoundCue.BlockFull, SoundCue.BlockPartial,
            }));
            Assert.That(_requests[1].Side, Is.EqualTo(Side.Left), "the crush in the winner's set");
        }

        [Test]
        public void ASever_Snaps_ScattersBolts_AndTheVictimCriesOut()
        {
            Add(new LimbSeveredEvent(Time, Side.Right, BodyPart.LeftArm));

            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.JointSnap, SoundCue.BoltScatter, SoundCue.GruntBigHurt }));
            Assert.That(_requests[2].Side, Is.EqualTo(Side.Right));
        }

        [Test]
        public void ADeath_SplitsTheTorso_AndTheDummyScreams()
        {
            Add(new FighterDiedEvent(Time, Side.Left));

            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.TorsoSplit, SoundCue.BoltScatter, SoundCue.GruntKnockOut }));
            Assert.That(_requests[2].Side, Is.EqualTo(Side.Left));
        }

        [Test]
        public void TheWall_CracklesOnAWarning_AndZapsItsOwner()
        {
            Add(new ElectricWallEvent(Time, Side.Left, ElectricWallPhase.Warning, 0f, -500f));
            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.WallWarning }));

            SetUp();
            Add(new ElectricWallEvent(Time, Side.Left, ElectricWallPhase.Active, 10f, -450f));
            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.WallZap, SoundCue.GruntBigHurt }));
            Assert.That(_requests[1].Side, Is.EqualTo(Side.Left));
        }

        [Test]
        public void ADroppedWeapon_Clatters_OtherEventsAreSilent()
        {
            Add(new WeaponDroppedEvent(Time, Side.Right, WeaponIds.Rapier));
            Add(new StatusAppliedEvent(Time, Side.Right, StatusKind.Stunned));
            Add(new AttackInterruptedEvent(Time, Side.Right));

            Assert.That(Cues(), Is.EqualTo(new[] { SoundCue.WeaponDrop }));
        }

        // --- Voices --------------------------------------------------------------------------------

        [Test]
        public void OnlyTheGrunts_AreVoices()
        {
            foreach (SoundCue cue in (SoundCue[])Enum.GetValues(typeof(SoundCue)))
            {
                Assert.That(SoundCues.IsVoice(cue), Is.EqualTo(cue.ToString().StartsWith("Grunt", StringComparison.Ordinal)), cue.ToString());
            }
        }

        [Test]
        public void AScream_CutsOffAGrunt_AGruntNeverCutsOffAScream()
        {
            Assert.That(SoundCues.VoiceMayInterrupt(true, SoundCue.GruntAttack, SoundCue.GruntKnockOut), Is.True);
            Assert.That(SoundCues.VoiceMayInterrupt(true, SoundCue.GruntKnockOut, SoundCue.GruntEffort), Is.False);
            Assert.That(SoundCues.VoiceMayInterrupt(false, SoundCue.GruntKnockOut, SoundCue.GruntEffort), Is.True, "once it is quiet");
        }

        // --- File names ----------------------------------------------------------------------------

        [Test]
        public void EveryCue_HasASnakeCaseName_ThatReadsBackAsItself()
        {
            Assert.That(AudioFileNaming.FileStem(SoundCue.GruntBigHurt), Is.EqualTo("grunt_big_hurt"));
            foreach (SoundCue cue in (SoundCue[])Enum.GetValues(typeof(SoundCue)))
            {
                Assert.That(AudioFileNaming.TryParse(AudioFileNaming.FileStem(cue) + ".wav", out SoundCue parsed), Is.True, cue.ToString());
                Assert.That(parsed, Is.EqualTo(cue));
            }
        }

        [TestCase("Assets/XRim/Audio/Incoming/clash_rebound_02.wav", SoundCue.ClashRebound)]
        [TestCase("hit_heavy_crunchy.ogg", SoundCue.HitHeavy)]
        [TestCase("hit_head.wav", SoundCue.HitHead)]
        [TestCase("GRUNT_Hurt_3.WAV", SoundCue.GruntHurt)]
        [TestCase("grunt_big_hurt_1.wav", SoundCue.GruntBigHurt)]
        public void AFile_IsReadAsTheCueItsNameStartsWith(string file, SoundCue expected)
        {
            Assert.That(AudioFileNaming.TryParse(file, out SoundCue cue), Is.True);
            Assert.That(cue, Is.EqualTo(expected));
        }

        [TestCase("random.wav")]
        [TestCase("hitlight.wav")]
        [TestCase("music_duel_loop.ogg")]
        [TestCase("")]
        public void OtherFiles_AreNoCue(string file) => Assert.That(AudioFileNaming.TryParse(file, out _), Is.False);

        [Test]
        public void MusicFiles_AreRecognised()
        {
            Assert.That(AudioFileNaming.IsMusic("Assets/XRim/Audio/Incoming/Music_Duel_Loop.ogg"), Is.True);
            Assert.That(AudioFileNaming.IsMusic("music.wav"), Is.True);
            Assert.That(AudioFileNaming.IsMusic("musical_hit.wav"), Is.False);
        }

        private void Add(MatchEvent matchEvent) => SoundCueMapper.Add(matchEvent, Settings(), _requests);

        private SoundCue[] Cues() => _requests.Select(request => request.Cue).ToArray();

        private static HitLandedEvent Hit(BodyPart part, float damage) => new HitLandedEvent(Time,
            new HitFacts(Side.Left, part, WeaponIds.Mace, Time, false), new DamageResult(damage, damage, 0f, false, false));

        private static WeaponClashEvent Clash(ClashKind kind, Side? winner) =>
            new WeaponClashEvent(Time, new ClashResult(kind, winner, 60f, 3f, 12f), new Vec2(300f, 120f));

        private static ShieldBlockEvent Block(bool full) =>
            new ShieldBlockEvent(Time, Side.Right, new BlockResult(full, full ? 0f : 0.5f, false), new Vec2(300f, 120f), 80f, 0.1f);
    }
}
