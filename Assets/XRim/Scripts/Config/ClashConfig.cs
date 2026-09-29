using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Clash", fileName = "Clash")]
    public sealed class ClashConfig : SettingsConfig<ClashSettings>
    {
    }
}
