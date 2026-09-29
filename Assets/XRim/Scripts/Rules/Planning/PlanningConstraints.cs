using System.Collections.Generic;
using XRim.Rules.Settings;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// Per-player limits for one planning phase. Status effects (stun, stagger) and limb loss change
    /// these rather than adding special cases to the planning rules (GDD §10, §11, §12).
    /// </summary>
    public sealed class PlanningConstraints
    {
        private readonly HashSet<BodyMove> _forbiddenBodyMoves = new HashSet<BodyMove>();

        public float PlanningDurationSeconds { get; set; }
        public float InkLengthMultiplier { get; set; } = 1f;
        public bool CanSwitchWeapon { get; set; } = true;
        public bool CanUseSignature { get; set; } = true;

        public bool IsBodyMoveAllowed(BodyMove move) => move == BodyMove.None || !_forbiddenBodyMoves.Contains(move);

        public void ForbidBodyMove(BodyMove move) => _forbiddenBodyMoves.Add(move);

        public static PlanningConstraints CreateDefault(MatchSettings settings) =>
            new PlanningConstraints { PlanningDurationSeconds = settings.PlanningDurationSeconds };
    }
}
