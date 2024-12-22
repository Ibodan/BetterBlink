using BepInEx;

namespace KK_BetterBlink
{
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInProcess("CharaStudio")]
    public partial class BetterBlinkPlugin : BaseUnityPlugin
    {
        public const string Name = "KKS_BetterBlink";
        public const string GUID = "KKS_BetterBlink";
    }
}