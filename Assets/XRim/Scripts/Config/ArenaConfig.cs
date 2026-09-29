using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Arena", fileName = "Arena")]
    public sealed class ArenaConfig : SettingsConfig<ArenaSettings>
    {
    }
}
