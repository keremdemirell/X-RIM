using NUnit.Framework;
using UnityEngine;
using XRim.App;
using XRim.Economy;
using XRim.Presentation.Vfx;

namespace XRim.Tests.EditMode.App
{
    /// <summary>What an equipped cosmetic turns into when a match is drawn (GDD §16, Decided: no cosmetic may change hitboxes, silhouette, reach or readability).</summary>
    public sealed class CosmeticLooksTests
    {
        [Test]
        public void NeonSparks_GivesTheNeonHitEffects()
        {
            VfxStyle style = CosmeticLooks.HitEffectStyle(CosmeticIds.NeonSparks);

            Assert.That(style.Spark, Is.Not.EqualTo(new VfxStyle().Spark));
            Assert.That(style.TrailHead, Is.Not.EqualTo(new VfxStyle().TrailHead));
        }

        [Test]
        public void NothingEquipped_OrALookThisBuildDoesNotKnow_GivesThePlainSet()
        {
            VfxStyle plain = new VfxStyle();

            foreach (string id in new string[] { null, string.Empty, "from_the_future", CosmeticIds.HazardStripes })
            {
                VfxStyle style = CosmeticLooks.HitEffectStyle(id);
                Assert.That(style.Spark, Is.EqualTo(plain.Spark), id);
                Assert.That(style.Flash, Is.EqualTo(plain.Flash), id);
            }
        }

        [Test]
        public void EverySetIsANewObject_SoOneDummysSetIsNeverSharedByAccident()
        {
            Assert.That(CosmeticLooks.HitEffectStyle(CosmeticIds.NeonSparks), Is.Not.SameAs(CosmeticLooks.HitEffectStyle(CosmeticIds.NeonSparks)));
        }
    }
}
