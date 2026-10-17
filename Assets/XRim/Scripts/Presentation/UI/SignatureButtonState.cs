using System;
using XRim.Rules;

namespace XRim.Presentation.UI
{
    /// <summary>One signature move's button on the planning HUD (GDD §8): its move and name, whether a tap can set it now, and whether it is set.</summary>
    public readonly struct SignatureButtonState : IEquatable<SignatureButtonState>
    {
        public SignatureMoveId Move { get; }
        public string Name { get; }

        /// <summary>Charged, allowed this turn and working with the weapon in hand: a tap sets it. A dim button still reports a tap (the refusal says why).</summary>
        public bool Usable { get; }

        /// <summary>The move is set: its pattern is the path, and a second tap cancels it.</summary>
        public bool Selected { get; }

        public SignatureButtonState(SignatureMoveId move, string name, bool usable, bool selected)
        {
            Move = move;
            Name = name ?? string.Empty;
            Usable = usable;
            Selected = selected;
        }

        public bool Equals(SignatureButtonState other) =>
            Move == other.Move && string.Equals(Name, other.Name, StringComparison.Ordinal) && Usable == other.Usable && Selected == other.Selected;

        public override bool Equals(object obj) => obj is SignatureButtonState other && Equals(other);

        public override int GetHashCode() => ((Move.GetHashCode() * 31 + (Name ?? string.Empty).GetHashCode()) * 31 + Usable.GetHashCode()) * 31 + Selected.GetHashCode();
    }
}
