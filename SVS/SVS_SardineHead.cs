using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;
using System;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using Character;
using CharacterCreation;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Fishbone;
using CoastalSmell;

namespace SardineHead
{
    internal static partial class UI
    {
        static Transform Root => HumanCustom.Instance.transform.Find("UI").Find("Root");
    }
    internal class EditWindow
    {
        Transform ListPanel;
        Transform EditPanel;
        EditGroup FaceGroup;
        EditGroup BodyGroup;
        Dictionary<int, EditGroup> HairGroups = new();
        Dictionary<int, EditGroup> ClothesGroups = new();
        Dictionary<int, EditGroup> AccessoryGroups = new();
        EditWindow(Tuple<Transform, Transform> panels) =>
            (ListPanel, EditPanel) = panels;
        EditWindow(GameObject window) : this(UI.Panels(window)) =>
            window
                .With(IntEdit.PrepareArchetype)
                .With(FloatEdit.PrepareArchetype)
                .With(RangeEdit.PrepareArchetype)
                .With(ColorEdit.PrepareArchetype)
                .With(VectorEdit.PrepareArchetype)
                .With(TextureEdit.PrepareArchetype)
                .With(RenderingEdit.PrepareArchetype)
                .With(Util.OnCustomHumanReady.Apply(() =>
                    window.GetComponentInParent<ObservableUpdateTrigger>()
                        .UpdateAsObservable().Subscribe(F.Ignoring<Unit>(Update))));
        EditWindow() : this(UI.Window(Handle)) =>
            (FaceGroup, BodyGroup) = (new EditGroup("Face", ListPanel), new EditGroup("Body", ListPanel));
        EditGroup GroupAt(string name, Dictionary<int, EditGroup> groups, int index) =>
            groups.TryGetValue(index, out var group) ? group : groups[index] = new EditGroup(name, ListPanel);
        void Initialize(Dictionary<string, MaterialWrapper> wrappers, EditGroup group) =>
            group.Initialize(wrappers, EditPanel);
        void OnBodyChange(HumanBody item) =>
            Initialize(item.Wrap(), BodyGroup);
        void OnFaceChange(HumanFace item) =>
            Initialize(item.Wrap(), FaceGroup);
        void OnHairChange(HumanHair item, int index) =>
            Initialize(item.Wrap(index), GroupAt($"Hair:{Enum.GetName(typeof(ChaFileDefine.HairKind), index)}", HairGroups, index));
        void OnClothesChange(HumanCloth item, int index) =>
            Initialize(item.Wrap(index), GroupAt($"Clothes:{Enum.GetName(typeof(ChaFileDefine.ClothesKind), index)}", ClothesGroups, index));
        void OnAccessoryChange(HumanAccessory item, int index) =>
            Initialize(item.Wrap(index), GroupAt($"Accessories{index}", AccessoryGroups, index));
        void Apply(CoordMods mods)
        {
            FaceGroup.Apply(mods.Face);
            BodyGroup.Apply(mods.Body);
            HairGroups.Do(entry => entry.Value.Apply(mods.Hairs.GetValueOrDefault(entry.Key, new())));
            ClothesGroups.Do(entry => entry.Value.Apply(mods.Clothes.GetValueOrDefault(entry.Key, new())));
            AccessoryGroups.Do(entry => entry.Value.Apply(mods.Accessories.GetValueOrDefault(entry.Key, new())));
        }
        CoordMods Store() => new()
        {
            Face = FaceGroup.Store(),
            Body = BodyGroup.Store(),
            Hairs = HairGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Clothes = ClothesGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Accessories = AccessoryGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
        };
        void OnCharacterSerialize(HumanData data, ZipArchive archive) =>
            CharaMods.Save(archive, CharaMods.Load(archive).Merge(data)(CoordLimit.All, Store()));
        void OnCoordinateSerialize(HumanDataCoordinate _, ZipArchive archive) =>
            CoordMods.Save(archive, Store());
        void OnPreCoordinateReload(Human human, int type, ZipArchive archive) =>
            CharaMods.Save(archive, CharaMods.Load(archive).Merge(human)(CoordLimit.All, Store()));
        void OnPostCoordinateReload(Human human, int type, ZipArchive archive) =>
            Apply(CharaMods.Load(archive).AsCoord(human));
        void OnCharacterDeserialize(Human human, CharaLimit limits, ZipArchive archive, ZipArchive storage) =>
            Apply(CharaMods.Load(storage).Merge(limits)(CharaMods.Load(archive)).With(CharaMods.Save.Apply(storage)).AsCoord(human));
        void OnCoordinateDeserialize(Human human, HumanDataCoordinate coord, CoordLimit limits, ZipArchive archive, ZipArchive storage) =>
            Apply(CharaMods.Load(storage).Merge(human)(limits, CoordMods.Load(archive)).AsCoord(human));
        void Update(IEnumerable<EditGroup> groups) => groups.Do(group => group.Update());
        void Update() => Update([FaceGroup, BodyGroup, .. HairGroups.Values, .. ClothesGroups.Values, .. AccessoryGroups.Values]);
        static WindowHandle Handle;
        static EditWindow Instance;
        internal static void Initialize()
        {
            Handle = new WindowHandle(Plugin.Instance, Plugin.Name, new(30, -80), new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl));
            Util<HumanCustom>.Hook(() =>
            {
                Plugin.Instance.Log.LogInfo("CustomInstantiate");
                Instance = new EditWindow();
                Event.OnCharacterSerialize += Instance.OnCharacterSerialize;
                Event.OnCoordinateSerialize += Instance.OnCoordinateSerialize;
                Event.OnPreCoordinateReload += Instance.OnPreCoordinateReload;
                Event.OnPostCoordinateReload += Instance.OnPostCoordinateReload;
                Event.OnPostCharacterDeserialize += Instance.OnCharacterDeserialize;
                Event.OnPostCoordinateDeserialize += Instance.OnCoordinateDeserialize;
                Hooks.OnBodyChange += Instance.OnBodyChange;
                Hooks.OnFaceChange += Instance.OnFaceChange;
                Hooks.OnHairChange += Instance.OnHairChange;
                Hooks.OnClothesChange += Instance.OnClothesChange;
                Hooks.OnAccessoryChange += Instance.OnAccessoryChange;
            }, () =>
            {
                Plugin.Instance.Log.LogInfo("CustomDestroyed");
                Event.OnCharacterSerialize -= Instance.OnCharacterSerialize;
                Event.OnCoordinateSerialize -= Instance.OnCoordinateSerialize;
                Event.OnPreCoordinateReload -= Instance.OnPreCoordinateReload;
                Event.OnPostCoordinateReload -= Instance.OnPostCoordinateReload;
                Event.OnPostCharacterDeserialize -= Instance.OnCharacterDeserialize;
                Event.OnPostCoordinateDeserialize -= Instance.OnCoordinateDeserialize;
                Hooks.OnBodyChange -= Instance.OnBodyChange;
                Hooks.OnFaceChange -= Instance.OnFaceChange;
                Hooks.OnHairChange -= Instance.OnHairChange;
                Hooks.OnClothesChange -= Instance.OnClothesChange;
                Hooks.OnAccessoryChange -= Instance.OnAccessoryChange;
                Instance = null;
            });
        }
    }
    partial class ModApplicator
    {
        static void OnPreActorHumanize(SaveData.Actor actor, HumanData data, ZipArchive archive) =>
            new ModApplicator(data, CharaMods.Load(archive).AsCoord(actor.charFile));
        static void OnPreCoordinateReload(Human human, int type, ZipArchive archive) =>
            new ModApplicator(human.data, CharaMods.Load(archive).AsCoord(type));
        static void OnPreCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive, ZipArchive storage) =>
            new ModApplicator(human.data, CharaMods.Load(storage).Merge(human)(limits, CoordMods.Load(archive)).AsCoord(human));
        static void OnPostActorHumanize(SaveData.Actor actor, Human human, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human.data));
        static void OnPostCoordinateReload(Human human, int type, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human.data));
        static void OnPostCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive, ZipArchive storage) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human.data));
        internal static void Initialize()
        {
            Event.OnPreActorHumanize += OnPreActorHumanize;
            Event.OnPreCoordinateReload += OnPreCoordinateReload;
            Event.OnPreCoordinateDeserialize += OnPreCoordinateDeserialize;
            Event.OnPostActorHumanize += OnPostActorHumanize;
            Event.OnPostCoordinateReload += OnPostCoordinateReload;
            Event.OnPostCoordinateDeserialize += OnPostCoordinateDeserialize;
            Util<HumanCustom>.Hook(() =>
            {
                Event.OnPreActorHumanize -= OnPreActorHumanize;
                Event.OnPreCoordinateReload -= OnPreCoordinateReload;
                Event.OnPreCoordinateDeserialize -= OnPreCoordinateDeserialize;
                Event.OnPostActorHumanize -= OnPostActorHumanize;
                Event.OnPostCoordinateReload -= OnPostCoordinateReload;
                Event.OnPostCoordinateDeserialize -= OnPostCoordinateDeserialize;
            }, () =>
            {
                Event.OnPreActorHumanize += OnPreActorHumanize;
                Event.OnPreCoordinateReload += OnPreCoordinateReload;
                Event.OnPreCoordinateDeserialize += OnPreCoordinateDeserialize;
                Event.OnPostActorHumanize += OnPostActorHumanize;
                Event.OnPostCoordinateReload += OnPostCoordinateReload;
                Event.OnPostCoordinateDeserialize += OnPostCoordinateDeserialize;
            });
        }
    }
    [BepInProcess(Process)]
    [BepInDependency(Fishbone.Plugin.Guid)]
    [BepInPlugin(Guid, Name, Version)]
    public partial class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
        public const string Guid = $"{Process}.{Name}";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(() => Instance = this)
                .With(ModApplicator.Initialize)
                .With(EditWindow.Initialize);
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}