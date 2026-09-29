using XRim.Rules.Paths;

namespace XRim.Rules.Planning
{
    /// <summary>A finished stroke, already resampled into arena units in the torso frame.</summary>
    public sealed class SetPathCommand : PlanningCommand
    {
        public WeaponPath Path { get; }

        public SetPathCommand(WeaponPath path)
        {
            Path = path;
        }
    }
}
