using System;

namespace XRim.Core
{
    /// <summary>A pair of values, one per arena side.</summary>
    public sealed class PerSide<T>
    {
        public T Left { get; set; }
        public T Right { get; set; }

        public PerSide(T left, T right)
        {
            Left = left;
            Right = right;
        }

        public T this[Side side]
        {
            get => side == Side.Left ? Left : Right;
            set
            {
                if (side == Side.Left) Left = value;
                else Right = value;
            }
        }

        public static PerSide<T> Create(Func<Side, T> factory) => new PerSide<T>(factory(Side.Left), factory(Side.Right));
    }
}
