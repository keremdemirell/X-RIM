using System.Globalization;
using XRim.Core;
using XRim.Networking;
using XRim.Presentation.Playback;
using XRim.Presentation.UI;
using XRim.Rules.Settings;
using XRim.Simulation.Recording;

namespace XRim.App
{
    /// <summary>
    /// Feeds the match HUD from the client flow, for one match: each fighter's HP under its own dummy (it follows the view flip,
    /// GDD §4), the turn counter, and the sudden-death banner (§14; its open parts are placeholders until Session 12).
    /// While planning it shows the frozen board; during and after a turn's playback it shows that turn, the HP dropping as playback
    /// passes each hit. Plain data in, plain data out: no rule lives here.
    /// </summary>
    public sealed class MatchHudPresenter
    {
        public const string SuddenDeathTitle = "SUDDEN DEATH";
        public const string SuddenDeathDetail = "1 HP each: the first hit wins. Board, ties and no-hit turns are placeholders until Session 12.";

        private readonly IMatchHudView _view;
        private readonly ClientMatchFlow _flow;
        private readonly RulesSettings _rules;
        private readonly PerSide<string> _names;
        private readonly PerSide<Shown> _shown = new PerSide<Shown>(null, null);
        private string _shownTurn;
        private bool? _shownSuddenDeath;

        public MatchHudPresenter(IMatchHudView view, ClientMatchFlow flow, RulesSettings rules, PerSide<string> names)
        {
            _view = Guard.NotNull(view, nameof(view));
            _flow = Guard.NotNull(flow, nameof(flow));
            _rules = Guard.NotNull(rules, nameof(rules));
            _names = Guard.NotNull(names, nameof(names));
        }

        /// <param name="playbackTime">Where playback of the current turn is; null when it is not playing.</param>
        /// <param name="viewFlipped">The arena is drawn mirrored, so the right dummy is on the left of the screen.</param>
        public void Tick(SimTime? playbackTime, bool viewFlipped)
        {
            TurnResult turn = _flow.CurrentResult;
            PlanningWindow window = _flow.CurrentWindow;

            // The turn being (or just) played is newer than the board once it resolved; a new planning window replaces it.
            bool showTurn = turn != null && (window == null || turn.TurnIndex >= window.TurnIndex);
            int turnIndex = showTurn ? turn.TurnIndex : window != null ? window.TurnIndex : 0;
            bool suddenDeath = showTurn ? turn.Report.ResolvedState.IsSuddenDeath : window != null && window.IsSuddenDeath;

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                float hp = showTurn ? PlaybackHitPoints.HpAt(turn, playbackTime ?? turn.Timeline.Duration, side)
                    : window != null ? window.Board.State.Fighters[side].Hp
                    : _rules.Damage.MaxHp;
                ShowFighter(side, hp, (side == Side.Left) != viewFlipped);
            }

            ShowTurn(turnIndex, suddenDeath);
        }

        private void ShowFighter(Side side, float hp, bool onScreenLeft)
        {
            var shown = new Shown(hp, onScreenLeft);
            if (shown.Equals(_shown[side])) return;
            _shown[side] = shown;
            _view.ShowFighter(side, _names[side], hp, _rules.Damage.MaxHp, onScreenLeft);
        }

        private void ShowTurn(int turnIndex, bool suddenDeath)
        {
            string text = suddenDeath
                ? SuddenDeathTitle
                : string.Format(CultureInfo.InvariantCulture, "TURN {0} / {1}", turnIndex + 1, _rules.Match.TurnCap);
            if (text != _shownTurn)
            {
                _shownTurn = text;
                _view.ShowTurn(text);
            }

            if (_shownSuddenDeath == suddenDeath) return;
            _shownSuddenDeath = suddenDeath;
            _view.ShowBanner(suddenDeath ? SuddenDeathTitle : string.Empty, suddenDeath ? SuddenDeathDetail : string.Empty);
        }

        private sealed class Shown
        {
            private readonly float _hp;
            private readonly bool _onScreenLeft;

            public Shown(float hp, bool onScreenLeft)
            {
                _hp = hp;
                _onScreenLeft = onScreenLeft;
            }

            public bool Equals(Shown other) => other != null && other._hp == _hp && other._onScreenLeft == _onScreenLeft;
        }
    }
}
