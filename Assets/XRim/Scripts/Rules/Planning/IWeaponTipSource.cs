using XRim.Core;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// Where a weapon's tip rests in the torso frame (arena units, +X toward the opponent): the point a stroke's
    /// lead-in starts from (D3). The authority builds one per side for each turn from the frozen board, so the
    /// planning rules never need to know about poses or physics.
    /// </summary>
    public interface IWeaponTipSource
    {
        Vec2 TipLocal(WeaponId weapon);
    }
}
