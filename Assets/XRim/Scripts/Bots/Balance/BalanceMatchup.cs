using System;

namespace XRim.Bots.Balance
{
    /// <summary>Which two brains meet in a run of the balance batch. They swap sides every other match, so neither side's start helps one.</summary>
    [Serializable]
    public sealed class BalanceMatchup
    {
        public BotBrainKind First = BotBrainKind.Heuristic;
        public BotBrainKind Second = BotBrainKind.Random;

        public BalanceMatchup()
        {
        }

        public BalanceMatchup(BotBrainKind first, BotBrainKind second)
        {
            First = first;
            Second = second;
        }

        public bool IsMirror => First == Second;

        public string Label => $"{First} vs {Second}";
    }
}
