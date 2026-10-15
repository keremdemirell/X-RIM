namespace XRim.Presentation.UI
{
    /// <summary>One weapon card on the loadout picker: what to call it, what it does, and which slot it fills.</summary>
    public readonly struct LoadoutChoice
    {
        public string Title { get; }

        /// <summary>A few lines of numbers (damage, speed, reach, ink, mass), split with new lines.</summary>
        public string Detail { get; }

        /// <summary>The slot this weapon fills, counting from 1 (1 is the weapon the match starts with); 0 when it is not picked.</summary>
        public int PickedOrder { get; }

        public LoadoutChoice(string title, string detail, int pickedOrder)
        {
            Title = title;
            Detail = detail;
            PickedOrder = pickedOrder;
        }
    }
}
