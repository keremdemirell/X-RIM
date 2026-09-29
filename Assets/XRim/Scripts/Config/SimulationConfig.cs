using UnityEngine;
using XRim.Simulation;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Root + "Simulation", fileName = "Simulation")]
    public sealed class SimulationConfig : SettingsConfig<SimulationSettings>
    {
    }
}
