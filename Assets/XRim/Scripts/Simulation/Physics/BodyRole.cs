namespace XRim.Simulation.Physics
{
    public enum BodyRole
    {
        /// <summary>A dummy's hitbox; see the tag's BodyPart.</summary>
        BodyPart = 0,

        /// <summary>The held weapon or shield.</summary>
        HeldItem = 1,
        SeveredLimb = 2,
        ElectricWall = 3,
        Floor = 4,
        ArenaEdge = 5,
    }
}
