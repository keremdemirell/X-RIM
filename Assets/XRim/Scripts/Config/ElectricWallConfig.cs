using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Electric Wall", fileName = "ElectricWall")]
    public sealed class ElectricWallConfig : SettingsConfig<ElectricWallSettings>
    {
    }
}
