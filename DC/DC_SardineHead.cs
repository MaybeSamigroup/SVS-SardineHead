using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using System.IO.Compression;
using Character;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Fishbone;
using CoastalSmell;

namespace SardineHead
{
    partial class ModApplicator
    {
        static void OnPreCoordinateReload(Human human, int type, ZipArchive archive) =>
            new ModApplicator(human.data, CharaMods.Load(archive).AsCoord(type));
        static void OnPreCharacterDeserialize(HumanData data, ZipArchive archive) =>
            new ModApplicator(data, CharaMods.Load(archive).AsCoord(data));
        static void OnPreCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive, ZipArchive storage) =>
            new ModApplicator(human.data, CharaMods.Load(storage).Merge(human)
                (limits, CoordMods.Load(archive)).With(CharaMods.Save.Apply(storage)).AsCoord(human));
        static void OnPostCoordinateReload(Human human, int type, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human));
        static void OnPostCharacterDeserialize(Human human, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human));
        static void OnPostCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive, ZipArchive storage) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human));
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
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(() => Instance = this)
                .With(ModApplicator.Initialize);
    }
}