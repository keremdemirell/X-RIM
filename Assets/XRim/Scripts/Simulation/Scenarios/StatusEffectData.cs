using System;
using XRim.Rules.Status;

namespace XRim.Simulation.Scenarios
{
    /// <summary>
    /// A pending status effect (GDD §10, §11) in plain fields: which of the built-in effects it is (<see cref="StunEffect"/> names the three
    /// GDD options), its kind, the turns it still shapes and its fraction (planning time or ink; unused by "no body move").
    /// </summary>
    [Serializable]
    public sealed class StatusEffectData
    {
        public StatusKind Kind;
        public StunEffect Effect;
        public int RemainingTurns;
        public float Fraction;
    }
}
