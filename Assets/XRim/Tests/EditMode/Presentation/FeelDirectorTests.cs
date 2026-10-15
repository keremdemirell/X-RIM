using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Cameras;
using XRim.Presentation.Feel;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The feel layer's pacing (Session 13): it plans each turn's slow motion when the turn starts, so playback slows before a big moment;
    /// it freezes on landings; it holds the turn while the camera still moves; and it says how long the turn will take on screen.
    /// </summary>
    public sealed class FeelDirectorTests
    {
        private const float Tolerance = 1e-4f;
        private FeelConfig _feel;

        [SetUp]
        public void SetUp() => _feel = ScriptableObject.CreateInstance<FeelConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_feel);

        [Test]
        public void ASever_SlowsPlaybackDown_BeforeItHappens()
        {
            var director = new FeelDirector(_feel);
            SimTime cut = SimTime.FromSeconds(0.5);
            director.Begin(PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, new LimbSeveredEvent(cut, Side.Right, BodyPart.RightArm)));

            float lead = _feel.SeverSlowMotionRealSeconds * _feel.SeverSlowMotionSpeed * _feel.SeverSlowMotionLeadFraction;
            Assert.That(director.Pace(0.01f, cut - SimTime.FromSeconds(lead + _feel.SlowMotionRampTurnSeconds + 0.05)), Is.EqualTo(0.01f).Within(Tolerance),
                "real time well before the cut");
            Assert.That(director.Pace(0.01f, cut - SimTime.FromSeconds(lead * 0.5)), Is.EqualTo(0.01f * _feel.SeverSlowMotionSpeed).Within(Tolerance),
                "already slowed just before the cut");
            Assert.That(director.SlowMotion.Windows.Count, Is.EqualTo(1));
        }

        [Test]
        public void ALandedHit_Freezes_ThenPlaybackMovesOn()
        {
            var director = new FeelDirector(_feel);
            HitLandedEvent hit = PlayedTurn.Hit(Side.Right, 0.4, 10f);
            director.Begin(PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, hit));
            float freeze = HitStopSettings.From(_feel).SecondsFor(hit);

            director.OnMatchEvent(hit);

            Assert.That(director.IsHolding, Is.True);
            Assert.That(director.Pace(freeze, hit.Time), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(director.IsHolding, Is.False);
            Assert.That(director.Pace(0.02f, hit.Time), Is.EqualTo(0.02f).Within(Tolerance));
        }

        [Test]
        public void TheTurnIsHeld_WhileTheCameraStillMoves()
        {
            var camera = new StubCamera();
            var director = new FeelDirector(_feel, camera);
            TurnResult turn = PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0);

            director.Begin(turn);
            Assert.That(camera.Begun, Is.SameAs(turn), "the camera is told the turn");

            camera.IsSettling = true;
            Assert.That(director.IsHolding, Is.True);
            camera.IsSettling = false;
            Assert.That(director.IsHolding, Is.False);
        }

        [Test]
        public void PlannedRealSeconds_IsTheTurnPlusItsFreezes_WhenNothingIsSlowed()
        {
            var director = new FeelDirector(_feel);
            HitLandedEvent hit = PlayedTurn.Hit(Side.Right, 0.4, 10f);

            director.Begin(PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, hit));

            Assert.That(director.PlannedRealSeconds, Is.EqualTo(PlayedTurn.Seconds + HitStopSettings.From(_feel).SecondsFor(hit)).Within(Tolerance));
        }

        [Test]
        public void PlannedRealSeconds_GrowsByTheSlowMotion()
        {
            var director = new FeelDirector(_feel);

            director.Begin(PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, new FighterDiedEvent(SimTime.FromSeconds(0.5), Side.Right)));

            float slowed = _feel.KnockOutSlowMotionRealSeconds;
            float turnSecondsSlowed = slowed * _feel.KnockOutSlowMotionSpeed;
            float atLeast = (float)PlayedTurn.Seconds - turnSecondsSlowed + slowed + _feel.KillHitStopSeconds;
            Assert.That(director.PlannedRealSeconds, Is.GreaterThan(atLeast - Tolerance), "the stretch plus its eases");
        }

        [Test]
        public void ThePacing_SplitsThePlannedTime_IntoTheTurnItsSlowMotionAndItsFreezes()
        {
            var director = new FeelDirector(_feel);
            HitLandedEvent hit = PlayedTurn.Hit(Side.Right, 0.2, 5f);
            HitLandedEvent sameInstant = PlayedTurn.Hit(Side.Left, 0.2, 5f);

            director.Begin(PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, hit, sameInstant,
                new FighterDiedEvent(SimTime.FromSeconds(0.5), Side.Right)));

            PacingBreakdown pacing = director.Pacing;
            Assert.That(pacing.TurnSeconds, Is.EqualTo((float)PlayedTurn.Seconds).Within(Tolerance));
            Assert.That(pacing.SlowMotionSeconds, Is.GreaterThan(0f), "the KO slows down");
            Assert.That(pacing.SlowMotionStretches, Is.EqualTo(1));
            Assert.That(pacing.Freezes, Is.EqualTo(2), "two hits at one instant share one freeze; the KO has its own");
            Assert.That(pacing.HitStopSeconds, Is.EqualTo(HitStopSettings.From(_feel).SecondsFor(hit) + _feel.KillHitStopSeconds).Within(Tolerance));
            Assert.That(director.PlannedRealSeconds, Is.EqualTo(pacing.TurnSeconds + pacing.SlowMotionSeconds + pacing.HitStopSeconds).Within(Tolerance));
        }

        [Test]
        public void ForAReplay_TheWholeTurnPlaysSlower_AndItsOwnSlowMotionStaysSlowest()
        {
            var director = new FeelDirector(_feel);
            SimTime death = SimTime.FromSeconds(0.5);
            TurnResult turn = PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, new FighterDiedEvent(death, Side.Right));
            director.Begin(turn);

            director.SlowWholeTurn(turn, 0.4f);

            Assert.That(director.Pace(0.1f, SimTime.FromSeconds(0.05)), Is.EqualTo(0.04f).Within(Tolerance), "0.4 × away from the moment");
            Assert.That(director.SlowMotion.SpeedAt(death), Is.EqualTo(_feel.KnockOutSlowMotionSpeed), "the KO's own 0.2 ×");
        }

        private sealed class StubCamera : ICameraDirector
        {
            public TurnResult Begun { get; private set; }
            public bool IsSettling { get; set; }
            public void SetArenaFlipped(bool flipped) { }
            public void SetArena(ArenaEdges edges) { }
            public void ShowBoard(PoseSnapshot pose) { }
            public void BeginTurn(TurnResult turn) => Begun = turn;
            public void OnMatchEvent(MatchEvent matchEvent) { }
            public void FocusOn(Vec2 pointUnits, float viewFraction, float seconds) { }
        }
    }
}
