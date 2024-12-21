using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace KK_BetterBlink
{
    [BepInPlugin(Name, GUID, Version)]
    public partial class BetterBlinkPlugin : BaseUnityPlugin
    {
        public const string Version = "1.0.0";

        #region Config properties

        public ConfigEntry<float> CfgSpeedClose { get; private set; }
		public ConfigEntry<float> CfgSpeedOpen { get; private set; }
		public ConfigEntry<float> CfgEyebrowSyncScale { get; private set; }
        public ConfigEntry<float> CfgBaseIdle { get; private set; }
		public ConfigEntry<float> CfgEyeMovementFactor { get; private set; }

        #endregion

		private static BetterBlinkPlugin plugin;
        private Harmony harmony;

        private Dictionary<int, Controller> controls = new Dictionary<int, Controller>();

        private void Awake()
        {
            plugin = this;

            SceneManager.sceneLoaded += SceneLoaded;

            const string s = "Settings";
            CfgSpeedClose = Config.Bind(s, "Blink Close Speed", 16f, new ConfigDescription("", new AcceptableValueRange<float>(1f, 30.0f)));
			CfgSpeedOpen = Config.Bind(s, "Blink Open Speed", 6f, new ConfigDescription("", new AcceptableValueRange<float>(1f, 30.0f)));
			CfgEyebrowSyncScale = Config.Bind(s, "Eyebrow Sync Scale", 0.2f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f)));
			CfgBaseIdle = Config.Bind(s, "Base Blink Idle",9f, new ConfigDescription("Moisture replenished per blink.", new AcceptableValueRange<float>(3f, 20f)));
			CfgEyeMovementFactor = Config.Bind(s, "Eye Movement Factor", 0.55f, new ConfigDescription("Moisture depletion due to eye movement", new AcceptableValueRange<float>(0f, 2f)));
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

				plugin.Logger.LogInfo("FaceBlendShape awoke and BetterBlink controller was created. "
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
            public static void FBSCtrolEyebrowCalcBlend(ref float blinkRate)
            {
				if (blinkRate == -1f) return;
                blinkRate = 1f - (1f - blinkRate) * plugin.CfgEyebrowSyncScale.Value;
            }
		}

		private class Controller
        {
            private float savedTime = 0f;
            private enum State { Idle, Closing, Opening };
            private State state = State.Idle;
            private EyeObject eye;
            private Quaternion lastEyeRot;

            public void SetEye(EyeObject eye) { this.eye = eye; }

            public float GetOpenRate()
            {
                if (state == 0 && Time.frameCount % 20 == 0)
                {
                    Quaternion eyeRot = eye?.eyeTransform.localRotation ?? Quaternion.identity;
                    savedTime -= Mathf.Abs(1f - Quaternion.Dot(eyeRot, lastEyeRot)) * 90f * plugin.CfgEyeMovementFactor.Value * plugin.CfgBaseIdle.Value;
                    lastEyeRot = eyeRot;
                }

                float t;
                switch (state)
                {
                    case State.Idle:
                        if (Time.time - savedTime > plugin.CfgBaseIdle.Value)
                        {
                            savedTime = Time.time;
                            state++;
                        }
                        return 1f;
                    case State.Closing:
                        t = (Time.time - savedTime) * plugin.CfgSpeedClose.Value;
                        if (t <= 1f)
                        {
							return 1f - Mathf.Pow(t, 3);
						}
						savedTime = 1f / plugin.CfgSpeedClose.Value + savedTime;
						state++;
						goto case State.Opening;
					case State.Opening:
						t = (Time.time - savedTime) * plugin.CfgSpeedOpen.Value;
						if (1f <= t)
						{
                            float idle = plugin.CfgBaseIdle.Value;
							savedTime = Time.time + UnityEngine.Random.Range(-idle * 0.3f, idle * 0.3f);
                            lastEyeRot = eye?.eyeTransform.localRotation ?? Quaternion.identity;
							state = 0;
						}
						return 1f - Mathf.Pow(1f - Mathf.Clamp(t, 0f, 1f), 3);
				}
                return 1f;
            }
        }
    }
}