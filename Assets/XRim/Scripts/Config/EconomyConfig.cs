using UnityEngine;
using XRim.Economy;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Root + "Economy", fileName = "Economy")]
    public sealed class EconomyConfig : SettingsConfig<EconomySettings>
    {
    }
}
