using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// Weapon-to-shield contact: the shield's own block rule, never the clash model (GDD §7). Using a shield never triggers the
    /// electric wall (Decided, §13): the wall reads only a backward swipe.
    /// </summary>
    [GddTbd("§7", "Shield block model",
        Proposal = "D20 default (designer, 2026-10-03): full block square-on to the face, partial at the rim or glancing")]
    [GddTbd("§7", "Can a mace break or stagger a shield?",
        Proposal = "D21 default (designer, 2026-10-03): no special rule; the block holds and the holder is pushed back")]
    public interface IShieldBlockModel
    {
        BlockResult Resolve(BlockFacts facts, ShieldBlockSettings settings);
    }
}
