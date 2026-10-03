using System;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// The default <see cref="IShieldBlockModel"/>: the options <see cref="ShieldBlockSettings"/> names, so every GDD option can be
    /// tried from the tuning panel.
    /// <list type="bullet">
    /// <item>D20 (<see cref="ShieldBlockSettings.Rule"/>): a hit is square on the face when its angle reaches
    /// <see cref="ShieldBlockSettings.SquareHitMinAngleDegrees"/> and it lands inside the rim
    /// (<see cref="ShieldBlockSettings.RimFraction"/>, edge counted as rim). The default fully blocks those and partially blocks the
    /// rest; "always full" and "always partial" ignore where the hit lands.</item>
    /// <item>D21 (<see cref="ShieldBlockSettings.HeavyWeapon"/>): a weapon of at least
    /// <see cref="ShieldBlockSettings.HeavyWeaponMinMass"/> may break through (a full block becomes partial) or stagger the holder
    /// when the block holds.</item>
    /// </list>
    /// </summary>
    public sealed class SettingsShieldBlockModel : IShieldBlockModel
    {
        private const float NoDamage = 0f;

        public BlockResult Resolve(BlockFacts facts, ShieldBlockSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(facts.AttackerWeapon, nameof(facts.AttackerWeapon));

            bool full = IsFull(facts, settings);
            bool heavy = facts.AttackerWeapon.Mass >= settings.HeavyWeaponMinMass;
            if (heavy && settings.HeavyWeapon == HeavyWeaponBlockRule.BreaksThrough) full = false;
            bool staggers = full && heavy && settings.HeavyWeapon == HeavyWeaponBlockRule.StaggersHolder;
            return new BlockResult(full, full ? NoDamage : settings.PartialBlockDamageMultiplier, staggers);
        }

        /// <summary>Square on the face: steep enough, and inside the rim.</summary>
        public static bool IsSquareOnTheFace(BlockFacts facts, ShieldBlockSettings settings) =>
            facts.ContactAngleDegrees >= Guard.NotNull(settings, nameof(settings)).SquareHitMinAngleDegrees &&
            facts.FacePositionFraction < 1f - settings.RimFraction;

        private static bool IsFull(BlockFacts facts, ShieldBlockSettings settings)
        {
            switch (settings.Rule)
            {
                case ShieldBlockRule.SquareFaceFullOtherwisePartial:
                    return IsSquareOnTheFace(facts, settings);
                case ShieldBlockRule.AlwaysFull:
                    return true;
                case ShieldBlockRule.AlwaysPartial:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings), settings.Rule, "Unknown shield block rule.");
            }
        }
    }
}
