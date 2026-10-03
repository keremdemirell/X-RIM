namespace XRim.Rules
{
    public static class BodyMoveExtensions
    {
        /// <summary>
        /// GDD §13 (Decided): a backward body swipe triggers the electric wall. Only the player's input counts, so this is
        /// asked of the planned move, never of where the dummy ended up: being knocked backwards never counts, and a lean in
        /// place (D12) is still a backward swipe.
        /// </summary>
        public static bool IsBackward(this BodyMove move) => move == BodyMove.StepBack;
    }
}
