using XRim.Core;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// What the player sees while drawing (GDD §6): the stroke as drawn, the path that will execute (clipped at the reach limit, D4,
    /// and where the ink runs out, at the weapon's thickness), the part a too-sharp turn broke (drawn red) and how much ink is left
    /// (D7). Built by <see cref="PathPreviewBuilder"/> with the same <see cref="PathBuilder"/> the authority uses, so the preview
    /// always matches what the rules will accept. Everything is in the torso frame, arena units.
    /// </summary>
    public sealed class PathPreview
    {
        /// <summary>The raw stroke as the finger drew it.</summary>
        public WeaponPath Stroke { get; }

        /// <summary>The path that executes: the lead-in from the weapon tip (D3), resampled, clamped to reach, cut where the ink ran out or the path broke.</summary>
        public WeaponPath Path { get; }

        /// <summary>The part after a too-sharp turn that does not execute; empty when nothing broke.</summary>
        public WeaponPath InvalidTail { get; }

        /// <summary>How wide the weapon's hit is along the path.</summary>
        public float ThicknessUnits { get; }

        public float InkBudgetUnits { get; }
        public float InkSpentUnits { get; }

        /// <summary>The reach limit the path is clamped to (arm plus weapon, D4).</summary>
        public ReachLimit Reach { get; }

        public bool WasClampedByReach { get; }
        public bool WasCutByInk { get; }
        public bool WasCutAtBreak { get; }

        public float InkRemainingUnits => InkBudgetUnits > InkSpentUnits ? InkBudgetUnits - InkSpentUnits : 0f;

        /// <summary>Ink left as a share of the weapon's budget, 0 to 1 (D7).</summary>
        public float InkRemainingFraction => InkBudgetUnits > 0f ? InkRemainingUnits / InkBudgetUnits : 0f;

        public PathPreview(WeaponPath stroke, WeaponPath path, WeaponPath invalidTail, float thicknessUnits, float inkBudgetUnits,
            float inkSpentUnits, ReachLimit reach, bool wasClampedByReach, bool wasCutByInk, bool wasCutAtBreak)
        {
            Stroke = stroke;
            Path = path;
            InvalidTail = invalidTail;
            ThicknessUnits = thicknessUnits;
            InkBudgetUnits = inkBudgetUnits;
            InkSpentUnits = inkSpentUnits;
            Reach = reach;
            WasClampedByReach = wasClampedByReach;
            WasCutByInk = wasCutByInk;
            WasCutAtBreak = wasCutAtBreak;
        }
    }
}
