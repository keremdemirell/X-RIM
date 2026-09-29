using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Signature Rules", fileName = "SignatureRules")]
    public sealed class SignatureRulesConfig : SettingsConfig<SignatureSettings>
    {
    }
}
