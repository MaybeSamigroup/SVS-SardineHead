using BepInEx;
using HarmonyLib;
using CoastalSmell;

namespace SardineHead
{
    static partial class Hooks
    {
        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.CharaData), nameof(SaveData.CharaData.SetRoot))]
        static void SaveDataCharaDataSetRootPostfix(SaveData.CharaData __instance) =>
            (__instance.chaCtrl != null).Maybe(F.Apply(ReloadingComplete.OnNext, __instance.chaCtrl));
    }

    [BepInDependency(VarietyOfScales.Plugin.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public partial class Plugin
    {
        public const string Process = "SamabakeScramble";
    }
}