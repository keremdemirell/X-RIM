using XRim.Core;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Rules.Limbs
{
    /// <summary>
    /// The leg-loss penalty <see cref="LimbLossSettings.LegLossPenalty"/> names (GDD §12, TBD). The default (designer, 2026-10-10):
    /// with one leg left the dummy cannot jump or lunge, with none it makes no body move. Applied to each side's planning limits
    /// at the start of every turn, after the status effects.
    /// </summary>
    public sealed class SettingsMobilityPenaltyPolicy : IMobilityPenaltyPolicy
    {
        public void Apply(FighterState fighter, PlanningConstraints constraints, RulesSettings settings)
        {
            Guard.NotNull(fighter, nameof(fighter));
            Guard.NotNull(constraints, nameof(constraints));
            Guard.NotNull(settings, nameof(settings));
            if (settings.Damage.LimbLoss.LegLossPenalty != LegLossPenalty.NoJumpOrLungeOnOneLeg) return;

            switch (fighter.RemainingLegs)
            {
                case 0:
                    constraints.ForbidAllBodyMoves();
                    break;
                case 1:
                    constraints.ForbidBodyMove(BodyMove.Jump);
                    constraints.ForbidBodyMove(BodyMove.Lunge);
                    break;
            }
        }
    }
}
