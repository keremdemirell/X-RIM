using XRim.Rules.Match;
using XRim.Rules.Paths;

namespace XRim.Rules.Limbs
{
    /// <summary>
    /// Body blows without arms (GDD §12, TBD). The default (designer, 2026-10-10): the drawn path steers the head, so every armless
    /// attack is a headbutt.
    /// </summary>
    public sealed class HeadbuttArmlessAttackMode : IArmlessAttackMode
    {
        public BodyPart ChooseStrikingPart(FighterState fighter, WeaponPath path) => BodyPart.Head;
    }
}
