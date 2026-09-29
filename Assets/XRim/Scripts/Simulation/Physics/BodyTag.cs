using XRim.Core;
using XRim.Rules;

namespace XRim.Simulation.Physics
{
    /// <summary>Identifies a physics body to the rules: whose it is and what it is.</summary>
    public readonly struct BodyTag
    {
        /// <summary>Null for arena bodies (floor, edges).</summary>
        public Side? Owner { get; }

        public BodyRole Role { get; }

        /// <summary>Meaningful for <see cref="BodyRole.BodyPart"/> and <see cref="BodyRole.SeveredLimb"/>.</summary>
        public BodyPart Part { get; }

        public BodyTag(Side? owner, BodyRole role, BodyPart part)
        {
            Owner = owner;
            Role = role;
            Part = part;
        }
    }
}
