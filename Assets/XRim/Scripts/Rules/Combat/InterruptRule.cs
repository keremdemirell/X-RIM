namespace XRim.Rules.Combat
{
    /// <summary>The GDD's options for which landed hits cancel the victim's attack (§9, TBD; D15).</summary>
    public enum InterruptRule
    {
        /// <summary>Every clean hit interrupts: whoever hits first wins the exchange.</summary>
        AnyHit = 0,

        /// <summary>Only hits on the arm that holds the weapon, or on the head. D15 (designer, 2026-10-03).</summary>
        WeaponArmOrHead = 1,

        /// <summary>Only hits whose damage is above <c>DamageSettings.InterruptDamageThreshold</c>.</summary>
        AboveDamageThreshold = 2,
    }
}
