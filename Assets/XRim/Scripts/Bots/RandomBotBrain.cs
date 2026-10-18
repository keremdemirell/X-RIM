using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;

namespace XRim.Bots
{
    /// <summary>
    /// Debug bot that emits only valid plans, all through the context's seeded <see cref="IRandom"/>: a weapon it can hold
    /// this turn (the loadout plus the §12 extra options: the severed-limb club, the armless body blow; switching only when the
    /// constraints allow it), a random allowed body move, then either a signature move (§8: sometimes, when its window says one can be
    /// used and the move works with the weapon) or one stroke that stays inside the weapon's ink and reach, and Ready. It always
    /// attacks, so it never idles and never forfeits. Enough to exercise the turn loop offline; not a design for a real AI opponent.
    /// </summary>
    public sealed class RandomBotBrain : IBotBrain
    {
        /// <summary>
        /// How often the bot plays a signature move when it can (bot behaviour only, not a game rule): about one turn in four, so with the
        /// once-per-match default a move usually comes in the first few turns.
        /// </summary>
        public const float DefaultSignatureChance = 0.25f;

        private readonly float _signatureChance;

        /// <param name="signatureChance">The chance to play a usable signature move instead of drawing (0 never, 1 whenever it can).</param>
        public RandomBotBrain(float signatureChance = DefaultSignatureChance)
        {
            _signatureChance = signatureChance;
        }

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
            PlanningConstraints constraints = context.Constraints;
            WeaponId weapon = ChooseWeapon(context, constraints);
            BodyMove bodyMove = ChooseBodyMove(context, constraints);
            SignatureMoveId signature = ChooseSignature(context, constraints, weapon);

            // A signature move's pattern is built by the rules when the move is sent, so its plan carries no stroke.
            WeaponPath path = signature.IsEmpty ? DrawStroke(context, constraints, weapon) : WeaponPath.Empty;
            return new TurnPlan(weapon, bodyMove, path, signature, true);
        }

        /// <summary>
        /// A random usable signature move, at <see cref="DefaultSignatureChance"/> by default, or none. Only moves the rules will take:
        /// the window says a move can be used this turn, the move works with the weapon, and a move with its own body move has one allowed.
        /// No random number is drawn when no move can be used, so a bot without signature moves plays exactly as before.
        /// </summary>
        private SignatureMoveId ChooseSignature(BotContext context, PlanningConstraints constraints, WeaponId weapon)
        {
            SignatureOptions options = context.Signatures;
            if (!options.CanUse) return default;

            var usable = new List<SignatureMoveStats>();
            foreach (SignatureMoveStats move in options.Moves)
            {
                if (!move.AllowsWeapon(weapon)) continue;
                if (move.OverridesBodyMove && !constraints.IsBodyMoveAllowed(move.BodyMoveOverride)) continue;
                usable.Add(move);
            }

            if (usable.Count == 0 || context.Random.NextFloat() >= _signatureChance) return default;
            return usable[context.Random.NextInt(0, usable.Count)].MoveId;
        }

        private static WeaponId ChooseWeapon(BotContext context, PlanningConstraints constraints)
        {
            WeaponId current = context.CurrentWeapon;
            List<WeaponId> available = constraints.AvailableWeapons(context.Loadout);
            if (!constraints.CanSwitchWeapon || available.Count == 0) return current;

            return available[context.Random.NextInt(0, available.Count)];
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

            Vec2 tip = context.WeaponTips.TipLocal(weapon);
            float reach = ReachLimit.For(paths, stats).RadiusUnits;
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
