using System;
using XRim.Core.Gdd;

namespace XRim.Economy
{
    /// <summary>Economy tuning (GDD §16). Every number is open; zeros mean "not decided", not "free".</summary>
    [Serializable]
    public sealed class EconomySettings
    {
        [GddTbd("§16", "Sources and amounts of earned currency")]
        public long RewardPerWin = 0;

        [GddTbd("§16", "Sources and amounts of earned currency")]
        public long RewardPerHit = 0;

        [GddTbd("§16", "Sources and amounts of earned currency")]
        public long RewardPerParry = 0;

        /// <summary>0 = no cap.</summary>
        [GddTbd("§16", "Farming protection", Proposal = "Per-match caps or lower rewards in friend matches")]
        public long PerMatchRewardCap = 0;

        [GddTbd("§16", "Farming protection", Proposal = "Per-match caps or lower rewards in friend matches")]
        public float FriendMatchRewardMultiplier = 1f;

        [GddTbd("§16", "Can premium currency be earned in play?")]
        public bool PremiumEarnableInPlay = false;
    }
}
