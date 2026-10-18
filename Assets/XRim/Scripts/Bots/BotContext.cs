using System.Collections.Generic;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using XRim.Simulation.Physics;

namespace XRim.Bots
{
    /// <summary>
    /// What a bot may know when planning (Session 16: public information only, enforced here): the frozen board everyone sees (both poses,
    /// each dummy's <see cref="PublicFighterView"/>), the opponent's public planning state when planning opened (its weapon, Ready, a charged
    /// move only if that is shown), and its own side's limits, signature options, weapon tips and loadout. It never holds the planning window,
    /// the rules state or anything of the opponent's that stays hidden (its signature charge, its options, its plan: plans exist only after
    /// the lock).
    /// </summary>
    public sealed class BotContext
    {
        public BotContext(Side side, PlanningWindow window, IReadOnlyList<WeaponId> loadout, RulesSettings settings, IRandom random)
        {
            Guard.NotNull(window, nameof(window));
            Side = side;
            Loadout = Guard.NotNull(loadout, nameof(loadout));
            Settings = Guard.NotNull(settings, nameof(settings));
            Random = Guard.NotNull(random, nameof(random));
            TurnIndex = window.TurnIndex;
            IsSuddenDeath = window.IsSuddenDeath;
            Constraints = window.Constraints[side];
            Signatures = window.Signatures[side];
            WeaponTips = window.WeaponTips[side];
            Pose = window.Board.Pose.Clone();
            Self = new PublicFighterView(window.Board.State.Fighters[side], window.Board.State.Walls[side]);
            Opponent = new PublicFighterView(window.Board.State.Fighters[side.Opponent()], window.Board.State.Walls[side.Opponent()]);
            OpponentPublicState = window.PublicStates[side.Opponent()];
        }

        public Side Side { get; }
        public IReadOnlyList<WeaponId> Loadout { get; }
        public RulesSettings Settings { get; }
        public IRandom Random { get; }
        public int TurnIndex { get; }
        public bool IsSuddenDeath { get; }

        /// <summary>This side's planning limits for the turn (statuses, lost limbs, sudden-death options).</summary>
        public PlanningConstraints Constraints { get; }

        /// <summary>This side's signature moves and charge (GDD §8); the opponent's stay hidden.</summary>
        public SignatureOptions Signatures { get; }

        /// <summary>Where this side's weapon tip rests in its torso frame: a stroke started there costs no lead-in ink (D3).</summary>
        public IWeaponTipSource WeaponTips { get; }

        /// <summary>Both dummies on the frozen board, as everyone sees them (a copy).</summary>
        public PoseSnapshot Pose { get; }

        public PublicFighterView Self { get; }
        public PublicFighterView Opponent { get; }

        /// <summary>The opponent's weapon and Ready as shown when planning opened (GDD §3, §6).</summary>
        public PublicPlanningState OpponentPublicState { get; }

        /// <summary>The weapon in this side's hand.</summary>
        public WeaponId CurrentWeapon => Self.CurrentWeapon;
    }
}
