using System;
using System.Collections.Generic;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Every tunable the rules and simulation read, in one serializable snapshot. ScriptableObjects in
    /// XRim.Config wrap these classes for editing; a match takes a copy at start so live tuning never
    /// changes a turn mid-simulation. A server can load the same snapshot as JSON.
    /// </summary>
    [Serializable]
    public sealed class RulesSettings
    {
        /// <summary>GDD §11: two hits at this cap would already sever a limb, but severing needs at least 3.</summary>
        private const float MaxPerHitLimbCapFraction = 0.5f;

        /// <summary>GDD §10: a contact angle runs from 0° (sliding) to 90° (square impact).</summary>
        private const float MaxContactAngleDegrees = 90f;

        public MatchSettings Match = new MatchSettings();
        public PathSettings Paths = new PathSettings();
        public DamageSettings Damage = new DamageSettings();
        public HitZoneSettings HitZones = new HitZoneSettings();
        public ClashSettings Clash = new ClashSettings();
        public ElectricWallSettings ElectricWall = new ElectricWallSettings();
        public ArenaSettings Arena = new ArenaSettings();
        public LoadoutSettings Loadout = new LoadoutSettings();
        public SignatureSettings Signature = new SignatureSettings();
        public SuddenDeathSettings SuddenDeath = new SuddenDeathSettings();
        public List<WeaponStats> Weapons = new List<WeaponStats>();
        public List<BodyMoveStats> BodyMoves = new List<BodyMoveStats>();
        public List<SignatureMoveStats> SignatureMoves = new List<SignatureMoveStats>();

        public WeaponStats FindWeapon(WeaponId id)
        {
            foreach (WeaponStats weapon in Weapons)
            {
                if (weapon.WeaponId == id) return weapon;
            }

            return null;
        }

        public BodyMoveStats FindBodyMove(BodyMove move)
        {
            foreach (BodyMoveStats stats in BodyMoves)
            {
                if (stats.Move == move) return stats;
            }

            return null;
        }

        /// <summary>
        /// These settings with another body-move list (debug: the sandbox tries body-move tuning from the next turn). A shallow
        /// copy that shares everything else with this snapshot, so treat it as read-only.
        /// </summary>
        public RulesSettings WithBodyMoves(List<BodyMoveStats> bodyMoves)
        {
            var copy = (RulesSettings)MemberwiseClone();
            copy.BodyMoves = bodyMoves ?? throw new ArgumentNullException(nameof(bodyMoves));
            return copy;
        }

        /// <summary>
        /// Appends a message for every value that breaks a GDD rule or cannot work. Placeholders are
        /// not errors. Called by the Config SOs (OnValidate) and by tests.
        /// </summary>
        public void Validate(ICollection<string> issues)
        {
            if (Match.PlanningDurationSeconds <= 0f) issues.Add("Match: planning duration must be positive.");
            if (Match.WeaponSwitchLockoutSeconds >= Match.PlanningDurationSeconds)
                issues.Add("Match: weapon switch lock-out must be shorter than the planning phase.");
            if (Match.TurnCap <= 0) issues.Add("Match: turn cap must be positive.");
            if (Match.ExecutionHardCapSeconds <= 0f) issues.Add("Match: execution hard cap must be positive.");
            if (Paths.SampleSpacingUnits <= 0f) issues.Add("Paths: sample spacing must be positive.");
            if (Paths.ArmLengthUnits <= 0f) issues.Add("Paths: arm length must be positive.");
            if (Damage.MaxHp <= 0f) issues.Add("Damage: max HP must be positive.");
            if (Damage.PerHitLimbCapFraction <= 0f || Damage.PerHitLimbCapFraction >= MaxPerHitLimbCapFraction)
                issues.Add("Damage: per-hit limb cap must be above 0 and below 0.5, so severing needs at least 3 hits (GDD §11).");
            if (Damage.ArmDurability <= 0f || Damage.LegDurability <= 0f) issues.Add("Damage: limb durability must be positive.");
            if (Damage.OffHandDamageMultiplier < 0f) issues.Add("Damage: the off-hand multiplier must not be negative.");
            if (Damage.HeadStunThreshold < 0f) issues.Add("Damage: the head stun threshold must not be negative.");
            if (Damage.StunPlanningDurationFraction <= 0f || Damage.StunPlanningDurationFraction > 1f)
                issues.Add("Damage: a stun's planning fraction must be in (0, 1].");
            else if (Match.PlanningDurationSeconds * Damage.StunPlanningDurationFraction <= Match.WeaponSwitchLockoutSeconds)
                issues.Add("Damage: a stun's shorter planning phase must stay longer than the weapon switch lock-out.");
            if (Damage.StunInkLengthFraction <= 0f || Damage.StunInkLengthFraction > 1f)
                issues.Add("Damage: a stun's ink fraction must be in (0, 1].");
            if (Damage.InterruptDamageThreshold < 0f) issues.Add("Damage: the interrupt damage threshold must not be negative.");
            if (Damage.MaxHitsPerWeaponPerTurn < 1) issues.Add("Damage: a weapon must be able to land at least 1 hit per turn.");
            if (Clash.MassWeight == Clash.SpeedWeight)
                issues.Add("Clash: GDD §10 requires the mass and speed weights to differ.");
            if (Clash.CrushRatio <= 1f) issues.Add("Clash: crush ratio must be greater than 1.");
            if (Clash.HardClashAngleDegrees < 0f || Clash.HardClashAngleDegrees > MaxContactAngleDegrees)
                issues.Add("Clash: the hard-clash angle must be between 0° and 90°.");
            if (Clash.MassWeight < 0f || Clash.SpeedWeight < 0f) issues.Add("Clash: the mass and speed weights must not be negative.");
            if (Clash.CrushThroughDamageMultiplier < 0f) issues.Add("Clash: the crush-through damage multiplier must not be negative.");
            if (Clash.GlancingSimilarMassBandFraction < 0f) issues.Add("Clash: the similar-mass band must not be negative.");
            if (Clash.MaxResolvedContactsPerWeaponPair < 0)
                issues.Add("Clash: the contacts resolved per weapon pair must not be negative (0 = every contact).");
            ShieldBlockSettings block = Clash.ShieldBlock;
            if (block == null)
            {
                issues.Add("Clash: the shield block settings are missing.");
            }
            else
            {
                if (block.PartialBlockDamageMultiplier < 0f || block.PartialBlockDamageMultiplier > 1f)
                    issues.Add("Shield block: the partial block damage multiplier must be in [0, 1].");
                if (block.SquareHitMinAngleDegrees < 0f || block.SquareHitMinAngleDegrees > MaxContactAngleDegrees)
                    issues.Add("Shield block: the square-hit angle must be between 0° and 90°.");
                if (block.RimFraction < 0f || block.RimFraction > 1f) issues.Add("Shield block: the rim fraction must be in [0, 1].");
                if (block.HeavyWeaponMinMass < 0f) issues.Add("Shield block: the heavy weapon mass must not be negative.");
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (WeaponStats weapon in Weapons)
            {
                if (string.IsNullOrEmpty(weapon.Id)) issues.Add("Weapons: a weapon has no id.");
                else if (!seenIds.Add(weapon.Id)) issues.Add($"Weapons: duplicate id '{weapon.Id}'.");
                if (weapon.InkLengthUnits <= 0f || weapon.InkThicknessUnits <= 0f)
                    issues.Add($"Weapons: '{weapon.Id}' needs a positive ink length and thickness.");
                if (weapon.SpeedUnitsPerSecond <= 0f) issues.Add($"Weapons: '{weapon.Id}' needs a positive speed.");
                if (weapon.BaseDamage < 0f) issues.Add($"Weapons: '{weapon.Id}' base damage must not be negative.");
                if (weapon.SpeedKeptAfterHitFraction < 0f || weapon.SpeedKeptAfterHitFraction > 1f)
                    issues.Add($"Weapons: '{weapon.Id}' speed kept after a hit must be in [0, 1].");
                if (weapon.LengthUnits <= 0f)
                    issues.Add($"Weapons: '{weapon.Id}' needs a positive length (run XRim > Setup > Fill New Tuning Fields).");
                RigiditySettings rigidity = weapon.Rigidity;
                if (rigidity != null && rigidity.Enabled)
                {
                    if (rigidity.BendCostK < 0f) issues.Add($"Weapons: '{weapon.Id}' rigidity k must not be negative.");
                    if (rigidity.BreakAngleDegrees <= rigidity.BendThresholdDegrees)
                        issues.Add($"Weapons: '{weapon.Id}' rigidity break angle must be above the bend threshold.");
                    if (rigidity.BreakWindowUnits < 0f)
                        issues.Add($"Weapons: '{weapon.Id}' rigidity break window must not be negative.");
                }
            }

            var seenMoves = new HashSet<BodyMove>();
            foreach (BodyMoveStats move in BodyMoves)
            {
                if (!seenMoves.Add(move.Move)) issues.Add($"Body moves: '{move.Move}' is listed twice.");
                if (move.DurationSeconds < 0f) issues.Add($"Body moves: '{move.Move}' duration must not be negative.");
                if (move.StrideUnits < 0f || move.FootLiftUnits < 0f)
                    issues.Add($"Body moves: '{move.Move}' stride and foot lift must not be negative.");
            }

            WeaponStats rapier = FindWeapon(WeaponIds.Rapier);
            WeaponStats mace = FindWeapon(WeaponIds.Mace);
            if (rapier != null && mace != null)
            {
                if (rapier.SpeedUnitsPerSecond <= mace.SpeedUnitsPerSecond)
                    issues.Add("Weapons: GDD §9 sets the order rapier fast, mace slow.");
                if (rapier.BaseDamage >= mace.BaseDamage)
                    issues.Add("Weapons: GDD §6 roster gives the rapier low damage and the mace high damage.");
            }
        }
    }
}
