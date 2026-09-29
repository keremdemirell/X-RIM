using System.Collections.Generic;
using XRim.Core;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Starting values from the GDD roster (§6) and Appendix A, plus clearly marked placeholders where the
    /// GDD has no number yet. The Editor's "Create Default Tuning Assets" menu and the tests build from here,
    /// so this is the one place the prototype's first numbers are written down.
    /// Global tunables (match, clash, damage...) take their Appendix A values from the field initializers
    /// of their settings classes.
    /// </summary>
    public static class GddStartingValues
    {
        public static RulesSettings CreateRulesSettings()
        {
            var settings = new RulesSettings();
            settings.Weapons.AddRange(CreateWeapons());
            settings.BodyMoves.AddRange(CreateBodyMoves());
            return settings;
        }

        public static List<WeaponStats> CreateWeapons() => new List<WeaponStats>
        {
            Rapier(), Sword(), Mace(), Spear(), Shield(), SeveredLimb(),
        };

        /// <summary>Appendix A: ink 600×10, mass 2. Fast, low damage. Length, speed and damage are placeholders.</summary>
        public static WeaponStats Rapier() => new WeaponStats
        {
            Id = WeaponIds.Rapier.Value, DisplayName = "Rapier", Kind = WeaponKind.Weapon,
            InkLengthUnits = 600f, InkThicknessUnits = 10f, LengthUnits = 400f, Mass = 2f,
            SpeedUnitsPerSecond = 900f, BaseDamage = 8f,
        };

        /// <summary>Appendix A: ink 200×30, mass 10. Slow, high damage. Length, speed and damage are placeholders.</summary>
        public static WeaponStats Mace() => new WeaponStats
        {
            Id = WeaponIds.Mace.Value, DisplayName = "Mace", Kind = WeaponKind.Weapon,
            InkLengthUnits = 200f, InkThicknessUnits = 30f, LengthUnits = 220f, Mass = 10f,
            SpeedUnitsPerSecond = 250f, BaseDamage = 20f,
        };

        /// <summary>GDD §6: design TBD. Every number is a placeholder.</summary>
        public static WeaponStats Sword() => new WeaponStats
        {
            Id = WeaponIds.Sword.Value, DisplayName = "Sword", Kind = WeaponKind.Weapon, DesignIsTbd = true,
            InkLengthUnits = 400f, InkThicknessUnits = 15f, LengthUnits = 320f, Mass = 5f,
            SpeedUnitsPerSecond = 600f, BaseDamage = 12f,
        };

        /// <summary>GDD §6: "Long, TBD", longest reach, straight thrusts. Rigidity is the TBD spear rule.</summary>
        public static WeaponStats Spear()
        {
            var spear = new WeaponStats
            {
                Id = WeaponIds.Spear.Value, DisplayName = "Spear", Kind = WeaponKind.Weapon, DesignIsTbd = true,
                InkLengthUnits = 800f, InkThicknessUnits = 8f, LengthUnits = 500f, Mass = 4f,
                SpeedUnitsPerSecond = 700f, BaseDamage = 10f,
            };
            spear.Rigidity.Enabled = true;
            return spear;
        }

        /// <summary>GDD §7: "very low (bash only)" damage; short and thick ink suggested. Placeholders.</summary>
        public static WeaponStats Shield() => new WeaponStats
        {
            Id = WeaponIds.Shield.Value, DisplayName = "Shield", Kind = WeaponKind.Shield, DesignIsTbd = true,
            InkLengthUnits = 150f, InkThicknessUnits = 40f, LengthUnits = 150f, Mass = 8f,
            SpeedUnitsPerSecond = 400f, BaseDamage = 3f, KnockbackImpulse = 5f,
        };

        /// <summary>GDD §12: blunt club after dismemberment; stats TBD. Placeholders.</summary>
        public static WeaponStats SeveredLimb() => new WeaponStats
        {
            Id = WeaponIds.SeveredLimb.Value, DisplayName = "Severed limb", Kind = WeaponKind.SeveredLimb, DesignIsTbd = true,
            InkLengthUnits = 200f, InkThicknessUnits = 25f, LengthUnits = 200f, Mass = 6f,
            SpeedUnitsPerSecond = 300f, BaseDamage = 10f,
        };

        /// <summary>GDD §5: the four stance swipes. Every distance and duration is a placeholder.</summary>
        public static List<BodyMoveStats> CreateBodyMoves() => new List<BodyMoveStats>
        {
            new BodyMoveStats { Move = BodyMove.Crouch, DisplacementUnits = new Vec2(0f, -40f) },
            new BodyMoveStats { Move = BodyMove.Lunge, DisplacementUnits = new Vec2(120f, 0f) },
            new BodyMoveStats { Move = BodyMove.StepBack, DisplacementUnits = new Vec2(-100f, 0f) },
            new BodyMoveStats { Move = BodyMove.Jump, DisplacementUnits = new Vec2(0f, 80f) },
        };
    }
}
