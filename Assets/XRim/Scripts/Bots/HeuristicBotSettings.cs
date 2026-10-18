using System;
using XRim.Core.Gdd;

namespace XRim.Bots
{
    /// <summary>
    /// How the heuristic bot plays (Session 16): its difficulty (how sloppy its aim is, how often it just scribbles) and how much it likes
    /// each kind of play. Bot behaviour, never a game rule: every number is a placeholder to tune by watching it at PT6. Chances run from 0
    /// (never) to 1 (whenever the situation allows).
    /// </summary>
    [Serializable]
    public sealed class HeuristicBotSettings
    {
        [Placeholder("Bot difficulty: how far, in arena units, each aim point may miss its target; tune at PT6")]
        public float AimErrorUnits = 25f;

        [Placeholder("Bot difficulty: the share of turns played like the random bot, so it stays beatable and surprising; tune at PT6")]
        public float MistakeChance = 0.1f;

        [Placeholder("Bot style: plays a usable signature move when the opponent is within the move's pattern; tune at PT6")]
        public float SignatureChance = 0.6f;

        [Placeholder("Bot style: ducks under a heavier weapon that can reach it and thrusts low (the GDD §1 moment); tune at PT6")]
        public float DuckChance = 0.5f;

        [Placeholder("Bot style: swings an arc onto its target instead of thrusting (never with a rigid weapon); tune at PT6")]
        public float SwingChance = 0.35f;

        [Placeholder("Bot style: aims at the head (×2.5, can stun) instead of the torso when it reaches; tune at PT6")]
        public float HeadAimChance = 0.25f;

        [Placeholder("Bot style: takes up and raises its shield when threatened and low on HP; tune at PT6")]
        public float ShieldGuardChance = 0.5f;

        [Placeholder("Bot style: steps back when threatened and low on HP, never into its own standing wall; tune at PT6")]
        public float RetreatChance = 0.5f;

        [Placeholder("Bot style: jumps when threatened and doing nothing else with its body; tune at PT6")]
        public float JumpChance = 0.1f;

        [Placeholder("Bot style: at or below this share of its maximum HP the bot counts as low (shield, step back); tune at PT6")]
        public float LowHpFraction = 0.35f;

        [Placeholder("Bot style: a limb at or above this share of its durability becomes the target when in reach (hunting a sever); tune at PT6")]
        public float DamagedLimbFocusFraction = 0.5f;

        /// <summary>Appends a message for every value that cannot work.</summary>
        public void Validate(System.Collections.Generic.ICollection<string> issues)
        {
            if (AimErrorUnits < 0f) issues.Add("Bots: the aim error must not be negative.");
            foreach ((string name, float value) in new[]
                     {
                         (nameof(MistakeChance), MistakeChance), (nameof(SignatureChance), SignatureChance), (nameof(DuckChance), DuckChance),
                         (nameof(SwingChance), SwingChance), (nameof(HeadAimChance), HeadAimChance), (nameof(ShieldGuardChance), ShieldGuardChance),
                         (nameof(RetreatChance), RetreatChance), (nameof(JumpChance), JumpChance), (nameof(LowHpFraction), LowHpFraction),
                         (nameof(DamagedLimbFocusFraction), DamagedLimbFocusFraction),
                     })
            {
                if (value < 0f || value > 1f) issues.Add($"Bots: {name} must be between 0 and 1.");
            }
        }
    }
}
