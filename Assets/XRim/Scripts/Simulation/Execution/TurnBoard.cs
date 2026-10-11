using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// What lost limbs change on the board around a turn (GDD §12), so the physics world builds only what it is given.
    /// <list type="bullet">
    /// <item><b>Before the turn</b> (<see cref="Prepare"/>): a club the dummy switched away from drops to the floor where it was
    /// held; a dummy that picked the club and wields none takes the nearest free limb on the floor its
    /// <see cref="ILimbRetrievalPolicy"/> allows (S3), the nearer claim winning a limb both want, and holds nothing if none is left;
    /// the item each dummy holds keeps its grip; a dummy that dropped its item last turn (its other hand takes it up, G2) or picked
    /// a club up now with nothing in hand holds it in the guard stance; a dummy that held nothing and dropped nothing still holds
    /// nothing; an armless body blow strikes with the head; a dropped item is not rebuilt; a living dummy's split torso is whole
    /// again (S5: revived for sudden death).</item>
    /// <item><b>After the turn</b> (<see cref="Finish"/>): a club dropped with its arm lies on the floor where it fell.</item>
    /// </list>
    /// </summary>
    public static class TurnBoard
    {
        private static readonly Side[] BothSides = { Side.Left, Side.Right };

        /// <summary>The board this turn loads, from the frozen one; records each retrieved club in the state.</summary>
        public static PoseSnapshot Prepare(PoseSnapshot board, MatchState state, RulesSettings rules, SimulationSettings simulation,
            RulePolicies policies, IWeaponAimModel aim, IWeaponAimModel shieldAim)
        {
            Guard.NotNull(board, nameof(board));
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(rules, nameof(rules));
            Guard.NotNull(simulation, nameof(simulation));
            Guard.NotNull(policies, nameof(policies));
            PoseSnapshot pose = board.Clone();
            foreach (Side side in BothSides)
            {
                ReturnClubSwitchedAwayFrom(side, pose, state, rules, simulation, aim, shieldAim);
            }

            PerSide<bool> retrieved = RetrieveClubs(pose, state, rules, simulation, policies);
            foreach (Side side in BothSides)
            {
                PlaceHeldItem(side, pose, state, retrieved[side], rules, simulation, aim, shieldAim);
            }

            return pose;
        }

        /// <param name="wieldedAtStart">The club each side wielded when the turn started (after <see cref="Prepare"/>).</param>
        public static void Finish(PoseSnapshot pose, MatchState state, PerSide<SeveredLimbRef?> wieldedAtStart, RulesSettings rules,
            SimulationSettings simulation)
        {
            Guard.NotNull(pose, nameof(pose));
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(wieldedAtStart, nameof(wieldedAtStart));
            foreach (Side side in BothSides)
            {
                SeveredLimbRef? club = wieldedAtStart[side];
                if (!club.HasValue || state.Fighters[side].HeldLimb == club) continue;

                FighterPose fighter = pose.Get(side);
                BodyPose where = fighter.HasDroppedItem ? fighter.DroppedItem : fighter.HeldItem;
                pose.SeveredLimbs.Add(SeveredLimbLayout.FromClub(club.Value, where, simulation.Segmentation, simulation.Ragdoll, rules.Paths));
                fighter.HasDroppedItem = false;
            }
        }

        private static void ReturnClubSwitchedAwayFrom(Side side, PoseSnapshot pose, MatchState state, RulesSettings rules,
            SimulationSettings simulation, IWeaponAimModel aim, IWeaponAimModel shieldAim)
        {
            FighterState fighter = state.Fighters[side];
            if (!fighter.HeldLimb.HasValue || fighter.CurrentWeapon == WeaponIds.SeveredLimb) return;

            SeveredLimbRef limb = fighter.HeldLimb.Value;
            fighter.HeldLimb = null;
            FighterPose fighterPose = pose.Get(side);
            WeaponStats clubStats = rules.FindWeapon(WeaponIds.SeveredLimb);
            BodyPose club = fighterPose.HasHeldItem || clubStats == null
                ? fighterPose.HeldItem
                : GuardStance.HeldItemPose(clubStats, TurnStartRoot.Of(fighterPose), side, simulation.Ragdoll, rules.Paths, aim, shieldAim);
            pose.SeveredLimbs.Add(SeveredLimbLayout.FromClub(limb, club, simulation.Segmentation, simulation.Ragdoll, rules.Paths));
        }

        /// <summary>
        /// Every claim of a dummy on a floor limb, nearest first; each dummy takes one limb and each limb goes to one dummy. Returns
        /// which sides picked a club up.
        /// </summary>
        private static PerSide<bool> RetrieveClubs(PoseSnapshot pose, MatchState state, RulesSettings rules, SimulationSettings simulation,
            RulePolicies policies)
        {
            var claims = new List<Claim>();
            foreach (Side side in BothSides)
            {
                FighterState fighter = state.Fighters[side];
                if (fighter.CurrentWeapon != WeaponIds.SeveredLimb || fighter.HeldLimb.HasValue || !fighter.HeldItemArm.HasValue) continue;

                List<SeveredLimbRef> free = DesperationOptions.FreeLimbs(state, side, rules.Damage.LimbLoss);
                Vec2 shoulder = TorsoFrame.ToArena(rules.Paths.ShoulderOffsetUnits, TurnStartRoot.Of(pose.Get(side)), side);
                foreach (SeveredLimbPose limb in pose.SeveredLimbs)
                {
                    if (!free.Contains(limb.Limb)) continue;
                    float distance = SeveredLimbLayout.DistanceTo(limb, shoulder, simulation.Ragdoll, rules.Paths);
                    if (policies.LimbRetrieval != null && !policies.LimbRetrieval.CanRetrieve(fighter, distance, rules)) continue;
                    claims.Add(new Claim(side, limb.Limb, distance));
                }
            }

            claims.Sort(Claim.Compare);
            var taken = new List<SeveredLimbRef>();
            var retrieved = new PerSide<bool>(false, false);
            foreach (Claim claim in claims)
            {
                FighterState fighter = state.Fighters[claim.Side];
                if (fighter.HeldLimb.HasValue || taken.Contains(claim.Limb)) continue;
                fighter.HeldLimb = claim.Limb;
                taken.Add(claim.Limb);
                retrieved[claim.Side] = true;
            }

            pose.SeveredLimbs.RemoveAll(limb => taken.Contains(limb.Limb));
            return retrieved;
        }

        /// <param name="retrievedClub">The side picked a club up from the floor this turn.</param>
        private static void PlaceHeldItem(Side side, PoseSnapshot pose, MatchState state, bool retrievedClub, RulesSettings rules,
            SimulationSettings simulation, IWeaponAimModel aim, IWeaponAimModel shieldAim)
        {
            FighterState fighter = state.Fighters[side];
            FighterPose fighterPose = pose.Get(side);
            bool droppedLastTurn = fighterPose.HasDroppedItem;
            fighterPose.HasDroppedItem = false;
            if (!fighter.IsDead) fighterPose.IsTorsoSplit = false;

            WeaponStats weapon = rules.FindWeapon(fighter.CurrentWeapon);
            if (weapon != null && weapon.Kind == WeaponKind.BodyBlow)
            {
                fighterPose.HasHeldItem = fighter.RemainingArms == 0;
                if (fighterPose.HasHeldItem) fighterPose.HeldItem = HeldItemShape.HeadAsItem(fighterPose.Get(BodyPart.Head));
                return;
            }

            bool holds = weapon != null && fighter.HeldItemArm.HasValue && (weapon.Kind != WeaponKind.SeveredLimb || fighter.HeldLimb.HasValue);
            if (!holds)
            {
                fighterPose.HasHeldItem = false;
                return;
            }

            if (fighterPose.HasHeldItem || !(droppedLastTurn || retrievedClub)) return;
            fighterPose.HeldItem = GuardStance.HeldItemPose(weapon, TurnStartRoot.Of(fighterPose), side, simulation.Ragdoll, rules.Paths, aim, shieldAim);
            fighterPose.HasHeldItem = true;
        }

        private readonly struct Claim
        {
            public Side Side { get; }
            public SeveredLimbRef Limb { get; }
            public float DistanceUnits { get; }

            public Claim(Side side, SeveredLimbRef limb, float distanceUnits)
            {
                Side = side;
                Limb = limb;
                DistanceUnits = distanceUnits;
            }

            /// <summary>Nearest first; ties in a stable order (the claiming side, then the limb's owner and part).</summary>
            public static int Compare(Claim a, Claim b)
            {
                int order = a.DistanceUnits.CompareTo(b.DistanceUnits);
                if (order == 0) order = a.Side.CompareTo(b.Side);
                if (order == 0) order = a.Limb.Owner.CompareTo(b.Limb.Owner);
                return order != 0 ? order : a.Limb.Part.CompareTo(b.Limb.Part);
            }
        }
    }
}
