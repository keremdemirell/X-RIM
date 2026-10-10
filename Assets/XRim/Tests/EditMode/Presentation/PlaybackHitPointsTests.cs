using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules.Settings;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>The HP bars drop as playback passes each hit, not when the turn resolved.</summary>
    public sealed class PlaybackHitPointsTests
    {
        private RulesSettings _settings;
        private TurnResult _turn;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _turn = PlayedTurn.Create(_settings, 0, PlayedTurn.Hit(Side.Right, 0.3, 20f), PlayedTurn.Hit(Side.Right, 0.6, 15f),
                PlayedTurn.Hit(Side.Left, 0.6, 8f));
        }

        private float HpAt(double seconds, Side side) => PlaybackHitPoints.HpAt(_turn, SimTime.FromSeconds(seconds), side);

        [Test]
        public void BeforeAnyHit_ShowsTheHpTheTurnStartedWith()
        {
            Assert.That(HpAt(0.0, Side.Right), Is.EqualTo(_settings.Damage.MaxHp));
            Assert.That(HpAt(0.0, Side.Left), Is.EqualTo(_settings.Damage.MaxHp));
        }

        [Test]
        public void EachHit_CountsFromTheMomentPlaybackReachesIt()
        {
            float max = _settings.Damage.MaxHp;

            Assert.That(HpAt(0.29, Side.Right), Is.EqualTo(max));
            Assert.That(HpAt(0.3, Side.Right), Is.EqualTo(max - 20f), "a hit counts at its own instant");
            Assert.That(HpAt(0.59, Side.Right), Is.EqualTo(max - 20f));
            Assert.That(HpAt(0.6, Side.Right), Is.EqualTo(max - 35f));
            Assert.That(HpAt(0.6, Side.Left), Is.EqualTo(max - 8f), "a trade: both bars drop together");
        }

        [Test]
        public void AtTheEnd_ShowsTheResolvedHp()
        {
            Assert.That(HpAt(PlayedTurn.Seconds, Side.Right), Is.EqualTo(_turn.Report.ResolvedState.Fighters[Side.Right].Hp));
            Assert.That(HpAt(PlayedTurn.Seconds, Side.Left), Is.EqualTo(_turn.Report.ResolvedState.Fighters[Side.Left].Hp));
        }
    }
}
