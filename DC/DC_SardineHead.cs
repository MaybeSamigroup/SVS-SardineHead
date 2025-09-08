using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Fishbone;

namespace SardineHead
{
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
        public override void Load()
        {
            (Instance, Patch) = (this, Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks"));
            Extension.Register<CharaMods, CoordMods>();
            Extension.OnPreprocessChara += (_, archive) => Textures.Load(archive);
            Extension.OnPreprocessCoord += (_, archive) => Textures.Load(archive);
            Extension.OnSaveChara += (human, archive) =>
                Textures.Save(HumanExtension<CharaMods, CoordMods>.Chara(human), archive);
            Extension.Register<CharaMods, CoordMods>();
            Extension.OnReloadChara += human => new ModApplicator(human);
            Extension.OnReloadCoord += human => new ModApplicator(human);
        }
    }
}