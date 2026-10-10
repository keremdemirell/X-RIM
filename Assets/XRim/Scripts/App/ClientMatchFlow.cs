using System;
using XRim.Core;
using XRim.Networking;
using XRim.Rules.Match;
using XRim.Simulation.Recording;

namespace XRim.App
{
    /// <summary>
    /// The client-side flow: Idle → Planning → WaitingForAuthority → Playback → (next turn) … → MatchOver. It follows the
    /// authority's events and holds the next planning phase or the match end until the current turn's playback has finished,
    /// so players always see the result before planning again. When playback finishes it tells the authority
    /// (<see cref="ITurnAuthority.NotifyPlaybackFinished"/>), which starts the next planning timer only then, so the replay never
    /// eats planning time (ARCHITECTURE §5). It mirrors the authority and never decides anything.
    /// </summary>
    public sealed class ClientMatchFlow : IDisposable
    {
        private readonly ITurnAuthority _authority;
        private PlanningWindow _pendingWindow;
        private MatchOutcome _pendingOutcome;

        public event Action<ClientPhase> PhaseChanged;

        public ClientPhase Phase { get; private set; } = ClientPhase.Idle;
        public PlanningWindow CurrentWindow { get; private set; }
        public TurnResult CurrentResult { get; private set; }
        public MatchOutcome Outcome { get; private set; }

        public ClientMatchFlow(ITurnAuthority authority)
        {
            _authority = Guard.NotNull(authority, nameof(authority));
            _authority.PlanningStarted += OnPlanningStarted;
            _authority.PlanningLocked += OnPlanningLocked;
            _authority.TurnResolved += OnTurnResolved;
            _authority.MatchEnded += OnMatchEnded;
        }

        public void Dispose()
        {
            _authority.PlanningStarted -= OnPlanningStarted;
            _authority.PlanningLocked -= OnPlanningLocked;
            _authority.TurnResolved -= OnTurnResolved;
            _authority.MatchEnded -= OnMatchEnded;
        }

        /// <summary>
        /// Called by presentation when the timeline player finishes the current turn: shows what comes next and reports the
        /// playback to the authority. Ignored outside playback (a debug replay watched while planning).
        /// </summary>
        public void OnPlaybackFinished()
        {
            if (Phase != ClientPhase.Playback) return;

            ShowWhatFollowsPlayback();
            _authority.NotifyPlaybackFinished(CurrentResult.TurnIndex);
        }

        private void ShowWhatFollowsPlayback()
        {
            if (_pendingOutcome != null)
            {
                Outcome = _pendingOutcome;
                _pendingOutcome = null;
                SetPhase(ClientPhase.MatchOver);
            }
            else if (_pendingWindow != null)
            {
                CurrentWindow = _pendingWindow;
                _pendingWindow = null;
                SetPhase(ClientPhase.Planning);
            }
            else
            {
                SetPhase(ClientPhase.WaitingForAuthority);
            }
        }

        private void OnPlanningStarted(PlanningWindow window)
        {
            if (Phase == ClientPhase.Playback)
            {
                _pendingWindow = window;
                return;
            }

            CurrentWindow = window;
            SetPhase(ClientPhase.Planning);
        }

        private void OnPlanningLocked(int turnIndex) => SetPhase(ClientPhase.WaitingForAuthority);

        private void OnTurnResolved(TurnResult result)
        {
            CurrentResult = result;
            SetPhase(ClientPhase.Playback);
        }

        private void OnMatchEnded(MatchOutcome outcome)
        {
            if (Phase == ClientPhase.Playback)
            {
                _pendingOutcome = outcome;
                return;
            }

            Outcome = outcome;
            SetPhase(ClientPhase.MatchOver);
        }

        private void SetPhase(ClientPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
