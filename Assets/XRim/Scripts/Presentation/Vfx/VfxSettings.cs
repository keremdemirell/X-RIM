using XRim.Config;
using XRim.Core;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// How much the mechanical effects spray, and how the struck dummy and the weapon trails are drawn (GDD §2; presentation only). Plain
    /// data read from <see cref="FeelConfig"/> at each turn, so tuning edits show on the next one.
    /// </summary>
    public sealed class VfxSettings
    {
        public int SeverDebrisPieces;
        public int SeverFluidDrops;
        public int SeverSprings;
        public int SeverGears;
        public float DeathBurstMultiplier = 1f;
        public int DeathSteamPuffs;
        public float DebrisSpeedUnitsPerSecond;
        public float DebrisSizeUnits;
        public float FluidDropSizeUnits;
        public float DebrisBounceFraction;

        public int HitSparksBase;
        public float HitSparksPerDamage;
        public int MaxHitSparks;
        public float HeavyHitMinDamage;
        public int HeavyHitDebrisPieces;
        public int ClashSparks;
        public int CrushDebrisPieces;
        public float SparkSpeedUnitsPerSecond;
        public float SparkSeconds;
        public float SparkSizeUnits;
        public int MaxPieces;

        public float PartFlashSeconds;
        public float FlinchDegreesPerDamage;
        public float MaxFlinchDegrees;
        public float FlinchPushUnitsPerDamage;
        public float MaxFlinchPushUnits;
        public float FlinchSeconds;

        public float TrailSeconds;
        public float TrailWidthMultiplier;
        public float TrailMinSpeedUnitsPerSecond;

        /// <summary>The simulation's gravity, so debris falls like the dummies do (arena units per second squared).</summary>
        public float GravityUnitsPerSecondSquared;

        public static VfxSettings From(FeelConfig config, float gravityUnitsPerSecondSquared)
        {
            Guard.NotNull(config, nameof(config));
            return new VfxSettings
            {
                SeverDebrisPieces = config.DebrisPieces,
                SeverFluidDrops = config.FluidDrops,
                SeverSprings = config.SeverSprings,
                SeverGears = config.SeverGears,
                DeathBurstMultiplier = config.DeathBurstMultiplier,
                DeathSteamPuffs = config.DeathSteamPuffs,
                DebrisSpeedUnitsPerSecond = config.DebrisSpeedUnitsPerSecond,
                DebrisSizeUnits = config.DebrisSizeUnits,
                FluidDropSizeUnits = config.FluidDropSizeUnits,
                DebrisBounceFraction = config.DebrisBounceFraction,
                HitSparksBase = config.HitSparksBase,
                HitSparksPerDamage = config.HitSparksPerDamage,
                MaxHitSparks = config.MaxHitSparks,
                HeavyHitMinDamage = config.HeavyHitMinDamage,
                HeavyHitDebrisPieces = config.HeavyHitDebrisPieces,
                ClashSparks = config.ClashSparks,
                CrushDebrisPieces = config.CrushDebrisPieces,
                SparkSpeedUnitsPerSecond = config.SparkSpeedUnitsPerSecond,
                SparkSeconds = config.SparkSeconds,
                SparkSizeUnits = config.SparkSizeUnits,
                MaxPieces = config.MaxVfxPieces,
                PartFlashSeconds = config.PartFlashSeconds,
                FlinchDegreesPerDamage = config.FlinchDegreesPerDamage,
                MaxFlinchDegrees = config.MaxFlinchDegrees,
                FlinchPushUnitsPerDamage = config.FlinchPushUnitsPerDamage,
                MaxFlinchPushUnits = config.MaxFlinchPushUnits,
                FlinchSeconds = config.FlinchSeconds,
                TrailSeconds = config.TrailSeconds,
                TrailWidthMultiplier = config.TrailWidthMultiplier,
                TrailMinSpeedUnitsPerSecond = config.TrailMinSpeedUnitsPerSecond,
                GravityUnitsPerSecondSquared = gravityUnitsPerSecondSquared,
            };
        }
    }
}
