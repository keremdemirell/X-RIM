using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Limbs
{
    /// <summary>
    /// The one place a sever changes the rules state (GDD §11, §12). The hit rules call it when a hit brings a limb to its
    /// durability; the simulation then breaks the joint by logic, never breakForce (§18), and drops the held item.
    /// <list type="bullet">
    /// <item>The limb is marked severed and a <see cref="LimbSeveredEvent"/> is recorded. It falls and stays on the floor, where it
    /// can become a club (§12).</item>
    /// <item>The arm holding the weapon, shield or club (the dominant arm while intact, then the other, §12) drops what it holds
    /// (<see cref="WeaponDroppedEvent"/>). The dummy keeps its current weapon: from the next turn its other hand holds it, at the
    /// off-hand damage share (§12, Decided), unless <see cref="LimbLossSettings.DroppedWeaponIsLost"/> (G2, off) takes it out of
    /// the loadout. A dropped club goes back to the floor.</item>
    /// <item>A lost leg changes nothing here: <see cref="IMobilityPenaltyPolicy"/> limits the next turn's body moves.</item>
    /// </list>
    /// </summary>
    public sealed class SeverRules
    {
        /// <param name="holdsItem">Whether the dummy holds its weapon, shield or club at this instant (it may have dropped it earlier this turn).</param>
        /// <returns>What the sever did; null when the limb was already severed.</returns>
        public SeverOutcome Sever(MatchState state, Side side, BodyPart part, SimTime time, bool holdsItem, LimbLossSettings settings)
        {
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(settings, nameof(settings));
            if (!part.IsSeverable()) throw new ArgumentException($"{part} cannot be severed: only arms and legs can (GDD §11).", nameof(part));

            FighterState fighter = state.Fighters[side];
            if (fighter.IsSevered(part)) return null;

            BodyPart? holdingArm = fighter.HeldItemArm;
            fighter.MarkSevered(part);
            var events = new List<MatchEvent> { new LimbSeveredEvent(time, side, part) };

            bool drops = holdsItem && holdingArm.HasValue && holdingArm.Value == part;
            if (!drops) return new SeverOutcome(time, side, part, false, default, null, events);

            WeaponId dropped = fighter.CurrentWeapon;
            events.Add(new WeaponDroppedEvent(time, side, dropped));
            SeveredLimbRef? club = fighter.HeldLimb;
            fighter.HeldLimb = null;
            if (!club.HasValue && settings.DroppedWeaponIsLost) fighter.LostWeapons.Add(dropped);
            return new SeverOutcome(time, side, part, true, dropped, club, events);
        }
    }
}
