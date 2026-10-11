using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Rules.Limbs
{
    /// <summary>
    /// Which weapons a dummy that lost limbs can hold this turn (GDD §12), applied to each turn's planning limits when the turn starts:
    /// <list type="bullet">
    /// <item><b>The club</b> (Decided: a severed arm or leg on the floor can be wielded by a remaining arm): offered beyond the loadout
    /// while a free limb lies on the floor or the dummy already wields one, if it has an arm. Whose limbs count follows
    /// <see cref="LimbLossSettings.AnySeveredLimbCanBeWielded"/> (G1: any). Its slot follows
    /// <see cref="LoadoutSettings.SeveredLimbIsExtraOption"/>; see <see cref="OnWeaponLocked"/>.</item>
    /// <item><b>The body blow</b> (Decided: body blows only without arms): the only option once both arms are gone, and the dummy's
    /// weapon from then on.</item>
    /// <item>Weapons lost with their arm (G2's option) are forbidden.</item>
    /// </list>
    /// A current weapon that can no longer be held (the club with no limb left, a lost weapon) becomes the first one that can.
    /// Retrieving a limb from the floor happens when the turn executes (<see cref="ILimbRetrievalPolicy"/>).
    /// </summary>
    public static class DesperationOptions
    {
        private static readonly Side[] BothSides = { Side.Left, Side.Right };

        public static void Apply(MatchState state, Side side, IReadOnlyList<WeaponId> loadout, PlanningConstraints constraints,
            RulesSettings settings)
        {
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(loadout, nameof(loadout));
            Guard.NotNull(constraints, nameof(constraints));
            Guard.NotNull(settings, nameof(settings));

            FighterState fighter = state.Fighters[side];
            foreach (WeaponId lost in fighter.LostWeapons)
            {
                constraints.ForbidWeapon(lost);
            }

            if (fighter.RemainingArms == 0)
            {
                constraints.CanUseLoadoutWeapons = false;
                if (settings.FindWeapon(WeaponIds.BodyBlow) != null) constraints.AddExtraWeapon(WeaponIds.BodyBlow);
            }
            else if (settings.FindWeapon(WeaponIds.SeveredLimb) != null &&
                     (fighter.HeldLimb.HasValue || FreeLimbs(state, side, settings.Damage.LimbLoss).Count > 0))
            {
                constraints.AddExtraWeapon(WeaponIds.SeveredLimb);
            }

            if (constraints.IsWeaponAvailable(fighter.CurrentWeapon, loadout)) return;
            List<WeaponId> available = constraints.AvailableWeapons(loadout);
            if (available.Count > 0) fighter.CurrentWeapon = available[0];
        }

        /// <summary>
        /// Severed limbs lying on the floor that this dummy may pick up as a club: every severed arm and leg of the allowed owners
        /// (G1) that nobody wields, in a stable order (Left's before Right's, by body part).
        /// </summary>
        public static List<SeveredLimbRef> FreeLimbs(MatchState state, Side wielder, LimbLossSettings settings)
        {
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(settings, nameof(settings));
            var free = new List<SeveredLimbRef>();
            foreach (Side owner in BothSides)
            {
                if (owner != wielder && !settings.AnySeveredLimbCanBeWielded) continue;
                FighterState fighter = state.Fighters[owner];
                foreach (BodyPart part in BodyParts.All)
                {
                    if (!part.IsSeverable() || !fighter.IsSevered(part)) continue;
                    var limb = new SeveredLimbRef(owner, part);
                    if (!IsWielded(state, limb)) free.Add(limb);
                }
            }

            return free;
        }

        /// <summary>
        /// When a plan locks. With <see cref="LoadoutSettings.SeveredLimbIsExtraOption"/> off (S4), picking up the club gives up the
        /// loadout weapon held before it for the rest of the match: the club takes its slot. Call before the current weapon changes.
        /// </summary>
        public static void OnWeaponLocked(FighterState fighter, WeaponId locked, IReadOnlyList<WeaponId> loadout, RulesSettings settings)
        {
            Guard.NotNull(fighter, nameof(fighter));
            Guard.NotNull(loadout, nameof(loadout));
            Guard.NotNull(settings, nameof(settings));
            if (settings.Loadout.SeveredLimbIsExtraOption || locked != WeaponIds.SeveredLimb) return;

            WeaponId previous = fighter.CurrentWeapon;
            if (previous == WeaponIds.SeveredLimb) return;
            foreach (WeaponId slot in loadout)
            {
                if (slot == previous) fighter.LostWeapons.Add(previous);
            }
        }

        private static bool IsWielded(MatchState state, SeveredLimbRef limb)
        {
            foreach (Side side in BothSides)
            {
                if (state.Fighters[side].HeldLimb == limb) return true;
            }

            return false;
        }
    }
}
