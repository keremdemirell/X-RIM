namespace XRim.Economy
{
    /// <summary>Why a finished match paid what it paid.</summary>
    public enum RewardDecision
    {
        /// <summary>Something was added to the wallet.</summary>
        Paid = 0,

        /// <summary>This kind of match pays nothing (<see cref="EconomySettings.Pays"/>).</summary>
        ModeDoesNotPay = 1,

        /// <summary>The policy found nothing to pay this side, or the farming guard reduced it to nothing.</summary>
        NothingEarned = 2,
    }
}
