using System;
using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.Networking
{
    /// <summary>
    /// The in-process authority running the whole loop against a scripted simulator: events and their order, the
    /// playback hold that keeps replays out of planning time, and the hand-over to the end of the match.
    /// </summary>
    public sealed class LocalTurnAuthorityTests
    {
        private const double StartSeconds = 50.0;

        private ManualClock _clock;
        private RulesSettings _settings;
        private SimulationSettings _simulation;
        private List<string> _log;
        private List<PlanningWindow> _windows;
        private List<TurnResult> _results;
        private MatchOutcome _outcome;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock(StartSeconds);
            _settings = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _log = new List<string>();
            _windows = new List<PlanningWindow>();
            _results = new List<TurnResult>();
            _outcome = null;
        }

        private LocalTurnAuthority Create(ScriptedTurnSimulator simulator, PlaybackHoldSettings hold = null)
        {
            var authority = new LocalTurnAuthority(TestData.CreateMatchSetup(), _settings, _simulation, new RulePolicies(),
                simulator, _clock, new LocalTurnAuthorityOptions { PlaybackHold = hold });
            authority.MatchStarted += info => _log.Add("MatchStarted");
            authority.PlanningStarted += window =>
            {
                _log.Add($"PlanningStarted:{window.TurnIndex}");
                _windows.Add(window);
            };
            authority.PublicStateChanged += (side, state) => _log.Add($"Public:{side}:{state.Weapon}:{(state.IsReady ? "ready" : "not-ready")}");
            authority.PlanningLocked += turn => _log.Add($"Locked:{turn}");
            authority.TurnResolved += result =>
            {
                _log.Add($"Resolved:{result.TurnIndex}");
                _results.Add(result);
            };
            authority.MatchEnded += outcome =>
            {
                _log.Add("MatchEnded");
                _outcome = outcome;
            };
            return authority;
        }

        private static void ReadyBoth(LocalTurnAuthority authority)
        {
            authority.Send(Side.Left, new SetReadyCommand(true));
            authority.Send(Side.Right, new SetReadyCommand(true));
        }

        // --- Starting --------------------------------------------------------------------------

        [Test]
        public void Start_AnnouncesTheMatch_ThenOpensPlanning()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator());

            authority.Start();

            Assert.That(_log, Is.EqualTo(new[] { "MatchStarted", "PlanningStarted:0" }));
            Assert.That(authority.Phase, Is.EqualTo(MatchPhase.Planning));
        }

        [Test]
        public void TheFirstWindow_CarriesTheFrozenBoardTheDeadlineAndTheWeaponTips()
        {
            Create(new ScriptedTurnSimulator()).Start();

            PlanningWindow window = _windows[0];
            Assert.That(window.TurnIndex, Is.EqualTo(0));
            Assert.That(window.DeadlineSeconds, Is.EqualTo(StartSeconds + 12.0));
            Assert.That(window.IsSuddenDeath, Is.False);
            Assert.That(window.Board.State.Fighters[Side.Left].CurrentWeapon, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(window.Constraints[Side.Left].PlanningDurationSeconds, Is.EqualTo(12f));

            // The placeholder guard-stance tip: shoulder + (arm × hand reach + weapon length) along the guard angle.
            Vec2 expected = _settings.Paths.ShoulderOffsetUnits + Vec2.FromAngleDegrees(_simulation.Ragdoll.GuardAngleDegrees) *
                            (_settings.Paths.ArmLengthUnits * _simulation.Ragdoll.GuardHandReachFraction +
                             _settings.FindWeapon(WeaponIds.Rapier).LengthUnits);
            Assert.That(Vec2.Distance(window.WeaponTips[Side.Left].TipLocal(WeaponIds.Rapier), expected), Is.LessThan(1e-3f));
        }

        [Test]
        public void Start_TwiceIsAProgrammerError()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator());
            authority.Start();

            Assert.Throws<InvalidOperationException>(() => authority.Start());
        }

        [Test]
        public void BeforeStart_CommandsAreRefused_AndTickIsHarmless()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator());

            Assert.That(authority.Send(Side.Left, new SetReadyCommand(true)).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
            authority.Tick();

            Assert.That(_log, Is.Empty);
            Assert.That(authority.Phase, Is.EqualTo(MatchPhase.MatchSetup));
        }

        // --- Commands and public state ---------------------------------------------------------

        [Test]
        public void WeaponSwitchesAndReady_AreShownToTheOpponentAtOnce_OnlyWhenTheyChange()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator());
            authority.Start();
            _log.Clear();

            authority.Send(Side.Left, new SelectWeaponCommand(WeaponIds.Mace));
            authority.Send(Side.Left, new SelectWeaponCommand(WeaponIds.Mace)); // nothing changes
            authority.Send(Side.Right, new SelectWeaponCommand(WeaponIds.Spear)); // Right owns it
            authority.Send(Side.Left, new SelectWeaponCommand(WeaponIds.Spear)); // Left does not: refused
            authority.Send(Side.Left, new SetReadyCommand(true));
            authority.Send(Side.Left, new SetReadyCommand(true)); // nothing changes

            Assert.That(_log, Is.EqualTo(new[]
            {
                "Public:Left:mace:not-ready",
                "Public:Right:spear:not-ready",
                "Public:Left:mace:ready",
            }));
        }

        [Test]
        public void ABodyMoveOrPath_NeverReachesTheOpponent()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator());
            authority.Start();
            _log.Clear();

            authority.Send(Side.Left, new SetBodyMoveCommand(BodyMove.Lunge));
            authority.Send(Side.Left, new SetPathCommand(PlanningTestData.Thrust(400f)));

            Assert.That(_log, Is.Empty, "§3: body moves and paths are secret until execution");
        }

        // --- Locking and resolving -------------------------------------------------------------

        [Test]
        public void TheSecondReady_LocksAtOnce_ButTheSimulatorWaitsForTick()
        {
            var simulator = new ScriptedTurnSimulator();
            LocalTurnAuthority authority = Create(simulator);
            authority.Start();
            _log.Clear();

            ReadyBoth(authority);

            Assert.That(authority.Phase, Is.EqualTo(MatchPhase.Locked));
            Assert.That(_log, Is.EqualTo(new[] { "Public:Left:rapier:ready", "Public:Right:mace:ready", "Locked:0" }));
            Assert.That(simulator.Inputs, Is.Empty, "the simulation never runs inside Send");
        }

        [Test]
        public void Tick_RunsTheTurn_ThenStartsTheNextPlanning()
        {
            var simulator = new ScriptedTurnSimulator();
            LocalTurnAuthority authority = Create(simulator);
            authority.Start();
            ReadyBoth(authority);
            _log.Clear();

            authority.Tick();

            Assert.That(_log, Is.EqualTo(new[] { "Resolved:0", "PlanningStarted:1" }));
            Assert.That(simulator.Inputs, Has.Count.EqualTo(1));
            Assert.That(authority.Phase, Is.EqualTo(MatchPhase.Planning));
        }

        [Test]
        public void ATick_RunsAtMostOneTurn()
        {
            var simulator = new ScriptedTurnSimulator();
            LocalTurnAuthority authority = Create(simulator);
            authority.Start();
            ReadyBoth(authority);

            authority.Tick();
            ReadyBoth(authority); // turn 2 is locked as well
            authority.Tick();

            Assert.That(simulator.Inputs, Has.Count.EqualTo(2));
            Assert.That(_results[1].TurnIndex, Is.EqualTo(1));
        }

        [Test]
        public void TheSimulator_GetsTheLockedPlansTheBoardAndTheSettings()
        {
            var simulator = new ScriptedTurnSimulator();
            LocalTurnAuthority authority = Create(simulator);
            authority.Start();
            authority.Send(Side.Left, new SelectWeaponCommand(WeaponIds.Mace));
            authority.Send(Side.Left, new SetBodyMoveCommand(BodyMove.Lunge));
            authority.Send(Side.Left, new SetPathCommand(PlanningTestData.Thrust(250f)));
            authority.Send(Side.Right, new SetBodyMoveCommand(BodyMove.Jump));
            ReadyBoth(authority);

            authority.Tick();

            TurnInput input = simulator.Inputs[0];
            Assert.That(input.Plans[Side.Left].Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(input.Plans[Side.Left].BodyMove, Is.EqualTo(BodyMove.Lunge));
            Assert.That(input.Plans[Side.Left].Path.IsEmpty, Is.False);
            Assert.That(input.Plans[Side.Right].BodyMove, Is.EqualTo(BodyMove.Jump));
            Assert.That(input.Board.State.TurnIndex, Is.EqualTo(0));
            Assert.That(input.Board.State.Fighters[Side.Left].CurrentWeapon, Is.EqualTo(WeaponIds.Mace), "the switch is on the board");
            Assert.That(input.Rules, Is.SameAs(_settings));
            Assert.That(input.Simulation, Is.SameAs(_simulation));
        }

        [Test]
        public void WhenTheTimerEnds_WhateverIsSetExecutes()
        {
            var simulator = new ScriptedTurnSimulator();
            LocalTurnAuthority authority = Create(simulator);
            authority.Start();
            authority.Send(Side.Left, new SetBodyMoveCommand(BodyMove.Crouch));
            _log.Clear();

            _clock.Set(StartSeconds + 12.0);
            authority.Tick();

            Assert.That(_log, Is.EqualTo(new[] { "Locked:0", "Resolved:0", "PlanningStarted:1" }));
            Assert.That(simulator.Inputs[0].Plans[Side.Left].BodyMove, Is.EqualTo(BodyMove.Crouch));
        }

        [Test]
        public void TheResultsFinalBoard_IsWhatTheNextPlanningStartsFrom()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator((turn, state) => state.Fighters[Side.Right].Hp -= 30f));
            authority.Start();
            authority.Send(Side.Left, new SetBodyMoveCommand(BodyMove.Lunge));
            ReadyBoth(authority);

            authority.Tick();

            TurnResult result = _results[0];
            Assert.That(result.TurnIndex, Is.EqualTo(0));
            Assert.That(result.FinalBoard.State.TurnIndex, Is.EqualTo(1), "the board for the next turn");
            Assert.That(result.FinalBoard.State.Fighters[Side.Right].Hp, Is.EqualTo(70f));
            Assert.That(result.FinalBoard.State.Fighters[Side.Right].ConsecutiveIdleTurns, Is.EqualTo(1), "right did nothing: the rules count it");
            Assert.That(result.FinalBoard.State.Fighters[Side.Left].ConsecutiveIdleTurns, Is.EqualTo(0));
            Assert.That(_windows[1].Board.State.Fighters[Side.Right].Hp, Is.EqualTo(70f));
            Assert.That(_windows[1].TurnIndex, Is.EqualTo(1));
        }

        // --- Ending the match ------------------------------------------------------------------

        [Test]
        public void AKo_RaisesTheResult_ThenTheEnd_AndNothingFurther()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator((turn, state) => state.Fighters[Side.Right].Hp = 0f));
            authority.Start();
            authority.Send(Side.Left, new SetBodyMoveCommand(BodyMove.Lunge));
            authority.Send(Side.Right, new SetBodyMoveCommand(BodyMove.Jump));
            ReadyBoth(authority);
            _log.Clear();

            authority.Tick();

            Assert.That(_log, Is.EqualTo(new[] { "Resolved:0", "MatchEnded" }));
            Assert.That(_outcome.Winner, Is.EqualTo(Side.Left));
            Assert.That(_outcome.Reason, Is.EqualTo(MatchEndReason.KnockOut));
            Assert.That(authority.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(authority.Outcome, Is.SameAs(_outcome));

            authority.Tick();
            Assert.That(authority.Send(Side.Left, new SetReadyCommand(true)).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
            Assert.That(_log, Has.Count.EqualTo(2), "nothing happens after the end");
        }

        [Test]
        public void ADoubleKo_GoesToSuddenDeath_AndThePlanningWindowSaysSo()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator((turn, state) =>
            {
                state.Fighters[Side.Left].Hp = 0f;
                state.Fighters[Side.Right].Hp = 0f;
            }));
            authority.Start();
            ReadyBoth(authority);

            authority.Tick();

            Assert.That(_outcome, Is.Null);
            Assert.That(_results[0].FinalBoard.State.IsSuddenDeath, Is.True);
            PlanningWindow window = _windows[1];
            Assert.That(window.IsSuddenDeath, Is.True);
            Assert.That(window.Board.State.Fighters[Side.Left].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            Assert.That(window.Board.State.Fighters[Side.Right].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
        }

        // --- The playback hold (ARCHITECTURE §5) -----------------------------------------------

        [Test]
        public void WithoutAHold_PlanningStartsAtOnce_WithAFullTimer()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator());
            authority.Start();
            ReadyBoth(authority);
            _clock.Advance(3.0);

            authority.Tick();

            Assert.That(_windows[1].DeadlineSeconds, Is.EqualTo(StartSeconds + 3.0 + 12.0));
        }

        [Test]
        public void ClientReport_HoldsPlanningUntilThePlaybackIsReported()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator(),
                new PlaybackHoldSettings { Mode = PlaybackHoldMode.ClientReport });
            authority.Start();
            ReadyBoth(authority);
            authority.Tick();
            Assert.That(_windows, Has.Count.EqualTo(1), "the result is out, but the next planning waits");
            Assert.That(authority.Phase, Is.EqualTo(MatchPhase.TurnStart));

            _clock.Advance(5.0); // playback takes 5 s
            authority.Tick();
            Assert.That(_windows, Has.Count.EqualTo(1));

            authority.NotifyPlaybackFinished(0);
            Assert.That(_windows, Has.Count.EqualTo(1), "released on the next Tick, never inside the call");
            authority.Tick();

            Assert.That(_windows, Has.Count.EqualTo(2));
            Assert.That(_windows[1].DeadlineSeconds, Is.EqualTo(StartSeconds + 5.0 + 12.0), "the timer starts after playback, not before");
        }

        [Test]
        public void ClientReport_IgnoresAReportForAnotherTurn()
        {
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator(),
                new PlaybackHoldSettings { Mode = PlaybackHoldMode.ClientReport });
            authority.Start();
            ReadyBoth(authority);
            authority.Tick();

            authority.NotifyPlaybackFinished(7);
            authority.Tick();

            Assert.That(_windows, Has.Count.EqualTo(1));
        }

        [Test]
        public void ClientReport_CarriesOnWhenTheClientNeverReports()
        {
            var hold = new PlaybackHoldSettings { Mode = PlaybackHoldMode.ClientReport, ClientReportTimeoutSeconds = 8f };
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator(), hold);
            authority.Start();
            ReadyBoth(authority);
            authority.Tick();

            _clock.Set(StartSeconds + 8.0 - 0.001);
            authority.Tick();
            Assert.That(_windows, Has.Count.EqualTo(1));

            _clock.Set(StartSeconds + 8.0);
            authority.Tick();
            Assert.That(_windows, Has.Count.EqualTo(2), "a dead client cannot stall the match");
        }

        [Test]
        public void TimelineDuration_WaitsForTheRecordingPlusTheExtra()
        {
            var hold = new PlaybackHoldSettings { Mode = PlaybackHoldMode.TimelineDuration, ExtraSeconds = 0.5f };
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator(timelineSeconds: 2.0), hold);
            authority.Start();
            ReadyBoth(authority);
            authority.Tick();

            _clock.Set(StartSeconds + 2.5 - 0.001);
            authority.Tick();
            Assert.That(_windows, Has.Count.EqualTo(1));

            _clock.Set(StartSeconds + 2.5);
            authority.Tick();
            Assert.That(_windows, Has.Count.EqualTo(2));
            Assert.That(_windows[1].DeadlineSeconds, Is.EqualTo(StartSeconds + 2.5 + 12.0));
        }

        [Test]
        public void TimelineDuration_IgnoresClientReports()
        {
            var hold = new PlaybackHoldSettings { Mode = PlaybackHoldMode.TimelineDuration, ExtraSeconds = 0.5f };
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator(timelineSeconds: 2.0), hold);
            authority.Start();
            ReadyBoth(authority);
            authority.Tick();

            authority.NotifyPlaybackFinished(0);
            authority.Tick();

            Assert.That(_windows, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheHold_AlsoAppliesBeforeASuddenDeathTurn()
        {
            var hold = new PlaybackHoldSettings { Mode = PlaybackHoldMode.ClientReport };
            LocalTurnAuthority authority = Create(new ScriptedTurnSimulator((turn, state) =>
            {
                state.Fighters[Side.Left].Hp = 0f;
                state.Fighters[Side.Right].Hp = 0f;
            }), hold);
            authority.Start();
            ReadyBoth(authority);
            authority.Tick();

            Assert.That(authority.Phase, Is.EqualTo(MatchPhase.SuddenDeathSetup));
            Assert.That(_windows, Has.Count.EqualTo(1));

            authority.NotifyPlaybackFinished(0);
            authority.Tick();

            Assert.That(_windows[1].IsSuddenDeath, Is.True);
        }

        [Test]
        public void TheHold_DoesNotDelayTheEndOfTheMatch()
        {
            LocalTurnAuthority authority = Create(
                new ScriptedTurnSimulator((turn, state) => state.Fighters[Side.Right].Hp = 0f),
                new PlaybackHoldSettings { Mode = PlaybackHoldMode.ClientReport });
            authority.Start();
            authority.Send(Side.Left, new SetBodyMoveCommand(BodyMove.Lunge));
            authority.Send(Side.Right, new SetBodyMoveCommand(BodyMove.Jump));
            ReadyBoth(authority);

            authority.Tick();

            Assert.That(_outcome, Is.Not.Null, "the client holds the end of the match itself until its playback is done");
        }
    }
}
