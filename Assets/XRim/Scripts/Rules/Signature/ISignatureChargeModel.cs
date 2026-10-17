using XRim.Core.Gdd;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// When a signature move is available (GDD §8). Signature moves are unlocked with earned currency only (§16).
    /// <para>
    /// A charge model holds no state of its own: a turn is simulated on a copy of the match state, and a model that remembered things
    /// itself would be changed by simulations and replays that never happened. What it counts lives on the
    /// <see cref="FighterState"/> it is given (<see cref="FighterState.SignatureCharge"/>, <see cref="FighterState.SignatureUses"/>),
    /// like the weapon traits (§15).
    /// </para>
    /// </summary>
    [GddTbd("§8", "Signature charge: once per match or Kinetic Energy Bar")]
    [GddTbd("§8", "Number of signature moves per match")]
    public interface ISignatureChargeModel
    {
        /// <summary>Whether the dummy may use a signature move now, as far as the charge goes.</summary>
        bool IsAvailable(FighterState fighter, SignatureSettings settings);

        /// <summary>How charged the dummy is, from 0 to 1, for the HUD: the bar's fill, or 1 / 0 for a move that is there or spent.</summary>
        float ChargeFraction(FighterState fighter, SignatureSettings settings);

        /// <summary>The dummy took HP damage from a hit or from its own electric wall.</summary>
        void OnDamageTaken(FighterState fighter, float hpDamage, SignatureSettings settings);

        /// <summary>The dummy dealt HP damage with a landed hit.</summary>
        void OnDamageDealt(FighterState fighter, float hpDamage, SignatureSettings settings);

        /// <summary>A plan holding a signature move locked: the move is spent.</summary>
        void OnUsed(FighterState fighter, SignatureSettings settings);
    }
}
