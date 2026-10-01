using System.Collections.Generic;
using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// The order contacts are handled in (ARCHITECTURE §6): earliest time first (GDD §9: the first weapon to reach a
    /// valid hitbox lands its hit), then a stable id that does not depend on the physics engine, so every run and a
    /// server handle simultaneous contacts identically: the first body's owner (left, right, then arena), role and part,
    /// then the second body's. Bodies that share a tag (the two segments of a ten-body limb) keep the engine's stable order.
    /// </summary>
    public static class TurnContactOrder
    {
        /// <summary>Arena bodies (no owner) sort after both fighters.</summary>
        private const int ArenaOwnerRank = 2;

        public static int Compare(TurnContact a, TurnContact b)
        {
            int byTime = a.Time.CompareTo(b.Time);
            if (byTime != 0) return byTime;
            int byFirst = Compare(a.Facts.A, b.Facts.A);
            return byFirst != 0 ? byFirst : Compare(a.Facts.B, b.Facts.B);
        }

        public static int Compare(BodyTag a, BodyTag b)
        {
            int byOwner = OwnerRank(a.Owner).CompareTo(OwnerRank(b.Owner));
            if (byOwner != 0) return byOwner;
            int byRole = ((int)a.Role).CompareTo((int)b.Role);
            return byRole != 0 ? byRole : ((int)a.Part).CompareTo((int)b.Part);
        }

        /// <summary>Stable insertion sort (a step reports only a few contacts); equal contacts keep their order.</summary>
        public static void Sort(List<TurnContact> contacts)
        {
            Guard.NotNull(contacts, nameof(contacts));
            for (int i = 1; i < contacts.Count; i++)
            {
                TurnContact current = contacts[i];
                int j = i - 1;
                while (j >= 0 && Compare(contacts[j], current) > 0)
                {
                    contacts[j + 1] = contacts[j];
                    j--;
                }

                contacts[j + 1] = current;
            }
        }

        private static int OwnerRank(Side? owner) => owner.HasValue ? (int)owner.Value : ArenaOwnerRank;
    }
}
