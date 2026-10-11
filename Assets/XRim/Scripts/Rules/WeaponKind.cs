namespace XRim.Rules
{
    public enum WeaponKind
    {
        Weapon = 0,

        /// <summary>Takes a loadout slot like a weapon; follows its path, then holds (GDD §7).</summary>
        Shield = 1,

        /// <summary>A severed arm or leg wielded as a melee club; never thrown (GDD §12).</summary>
        SeveredLimb = 2,

        /// <summary>
        /// With both arms severed, the dummy strikes with its body: a headbutt by default (§12, <c>IArmlessAttackMode</c>). The
        /// striking part follows the drawn path; nothing is held.
        /// </summary>
        BodyBlow = 3,
    }
}
