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
            if (Damage.PerHitLimbCapFraction <= 0f || Damage.PerHitLimbCapFraction > 1f)
                issues.Add("Damage: per-hit limb cap must be in (0, 1].");
            if (Clash.MassWeight == Clash.SpeedWeight)
                issues.Add("Clash: GDD §10 requires the mass and speed weights to differ.");
            if (Clash.CrushRatio <= 1f) issues.Add("Clash: crush ratio must be greater than 1.");

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (WeaponStats weapon in Weapons)
            {
                if (string.IsNullOrEmpty(weapon.Id)) issues.Add("Weapons: a weapon has no id.");
                else if (!seenIds.Add(weapon.Id)) issues.Add($"Weapons: duplicate id '{weapon.Id}'.");
                if (weapon.InkLengthUnits <= 0f || weapon.InkThicknessUnits <= 0f)
                    issues.Add($"Weapons: '{weapon.Id}' needs a positive ink length and thickness.");
                if (weapon.SpeedUnitsPerSecond <= 0f) issues.Add($"Weapons: '{weapon.Id}' needs a positive speed.");
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
