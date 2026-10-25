using System;
using System.Collections.Generic;
using XRim.Core.Gdd;
using XRim.Rules;

namespace XRim.Economy
{
    /// <summary>
    /// Economy tuning (GDD §16). Nothing here is decided by the GDD beyond the Decided rules (two currencies, no conversion, earned-only
    /// gameplay unlocks, real money for cosmetics only); every name, amount and price is a placeholder the designer set on 2026-10-25 so
    /// the shop can be tried. A reward or a price of 0 means "not paid" or "not sold", not "free".
    /// </summary>
    [Serializable]
    public sealed class EconomySettings
    {
        private const string PricePlaceholder = "§16 prices are TBD (designer 2026-10-25: placeholders)";
        private const string AmountPlaceholder = "§16 reward amounts are TBD (designer 2026-10-25: placeholders)";

        /// <summary>What a weapon unlock costs: three wins of a bot match at the placeholder reward.</summary>
        private const long WeaponUnlockPrice = 300;

        private const long SignatureMoveUnlockPrice = 400;
        private const long CosmeticEarnedPrice = 250;
        private const long CosmeticPremiumPrice = 50;

        [GddTbd("§16", "Currency names", Proposal = "Designer 2026-10-25 (placeholders): Bolts (earned) and Gears (premium)")]
        [Placeholder("§16 currency names are TBD")]
        public string EarnedCurrencyName = "Bolts";

        [GddTbd("§16", "Currency names", Proposal = "Designer 2026-10-25 (placeholders): Bolts (earned) and Gears (premium)")]
        [Placeholder("§16 currency names are TBD")]
        public string PremiumCurrencyName = "Gears";

        [GddTbd("§16", "Sources and amounts of earned currency", Proposal = "Designer 2026-10-25: a match win only")]
        [Placeholder(AmountPlaceholder)]
        public long RewardPerWin = 100;

        /// <summary>A switch, off: the designer is unsure about paying for hits (§16: it could reward reckless play over winning).</summary>
        [GddTbd("§16", "Sources and amounts of earned currency", Proposal = "Designer 2026-10-25: off (0); raise it to try paying per hit")]
        [Placeholder(AmountPlaceholder)]
        public long RewardPerHit = 0;

        /// <summary>A switch, off, like <see cref="RewardPerHit"/>. What counts as a parry is itself TBD (see <c>MatchSummaryTally</c>).</summary>
        [GddTbd("§16", "Sources and amounts of earned currency", Proposal = "Designer 2026-10-25: off (0); raise it to try paying per parry")]
        [Placeholder(AmountPlaceholder)]
        public long RewardPerParry = 0;

        /// <summary>The most one match can pay a player. 0 = no cap.</summary>
        [GddTbd("§16", "Farming protection", Proposal = "Designer 2026-10-25: a per-match cap; lower rewards in friend matches stay a switch")]
        [Placeholder(AmountPlaceholder)]
        public long PerMatchRewardCap = 150;

        /// <summary>Friend matches (Session 20) pay this share of the reward. 1 = the same as any match.</summary>
        [GddTbd("§16", "Farming protection", Proposal = "Designer 2026-10-25: a per-match cap; lower rewards in friend matches stay a switch")]
        public float FriendMatchRewardMultiplier = 1f;

        /// <summary>
        /// Not wired: no source of premium currency exists in the game, whatever this says. If the designer ever says yes, a source and
        /// an amount have to be decided first (Session 22 revisits it).
        /// </summary>
        [GddTbd("§16", "Can premium currency be earned in play?", Proposal = "Designer 2026-10-25: no")]
        public bool PremiumEarnableInPlay = false;

        [GddTbd("§17", "Bot opponent and practice", Proposal = "Designer 2026-10-25: only a win against the bot pays (the only way to earn before online play)")]
        public bool PaysInVersusBot = true;

        [GddTbd("§16", "Which matches pay", Proposal = "Designer 2026-10-25: hot-seat pays nothing (two people, one account)")]
        public bool PaysInHotSeat = false;

        [GddTbd("§17", "Bot opponent and practice", Proposal = "Designer 2026-10-25: practice has no stakes")]
        public bool PaysInPractice = false;

        /// <summary>Weapons, shields and signature moves (earned currency only, Decided). The starting ones cost nothing and are owned from the start.</summary>
        [GddTbd("§6", "Starting weapons and default loadout", Proposal = "Designer 2026-10-15: rapier, mace and shield; sword and spear are unlockables")]
        [GddTbd("§16", "Progression structure and prices", Proposal = "Designer 2026-10-25: no levels, placeholder prices")]
        public List<UnlockableEntry> Unlockables = CreateDefaultUnlockables();

        /// <summary>Cosmetics (Decided: none may change hitboxes, silhouette, reach or readability). Placeholders until the art exists (Session 17).</summary>
        [GddTbd("§16", "Progression structure and prices", Proposal = "Designer 2026-10-25: a few placeholder cosmetics")]
        public List<CosmeticEntry> Cosmetics = CreateDefaultCosmetics();

        /// <summary>Whether a finished match of this kind pays earned currency at all.</summary>
        public bool Pays(MatchKind kind)
        {
            switch (kind)
            {
                case MatchKind.VersusBot: return PaysInVersusBot;
                case MatchKind.HotSeat: return PaysInHotSeat;
                case MatchKind.Practice: return PaysInPractice;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown match kind.");
            }
        }

        /// <summary>Appends a message for everything in the economy that cannot work as set (the way the other settings classes do).</summary>
        public void Validate(ICollection<string> issues)
        {
            if (string.IsNullOrWhiteSpace(EarnedCurrencyName)) issues.Add("Economy: the earned currency has no name.");
            if (string.IsNullOrWhiteSpace(PremiumCurrencyName)) issues.Add("Economy: the premium currency has no name.");
            if (RewardPerWin < 0 || RewardPerHit < 0 || RewardPerParry < 0) issues.Add("Economy: a reward is negative (treated as 0).");
            if (PerMatchRewardCap < 0) issues.Add("Economy: the per-match cap is negative (treated as no cap).");
            if (FriendMatchRewardMultiplier < 0f) issues.Add("Economy: the friend-match multiplier is negative (treated as 0).");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnlockableEntry entry in Unlockables)
            {
                if (string.IsNullOrEmpty(entry.Id)) issues.Add("Economy: an unlockable has no id.");
                else if (!seen.Add(entry.Kind + ":" + entry.Id)) issues.Add($"Economy: unlockable {entry.Kind} '{entry.Id}' is listed twice.");
                if (entry.EarnedPrice < 0) issues.Add($"Economy: unlockable '{entry.Id}' has a negative price (treated as 0).");
                if (entry.EarnedPrice == 0 && !entry.StartsOwned) issues.Add($"Economy: unlockable '{entry.Id}' costs 0 and is not owned at the start: it is free to buy.");
            }

            seen.Clear();
            foreach (CosmeticEntry entry in Cosmetics)
            {
                if (string.IsNullOrEmpty(entry.Id)) issues.Add("Economy: a cosmetic has no id.");
                else if (!seen.Add(entry.Id)) issues.Add($"Economy: cosmetic '{entry.Id}' is listed twice.");
                if (entry.EarnedPrice < 0 || entry.PremiumPrice < 0) issues.Add($"Economy: cosmetic '{entry.Id}' has a negative price (treated as not sold).");
                if (entry.EarnedPrice <= 0 && entry.PremiumPrice <= 0) issues.Add($"Economy: cosmetic '{entry.Id}' is not sold for either currency.");
            }
        }

        /// <summary>The starting shop's weapons, shield and moves (designer 2026-10-15 and 2026-10-25).</summary>
        public static List<UnlockableEntry> CreateDefaultUnlockables() => new List<UnlockableEntry>
        {
            new UnlockableEntry(WeaponIds.Rapier.Value, GameplayUnlockKind.Weapon, 0, true),
            new UnlockableEntry(WeaponIds.Mace.Value, GameplayUnlockKind.Weapon, 0, true),
            new UnlockableEntry(WeaponIds.Shield.Value, GameplayUnlockKind.Shield, 0, true),
            new UnlockableEntry(WeaponIds.Sword.Value, GameplayUnlockKind.Weapon, WeaponUnlockPrice, false),
            new UnlockableEntry(WeaponIds.Spear.Value, GameplayUnlockKind.Weapon, WeaponUnlockPrice, false),
            new UnlockableEntry(SignatureMoveIds.FiveFingerExplodingTouch.Value, GameplayUnlockKind.SignatureMove, 0, true),
            new UnlockableEntry(SignatureMoveIds.HeadImpactSimulation404.Value, GameplayUnlockKind.SignatureMove, SignatureMoveUnlockPrice, false),
            new UnlockableEntry(SignatureMoveIds.WhiplashCalibrationSpin.Value, GameplayUnlockKind.SignatureMove, SignatureMoveUnlockPrice, false),
        };

        /// <summary>Four placeholder cosmetics, one per kind of cosmetic the game will sell first. Only Neon Sparks changes a match so far.</summary>
        public static List<CosmeticEntry> CreateDefaultCosmetics() => new List<CosmeticEntry>
        {
            new CosmeticEntry(CosmeticIds.NeonSparks, "Neon Sparks", CosmeticKind.HitEffect, CosmeticEarnedPrice, CosmeticPremiumPrice, true),
            new CosmeticEntry(CosmeticIds.HazardStripes, "Hazard Stripes", CosmeticKind.Skin, CosmeticEarnedPrice, 0, false),
            new CosmeticEntry(CosmeticIds.NightShift, "Night Shift", CosmeticKind.ArenaTheme, 0, CosmeticPremiumPrice, false),
            new CosmeticEntry(CosmeticIds.DrillSergeant, "Drill Sergeant", CosmeticKind.GruntPack, CosmeticEarnedPrice, CosmeticPremiumPrice, false),
        };
    }
}
