using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Sudden Death", fileName = "SuddenDeath")]
    public sealed class SuddenDeathConfig : SettingsConfig<SuddenDeathSettings>
    {
    }
}
