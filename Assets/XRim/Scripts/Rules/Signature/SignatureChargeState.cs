using XRim.Core;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// How charged one dummy is when a planning phase opens (GDD §8): a plain snapshot a <see cref="XRim.Rules.Planning.PlanningSession"/>
    /// reads, so planning never touches the match state. Nothing changes the charge during planning: it is spent when the plan locks and
    /// fills while a turn executes. The default value is "not charged".
    /// </summary>
    public readonly struct SignatureChargeState
    {
        /// <summary>The charge allows a signature move now (the catalogue and this turn's constraints are separate questions).</summary>
        public bool IsAvailable { get; }

        /// <summary>From 0 to 1, for the HUD.</summary>
        public float ChargeFraction { get; }

        public SignatureChargeState(bool isAvailable, float chargeFraction)
        {
            IsAvailable = isAvailable;
            ChargeFraction = chargeFraction;
        }

        /// <summary>The fighter's charge under the model; "not charged" without a model.</summary>
        public static SignatureChargeState Of(ISignatureChargeModel model, FighterState fighter, SignatureSettings settings)
        {
            if (model == null) return default;
            Guard.NotNull(fighter, nameof(fighter));
            Guard.NotNull(settings, nameof(settings));
            return new SignatureChargeState(model.IsAvailable(fighter, settings), model.ChargeFraction(fighter, settings));
        }
    }
}
