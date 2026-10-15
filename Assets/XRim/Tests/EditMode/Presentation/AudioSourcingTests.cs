using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XRim.Config;
using XRim.Presentation.Audio;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// Adding sounds by hand (Session 13 batch 7): file names are read forgivingly (case, spaces, dashes, capitals, a number straight
    /// after the name), a name that matches nothing gets a suggestion, every file gets phone-friendly import settings, the coverage report
    /// says what plays, what is silent and what cannot be used, and the guide and the folder's read-me list every sound name.
    /// </summary>
    public sealed class AudioSourcingTests
    {
        [TestCase("Hit Light 2.wav", SoundCue.HitLight)]
        [TestCase("hit-light.wav", SoundCue.HitLight)]
        [TestCase("HitLight.wav", SoundCue.HitLight)]
        [TestCase("hit_light2.wav", SoundCue.HitLight)]
        [TestCase("hitHeavy02.ogg", SoundCue.HitHeavy)]
        [TestCase("Grunt Big Hurt.wav", SoundCue.GruntBigHurt)]
        [TestCase("grunt.knock.out.wav", SoundCue.GruntKnockOut)]
        [TestCase("  body_move .wav", SoundCue.BodyMove)]
        public void HandTypedNames_AreUnderstood(string file, SoundCue expected)
        {
            Assert.That(AudioFileNaming.TryParse(file, out SoundCue cue), Is.True, file);
            Assert.That(cue, Is.EqualTo(expected));
        }

        [TestCase("hit_headbutt.wav")]
        [TestCase("light_hit.wav")]
        [TestCase("musical_hit.wav")]
        public void NamesThatOnlyLookAlike_AreNotTaken(string file) => Assert.That(AudioFileNaming.TryParse(file, out _), Is.False, file);

        [Test]
        public void MusicNames_AreForgivingToo()
        {
            Assert.That(AudioFileNaming.IsMusic("Music Duel Loop.ogg"), Is.True);
            Assert.That(AudioFileNaming.IsMusic("music2.ogg"), Is.True);
            Assert.That(AudioFileNaming.IsMusic("musical_hit.wav"), Is.False);
        }

        [TestCase("hit_ligth_02.wav", "hit_light")]
        [TestCase("hitlight.wav", "hit_light")]
        [TestCase("grunt_hurtt.wav", "grunt_hurt")]
        [TestCase("musik_loop.ogg", "music")]
        [TestCase("clash.wav", "clash_rebound, clash_crush or clash_slide")]
        [TestCase("whoosh_1.wav", "swing_whoosh")]
        [TestCase("zap.wav", "wall_zap")]
        public void AMisnamedFile_GetsASuggestion(string file, string suggestion) =>
            Assert.That(AudioFileNaming.Suggest(file), Is.EqualTo(suggestion));

        [TestCase("random.wav")]
        [TestCase("ab.wav")]
        [TestCase("hit_light_01.wav")]
        [TestCase("music_duel_loop.ogg")]
        public void NoSuggestion_WhenNothingIsClose_OrTheNameWorks(string file) => Assert.That(AudioFileNaming.Suggest(file), Is.Null);

        [Test]
        public void Effects_AreMonoAndReadyAtOnce_TheMusicStreamsInStereo()
        {
            AudioImportProfile music = AudioImportProfile.For("Assets/XRim/Audio/Incoming/music_duel_loop.ogg");
            Assert.That(music.Streams, Is.True);
            Assert.That(music.ForceToMono, Is.False);

            foreach (string file in new[] { "hit_light_01.wav", "grunt_hurt_02.wav", "misnamed.wav" })
            {
                AudioImportProfile effect = AudioImportProfile.For(file);
                Assert.That(effect.Streams, Is.False, file);
                Assert.That(effect.ForceToMono, Is.True, file);
                Assert.That(effect.Quality, Is.InRange(0f, 1f), file);
            }
        }

        [Test]
        public void ThePriorityList_HoldsEveryCueOnce()
        {
            var all = (SoundCue[])Enum.GetValues(typeof(SoundCue));
            Assert.That(AudioCoverage.Priority, Is.EquivalentTo(all));
            Assert.That(AudioCoverage.Priority.Distinct().Count(), Is.EqualTo(all.Length));
        }

        [Test]
        public void TheCoverageReport_SaysWhatPlays_WhatIsSilent_AndWhatCannotBeUsed()
        {
            var versions = new Dictionary<SoundCue, int> { [SoundCue.HitLight] = 3, [SoundCue.GruntHurt] = 1 };
            string[] files =
            {
                "hit_light_01.wav", "hit_light_02.wav", "hit_light_03.wav", "grunt_hurt.wav", "README.md", "hit_ligth.wav", "scream.m4a",
                "clash_rebound.wav", "music_a.ogg", "music_b.ogg",
            };
            string[] built = { "hit_light_01", "hit_light_02", "hit_light_03", "grunt_hurt" };

            string report = AudioCoverage.Report(cue => versions.TryGetValue(cue, out int count) ? count : 0, null, files, built);

            Assert.That(report, Does.Contain("Sounds that play: 2 of 21: hit_light (3 versions), grunt_hurt."));
            Assert.That(report, Does.Contain("Still silent, the most important first: hit_heavy, clash_rebound,"));
            Assert.That(report, Does.Contain("Music: none yet"));
            Assert.That(report, Does.Contain("hit_ligth.wav: not a sound name the game knows. Did you mean hit_light?"));
            Assert.That(report, Does.Contain("scream.m4a: Unity cannot read .m4a files."));
            Assert.That(report, Does.Contain("music_b.ogg: only one music loop is used (music_a.ogg"));
            Assert.That(report, Does.Contain("New since the sound sets were last built (2): clash_rebound.wav, music_a.ogg."));
            Assert.That(report, Does.Not.Contain("README.md"), "the read-me is not a sound");
        }

        [Test]
        public void TheCoverageReport_SaysSo_WhenEverythingIsIn()
        {
            string report = AudioCoverage.Report(_ => 1, "music_duel_loop", new[] { "music_duel_loop.ogg", "README.md" }, new[] { "music_duel_loop" });

            Assert.That(report, Does.Contain("Still silent: none."));
            Assert.That(report, Does.Contain("Music: music_duel_loop."));
            Assert.That(report, Does.Contain("Every sound file in the folder is in the game."));
        }

        [Test]
        public void AFileWithoutAType_IsExplained() =>
            Assert.That(AudioCoverage.ProblemWith("voice"), Does.Contain("has no type at the end of its name"));

        [TestCase("Docs/SOUND_GUIDE.md")]
        [TestCase("Assets/XRim/Audio/Incoming/README.md")]
        public void TheGuides_NameEverySound(string path)
        {
            if (!File.Exists(path)) Assert.Ignore($"{path} is read from the project folder; this runner started elsewhere.");
            string text = File.ReadAllText(path);

            foreach (SoundCue cue in (SoundCue[])Enum.GetValues(typeof(SoundCue)))
            {
                Assert.That(text, Does.Contain("`" + AudioFileNaming.FileStem(cue) + "`"), cue.ToString());
            }

            Assert.That(text, Does.Contain("`" + AudioFileNaming.MusicPrefix + "`"));
        }
    }
}
