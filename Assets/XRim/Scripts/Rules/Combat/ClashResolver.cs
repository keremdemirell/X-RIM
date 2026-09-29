using System;
using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// Two-stage clash model (GDD §10, Decided):
    /// stage 1, contact angle picks hard clash vs glancing; stage 2, power P = W_m·m + W_v·v picks the winner.
    /// Shield contacts use <see cref="IShieldBlockModel"/> instead.
    /// </summary>
    public sealed class ClashResolver
    {
        public ClashResult Resolve(ClashFacts facts, ClashSettings settings)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("ClashResolver.Resolve is not implemented yet.");
        }
    }
}
