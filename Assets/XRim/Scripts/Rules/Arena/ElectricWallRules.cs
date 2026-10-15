using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Arena
{
    /// <summary>
    /// Electric wall sequence (GDD §13, Decided). Only the player's input counts: a backward swipe triggers it; being knocked back,
    /// holding a shield or standing still never does, because the sequence reads nothing but the planned body move, at lock.
    /// <list type="table">
    /// <item><term>Turn N, backward</term><description>The warning springs up behind the dummy when its step back ends. No damage.</description></item>
    /// <item><term>N+1, anything else</term><description>The wall goes away completely (including no body move).</description></item>
    /// <item><term>N+1, backward again</term><description>The dummy hits the wall: damage plus a bounce, and the wall moves further forward.</description></item>
    /// <item><term>N+2 and later, backward</term><description>Damage and bounce again, and the wall advances again.</description></item>
    /// </list>
    /// Each player has their own wall, so both can be up at once. Decided with the designer (2026-10-11, not in the GDD):
    /// <list type="bullet">
    /// <item>P1 geometry: the wall stands behind its owner, away from the opponent; the warning springs up
    /// <see cref="ElectricWallSettings.SpawnOffsetBehindUnits"/> behind the pelvis, never beyond a solid arena edge (on the edge line
    /// there); a zap throws the dummy <see cref="ElectricWallSettings.BounceDistanceUnits"/> forward and the wall advances
    /// <see cref="ElectricWallSettings.AdvanceDistanceUnits"/> toward the opponent. Only the owner's body touching it counts.</item>
    /// <item>P2 damage: a flat HP loss (no zone multiplier, no limb damage, no interrupt or stun, no D27 clamp); it can knock out.</item>
    /// <item>P3: any turn without a backward swipe removes the wall, at every step of the sequence, which starts over.</item>
    /// </list>
    /// The rules decide; the simulation places the wall, detects the touch and applies the bounce and the advance.
    /// </summary>
    public sealed class ElectricWallRules
    {
        /// <summary>Walls work everywhere except in sudden death with <see cref="ElectricWallSettings.ActiveInSuddenDeath"/> off (§14, TBD).</summary>
        public static bool AreLive(bool isSuddenDeath, ElectricWallSettings settings) =>
            !isSuddenDeath || Guard.NotNull(settings, nameof(settings)).ActiveInSuddenDeath;

        /// <summary>
        /// At lock: this turn's wall, from the planned body move alone. A backward swipe with a wall standing behind the dummy makes it
        /// active (it zaps when the dummy touches it); a first backward swipe raises the warning this turn; anything else, or walls that
        /// are not live in sudden death, removes it.
        /// </summary>
        public void OnPlansLocked(ElectricWallState wall, BodyMove plannedMove, bool isSuddenDeath, ElectricWallSettings settings)
        {
            Guard.NotNull(wall, nameof(wall));
            if (!AreLive(isSuddenDeath, settings) || !plannedMove.IsBackward())
            {
                wall.Clear();
                return;
            }

            if (wall.IsUp && wall.Phase != ElectricWallPhase.Inactive)
            {
                wall.Phase = ElectricWallPhase.Active;
                wall.ConsecutiveRetreats++;
                return;
            }

            // The first retreat, or one after a warning that never stood: the warning springs up this turn.
            wall.Phase = ElectricWallPhase.Warning;
            wall.IsUp = false;
            wall.ConsecutiveRetreats = 1;
        }

        /// <summary>Where a warning wall stands for a dummy whose pelvis is here: behind it, away from the opponent, inside solid edges.</summary>
        public static float WarningPositionUnits(Side side, float pelvisXUnits, ArenaEdges edges, ElectricWallSettings settings) =>
            edges.ClampX(pelvisXUnits - side.FacingSign() * Guard.NotNull(settings, nameof(settings)).SpawnOffsetBehindUnits, 0f);

        /// <summary>The warning springs up behind the dummy (the step back ended). It never hurts. Returns its event for the timeline.</summary>
        public ElectricWallEvent RaiseWarning(SimTime time, Side side, ElectricWallState wall, float pelvisXUnits, ArenaEdges edges,
            ElectricWallSettings settings)
        {
            Guard.NotNull(wall, nameof(wall));
            if (wall.Phase != ElectricWallPhase.Warning || wall.IsUp)
                throw new InvalidOperationException($"Only a warning raised this turn can spring up; the {side} wall is {wall.Phase} (up: {wall.IsUp}).");

            wall.PositionUnits = WarningPositionUnits(side, pelvisXUnits, edges, settings);
            wall.IsUp = true;
            return new ElectricWallEvent(time, side, ElectricWallPhase.Warning, 0f, wall.PositionUnits);
        }

        /// <summary>
        /// The dummy's body touched its own wall at this time. The first touch of a retreat turn is the zap (damage, bounce, advance); a
        /// later one in the same turn (<paramref name="retreatZapDone"/>: it was knocked back into the wall) only deals damage, and only
        /// with <see cref="ElectricWallSettings.DamageWhenKnockedInto"/> on. A warning wall, a wall that is not up and a dead dummy do
        /// nothing: null. The HP loss is applied to the state here.
        /// </summary>
        public ElectricWallZap OnTouched(SimTime time, Side side, MatchState state, bool retreatZapDone, ArenaEdges edges,
            ElectricWallSettings settings)
        {
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(settings, nameof(settings));
            ElectricWallState wall = state.Walls[side];
            FighterState fighter = state.Fighters[side];
            if (!wall.IsUp || wall.Phase != ElectricWallPhase.Active || fighter.IsDead) return null;
            if (retreatZapDone && !settings.DamageWhenKnockedInto) return null;

            float before = fighter.Hp;
            fighter.Hp = Math.Max(0f, before - ZapDamage(wall, settings));
            float hpDamage = before - fighter.Hp;
            fighter.HpDamageTaken += hpDamage;
            if (!retreatZapDone)
                wall.PositionUnits = edges.ClampX(wall.PositionUnits + side.FacingSign() * settings.AdvanceDistanceUnits, 0f);

            var events = new List<MatchEvent> { new ElectricWallEvent(time, side, ElectricWallPhase.Active, hpDamage, wall.PositionUnits, retreatZapDone) };
            bool died = fighter.IsDead;
            if (died) events.Add(new FighterDiedEvent(time, side));
            return new ElectricWallZap(side, hpDamage, died, retreatZapDone, retreatZapDone ? 0f : settings.BounceDistanceUnits,
                wall.PositionUnits, events);
        }

        /// <summary>
        /// What one zap costs: <see cref="ElectricWallSettings.Damage"/>, plus <see cref="ElectricWallSettings.DamageGrowthPerConsecutiveRetreat"/>
        /// for each zap in a row before this one (the first zap is the second retreat). Never negative.
        /// </summary>
        public static float ZapDamage(ElectricWallState wall, ElectricWallSettings settings)
        {
            Guard.NotNull(wall, nameof(wall));
            Guard.NotNull(settings, nameof(settings));
            int earlierZaps = Math.Max(0, wall.ConsecutiveRetreats - 2);
            return Math.Max(0f, settings.Damage + settings.DamageGrowthPerConsecutiveRetreat * earlierZaps);
        }
    }
}
