using NUnit.Framework;
using XRim.Presentation.ArtSpec;
using XRim.Presentation.Vfx;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The art spec (Session 13, Docs/ART_SPEC.md): written from the layout the ragdoll builder builds and from the tuning, it lists every
    /// body piece with its size, pivot and marker, every joint, every held item (the club; the body blow holds nothing), the debris, the
    /// wall and the background, and how to name and deliver the files. It follows the tuning and is the same for the same inputs.
    /// </summary>
    public sealed class ArtSpecDocumentTests
    {
        private static string Write(ArtSpecInputs inputs = null) => ArtSpecDocument.Write(inputs ?? new ArtSpecInputs { Date = "2026-10-11" });

        [Test]
        public void EveryBodyPiece_IsListedWithItsSizePivotAndMarker()
        {
            string spec = Write();

            foreach (DummyBodyLayout body in DummyLayout.Of(new RagdollSettings(), new PathSettings(), RagdollSegmentation.TenBodies).Bodies)
            {
                Assert.That(spec, Does.Contain($"| {body.Name} |"), body.Name);
            }

            Assert.That(spec, Does.Contain("| Torso | Torso | 60 × 120 | 240 × 480 | bottom middle | (0, 0) | 0 | 48 |"));
            Assert.That(spec, Does.Contain("| RightArmLower | RightArm | 24 × 120 | 96 × 480 | top middle | (0, -20) | 20 | 19.2 |"));
            Assert.That(spec, Does.Contain("| Head | Head | Ø 50 | 200 × 200 | bottom middle (the neck) | (0, 120) | 10 | 40 |"));
        }

        [Test]
        public void EveryJoint_IsListedWithItsPointAndRange()
        {
            string spec = Write();

            Assert.That(spec, Does.Contain("| Neck | Head | Torso | (0, 120) | -30 to 30 |"));
            Assert.That(spec, Does.Contain("| LeftKnee | LeftLegLower | LeftLegUpper | (0, -90) | -130 to 0 |"));
        }

        [Test]
        public void TheTorsoSplit_AndTheSixBodyLimbs_AreExplained()
        {
            string spec = Write();

            Assert.That(spec, Does.Contain("at y = 60: **TorsoBottom** and **TorsoTop** replace the torso"));
            Assert.That(spec, Does.Contain("LeftArm 24 × 240 (pivot top middle, at (0, 100))"));
        }

        [Test]
        public void EveryHeldItem_HasARow_AndTheBodyBlowHoldsNothing()
        {
            var inputs = new ArtSpecInputs();
            string spec = Write(inputs);

            foreach (WeaponStats weapon in inputs.Rules.Weapons)
            {
                Assert.That(spec, Does.Contain($"| {weapon.DisplayName} | {weapon.Id} |"), weapon.Id);
                if (weapon.Kind != WeaponKind.BodyBlow) Assert.That(spec, Does.Contain(ArtSpecDocument.WeaponFileName(weapon)), weapon.Id);
            }

            Assert.That(spec, Does.Contain("| Rapier | rapier | 400 × 10 | 1600 × 40 | left end, middle |"));
            Assert.That(spec, Does.Contain("| Shield | shield | 40 × 150 | 160 × 600 | centre |"));
            Assert.That(spec, Does.Contain("Nothing is held: the head strikes"));
            Assert.That(spec, Does.Not.Contain("weapon_body_blow.png"));
        }

        [Test]
        public void TheDebris_TheWall_AndTheBackground_AreSized()
        {
            string spec = Write();

            Assert.That(spec, Does.Contain("| fx_bolt.png | 9 × 21 |"));
            Assert.That(spec, Does.Contain("| fx_gear.png | 16.2 × 16.2 |"));
            Assert.That(spec, Does.Contain("| fx_drop.png | 7 × 9.6 |"));
            Assert.That(spec, Does.Contain("| wall_bar.png | 14 × 700 | 56 × 2800 |"));
            Assert.That(spec, Does.Contain("| arena_background.png | 2600 × 1250 |"));
            Assert.That(spec, Does.Contain("never blood"));
        }

        [Test]
        public void TheDelivery_NamesTheFolderAndEveryFile()
        {
            string spec = Write();

            Assert.That(spec, Does.Contain("`Assets/XRim/Art/Incoming/`"));
            string[] files =
            {
                "dummy_torso.png", "dummy_torso_bottom.png", "dummy_torso_top.png", "dummy_head.png", "dummy_arm_upper.png", "dummy_arm_lower.png",
                "dummy_leg_upper.png", "dummy_leg_lower.png", "weapon_severed_limb.png", "fx_nut.png", "fx_spring.png", "wall_glow.png",
                "arena_background.png",
            };
            foreach (string file in files)
            {
                Assert.That(spec, Does.Contain(file), file);
            }
        }

        [Test]
        public void FileNames_AreLowerCaseWithUnderscores_AndLeftAndRightShareOne()
        {
            Assert.That(ArtSpecDocument.DummyFileName("LeftArmUpper"), Is.EqualTo("dummy_arm_upper.png"));
            Assert.That(ArtSpecDocument.DummyFileName("RightArmUpper"), Is.EqualTo("dummy_arm_upper.png"));
            Assert.That(ArtSpecDocument.DummyFileName("TorsoBottom"), Is.EqualTo("dummy_torso_bottom.png"));
            Assert.That(ArtSpecDocument.EffectFileName(VfxParticleKind.Drop), Is.EqualTo("fx_drop.png"));
        }

        [Test]
        public void TheSpec_FollowsTheTuning_AndIsTheSameForTheSameInputs()
        {
            var taller = new ArtSpecInputs { Date = "2026-10-11" };
            taller.Simulation.Ragdoll.TorsoHeightUnits = 150f;

            Assert.That(Write(taller), Does.Contain("| Torso | Torso | 60 × 150 |"));
            Assert.That(Write(), Is.EqualTo(Write()));
        }
    }
}
