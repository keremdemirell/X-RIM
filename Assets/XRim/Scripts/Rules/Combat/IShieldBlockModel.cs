using XRim.Core.Gdd;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// Shield vs weapon contact. GDD §7 proposes its own block rule rather than the clash model.
    /// Using a shield never triggers the electric wall (Decided, §13).
    /// </summary>
    [GddTbd("§7", "Shield block model", Proposal = "Full block square-on, partial at edges")]
    [GddTbd("§7", "Can a mace break or stagger a shield?")]
    public interface IShieldBlockModel
    {
        BlockResult Resolve(BlockFacts facts);
    }
}
