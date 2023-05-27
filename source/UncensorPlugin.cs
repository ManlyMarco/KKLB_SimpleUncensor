using System;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using sv08;
using UnityEngine;

namespace KKLB_SimpleUncensor
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class UncensorPlugin : BaseUnityPlugin
    {
        public const string PluginName = "Simple Uncensor for KoiKoi Love Blossoms";
        public const string GUID = "KKLB_SimpleUncensor";
        public const string Version = "1.0";

        private static UncensorPlugin _instance;
        private static Harmony _hi;
        
        private void Start()
        {
            _instance = this;
            try
            {
                GetUncensorTex();
            }
            catch (Exception ex)
            {
                Logger.LogMessage("Failed to load uncensor texture: " + ex.Message);
                Logger.LogError(ex);
                enabled = false;
                return;
            }

            _hi = Harmony.CreateAndPatchAll(typeof(UncensorPlugin), GUID);
        }

        private void OnDestroy()
        {
            _hi?.UnpatchSelf();
        }

        public Texture2D UncensorTex;
        public Texture2D GetUncensorTex()
        {
            if (UncensorTex == null)
            {
                var pluginLocation = Info.Location;
                if (pluginLocation == null) throw new ArgumentNullException(nameof(pluginLocation));
                var pluginDir = Path.GetDirectoryName(pluginLocation);
                if (pluginDir == null) throw new ArgumentNullException(nameof(pluginDir));
                var texPath = Path.Combine(pluginDir, "KKLB_SimpleUncensor_Texture.png");
                Logger.LogInfo("Loading body texture from " + texPath);
                UncensorTex = new Texture2D(1, 1);
                UncensorTex.LoadImage(File.ReadAllBytes(texPath));
                DontDestroyOnLoad(UncensorTex);
            }

            return UncensorTex;
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MosaicAttach), nameof(MosaicAttach.ShowMosaics))]
        private static bool DisableMosaicsPatch(GameObject _tgtChr, bool _bDisp)
        {
            // Hide all mosaics except for the dick (HideMesh_10), it will get material swapped later
            // Patching MosaicAttach.Show doesn't work as expected because of inlining
            foreach (var mos in _tgtChr.GetComponentsInChildren<MosaicAttach>())
            {
                if (mos.ownerChr == _tgtChr)
                {
                    if (mos.name.StartsWith("HideMesh_10"))
                        mos.Show(_bDisp);
                    else
                        mos.Show(false);
                }
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(PartsScriptBase), nameof(PartsScriptBase.Start))]
        private static void TextureReplacePatch(PartsScriptBase __instance, SkinnedMeshRenderer[] ___skinMeshes)
        {
            // Replace female body textures
            foreach (var skinMesh in ___skinMeshes)
            {
                // Filter which textures need to be replaced. 
                if (skinMesh?.name != null && skinMesh.name.StartsWith("BodyMesh") && !__instance.GetType().Name.StartsWith("Parts_M0")) // Don't apply to male
                    // if (skinMesh.name.StartsWith("BodyMesh") || // BodyMesh is always body tex
                    // skinMesh.name.StartsWith("HandMesh") || // HandMesh is always body tex
                    // (skinMesh.name.StartsWith("LegsMesh") && (__instance.GetType() == typeof(Parts_F01_L_NKD_00) || __instance.GetType() == typeof(Parts_F02_L_NKD_00)))) // LegsMesh is body tex only in some cases
                {
                    var uncensorTex = _instance.GetUncensorTex();
                    if (uncensorTex == null) throw new ArgumentNullException(nameof(uncensorTex));
                    if (skinMesh.material != null && skinMesh.material.mainTexture != uncensorTex)
                    {
                        _instance.Logger.LogInfo($"Replacing female body texture [{skinMesh.name}] on {__instance.GetType().Name}");
                        skinMesh.material.mainTexture = uncensorTex;
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CharacterCombine), nameof(CharacterCombine.SpawnParts))]
        private static void DiccMaterialFixer(CharacterCombine __instance)
        {
            // Replace male dick material from pink to male body material. It has no UVs so texture doesn't really matter.
            if (__instance.name == "M01_player_combine")
            {
                var diccRend = __instance.gameObject.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(x => x.name.StartsWith("HideMesh_10"));

                if (diccRend != null && !diccRend.material.shader.name.StartsWith("UnityChanToonShader"))
                {
                    var maleBodyRend = __instance.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(x => x.name.StartsWith("BodyMesh"));
                    if (maleBodyRend == null) throw new ArgumentNullException(nameof(maleBodyRend));

                    _instance.Logger.LogInfo($"Replacing meat sausage material [{diccRend.material.name}] -> [{maleBodyRend.material.name}] on {diccRend.name}");

                    diccRend.material = Instantiate(maleBodyRend.material);
                }
            }
        }
    }
}
