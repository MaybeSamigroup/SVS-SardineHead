using System;
using System.Reactive.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Fishbone;

namespace SardineHead
{
    static partial class Hooks
    {
        internal static IDisposable[] Initialize(Plugin _) => [
            ..Extension.Register<CharaMods, CoordMods>(),
            Extension.OnPreprocessChara.Select(tuple => tuple.Item2).Subscribe(Textures.Load),
            Extension.OnPreprocessCoord.Select(tuple => tuple.Item2).Subscribe(Textures.Load),
            Extension.OnSaveChara.Subscribe(tuple => Textures.Save(Extension<CharaMods, CoordMods>.Humans[tuple.Human], tuple.Archive)),
            Extension.OnLoadChara.Subscribe(human => new ModApplicator(human)),
            Extension.OnLoadCoord.Subscribe(human => new ModApplicator(human))
        ];
    }

    [BepInDependency(VarietyOfScales.Plugin.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}