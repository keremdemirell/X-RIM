namespace XRim.Core
{
    public static class SideExtensions
    {
        public static Side Opponent(this Side side) => side == Side.Left ? Side.Right : Side.Left;

        /// <summary>+1 for Left (faces +X), -1 for Right (faces -X).</summary>
        public static int FacingSign(this Side side) => side == Side.Left ? 1 : -1;
    }
}
