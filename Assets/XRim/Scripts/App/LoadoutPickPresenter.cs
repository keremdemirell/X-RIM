using System;
using System.Collections.Generic;
using XRim.Bots;
using XRim.Core;
using XRim.Presentation.UI;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.App
{
    /// <summary>
    /// Draws the pre-match pick (GDD §6) on the match screens and turns taps into <see cref="LoadoutPickSession"/> steps. The
    /// session holds every rule (3 slots, the shield counting, the optional shield requirement, blind picks, the reveal); this only
    /// maps its stage to a screen: the cover between hot-seat players, the weapon cards with a lock-in button, then both loadouts.
    /// <see cref="Create"/> locks the bots' random loadouts in first, so a bot's picks stay hidden until the reveal.
    /// </summary>
    public sealed class LoadoutPickPresenter : IDisposable
    {
        private static readonly Side[] BothSides = { Side.Left, Side.Right };

        private readonly IMatchScreensView _screens;
        private readonly RulesSettings _rules;
        private readonly LoadoutPickSession _session;
        private readonly MatchMode _mode;
        private readonly PerSide<string> _names;
        private readonly bool _remoteOpponent;
        private readonly Side _localSide;
        private LoadoutReveal _remoteReveal;
        private string _notice;
        private bool _begun;
        private bool _finished;

        /// <summary>The picks are made and confirmed: both loadouts, to start the match with.</summary>
        public event Action<LoadoutReveal> Completed;

        /// <summary>
        /// Online (Session 19): this device's player locked their pick in. It goes to the server, which holds it blind (GDD §6) until the
        /// opponent has picked; <see cref="ShowRemoteReveal"/> then shows both. Nothing is drawn meanwhile: the caller shows the wait.
        /// </summary>
        public event Action<IReadOnlyList<WeaponId>> LocalPicked;

        /// <param name="localSide">Online: the side this device's player plays (the other picks on their own device).</param>
        public LoadoutPickPresenter(IMatchScreensView screens, RulesSettings rules, LoadoutPickSession session, MatchMode mode,
            PerSide<string> names, Side localSide = Side.Left)
        {
            _screens = Guard.NotNull(screens, nameof(screens));
            _rules = Guard.NotNull(rules, nameof(rules));
            _session = Guard.NotNull(session, nameof(session));
            _mode = mode;
            _names = Guard.NotNull(names, nameof(names));
            _remoteOpponent = mode == MatchMode.Online;
            _localSide = localSide;
        }

        /// <summary>Online: both loadouts as the server revealed them (GDD §6, Decided); the reveal shows them and its button starts the match.</summary>
        public void ShowRemoteReveal(LoadoutReveal reveal)
        {
            if (!_remoteOpponent || _finished) return;
            _remoteReveal = Guard.NotNull(reveal, nameof(reveal));
            RenderReveal();
        }

        public LoadoutPickSession Session => _session;

        /// <summary>
        /// Starts a pick for a match mode: the humans on this device pick, the bots lock a random valid loadout in at once (when
        /// they cannot, the default for the owned weapons).
        /// </summary>
        /// <param name="humanSide">The side the human plays against the bot, or online (the opponent picks on their own device).</param>
        /// <param name="previousPicks">The last match's picks, to start from (a rematch); null for none.</param>
        public static LoadoutPickPresenter Create(IMatchScreensView screens, RulesSettings rules, IWeaponOwnership ownership, MatchMode mode,
            Side humanSide, IBotLoadoutPicker botPicker, IRandom random, PerSide<IReadOnlyList<WeaponId>> previousPicks)
        {
            Guard.NotNull(rules, nameof(rules));
            Guard.NotNull(ownership, nameof(ownership));
            Guard.NotNull(botPicker, nameof(botPicker));
            Guard.NotNull(random, nameof(random));
            if (mode == MatchMode.Sandbox) throw new ArgumentException("The sandbox does not pick loadouts.", nameof(mode));

            var selection = new LoadoutSelection(rules, ownership);
            var humans = new List<Side>();
            foreach (Side side in BothSides)
            {
                if (IsHuman(mode, side, humanSide))
                {
                    humans.Add(side);
                    continue;
                }

                // Online the other side picks on its own device: a stand-in fills its slot here and is never shown (the server reveals the real one).
                if (mode == MatchMode.Online || !botPicker.TryPick(rules, ownership, random, out IReadOnlyList<WeaponId> loadout))
                {
                    loadout = LoadoutOptions.DefaultFor(rules, ownership);
                }

                if (!selection.TryLockIn(side, loadout, out IReadOnlyList<string> issues))
                    throw new InvalidOperationException("The " + side + " bot has no valid loadout: " + string.Join(" ", issues));
            }

            var session = new LoadoutPickSession(rules, ownership, selection, humans, mode == MatchMode.HotSeat, previousPicks);
            return new LoadoutPickPresenter(screens, rules, session, mode, MatchLabels.FighterNames(mode, humanSide), humanSide);
        }

        /// <summary>Shows the first step and listens to the screen.</summary>
        public void Begin()
        {
            if (_begun) return;
            _begun = true;
            _screens.CoverConfirmed += OnCoverConfirmed;
            _screens.LoadoutChoiceTapped += OnChoiceTapped;
            _screens.LoadoutConfirmed += OnConfirmed;
            _screens.LoadoutRevealConfirmed += OnRevealConfirmed;
            Render();
        }

        public void Dispose()
        {
            if (!_begun) return;
            _begun = false;
            _screens.CoverConfirmed -= OnCoverConfirmed;
            _screens.LoadoutChoiceTapped -= OnChoiceTapped;
            _screens.LoadoutConfirmed -= OnConfirmed;
            _screens.LoadoutRevealConfirmed -= OnRevealConfirmed;
        }

        private static bool IsHuman(MatchMode mode, Side side, Side humanSide)
        {
            switch (mode)
            {
                case MatchMode.HotSeat: return true;
                case MatchMode.HumanVsBot:
                case MatchMode.Online:
                    return side == humanSide;
                case MatchMode.BotVsBot: return false;
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, "This mode has no loadout pick.");
            }
        }

        private void OnCoverConfirmed()
        {
            if (_finished || !_session.ConfirmHandOver()) return;
            _notice = null;
            Render();
        }

        private void OnChoiceTapped(int index)
        {
            if (_finished || _session.Stage != LoadoutPickStage.Picking) return;
            IReadOnlyList<WeaponId> choices = _session.Choices;
            if (index < 0 || index >= choices.Count) return;

            _notice = _session.Toggle(choices[index]) ? null : MatchLabels.LoadoutSlotsFull;
            Render();
        }

        private void OnConfirmed()
        {
            if (_finished || _session.Stage != LoadoutPickStage.Picking) return;
            _notice = null;
            _session.Confirm();
            if (_remoteOpponent && _session.Stage == LoadoutPickStage.Reveal)
            {
                LocalPicked?.Invoke(_session.Result.Of(_localSide));
                return;
            }

            Render();
        }

        private void OnRevealConfirmed()
        {
            if (_finished || (_remoteOpponent && _remoteReveal == null) || !_session.ConfirmReveal()) return;
            _finished = true;
            Completed?.Invoke(_remoteReveal ?? _session.Result);
        }

        private void Render()
        {
            switch (_session.Stage)
            {
                case LoadoutPickStage.HandOver:
                    _screens.ShowCover(MatchLabels.CoverPlayer(_session.PickingSide), MatchLabels.LoadoutCoverInstruction,
                        MatchLabels.LoadoutCoverAction);
                    break;
                case LoadoutPickStage.Picking:
                    RenderPicker();
                    break;
                case LoadoutPickStage.Reveal:
                    if (!_remoteOpponent || _remoteReveal != null) RenderReveal(); // online, the wait is the caller's screen
                    break;
            }
        }

        private void RenderPicker()
        {
            IReadOnlyList<WeaponId> options = _session.Choices;
            var choices = new List<LoadoutChoice>(options.Count);
            foreach (WeaponId id in options)
            {
                WeaponStats stats = _rules.FindWeapon(id);
                choices.Add(new LoadoutChoice(MatchLabels.WeaponTitle(stats), MatchLabels.WeaponDetail(stats), SlotOf(id)));
            }

            int missing = RuleConstants.LoadoutSlotCount - _session.Picks.Count;
            string confirm = _session.CanConfirm ? MatchLabels.LoadoutLockIn
                : missing > 0 ? MatchLabels.LoadoutPickMore(missing)
                : MatchLabels.LoadoutShieldNeeded;
            string note = _notice ?? _session.Problem ?? (options.Count <= RuleConstants.LoadoutSlotCount ? MatchLabels.LoadoutFewWeapons : MatchLabels.LoadoutNote);
            _screens.ShowLoadoutPicker(MatchLabels.LoadoutTitle(_mode, _session.PickingSide), note, choices, confirm, _session.CanConfirm);
        }

        private void RenderReveal()
        {
            LoadoutReveal reveal = _remoteReveal ?? _session.Result;
            _screens.ShowLoadoutReveal(MatchLabels.RevealTitle, _names.Left, DisplayNames(reveal.Left), _names.Right,
                DisplayNames(reveal.Right), MatchLabels.RevealAction);
        }

        /// <summary>The slot a weapon fills (1 is the starting weapon), 0 when it is not picked.</summary>
        private int SlotOf(WeaponId id)
        {
            IReadOnlyList<WeaponId> picks = _session.Picks;
            for (int i = 0; i < picks.Count; i++)
            {
                if (picks[i] == id) return i + 1;
            }

            return 0;
        }

        private List<string> DisplayNames(IReadOnlyList<WeaponId> loadout)
        {
            var names = new List<string>(loadout.Count);
            foreach (WeaponId id in loadout)
            {
                WeaponStats stats = _rules.FindWeapon(id);
                names.Add(stats != null ? MatchLabels.WeaponTitle(stats) : id.Value.ToUpperInvariant());
            }

            return names;
        }
    }
}
