using BepInEx;

namespace KK_BetterBlink
{
    [BepInProcess("Koikatu")]
    [BepInProcess("Koikatsu Party")]
    [BepInProcess("KoikatuVR")]
    [BepInProcess("Koikatsu Party VR")]
    [BepInProcess("CharaStudio")]
    public partial class BetterBlinkPlugin : BaseUnityPlugin
    {
        public const string Name = "KK_BetterBlink";
        public const string GUID = "KK_BetterBlink";
    }
}