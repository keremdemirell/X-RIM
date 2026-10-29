using System.Globalization;
using XRim.Core;
using XRim.Networking;
using XRim.Presentation.Playback;
using XRim.Presentation.UI;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Recording;

namespace XRim.App
{
    /// <summary>
    /// Feeds the match HUD from the client flow, for one match: each fighter's HP under its own dummy (it follows the view flip,
    /// GDD §4), the turn counter, the sudden-death banner (§14: 1 HP each, the first clean hit wins, and which of its capped tries
    /// this is, designer 2026-10-11), and a signature move's name shouted over its fighter while that turn plays (§8).
    /// While planning it shows the frozen board; during and after a turn's playback it shows that turn, the HP dropping as playback
    /// passes each hit. Plain data in, plain data out: no rule lives here.
    /// </summary>
    public sealed class MatchHudPresenter
    {
        public const string SuddenDeathTitle = "SUDDEN DEATH";

        private readonly IMatchHudView _view;
        private readonly ClientMatchFlow _flow;
        private readonly RulesSettings _rules;
        private readonly PerSide<string> _names;
        private readonly PerSide<Shown> _shown = new PerSide<Shown>(null, null);
        private string _shownShoutScreenLeft;
        private string _shownShoutScreenRight;
        private string _shownTurn;
        private string _shownBannerDetail;
        private string _shownBannerTitle;
        private string _noticeTitle = string.Empty;
        private string _noticeDetail = string.Empty;

        public MatchHudPresenter(IMatchHudView view, ClientMatchFlow flow, RulesSettings rules, PerSide<string> names)
        {
            _view = Guard.NotNull(view, nameof(view));
            _flow = Guard.NotNull(flow, nameof(flow));
            _rules = Guard.NotNull(rules, nameof(rules));
            _names = Guard.NotNull(names, nameof(names));
        }

        /// <param name="playbackTime">Where playback of the current turn is; null when it is not playing.</param>
        /// <param name="viewFlipped">The arena is drawn mirrored, so the right dummy is on the left of the screen.</param>
        /// <summary>
        /// Online (Session 19): a connection notice for the banner ("Opponent reconnecting", with the wait left), shown instead of the
        /// sudden-death banner while it lasts; empty clears it. Applied on the next <see cref="Tick"/>.
        /// </summary>
        public void SetNotice(string title, string detail)
        {
            _noticeTitle = title ?? string.Empty;
            _noticeDetail = detail ?? string.Empty;
        }

        public void Tick(SimTime? playbackTime, bool viewFlipped)
        {
            TurnResult turn = _flow.CurrentResult;
            PlanningWindow window = _flow.CurrentWindow;

            // The turn being (or just) played is newer than the board once it resolved; a new planning window replaces it.
            bool showTurn = turn != null && (window == null || turn.TurnIndex >= window.TurnIndex);
            int turnIndex = showTurn ? turn.TurnIndex : window != null ? window.TurnIndex : 0;
            MatchState shownState = showTurn ? turn.Report.ResolvedState : window?.Board.State;
            bool suddenDeath = showTurn ? turn.Report.ResolvedState.IsSuddenDeath : window != null && window.IsSuddenDeath;

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                float hp = showTurn ? PlaybackHitPoints.HpAt(turn, playbackTime ?? turn.Timeline.Duration, side)
                    : window != null ? window.Board.State.Fighters[side].Hp
                    : _rules.Damage.MaxHp;
                ShowFighter(side, hp, (side == Side.Left) != viewFlipped);
            }

            ShowShouts(showTurn && playbackTime.HasValue ? turn : null, viewFlipped);
            ShowTurn(turnIndex, suddenDeath, shownState != null ? shownState.SuddenDeathRepeats : 0);
        }

        /// <summary>A signature move's name as the HUD shouts it (§8): "HEAD IMPACT SIMULATION #404!".</summary>
        public static string ShoutText(string moveName) => string.IsNullOrEmpty(moveName) ? string.Empty : moveName.ToUpperInvariant() + "!";

        /// <summary>The shout for the side's signature move in that turn; empty when it drew its own path (the move stays hidden until the turn plays).</summary>
        private static string SignatureShoutOf(TurnResult turn, Side side)
        {
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (matchEvent is SignatureUsedEvent used && used.Side == side) return ShoutText(used.DisplayName);
            }

            return string.Empty;
        }

        /// <summary>
        /// Each half of the screen shouts the move of the fighter drawn there (it follows the view flip like the bars); nothing while no
        /// turn plays. Kept per half, since a flip swaps which fighter a half belongs to.
        /// </summary>
        private void ShowShouts(TurnResult playing, bool viewFlipped)
        {
            Side onScreenLeft = viewFlipped ? Side.Right : Side.Left;
            Side onScreenRight = onScreenLeft.Opponent();
            string left = playing != null ? SignatureShoutOf(playing, onScreenLeft) : string.Empty;
            string right = playing != null ? SignatureShoutOf(playing, onScreenRight) : string.Empty;

            if (left != _shownShoutScreenLeft)
            {
                _shownShoutScreenLeft = left;
                _view.ShowSignatureShout(onScreenLeft, left, true);
            }

            if (right != _shownShoutScreenRight)
            {
                _shownShoutScreenRight = right;
                _view.ShowSignatureShout(onScreenRight, right, false);
            }
        }

        /// <summary>
        /// The banner's line under "SUDDEN DEATH": 1 HP each, the first clean hit wins, and which try this is of the capped number (after the
        /// last, the fallback decides, P6).
        /// </summary>
        public static string SuddenDeathDetail(int repeatsSoFar, int maxRepeats) =>
            string.Format(CultureInfo.InvariantCulture, "1 HP each: the first clean hit wins · try {0} of {1}", repeatsSoFar + 1, maxRepeats + 1);

        private void ShowFighter(Side side, float hp, bool onScreenLeft)
        {
            var shown = new Shown(hp, onScreenLeft);
            if (shown.Equals(_shown[side])) return;
            _shown[side] = shown;
            _view.ShowFighter(side, _names[side], hp, _rules.Damage.MaxHp, onScreenLeft);
        }

        private void ShowTurn(int turnIndex, bool suddenDeath, int repeatsSoFar)
        {
            string text = suddenDeath
                ? SuddenDeathTitle
                : string.Format(CultureInfo.InvariantCulture, "TURN {0} / {1}", turnIndex + 1, _rules.Match.TurnCap);
            if (text != _shownTurn)
            {
                _shownTurn = text;
                _view.ShowTurn(text);
            }

            // A connection notice (online, Session 19) takes the banner while it lasts; sudden death's comes back after it.
            bool notice = _noticeTitle.Length > 0;
            string title = notice ? _noticeTitle : suddenDeath ? SuddenDeathTitle : string.Empty;
            string detail = notice ? _noticeDetail
                : suddenDeath ? SuddenDeathDetail(repeatsSoFar, _rules.SuddenDeath.MaxRepeatsWithoutHit) : string.Empty;
            if (_shownBannerTitle == title && _shownBannerDetail == detail) return;
            _shownBannerTitle = title;
            _shownBannerDetail = detail;
            _view.ShowBanner(title, detail);
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
