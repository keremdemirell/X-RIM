namespace XRim.Rules
{
    /// <summary>Ids of the roster weapons in GDD §6. Unlockable sidegrades add their own ids in data.</summary>
    public static class WeaponIds
    {
        public static readonly WeaponId Rapier = new WeaponId("rapier");
        public static readonly WeaponId Sword = new WeaponId("sword");
        public static readonly WeaponId Mace = new WeaponId("mace");
        public static readonly WeaponId Spear = new WeaponId("spear");
        public static readonly WeaponId Shield = new WeaponId("shield");
        public static readonly WeaponId SeveredLimb = new WeaponId("severed_limb");

        /// <summary>The armless headbutt or kick (GDD §12): the only option once both arms are gone.</summary>
        public static readonly WeaponId BodyBlow = new WeaponId("body_blow");
    }
}
