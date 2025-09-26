using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Fishbone;

namespace SardineHead
{
    [BepInDependency(VarietyOfScales.Plugin.Guid, BepInDependency.DependencyFlags.SoftDependency)]
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
                Textures.Save(Extension.Chara<CharaMods, CoordMods>(human), archive);
            Extension.Register<CharaMods, CoordMods>();
            Extension.OnLoadChara += human => new ModApplicator(human);
            Extension.OnLoadCoord += human => new ModApplicator(human);
        }
    }
}