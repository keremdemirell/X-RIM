using XRim.Core;
using XRim.Rules.Planning;
using XRim.Rules.Signature;
using XRim.Simulation.Recording;

namespace XRim.Networking
{
    /// <summary>A planning phase as announced by the authority.</summary>
    public sealed class PlanningWindow
    {
        public int TurnIndex { get; }

        /// <summary>Deadline on the authority's clock. Clients show a countdown toward it.</summary>
        public double DeadlineSeconds { get; }

        public PerSide<PlanningConstraints> Constraints { get; }
        public bool IsSuddenDeath { get; }

        /// <summary>The frozen board both players plan from (GDD §3).</summary>
        public BoardSnapshot Board { get; }

        /// <summary>
        /// Where each side's weapon tip rests in its torso frame, the point a stroke's lead-in starts from (D3). Clients
        /// need it for the ink preview (Session 08) and bots for their strokes; the authority uses the same values.
        /// </summary>
        public PerSide<IWeaponTipSource> WeaponTips { get; }

        /// <summary>
        /// What each player may do with signature moves this turn (GDD §8): moves brought, charge, whether one can be used. A player's
        /// HUD reads only its own side; a server (Session 19) sends each client only theirs, since a chosen move and its charge are
        /// hidden from the opponent.
        /// </summary>
        public PerSide<SignatureOptions> Signatures { get; }

        /// <summary>
        /// What each side shows the other as planning opens (Session 16): its weapon, Ready, a charged signature move only when that is
        /// shown (GDD §3, §6, §8). Without one from the authority: the board's weapons, nobody Ready, no charge shown.
        /// </summary>
        public PerSide<PublicPlanningState> PublicStates { get; }

        public PlanningWindow(int turnIndex, double deadlineSeconds, PerSide<PlanningConstraints> constraints,
            bool isSuddenDeath, BoardSnapshot board, PerSide<IWeaponTipSource> weaponTips, PerSide<SignatureOptions> signatures = null,
            PerSide<PublicPlanningState> publicStates = null)
        {
            TurnIndex = turnIndex;
            DeadlineSeconds = deadlineSeconds;
            Constraints = Guard.NotNull(constraints, nameof(constraints));
            IsSuddenDeath = isSuddenDeath;
            Board = Guard.NotNull(board, nameof(board));
            WeaponTips = Guard.NotNull(weaponTips, nameof(weaponTips));
            Signatures = signatures ?? new PerSide<SignatureOptions>(SignatureOptions.None, SignatureOptions.None);
            PublicStates = publicStates ?? PerSide<PublicPlanningState>.Create(side =>
                new PublicPlanningState(board.State.Fighters[side].CurrentWeapon, false, false));
        }
    }
}
