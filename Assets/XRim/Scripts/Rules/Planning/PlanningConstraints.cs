using System.Collections.Generic;
using XRim.Rules.Settings;
using XRim.Rules.Status;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// Per-player limits for one planning phase. Status effects (stun, stagger) and limb loss change
    /// these rather than adding special cases to the planning rules (GDD §10, §11, §12).
    /// </summary>
    public sealed class PlanningConstraints
    {
        private readonly HashSet<BodyMove> _forbiddenBodyMoves = new HashSet<BodyMove>();
        private readonly List<StatusKind> _statuses = new List<StatusKind>();
        private bool _allBodyMovesForbidden;

        public float PlanningDurationSeconds { get; set; }
        public float InkLengthMultiplier { get; set; } = 1f;
        public bool CanSwitchWeapon { get; set; } = true;
        public bool CanUseSignature { get; set; } = true;

        /// <summary>The status effects shaping this turn (a stun, a stagger), in the order they were applied. For the HUD.</summary>
        public IReadOnlyList<StatusKind> Statuses => _statuses;

        /// <summary>No move is always allowed: the dummy holds its pose (GDD §3).</summary>
        public bool IsBodyMoveAllowed(BodyMove move) =>
            move == BodyMove.None || (!_allBodyMovesForbidden && !_forbiddenBodyMoves.Contains(move));

        public void ForbidBodyMove(BodyMove move) => _forbiddenBodyMoves.Add(move);

        /// <summary>Every body move except none, including moves added to the game later (a stun, D17 default).</summary>
        public void ForbidAllBodyMoves() => _allBodyMovesForbidden = true;

        public void AddStatus(StatusKind kind) => _statuses.Add(kind);

        public static PlanningConstraints CreateDefault(MatchSettings settings) =>
            new PlanningConstraints { PlanningDurationSeconds = settings.PlanningDurationSeconds };
    }
}
