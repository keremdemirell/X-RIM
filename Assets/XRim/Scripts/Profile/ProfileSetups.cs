using System.Collections.Generic;
using XRim.Core;
using XRim.Economy;
using XRim.Rules;
using XRim.Rules.Match;

namespace XRim.Profile
{
    /// <summary>
    /// What a profile brings into a match (GDD §4, §6, §8, §16), as the rules' <see cref="FighterSetup"/>. Cosmetics are deliberately not
    /// part of it: they never reach the rules.
    /// </summary>
    public static class ProfileSetups
    {
        /// <summary>
        /// A bot's hand (designer 2026-10-25, Q3): bots always hold their weapon in the right hand, however the player does. Only the player's
        /// handedness is a choice (GDD §4).
        /// </summary>
        public const Handedness BotHandedness = Handedness.Right;

        /// <summary>
        /// The hand the account holds its weapon in: the first-launch pick, changeable in Settings (D24). A profile that has not picked yet
        /// (the pick screen comes before any match) holds it in the right.
        /// </summary>
        public static Handedness HandednessOf(PlayerProfile profile) =>
            Guard.NotNull(profile, nameof(profile)).ChosenHandedness ?? Handedness.Right;

        /// <summary>The fighter for a person playing on this device: the account's hand and every signature move the account owns.</summary>
        public static FighterSetup ForPlayer(PlayerProfile profile, IReadOnlyList<WeaponId> loadout, ShopCatalogue catalogue) =>
            new FighterSetup(HandednessOf(profile), loadout, OwnedSignatureMoves(profile, catalogue));

        /// <summary>The fighter for a bot: it brings the same moves the account owns, so it grows as the player does (designer 2026-10-25, Q3).</summary>
        public static FighterSetup ForBot(PlayerProfile profile, IReadOnlyList<WeaponId> loadout, ShopCatalogue catalogue) =>
            new FighterSetup(BotHandedness, loadout, OwnedSignatureMoves(profile, catalogue));

        /// <summary>
        /// The signature moves the profile owns, in the shop's order (so the buttons keep their places). Moves are earned currency only
        /// (GDD §8, §16), so this is exactly what the player has bought plus the starting move.
        /// </summary>
        public static IReadOnlyList<SignatureMoveId> OwnedSignatureMoves(PlayerProfile profile, ShopCatalogue catalogue)
        {
            Guard.NotNull(profile, nameof(profile));
            Guard.NotNull(catalogue, nameof(catalogue));
            var moves = new List<SignatureMoveId>();
            foreach (GameplayUnlock unlock in catalogue.Unlocks)
            {
                if (unlock.Kind == GameplayUnlockKind.SignatureMove && profile.Inventory.Owns(unlock)) moves.Add(new SignatureMoveId(unlock.Id));
            }

            return moves;
        }
    }
}
