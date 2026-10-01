using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Bots
{
    /// <summary>
    /// Debug bot that emits only valid plans, all through the context's seeded <see cref="IRandom"/>: a weapon from
    /// the loadout (switching only when the constraints allow it), a random allowed body move, one stroke that stays
    /// inside the weapon's ink and reach, and Ready. It always draws, so it never idles and never forfeits.
    /// Enough to exercise the turn loop offline; not a design for a real AI opponent.
    /// </summary>
    public sealed class RandomBotBrain : IBotBrain
    {
        /// <summary>How far toward the opponent the stroke may aim, either side of straight ahead.</summary>
        private const float MaxAimAngleDegrees = 60f;

        /// <summary>The stroke ends between these shares of the reach, measured from the shoulder.</summary>
        private const float MinReachShare = 0.5f;
        private const float MaxReachShare = 1f;

        /// <summary>The stroke uses at most this share of the ink, so rounding never tips it over the budget.</summary>
        private const float InkSafetyShare = 0.95f;

        /// <summary>The stroke bows sideways by up to this share of its length.</summary>
        private const float MaxBowShare = 0.25f;

        private static readonly BodyMove[] Candidates =
        {
            BodyMove.None, BodyMove.Crouch, BodyMove.Lunge, BodyMove.StepBack, BodyMove.Jump,
        };

        public TurnPlan PlanTurn(BotContext context)
        {
            Guard.NotNull(context, nameof(context));
            PlanningConstraints constraints = context.Window.Constraints[context.Side];
            WeaponId weapon = ChooseWeapon(context, constraints);
            BodyMove bodyMove = ChooseBodyMove(context, constraints);
            WeaponPath path = DrawStroke(context, constraints, weapon);
            return new TurnPlan(weapon, bodyMove, path, default, true);
        }

        private static WeaponId ChooseWeapon(BotContext context, PlanningConstraints constraints)
        {
            WeaponId current = context.Window.Board.State.Fighters[context.Side].CurrentWeapon;
            if (!constraints.CanSwitchWeapon || context.Loadout.Count == 0) return current;

            return context.Loadout[context.Random.NextInt(0, context.Loadout.Count)];
        }

        private static BodyMove ChooseBodyMove(BotContext context, PlanningConstraints constraints)
        {
            var allowed = new List<BodyMove>();
            foreach (BodyMove move in Candidates)
            {
                if (constraints.IsBodyMoveAllowed(move)) allowed.Add(move);
            }

            return allowed[context.Random.NextInt(0, allowed.Count)];
        }

        /// <summary>
        /// One stroke from the weapon tip toward a random point in reach, bowed a little. The tip is the stroke's first
        /// point, so the lead-in costs no ink, and the stroke is scaled to fit the ink left after the bow.
        /// </summary>
        private static WeaponPath DrawStroke(BotContext context, PlanningConstraints constraints, WeaponId weapon)
        {
            WeaponStats stats = context.Settings.FindWeapon(weapon)
                ?? throw new InvalidOperationException($"The rules settings have no stats for weapon '{weapon}'.");
            stats = stats.WithInkLengthMultiplier(constraints.InkLengthMultiplier);
            PathSettings paths = context.Settings.Paths;
            IRandom random = context.Random;

            Vec2 tip = context.Window.WeaponTips[context.Side].TipLocal(weapon);
            float reach = paths.ArmLengthUnits + stats.LengthUnits;
            float aimDegrees = (random.NextFloat() * 2f - 1f) * MaxAimAngleDegrees;
            float reachShare = MinReachShare + random.NextFloat() * (MaxReachShare - MinReachShare);
            Vec2 end = paths.ShoulderOffsetUnits + Vec2.FromAngleDegrees(aimDegrees) * (reach * reachShare);
            float bow = (random.NextFloat() * 2f - 1f) * MaxBowShare;

            Vec2 chord = end - tip;
            Vec2 sideways = new Vec2(-chord.Y, chord.X);
            Vec2 middle = tip + chord * 0.5f + sideways * bow;

            // A bow near the edge of the reach could poke out of it: pull the middle back onto the reach circle.
            Vec2 fromShoulder = middle - paths.ShoulderOffsetUnits;
            if (fromShoulder.Length > reach) middle = paths.ShoulderOffsetUnits + fromShoulder.Normalized * reach;

            float length = Vec2.Distance(tip, middle) + Vec2.Distance(middle, end);
            float fit = length > 0f ? Math.Min(1f, stats.InkLengthUnits * InkSafetyShare / length) : 1f;

            // Scaling every point toward the tip keeps the stroke inside the reach circle (the tip is inside it).
            return new WeaponPath(new[] { tip, tip + (middle - tip) * fit, tip + (end - tip) * fit });
        }
    }
}
