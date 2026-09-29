namespace XRim.Rules.Paths
{
    /// <summary>A path limited by a weapon's ink budget and measured against it (GDD §6).</summary>
    public readonly struct InkedPath
    {
        /// <summary>The path that will execute: cut where the ink ran out.</summary>
        public WeaponPath Path { get; }

        public InkMeasurement Ink { get; }

        /// <summary>The weapon's ink length: how far its path can travel (GDD §6, Decided).</summary>
        public float BudgetUnits { get; }

        /// <summary>The weapon's hit width along the path (GDD §6, Decided). Comes from the weapon, never from input.</summary>
        public float ThicknessUnits { get; }

        /// <summary>True when drawing went past the budget and the rest of the stroke was dropped.</summary>
        public bool WasCut { get; }

        /// <summary>Ink left for the remaining-ink display. Never negative.</summary>
        public float RemainingUnits => BudgetUnits > Ink.CostUnits ? BudgetUnits - Ink.CostUnits : 0f;

        public InkedPath(WeaponPath path, InkMeasurement ink, float budgetUnits, float thicknessUnits, bool wasCut)
        {
            Path = path;
            Ink = ink;
            BudgetUnits = budgetUnits;
            ThicknessUnits = thicknessUnits;
            WasCut = wasCut;
        }
    }
}
