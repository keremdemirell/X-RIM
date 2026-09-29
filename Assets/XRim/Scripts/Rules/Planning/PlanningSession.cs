using System;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// One player's planning phase. Clients run it for instant feedback (ink meter, lock-out);
    /// the authority runs the same code to validate (GDD §18).
    /// Enforces: weapon switch erases the path and is refused in the lock-out window (§6),
    /// ink budget (§6), Ready and its cancel flag (§3).
    /// </summary>
    public sealed class PlanningSession
    {
        private WeaponId _weapon;
        private BodyMove _bodyMove = BodyMove.None;
        private WeaponPath _path = WeaponPath.Empty;
        private SignatureMoveId _signature;

        public Side Side { get; }
        public PlanningConstraints Constraints { get; }
        public double DeadlineSeconds { get; }
        public bool IsReady { get; private set; }

        private RulesSettings Settings { get; }

        public PlanningSession(Side side, WeaponId currentWeapon, PlanningConstraints constraints,
            double deadlineSeconds, RulesSettings settings)
        {
            Side = side;
            _weapon = currentWeapon;
            Constraints = Guard.NotNull(constraints, nameof(constraints));
            DeadlineSeconds = deadlineSeconds;
            Settings = Guard.NotNull(settings, nameof(settings));
        }

        public TurnPlan CurrentPlan => new TurnPlan(_weapon, _bodyMove, _path, _signature, IsReady);

        public PublicPlanningState PublicState => new PublicPlanningState(_weapon, IsReady, false);

        public CommandResult Apply(PlanningCommand command, double nowSeconds)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("PlanningSession.Apply is not implemented yet.");
        }
    }
}
