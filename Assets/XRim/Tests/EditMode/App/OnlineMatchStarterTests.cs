using System;
using System.Linq;
using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Networking.Client;
using XRim.Networking.Protocol;
using XRim.Networking.Transport;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Tests.EditMode.Networking.Server;

namespace XRim.Tests.EditMode.App
{
    /// <summary>
    /// The way into an online match on this device (Session 19), against the real engine-free server on an in-process network: finding an
    /// opponent behind a waiting screen, the usual blind pick, waiting for the opponent's pick, the reveal of both loadouts as the server sent
    /// them, then ready. Leaving, a missing server, an opponent who leaves first and settings that cannot be read each end it with the reason.
    /// </summary>
    public sealed class OnlineMatchStarterTests
    {
        private ServerHarness _harness;
        private ManualClock _clock;
        private LoadoutPickPresenterTests.FakeScreens _screens;
        private OnlineMatchStarter _starter;
        private int _ready;
        private string _failed;
        private int _cancelled;

        [SetUp]
        public void SetUp()
        {
            _harness = new ServerHarness();
            _clock = new ManualClock();
            _screens = new LoadoutPickPresenterTests.FakeScreens();
            _ready = 0;
            _failed = null;
            _cancelled = 0;
        }

        [TearDown]
        public void TearDown() => _starter?.Dispose();

        [Test]
        public void FindingAnOpponent_ShowsTheWaitingScreen_ThenThePicker()
        {
            Begin();
            Frame();

            Assert.That(_screens.CoverPlayer, Is.EqualTo(MatchLabels.OnlineFindingTitle));

            _harness.Join();
            Frame();

            Assert.That(_screens.PickerShown, Is.True);
            Assert.That(_starter.Rules, Is.Not.Null, "the picker uses the server's settings");
        }

        [Test]
        public void TheWholeWayIn_Pick_WaitForTheOpponent_Reveal_Ready()
        {
            Begin();
            Frame();
            TestPlayer opponent = _harness.Join(Handedness.Left);
            Frame();

            _screens.TapConfirm(); // the picker starts on the default loadout, ready to lock in
            Frame();

            Assert.That(_screens.CoverPlayer, Is.EqualTo(MatchLabels.OnlineWaitingPickTitle));
            Assert.That(_ready, Is.EqualTo(0));

            opponent.Read();
            opponent.Pick(WeaponIds.Spear, WeaponIds.Sword, WeaponIds.Shield);
            Frame();

            Assert.That(_screens.Last, Is.EqualTo("reveal"));
            Assert.That(_screens.RevealRight, Is.EqualTo(new[] { "SPEAR", "SWORD", "SHIELD" }), "both loadouts, as the server revealed them");

            _screens.TapReveal();

            Assert.That(_ready, Is.EqualTo(1));
            Assert.That(_starter.Join.Stage, Is.EqualTo(OnlineJoinStage.Ready));
            Assert.That(_starter.Reveal.Of(Side.Right), Is.EqualTo(new[] { WeaponIds.Spear, WeaponIds.Sword, WeaponIds.Shield }));
            Assert.That(_failed, Is.Null);
        }

        [Test]
        public void Leave_WhileWaiting_Cancels_AndClosesTheLink()
        {
            Begin();
            Frame();

            _screens.TapCover();

            Assert.That(_cancelled, Is.EqualTo(1));
            Assert.That(_starter.Join.Link.IsEnded, Is.True);
        }

        [Test]
        public void NoServer_Fails_WithTheReason()
        {
            var nowhere = new LoopbackNetwork(); // nothing listens on it
            Begin(() => nowhere.Connect());
            Frame();

            Assert.That(_failed, Is.EqualTo(MatchLabels.OnlineEndReason(GoodbyeReason.ConnectionLost)));
        }

        [Test]
        public void AnOpponentLeavingBeforeTheStart_Fails_WithThatReason()
        {
            Begin();
            Frame();
            TestPlayer opponent = _harness.Join();
            Frame();

            opponent.Drop();
            _harness.Tick();
            _harness.Advance(_harness.Network.ReconnectWaitSeconds);
            _starter.Tick();

            Assert.That(_failed, Is.EqualTo(MatchLabels.OpponentLeftBeforeStart));
        }

        [Test]
        public void SettingsThatCannotBeRead_Fail()
        {
            Begin(settings: new UnreadableSettings());
            Frame();
            _harness.Join();
            Frame();

            Assert.That(_failed, Does.StartWith(MatchLabels.OnlineSettingsUnreadable));
            Assert.That(_starter.Join.Link.IsEnded, Is.True);
        }

        private void Begin(Func<INetConnection> connect = null, ISettingsTextCodec settings = null)
        {
            var link = new ServerLink(connect ?? (() => _harness.Loopback.Connect()), _clock, _harness.Network);
            var join = new OnlineMatchJoin(link, new ClientHelloMessage(Handedness.Right, new[] { SignatureMoveIds.FiveFingerExplodingTouch }));
            _starter = new OnlineMatchStarter(_screens, join, settings ?? new StartingSettings(), new AllWeaponsOwned(), null, "Server here");
            _starter.Ready += () => _ready++;
            _starter.Failed += reason => _failed = reason;
            _starter.Cancelled += () => _cancelled++;
            _starter.Begin();
        }

        private void Frame()
        {
            _harness.Tick();
            _starter.Tick();
        }

        /// <summary>Reads any text as the GDD starting values (the harness's server sends a stand-in text).</summary>
        private sealed class StartingSettings : ISettingsTextCodec
        {
            public string Write(RulesSettings rules, SimulationSettings simulation) => "starting values";

            public void Read(string text, out RulesSettings rules, out SimulationSettings simulation)
            {
                rules = GddStartingValues.CreateRulesSettings();
                simulation = new SimulationSettings();
            }
        }

        private sealed class UnreadableSettings : ISettingsTextCodec
        {
            public string Write(RulesSettings rules, SimulationSettings simulation) => string.Empty;

            public void Read(string text, out RulesSettings rules, out SimulationSettings simulation) =>
                throw new FormatException("not settings");
        }
    }
}
