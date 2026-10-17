using System.Collections.Generic;
using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Networking;
using XRim.Presentation.UI;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation.Recording;
using XRim.Tests.EditMode.Networking;
using XRim.Tests.EditMode.Presentation;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.App
{
    /// <summary>
    /// The match HUD: HP under each dummy (following the view flip, GDD §4) that drops as playback passes each hit, the turn counter,
    /// and the sudden-death banner (§14).
    /// </summary>
    public sealed class MatchHudPresenterTests
    {
        private sealed class FakeMatchHud : IMatchHudView
        {
            public readonly Dictionary<Side, (string Name, float Hp, float MaxHp, bool OnScreenLeft)> Fighters =
                new Dictionary<Side, (string, float, float, bool)>();

            public int FighterCalls { get; private set; }
            public string Turn { get; private set; }
            public string BannerTitle { get; private set; }
            public string BannerDetail { get; private set; }

            public void SetVisible(bool visible)
            {
            }

            public void ShowFighter(Side side, string name, float hp, float maxHp, bool onScreenLeft)
            {
                Fighters[side] = (name, hp, maxHp, onScreenLeft);
                FighterCalls++;
            }

            public void ShowTurn(string text) => Turn = text;

            public void ShowBanner(string title, string detail)
            {
                BannerTitle = title;
                BannerDetail = detail;
            }

            /// <summary>The shout on each half of the screen: true is the left half.</summary>
            public readonly Dictionary<bool, string> Shouts = new Dictionary<bool, string>();

            public void ShowSignatureShout(Side side, string text, bool onScreenLeft) => Shouts[onScreenLeft] = text;

            public string ShoutOn(bool screenLeft) => Shouts.TryGetValue(screenLeft, out string text) ? text : string.Empty;
        }

        private RulesSettings _settings;
        private FakeTurnAuthority _authority;
        private ClientMatchFlow _flow;
        private FakeMatchHud _hud;
        private MatchHudPresenter _presenter;

        private float MaxHp => _settings.Damage.MaxHp;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _authority = new FakeTurnAuthority();
            _flow = new ClientMatchFlow(_authority);
            _hud = new FakeMatchHud();
            _presenter = new MatchHudPresenter(_hud, _flow, _settings, new PerSide<string>("YOU", "BOT"));
        }

        [TearDown]
        public void TearDown() => _flow.Dispose();

        private PlanningWindow Window(int turnIndex, float leftHp, float rightHp, bool suddenDeath = false)
        {
            BoardSnapshot board = TestData.CreateInitialBoard(_settings);
            board.State.TurnIndex = turnIndex;
            board.State.IsSuddenDeath = suddenDeath;
            board.State.Fighters[Side.Left].Hp = leftHp;
            board.State.Fighters[Side.Right].Hp = rightHp;
            return new PlanningWindow(turnIndex, 12.0, PerSide<PlanningConstraints>.Create(_ => PlanningConstraints.CreateDefault(_settings.Match)),
                suddenDeath, board, PerSide<IWeaponTipSource>.Create(_ => new FixedWeaponTipSource(new Vec2(100f, 100f))));
        }

        /// <summary>Turn <paramref name="turnIndex"/> is planned, locked and resolved; playback starts.</summary>
        private TurnResult PlayTurn(int turnIndex)
        {
            _authority.RaisePlanningStarted(Window(turnIndex, MaxHp, MaxHp));
            _authority.RaisePlanningLocked(turnIndex);
            TurnResult turn = PlayedTurn.Create(_settings, turnIndex, PlayedTurn.Hit(Side.Right, 0.5, 30f));
            _authority.RaiseTurnResolved(turn);
            return turn;
        }

        [Test]
        public void BeforeTheMatchStarts_BothHaveFullHp_AndItIsTurnOne()
        {
            _presenter.Tick(null, false);

            Assert.That(_hud.Fighters[Side.Left], Is.EqualTo(("YOU", MaxHp, MaxHp, true)));
            Assert.That(_hud.Fighters[Side.Right], Is.EqualTo(("BOT", MaxHp, MaxHp, false)));
            Assert.That(_hud.Turn, Is.EqualTo("TURN 1 / " + _settings.Match.TurnCap));
            Assert.That(_hud.BannerTitle, Is.Empty);
        }

        [Test]
        public void WhilePlanning_ItShowsTheFrozenBoard()
        {
            _authority.RaisePlanningStarted(Window(4, 70f, 35f));

            _presenter.Tick(null, false);

            Assert.That(_hud.Fighters[Side.Left].Hp, Is.EqualTo(70f));
            Assert.That(_hud.Fighters[Side.Right].Hp, Is.EqualTo(35f));
            Assert.That(_hud.Turn, Is.EqualTo("TURN 5 / " + _settings.Match.TurnCap));
        }

        [Test]
        public void DuringPlayback_TheHpDropsAsPlaybackPassesTheHit()
        {
            PlayTurn(2);

            _presenter.Tick(SimTime.FromSeconds(0.4), false);
            Assert.That(_hud.Fighters[Side.Right].Hp, Is.EqualTo(MaxHp), "the hit is still ahead");
            Assert.That(_hud.Turn, Is.EqualTo("TURN 3 / " + _settings.Match.TurnCap));

            _presenter.Tick(SimTime.FromSeconds(0.5), false);
            Assert.That(_hud.Fighters[Side.Right].Hp, Is.EqualTo(MaxHp - 30f));
        }

        [Test]
        public void AfterPlayback_ItKeepsTheTurnsEnd_UntilTheNextPlanningWindow()
        {
            PlayTurn(0);
            _flow.OnPlaybackFinished();

            _presenter.Tick(null, false);
            Assert.That(_hud.Fighters[Side.Right].Hp, Is.EqualTo(MaxHp - 30f));

            _authority.RaisePlanningStarted(Window(1, MaxHp, 60f));
            _presenter.Tick(null, false);
            Assert.That(_hud.Fighters[Side.Right].Hp, Is.EqualTo(60f));
            Assert.That(_hud.Turn, Is.EqualTo("TURN 2 / " + _settings.Match.TurnCap));
        }

        [Test]
        public void AfterTheNextTurnLocks_ItStillShowsThatTurnsBoard_UntilItsResultArrives()
        {
            PlayTurn(0);
            _flow.OnPlaybackFinished();
            _authority.RaisePlanningStarted(Window(1, MaxHp, 60f));
            _authority.RaisePlanningLocked(1);

            _presenter.Tick(null, false);

            Assert.That(_hud.Fighters[Side.Right].Hp, Is.EqualTo(60f), "not the older turn's result");
            Assert.That(_hud.Turn, Is.EqualTo("TURN 2 / " + _settings.Match.TurnCap));
        }

        [Test]
        public void TheBars_FollowTheViewFlip()
        {
            _presenter.Tick(null, true);

            Assert.That(_hud.Fighters[Side.Left].OnScreenLeft, Is.False, "mirrored: the left dummy is drawn on the right");
            Assert.That(_hud.Fighters[Side.Right].OnScreenLeft, Is.True);
        }

        [Test]
        public void SuddenDeath_ShowsTheBanner_InPlaceOfTheTurnCounter()
        {
            _authority.RaisePlanningStarted(Window(30, 1f, 1f, suddenDeath: true));

            _presenter.Tick(null, false);

            Assert.That(_hud.Turn, Is.EqualTo(MatchHudPresenter.SuddenDeathTitle));
            Assert.That(_hud.BannerTitle, Is.EqualTo(MatchHudPresenter.SuddenDeathTitle));
            Assert.That(_hud.BannerDetail, Is.EqualTo("1 HP each: the first clean hit wins · try 1 of 4"), "3 repeats at most: 4 tries");
            Assert.That(_hud.Fighters[Side.Left].Hp, Is.EqualTo(1f));
        }

        [Test]
        public void TheSuddenDeathBanner_CountsTheTries()
        {
            PlanningWindow repeated = Window(32, 1f, 1f, suddenDeath: true);
            repeated.Board.State.SuddenDeathRepeats = 2;
            _authority.RaisePlanningStarted(repeated);

            _presenter.Tick(null, false);

            Assert.That(_hud.BannerDetail, Is.EqualTo("1 HP each: the first clean hit wins · try 3 of 4"));
        }


        // --- Signature moves (GDD §8, Session 15) ------------------------------------------------------

        private void PlayTurnWithSignature(Side side)
        {
            _authority.RaisePlanningStarted(Window(0, MaxHp, MaxHp));
            _authority.RaisePlanningLocked(0);
            _authority.RaiseTurnResolved(PlayedTurn.Create(_settings, 0,
                new SignatureUsedEvent(SimTime.Zero, side, SignatureMoveIds.HeadImpactSimulation404, "Head Impact Simulation #404")));
        }

        [Test]
        public void ASignatureMove_IsShoutedOverItsFighter_WhileTheTurnPlays()
        {
            PlayTurnWithSignature(Side.Right);

            _presenter.Tick(SimTime.FromSeconds(0.2), false);

            Assert.That(_hud.ShoutOn(false), Is.EqualTo("HEAD IMPACT SIMULATION #404!"), "over the right dummy's bar");
            Assert.That(_hud.ShoutOn(true), Is.Empty);
        }

        [Test]
        public void TheShout_IsGoneOncePlaybackStops_AndNeverShowsDuringPlanning()
        {
            _authority.RaisePlanningStarted(Window(0, MaxHp, MaxHp));
            _presenter.Tick(null, false);
            Assert.That(_hud.ShoutOn(true), Is.Empty, "§8: a chosen move stays hidden until execution");

            PlayTurnWithSignature(Side.Left);
            _presenter.Tick(SimTime.FromSeconds(0.2), false);
            Assert.That(_hud.ShoutOn(true), Is.Not.Empty);

            _flow.OnPlaybackFinished();
            _presenter.Tick(null, false);
            Assert.That(_hud.ShoutOn(true), Is.Empty);
        }

        [Test]
        public void TheShout_FollowsTheViewFlip()
        {
            PlayTurnWithSignature(Side.Left);
            _presenter.Tick(SimTime.FromSeconds(0.2), false);

            _presenter.Tick(SimTime.FromSeconds(0.3), true);

            Assert.That(_hud.ShoutOn(false), Is.Not.Empty, "mirrored: the left dummy is drawn on the right");
            Assert.That(_hud.ShoutOn(true), Is.Empty, "the old half is cleared");
        }

        [Test]
        public void ATurnWithoutAMove_ShoutsNothing()
        {
            PlayTurn(0);

            _presenter.Tick(SimTime.FromSeconds(0.2), false);

            Assert.That(_hud.ShoutOn(true), Is.Empty);
            Assert.That(_hud.ShoutOn(false), Is.Empty);
        }

        [Test]
        public void NothingChanged_SendsNothingAgain()
        {
            _presenter.Tick(null, false);
            int calls = _hud.FighterCalls;

            _presenter.Tick(null, false);

            Assert.That(_hud.FighterCalls, Is.EqualTo(calls));
        }
    }
}
