using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Tests.EditMode.Input;

namespace XRim.Tests.EditMode.App
{
    /// <summary>A touch on any screen area that belongs to something else (HUD, debug overlay) never draws or swipes.</summary>
    public sealed class TouchBlockerSetTests
    {
        [Test]
        public void ATouchIsBlocked_WhenAnyBlockerClaimsIt()
        {
            var set = new TouchBlockerSet();
            var hud = new FixedBlocker(new Vec2(0f, 0f), new Vec2(100f, 100f));
            var overlay = new FixedBlocker(new Vec2(500f, 500f), new Vec2(600f, 600f));

            Assert.That(set.Blocks(new Vec2(50f, 50f)), Is.False, "nothing claims the screen yet");

            set.Add(hud);
            set.Add(overlay);
            set.Add(hud);

            Assert.That(set.Blocks(new Vec2(50f, 50f)), Is.True);
            Assert.That(set.Blocks(new Vec2(550f, 550f)), Is.True);
            Assert.That(set.Blocks(new Vec2(300f, 300f)), Is.False, "the arena");

            set.Remove(hud);
            Assert.That(set.Blocks(new Vec2(50f, 50f)), Is.False, "added twice, removed once: it was only ever in once");
        }
    }
}
