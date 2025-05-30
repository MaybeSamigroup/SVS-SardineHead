using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using System.IO.Compression;
using Character;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Fishbone;

namespace SardineHead
{
    partial class ModApplicator
    {
        static void OnPreCoordinateReload(Human human, int type, ZipArchive archive) =>
            new ModApplicator(human.data, archive.LoadChara().Transform(type));
        static void OnPreCharacterDeserialize(HumanData data, ZipArchive archive) =>
            new ModApplicator(data, data.Transform(archive.LoadTextures().LoadChara()));
        static void OnPreCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive, ZipArchive storage) =>
            new ModApplicator(human.data, human.Transform(limits.Merge(human,
                archive.LoadTextures().LoadCoord(), storage.LoadChara()).With(storage.Save)));
        static void OnPostCoordinateReload(Human human, int type, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(() => applicator.Cleanup(human.data));
        static void OnPostCharacterDeserialize(Human human, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(() => applicator.Cleanup(human.data));
        static void OnPostCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive, ZipArchive storage) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(() => applicator.Cleanup(human.data));
        internal static void Initialize()
        {
            Event.OnPreCoordinateReload += OnPreCoordinateReload;
            Event.OnPreCharacterDeserialize += OnPreCharacterDeserialize;
            Event.OnPreCoordinateDeserialize += OnPreCoordinateDeserialize;
            Event.OnPostCoordinateReload += OnPostCoordinateReload;
            Event.OnPostCharacterDeserialize += OnPostCharacterDeserialize;
            Event.OnPostCoordinateDeserialize += OnPostCoordinateDeserialize;
        }
    }
    [BepInProcess(Process)]
    [BepInDependency(Fishbone.Plugin.Guid)]
    [BepInPlugin(Guid, Name, Version)]
    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
        public const string Guid = $"{Process}.{Name}";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(() => Instance = this)
                .With(ModApplicator.Initialize);
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}