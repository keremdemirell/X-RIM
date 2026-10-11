using System;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Dummy;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Playback
{
    /// <summary>
    /// Shows the duel on the visible dummies: the frozen board between turns, and a resolved turn played back through a
    /// <see cref="TimelinePlayer"/> (ARCHITECTURE §1: simulated up front, then played). <see cref="Finished"/> fires once
    /// per play, when playback reaches its end, so the client can let the next planning phase start. While the player loops
    /// or is paused the turn never finishes, so the match waits on it (debug Playback tab).
    /// Reactors (<see cref="AddReactor"/>: feel, later VFX, audio, camera) hear each event as playback passes it, and a
    /// <see cref="Pacer"/> may bend the pace (hitstop); a turn does not finish while the pacer holds it.
    /// </summary>
    public sealed class TurnPlayback
    {
        private readonly ArenaSpace _space;
        private PerSide<DummyView> _views;

        public event Action<TurnResult> Finished;

        public TimelinePlayer Player { get; } = new TimelinePlayer();

        /// <summary>The turn being played or last played; null before the first.</summary>
        public TurnResult Current { get; private set; }

        public bool IsPlaying { get; private set; }

        /// <summary>The drawn dummies (reactors read where their parts are, for effects).</summary>
        public PerSide<DummyView> Views => _views;

        /// <summary>Bends the pace of playback (hitstop); null plays at the player's speed.</summary>
        public IPlaybackPacer Pacer { get; set; }

        public TurnPlayback(PerSide<DummyView> views, ArenaSpace space)
        {
            _views = Guard.NotNull(views, nameof(views));
            _space = space;
            Player.PoseChanged += ShowPose;
        }

        /// <summary>The reactor hears every event as playback passes it, in time order.</summary>
        public void AddReactor(IMatchEventReactor reactor) => Player.EventReached += Guard.NotNull(reactor, nameof(reactor)).OnMatchEvent;

        public void RemoveReactor(IMatchEventReactor reactor) => Player.EventReached -= Guard.NotNull(reactor, nameof(reactor)).OnMatchEvent;

        /// <summary>Swaps the drawn dummies (for example after the ragdoll segmentation changed).</summary>
        public void SetViews(PerSide<DummyView> views) => _views = Guard.NotNull(views, nameof(views));

        /// <summary>Plays the current turn again from the start (it finishes again at its end).</summary>
        public void Replay()
        {
            if (Current != null) Play(Current);
        }

        public void Play(TurnResult result)
        {
            Current = Guard.NotNull(result, nameof(result));
            ShowWeapons(result.FinalBoard.State);
            IsPlaying = true;
            Pacer?.Reset();
            Player.Play(result.Timeline);
            FinishIfDone();
        }

        /// <summary>Stops playing and shows a frozen board (match start, planning).</summary>
        public void ShowBoard(BoardSnapshot board)
        {
            Guard.NotNull(board, nameof(board));
            IsPlaying = false;
            ShowWeapons(board.State);
            ShowPose(board.Pose);
        }

        public void ShowWeapon(Side side, WeaponId weapon) => _views[side].ShowWeapon(weapon);

        /// <summary>Advances by real (screen) time; the player's speed and pause apply.</summary>
        public void Advance(float realDeltaSeconds)
        {
            if (!IsPlaying) return;
            float delta = Pacer != null && !Player.IsPaused ? Pacer.Pace(realDeltaSeconds) : realDeltaSeconds;
            Player.Advance(delta);
            FinishIfDone();
        }

        private void FinishIfDone()
        {
            if (!IsPlaying || Player.IsPaused || Player.Loop || !Player.IsFinished) return;
            if (Pacer != null && Pacer.IsHolding) return; // a hit on the last frame still gets its freeze
            IsPlaying = false;
            Finished?.Invoke(Current);
        }

        private void ShowWeapons(MatchState state)
        {
            ShowWeapon(Side.Left, state.Fighters[Side.Left].CurrentWeapon);
            ShowWeapon(Side.Right, state.Fighters[Side.Right].CurrentWeapon);
        }

        /// <summary>Both dummies, each drawing its own severed limbs where they lie (GDD §12).</summary>
        private void ShowPose(PoseSnapshot pose)
        {
            _views.Left.ApplyPose(pose.Left, Side.Left, pose.SeveredLimbs, _space);
            _views.Right.ApplyPose(pose.Right, Side.Right, pose.SeveredLimbs, _space);
        }
    }
}
