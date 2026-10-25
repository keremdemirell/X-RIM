using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XRim.Presentation.Vfx;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>The hit-effect cosmetics (Session 18, GDD §16): colours only, and the colours that carry information stay put.</summary>
    public sealed class VfxStylesTests
    {
        [Test]
        public void AHitEffectSet_IsNothingButColours()
        {
            foreach (FieldInfo field in typeof(VfxStyle).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.That(field.FieldType, Is.EqualTo(typeof(Color)), field.Name + ": a cosmetic can change a colour and nothing else");
            }
        }

        [Test]
        public void NeonSparks_ChangesTheSparksFlashAndTrails()
        {
            VfxStyle plain = VfxStyles.Default();
            VfxStyle neon = VfxStyles.NeonSparks();

            Assert.That(neon.Spark, Is.Not.EqualTo(plain.Spark));
            Assert.That(neon.Flash, Is.Not.EqualTo(plain.Flash));
            Assert.That(neon.TrailHead, Is.Not.EqualTo(plain.TrailHead));
            Assert.That(neon.TrailTail, Is.Not.EqualTo(plain.TrailTail));
        }

        [Test]
        public void NeonSparks_LeavesBlocksAndTheWallZapReadingTheSame()
        {
            VfxStyle plain = VfxStyles.Default();
            VfxStyle neon = VfxStyles.NeonSparks();

            Assert.That(neon.FullBlock, Is.EqualTo(plain.FullBlock), "a full block must read the same in every set (readability, GDD §16)");
            Assert.That(neon.PartialBlock, Is.EqualTo(plain.PartialBlock));
            Assert.That(neon.Zap, Is.EqualTo(plain.Zap), "the electric wall zap is a warning");
        }

        [Test]
        public void NeonSparks_StaysVisible()
        {
            VfxStyle neon = VfxStyles.NeonSparks();

            foreach (FieldInfo field in typeof(VfxStyle).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var colour = (Color)field.GetValue(neon);
                Assert.That(colour.a, Is.GreaterThan(0.4f).Or.EqualTo(0f), field.Name + " is neither visible nor deliberately a fade-out");
            }
        }
    }
}
