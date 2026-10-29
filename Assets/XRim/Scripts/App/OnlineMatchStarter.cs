using System;
using System.Collections.Generic;
using XRim.Bots;
using XRim.Core;
using XRim.Networking.Client;
using XRim.Networking.Protocol;
using XRim.Presentation.UI;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.App
{
    /// <summary>
    /// The way into an online match on this device (Session 19), before there is anything to plan. It drives an <see cref="OnlineMatchJoin"/>
    /// and the match screens:
    /// <list type="number">
    /// <item>"Finding an opponent" (a LEAVE button) while the server pairs this player with another.</item>
    /// <item>The usual loadout picker (GDD §6, blind), built from the settings the server plays with (its text, read with an
    /// <see cref="ISettingsTextCodec"/>) and the weapons this account owns (designer 2026-10-25, Q1: the phone's own claim for now).</item>
    /// <item>"Waiting for your opponent" while the server holds the pick until both have picked.</item>
    /// <item>The reveal of both loadouts as the server sent them; its button raises <see cref="Ready"/>.</item>
    /// </list>
    /// When the link ends before that (no server, a version mismatch, the opponent left), <see cref="Failed"/> says why in words; LEAVE
    /// raises <see cref="Cancelled"/>. Call <see cref="Tick"/> every frame after <see cref="Begin"/>.
    /// </summary>
    public sealed class OnlineMatchStarter : IDisposable
    {
        private readonly IMatchScreensView _screens;
        private readonly ISettingsTextCodec _settingsCodec;
        private readonly IWeaponOwnership _ownership;
        private readonly PerSide<IReadOnlyList<WeaponId>> _previousPicks;
        private readonly string _findingNote;
        private LoadoutPickPresenter _pick;
        private Waiting _shown;
        private bool _begun;
        private bool _revealed;
        private bool _done;

        /// <param name="previousPicks">The last picks, to start the picker from (a rematch); null for none.</param>
        /// <param name="findingNote">The finding screen's note (where the server is).</param>
        public OnlineMatchStarter(IMatchScreensView screens, OnlineMatchJoin join, ISettingsTextCodec settingsCodec, IWeaponOwnership ownership,
            PerSide<IReadOnlyList<WeaponId>> previousPicks, string findingNote)
        {
            _screens = Guard.NotNull(screens, nameof(screens));
            Join = Guard.NotNull(join, nameof(join));
            _settingsCodec = Guard.NotNull(settingsCodec, nameof(settingsCodec));
            _ownership = Guard.NotNull(ownership, nameof(ownership));
            _previousPicks = previousPicks;
            _findingNote = findingNote ?? string.Empty;
        }

        /// <summary>Both loadouts are revealed and confirmed: <see cref="Join"/> can create the match's authority with <see cref="Rules"/> and <see cref="Simulation"/>.</summary>
        public event Action Ready;

        /// <summary>Online play cannot go on; the reason in words for the screen.</summary>
        public event Action<string> Failed;

        /// <summary>The player left before the match started.</summary>
        public event Action Cancelled;

        public OnlineMatchJoin Join { get; }

        /// <summary>The rules the server plays with; null until an opponent is found.</summary>
        public RulesSettings Rules { get; private set; }

        public SimulationSettings Simulation { get; private set; }

        /// <summary>Both loadouts as revealed; null until <see cref="Ready"/>.</summary>
        public LoadoutReveal Reveal { get; private set; }

        private enum Waiting
        {
            Nothing = 0,
            Opponent = 1,
            OpponentsPick = 2,
        }

        public void Begin()
        {
            if (_begun) return;
            _begun = true;
            _screens.CoverConfirmed += OnLeavePressed;
            Show(Waiting.Opponent);
        }

        public void Tick()
        {
            if (!_begun || _done) return;
            Join.Tick();
            switch (Join.Stage)
            {
                case OnlineJoinStage.Picking:
                    if (_pick == null) StartPick();
                    break;
                case OnlineJoinStage.Ready:
                    if (_pick == null) StartPick();
                    if (!_done && !_revealed) RevealBoth();
                    break;
                case OnlineJoinStage.Ended:
                    Fail(Join.Link.EndReason == GoodbyeReason.MatchOver ? MatchLabels.OpponentLeftBeforeStart : MatchLabels.OnlineEndReason(Join.Link.EndReason));
                    break;
            }
        }

        public void Dispose()
        {
            if (_begun) _screens.CoverConfirmed -= OnLeavePressed;
            _begun = false;
            DisposePick();
        }

        private void StartPick()
        {
            try
            {
                _settingsCodec.Read(Join.SettingsText, out RulesSettings rules, out SimulationSettings simulation);
                Rules = rules;
                Simulation = simulation;
            }
            catch (FormatException error)
            {
                Join.Link.Close();
                Fail(MatchLabels.OnlineSettingsUnreadable + " " + error.Message);
                return;
            }

            // The random picker is never asked: online the other side picks on its own device.
            _pick = LoadoutPickPresenter.Create(_screens, Rules, _ownership, MatchMode.Online, Join.Side, new RandomLoadoutPicker(),
                new XorShiftRandom(1u), _previousPicks);
            _pick.LocalPicked += OnLocalPicked;
            _pick.Completed += OnPickCompleted;
            _shown = Waiting.Nothing;
            _pick.Begin();
        }

        private void RevealBoth()
        {
            _revealed = true;
            _shown = Waiting.Nothing;
            MatchSetup setup = Join.Started.Setup;
            _pick.ShowRemoteReveal(new LoadoutReveal(PerSide<IReadOnlyList<WeaponId>>.Create(side => setup.Fighters[side].Loadout)));
        }

        private void OnLocalPicked(IReadOnlyList<WeaponId> loadout)
        {
            Join.Pick(loadout);
            Show(Waiting.OpponentsPick);
        }

        private void OnPickCompleted(LoadoutReveal reveal)
        {
            _done = true;
            Reveal = reveal;
            Ready?.Invoke();
        }

        private void OnLeavePressed()
        {
            if (_done || _shown == Waiting.Nothing) return;
            _done = true;
            Join.Link.Close();
            Cancelled?.Invoke();
        }

        private void Show(Waiting waiting)
        {
            if (_shown == waiting) return;
            _shown = waiting;
            if (waiting == Waiting.Opponent) _screens.ShowCover(MatchLabels.OnlineFindingTitle, _findingNote, MatchLabels.OnlineCancel);
            else _screens.ShowCover(MatchLabels.OnlineWaitingPickTitle, MatchLabels.OnlineWaitingPickNote, MatchLabels.OnlineCancel);
        }

        private void Fail(string reason)
        {
            if (_done) return;
            _done = true;
            Failed?.Invoke(reason);
        }

        private void DisposePick()
        {
            if (_pick == null) return;
            _pick.LocalPicked -= OnLocalPicked;
            _pick.Completed -= OnPickCompleted;
            _pick.Dispose();
            _pick = null;
        }
    }
}
