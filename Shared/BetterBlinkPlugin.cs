using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KK_BetterBlink
{
    [BepInPlugin(Name, GUID, Version)]
    public partial class BetterBlinkPlugin : BaseUnityPlugin
    {
        public const string Version = "1.0.1";

        #region Config properties

        public ConfigEntry<float> CfgSpeedClose { get; private set; }
        public ConfigEntry<float> CfgSpeedOpen { get; private set; }
        public ConfigEntry<float> CfgEyebrowSyncScale { get; private set; }
        public ConfigEntry<float> CfgBaseIdle { get; private set; }
        public ConfigEntry<float> CfgEyeMovementFactor { get; private set; }

        public ConfigEntry<float> CfgBaseIdleRandom { get; private set; }
        public ConfigEntry<float> CfgEaseInPower { get; private set; }
        public ConfigEntry<float> CfgClosedThreshold { get; private set; }

        #endregion

        private static BetterBlinkPlugin plugin;
        private Harmony harmony;

        private Dictionary<int, Controller> controls = new Dictionary<int, Controller>();

        private void Awake()
        {
            plugin = this;

            SceneManager.sceneLoaded += SceneLoaded;

            const string s = "Settings";
            CfgSpeedClose = Config.Bind(s, "Blink Close Speed", 16f, new ConfigDescription("", new AcceptableValueRange<float>(1f, 30.0f),
                new ConfigurationManagerAttributes { Order = 100 }));
            CfgSpeedOpen = Config.Bind(s, "Blink Open Speed", 7f, new ConfigDescription("", new AcceptableValueRange<float>(1f, 30.0f),
                new ConfigurationManagerAttributes { Order = 99 }));
            CfgEyebrowSyncScale = Config.Bind(s, "Eyebrow Sync Scale", 0.2f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = 98 }));
            CfgBaseIdle = Config.Bind(s, "Base Blink Interval", 9f, new ConfigDescription("Moisture replenished per blink.", new AcceptableValueRange<float>(3f, 20f),
                new ConfigurationManagerAttributes { Order = 97 }));
            CfgEyeMovementFactor = Config.Bind(s, "Eye Movement Factor", 0.022f, new ConfigDescription("Moisture depletion due to eye movement", new AcceptableValueRange<float>(0f, 0.2f),
                new ConfigurationManagerAttributes { Order = 96 }));
            CfgBaseIdleRandom = Config.Bind(s, "Blink Idle Randomize Factor", 0.4f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = 50, IsAdvanced = true }));
            CfgEaseInPower = Config.Bind(s, "Blink Animation Ease-In Power", 4f, new ConfigDescription("", new AcceptableValueRange<float>(1f, 9f),
                new ConfigurationManagerAttributes { Order = 49, IsAdvanced = true }));
            CfgClosedThreshold = Config.Bind(s, "Rotation Speed Threshold Keeps Eyes Closed", 0.9f, new ConfigDescription("", new AcceptableValueRange<float>(0.01f, 10f),
                new ConfigurationManagerAttributes { Order = 48, IsAdvanced = true }));

            Config.SettingChanged += (sender, args) => ConfigSettingChanged();
        }

        private void SceneLoaded(Scene s, LoadSceneMode lsm)
        {
            if (harmony == null)
            {
                harmony = Harmony.CreateAndPatchAll(typeof(Hooks));
            }
        }

        private void Update()
        {
        }

        private void LateUpdate()
        {
        }

        private void ConfigSettingChanged()
        {
        }

        private static class Hooks
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(FaceBlendShape), nameof(FaceBlendShape.Awake))]
            public static void FaceBlendShapeAwake(FaceBlendShape __instance)
            {
                Controller controller = new Controller();
                plugin.controls.Add(__instance.GetHashCode(), controller);
                plugin.controls.Add(__instance.BlinkCtrl.GetHashCode(), controller);

                plugin.Logger.LogInfo("FaceBlendShape awoke and controller was created. "
                    + __instance.GetHashCode() + " "
                    + __instance.BlinkCtrl.GetHashCode());
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(FaceBlendShape), nameof(FaceBlendShape.LateUpdate))]
            public static void FaceBlendShapeLateUpdate(FaceBlendShape __instance)
            {
                plugin.controls[__instance.GetHashCode()]?.SetEye(__instance.EyeLookController?.eyeLookScript.eyeObjs.First());
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(FBSBlinkControl), nameof(FBSBlinkControl.CalcBlink))]
            public static void FBSBlinkControlCalcBlink(FBSBlinkControl __instance)
            {
                __instance.openRate = plugin.controls[__instance.GetHashCode()]?.GetOpenRate() ?? __instance.openRate;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(FBSCtrlEyebrow), nameof(FBSCtrlEyebrow.CalcBlend))]
            public static void FBSCtrlEyebrowCalcBlend(ref float blinkRate)
            {
                if (blinkRate == -1f) return;
                blinkRate = 1f - (1f - blinkRate) * plugin.CfgEyebrowSyncScale.Value;
            }
        }

        private class Controller
        {
            private float savedTime = 0f;
            private enum State { Idle, Closing, Closed, Opening };
            private State state = State.Idle;
            private EyeObject eye;
            private Quaternion lastEyeRot;

            private Quaternion eyeRot { get { return eye?.eyeTransform.localRotation ?? Quaternion.identity; } }
            private float deltaAngle { get { return Quaternion.Angle(eyeRot, lastEyeRot); } }

            public void SetEye(EyeObject eye) { this.eye = eye; }

            public float GetOpenRate()
            {
                if (state == State.Idle)
                {
                    savedTime -= deltaAngle * plugin.CfgEyeMovementFactor.Value * plugin.CfgBaseIdle.Value;
                }


                float t, openRate = 1f;
                switch (state)
                {
                    case State.Idle:
                        if (Time.time - savedTime > plugin.CfgBaseIdle.Value)
                        {
                            savedTime = Time.time;
                            state = State.Closing;
                        }
                        openRate = 1f;
                        break;
                    case State.Closing:
                        t = (Time.time - savedTime) * plugin.CfgSpeedClose.Value;
                        if (1f <= t)
                        {
                            if (plugin.CfgClosedThreshold.Value < deltaAngle)
                            {
                                openRate = 0f;
                                state = State.Closed;
                                break;
                            }
                            savedTime = 1f / plugin.CfgSpeedClose.Value + savedTime;
                            state = State.Opening;
                            goto case State.Opening;
                        }
                        openRate = 1f - Mathf.Pow(t, plugin.CfgEaseInPower.Value);
                        break;
                    case State.Closed:
                        if (deltaAngle <= plugin.CfgClosedThreshold.Value)
                        {
                            state = State.Opening;
                            savedTime = Time.time;
                        }
                        openRate = 0f;
                        break;
                    case State.Opening:
                        t = (Time.time - savedTime) * plugin.CfgSpeedOpen.Value;
                        if (1f <= t)
                        {
                            savedTime = Time.time + UnityEngine.Random.Range(-1f, 1f) * plugin.CfgBaseIdleRandom.Value * plugin.CfgBaseIdle.Value;
                            state = State.Idle;
                        }
                        openRate = 1f - Mathf.Pow(1f - Mathf.Clamp(t, 0f, 1f), plugin.CfgEaseInPower.Value);
                        break;
                }
                lastEyeRot = eyeRot;
                return openRate;
            }
        }
    }
}