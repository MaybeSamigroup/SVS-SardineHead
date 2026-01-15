using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using UnityEngine;
using Character;
using CharacterCreation;
using HarmonyLib;
using BepInEx.Configuration;
using Fishbone;
using CoastalSmell;

namespace SardineHead
{
    internal class EditWindow
    {
        Window Window;
        Transform ListPanel;
        Transform EditPanel;
        EditGroup FaceGroup;
        EditGroup BodyGroup;
        Dictionary<int, EditGroup> HairGroups = new();
        Dictionary<int, EditGroup> ClothesGroups = new();
        Dictionary<int, EditGroup> AccessoryGroups = new();
        CompositeDisposable Subscriptions;
        EditWindow(GameObject go) => go
            .With("Menus".AsChild(
                UGUI.Scroll(215, 800, UGUI.ColorPanel +
                "Contents".AsChild(
                    UGUI.LayoutV(padding: UGUI.Offset(5, 5)) +
                    new UIAction(go => ListPanel = go.transform)))))
             .With("Edits".AsChild(
                UGUI.Scroll(515, 800, UGUI.ColorPanel +
                "Contents".AsChild(
                    UGUI.LayoutV(padding: UGUI.Offset(5, 5)) +
                    new UIAction(go => EditPanel = go.transform)))));

        EditWindow(Window window) : this(window.Content) =>
            (Window, FaceGroup, BodyGroup, Subscriptions) = (window, new EditGroup("Face", ListPanel), new EditGroup("Body", ListPanel), [
                HumanCustomExtension.OnBodyChange.Subscribe(OnBodyChange),
                HumanCustomExtension.OnFaceChange.Subscribe(OnFaceChange),
                HumanCustomExtension.OnHairChange.Subscribe(tuple => OnHairChange(tuple.Item1, tuple.Item2)),
                HumanCustomExtension.OnClothesChange.Subscribe(tuple => OnClothesChange(tuple.Item1, tuple.Item2)),
                HumanCustomExtension.OnAccessoryChange.Subscribe(tuple => OnAccessoryChange(tuple.Item1, tuple.Item2)),
                Extension.OnPrepareSaveChara.Subscribe(_ => Store()),
                Extension.OnPrepareSaveCoord.Subscribe(_ => Store()),
                ModApplicator.OnApplicationComplete.Subscribe(_ => Apply()),
                window.OnUpdate.Subscribe(_ => Update())
            ]);

        EditGroup GroupAt(string name, Dictionary<int, EditGroup> groups, int index) =>
            groups.TryGetValue(index, out var group) ? group : groups[index] = new EditGroup(name, ListPanel);
        void Initialize(Dictionary<string, MaterialWrapper> wrappers, EditGroup group) =>
            group.Initialize(wrappers, Window, EditPanel);
        void OnBodyChange(HumanBody item) =>
            Initialize(item.WrapCtc().Concat(item.Wrap()).ToDictionary(), BodyGroup);
        void OnFaceChange(HumanFace item) =>
            Initialize(item.WrapCtc().Concat(item.Wrap()).ToDictionary(), FaceGroup);
        void OnHairChange(HumanHair item, int index) =>
            Initialize(item.Wrap(index), GroupAt($"Hair:{Enum.GetName(typeof(ChaFileDefine.HairKind), index)}", HairGroups, index));
        void OnClothesChange(HumanCloth item, int index) =>
            Initialize(item.Clothess[index].WrapCtc().Concat(item.Wrap(index)).ToDictionary(),
                GroupAt($"Clothes:{Enum.GetName(typeof(ChaFileDefine.ClothesKind), index)}", ClothesGroups, index));
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
        void Apply() => Apply(Extension<CharaMods, CoordMods>.Humans.NowCoordinate[HumanCustom.Instance.Human]);
        void Store() => Extension<CharaMods, CoordMods>.Humans.NowCoordinate[HumanCustom.Instance.Human] = new CoordMods()
        {
            Face = FaceGroup.Store(),
            Body = BodyGroup.Store(),
            Hairs = HairGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Clothes = ClothesGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Accessories = AccessoryGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store())
        };
        void Update(IEnumerable<EditGroup> groups) => groups.Do(group => group.Update());
        void Update() =>
            Update([FaceGroup, BodyGroup, .. HairGroups.Values, .. ClothesGroups.Values, .. AccessoryGroups.Values]);
        static EditWindow Instance;

        static IDisposable[] Initialize(WindowConfig config) => [
            SingletonInitializerExtension<HumanCustom>.OnStartup.Subscribe(_ => Instance = new EditWindow(UI.Window(config))),
            SingletonInitializerExtension<HumanCustom>.OnDestroy.Subscribe(_ => Instance.Subscriptions.Dispose())
        ];

        internal static IDisposable[] Initialize(Plugin plugin) =>
            Initialize(new WindowConfig(plugin, Plugin.Name, new(30, -80), new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl)));
    }
    static partial class Hooks
    {
        internal static void OnCustomLoaded() =>
            ReloadingComplete.OnNext(HumanCustom.Instance.Human);
        internal static IDisposable[] Initialize(Plugin plugin) => [
            #if SamabakeScrable
            Extension<CharaMods, CoordMods>.Translate<LegacyCharaMods>(Path.Combine(Guid, "modifications.json"), mods => mods),
            Extension<CharaMods, CoordMods>.Translate<LegacyCoordMods>(Path.Combine(Guid, "modifications.json"), mods => mods),
            #endif
            Extension.OnPreprocessChara.Select(tuple => tuple.Item2).Subscribe(Textures.Load),
            Extension.OnPreprocessCoord.Select(tuple => tuple.Item2).Subscribe(Textures.Load),
            ..Extension.Register<CharaMods, CoordMods>(),
            Extension.OnSaveActor.Subscribe(tuple => Textures.Save(Extension<CharaMods, CoordMods>.Indices[tuple.Index], tuple.Archive)),
            Extension.OnSaveChara.Subscribe(tuple => Textures.Save(Extension<CharaMods, CoordMods>.Humans[tuple.Human], tuple.Archive)),
            Extension.OnSaveCoord.Subscribe(tuple => Textures.Save(Extension<CharaMods, CoordMods>.Humans.NowCoordinate[tuple.Human], tuple.Archive)),
            Extension.OnLoadChara.Subscribe(human => new ModApplicator(human)),
            Extension.OnLoadCoord.Subscribe(human => new ModApplicator(human)),
            ..EditWindow.Initialize(plugin)
        ];
    }
}