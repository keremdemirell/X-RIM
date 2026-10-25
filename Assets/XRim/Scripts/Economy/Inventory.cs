using System;
using System.Collections.Generic;
using XRim.Core;

namespace XRim.Economy
{
    /// <summary>
    /// What a player owns (GDD §16): weapons, shields and signature moves, and cosmetics with the one equipped per kind. Pure data: it
    /// does not know prices, so only the <see cref="Shop"/> (and starting ownership) decides how an item gets in.
    /// </summary>
    public sealed class Inventory
    {
        private readonly HashSet<(GameplayUnlockKind Kind, string Id)> _unlocks = new HashSet<(GameplayUnlockKind, string)>();
        private readonly Dictionary<string, CosmeticKind> _cosmetics = new Dictionary<string, CosmeticKind>(StringComparer.Ordinal);
        private readonly Dictionary<CosmeticKind, string> _equipped = new Dictionary<CosmeticKind, string>();

        /// <summary>Every owned weapon, shield and move, in a stable order (saving needs one).</summary>
        public IEnumerable<(GameplayUnlockKind Kind, string Id)> OwnedUnlocks()
        {
            var sorted = new List<(GameplayUnlockKind Kind, string Id)>(_unlocks);
            sorted.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : string.CompareOrdinal(a.Id, b.Id));
            return sorted;
        }

        /// <summary>Every owned cosmetic, in a stable order.</summary>
        public IEnumerable<(CosmeticKind Kind, string Id)> OwnedCosmetics()
        {
            var sorted = new List<(CosmeticKind Kind, string Id)>();
            foreach (KeyValuePair<string, CosmeticKind> pair in _cosmetics)
            {
                sorted.Add((pair.Value, pair.Key));
            }

            sorted.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : string.CompareOrdinal(a.Id, b.Id));
            return sorted;
        }

        public bool Owns(GameplayUnlockKind kind, string id) => id != null && _unlocks.Contains((kind, id));

        public bool Owns(GameplayUnlock unlock) => Owns(Guard.NotNull(unlock, nameof(unlock)).Kind, unlock.Id);

        public bool OwnsCosmetic(string id) => id != null && _cosmetics.ContainsKey(id);

        /// <summary>Adds it. False when it was already owned.</summary>
        public bool Grant(GameplayUnlockKind kind, string id) => _unlocks.Add((kind, Guard.NotNull(id, nameof(id))));

        /// <summary>Adds it. False when it was already owned.</summary>
        public bool GrantCosmetic(CosmeticKind kind, string id)
        {
            Guard.NotNull(id, nameof(id));
            if (_cosmetics.ContainsKey(id)) return false;
            _cosmetics.Add(id, kind);
            return true;
        }

        /// <summary>Gives a new player what they start with.</summary>
        public void GrantStarting(ShopCatalogue catalogue)
        {
            foreach (GameplayUnlock unlock in Guard.NotNull(catalogue, nameof(catalogue)).StartingUnlocks())
            {
                Grant(unlock.Kind, unlock.Id);
            }
        }

        /// <summary>The id of the cosmetic equipped for this kind; null: the game's own default look or sound.</summary>
        public string EquippedCosmetic(CosmeticKind kind) => _equipped.TryGetValue(kind, out string id) ? id : null;

        /// <summary>Equips an owned cosmetic of that kind in place of the one before it. False when it is not owned or is another kind.</summary>
        public bool Equip(CosmeticKind kind, string id)
        {
            if (id == null || !_cosmetics.TryGetValue(id, out CosmeticKind ownedKind) || ownedKind != kind) return false;
            _equipped[kind] = id;
            return true;
        }

        public void Unequip(CosmeticKind kind) => _equipped.Remove(kind);
    }
}
