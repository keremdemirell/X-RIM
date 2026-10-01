using XRim.Core;
using XRim.Rules.Planning;
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

        public PlanningWindow(int turnIndex, double deadlineSeconds, PerSide<PlanningConstraints> constraints,
            bool isSuddenDeath, BoardSnapshot board, PerSide<IWeaponTipSource> weaponTips)
        {
            TurnIndex = turnIndex;
            DeadlineSeconds = deadlineSeconds;
            Constraints = Guard.NotNull(constraints, nameof(constraints));
            IsSuddenDeath = isSuddenDeath;
            Board = Guard.NotNull(board, nameof(board));
            WeaponTips = Guard.NotNull(weaponTips, nameof(weaponTips));
        }
    }
}
