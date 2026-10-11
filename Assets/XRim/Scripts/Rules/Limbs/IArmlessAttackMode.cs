using XRim.Core.Gdd;
using XRim.Rules.Match;
using XRim.Rules.Paths;

namespace XRim.Rules.Limbs
{
    /// <summary>
    /// With both arms severed the dummy fights with headbutts and kicks (Decided, §12). How the player
    /// aims them is not decided. The simulation asks which part strikes along the drawn path of the body blow
    /// (<see cref="WeaponIds.BodyBlow"/>, the only option once both arms are gone).
    /// </summary>
    [GddTbd("§12", "Body blow controls without arms", Proposal = "The drawn path steers the head or leg")]
    public interface IArmlessAttackMode
    {
        BodyPart ChooseStrikingPart(FighterState fighter, WeaponPath path);
    }
}
