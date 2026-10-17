using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// The path a signature move executes (GDD §8: "tapping the move's button draws its pattern automatically"). The preset is built like
    /// a drawn stroke through <see cref="PathBuilder"/>, so every path rule still holds: the lead-in from the weapon tip (D3), the
    /// reach limit (D4) and, for a move that pays ink (<see cref="SignatureMoveStats.ConsumesInk"/>), the ink cut and the rigidity
    /// break. A move that ignores ink is measured by plain length at no cost, so the whole pattern always plays. The planning session and
    /// the authority's validator build it with this one class, so what a client previews is what executes.
    /// </summary>
    public sealed class SignaturePathBuilder
    {
        private readonly PathBuilder _paid;
        private readonly PathBuilder _free;

        public SignaturePathBuilder(RulePolicies policies)
        {
            Guard.NotNull(policies, nameof(policies));
            _paid = new PathBuilder(policies);

            // The same path policies with an ink model that charges nothing and never breaks the path.
            var freePolicies = new RulePolicies
            {
                Stroke = policies.Stroke,
                PathStart = policies.PathStart,
                Reach = policies.Reach,
                InkCost = new FreeInkCostModel(new LengthInkCostModel()),
            };
            _free = new PathBuilder(freePolicies);
        }

        /// <param name="move">The move's preset, in the torso frame.</param>
        /// <param name="weaponTipLocal">Where the weapon's tip rests now, in the torso frame (the lead-in starts there).</param>
        /// <param name="weapon">The held weapon, with this turn's ink multiplier applied (a move that pays ink is cut by it).</param>
        public BuiltPath Build(SignatureMoveStats move, Vec2 weaponTipLocal, WeaponStats weapon, PathSettings paths)
        {
            Guard.NotNull(move, nameof(move));
            Guard.NotNull(weapon, nameof(weapon));
            Guard.NotNull(paths, nameof(paths));
            var preset = new WeaponPath(move.PresetPath);
            PathBuilder builder = move.ConsumesInk ? _paid : _free;
            return builder.Build(WeaponPath.Empty, preset, weaponTipLocal, weapon, paths);
        }
    }
}
