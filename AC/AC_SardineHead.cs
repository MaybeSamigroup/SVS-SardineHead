using HarmonyLib;
using Character;
using CoastalSmell;

namespace SardineHead
{
    static partial class Hooks
    {
        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(AC.CharaBase), nameof(AC.CharaBase.SetRoot))]
        static void CharaBaseSetRootPostfix(AC.CharaBase __instance) =>
            (__instance._chara != null).Maybe(F.Apply(ReloadingComplete.OnNext, __instance._chara));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Load))]
        static void HumanLoadPostfix(Human __instance) => ReloadingComplete.OnNext(__instance);
    }

    public partial class Plugin
    {
        public const string Process = "Aicomi";
    }
}