using System;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;
using HarmonyLib;
using Character;
using CoastalSmell;
using Fishbone;
using ItemExtension = Fishbone.Extension<CoastalSmell.Item, DigitalCraft.OIItemInfo, DigitalCraft.OCIItem, SardineHead.ItemMods>;

namespace SardineHead
{
   internal class EditWindow
    {
        Window Window;
        Transform ListPanel;
        Transform EditPanel;
        EditGroup ItemGroup;
        EditGroup FaceGroup;
        EditGroup BodyGroup;
        Dictionary<int, EditGroup> HairGroups = new();
        Dictionary<int, EditGroup> ClothesGroups = new();
        Dictionary<int, EditGroup> AccessoryGroups = new();
        CompositeDisposable Subscriptions;
        CompositeDisposable Target = [Disposable.Create(F.DoNothing)];
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
            (Window, ItemGroup, FaceGroup, BodyGroup, Subscriptions) = (
                window,
                new EditGroup("Item", ListPanel),
                new EditGroup("Face", ListPanel),
                new EditGroup("Body", ListPanel), [
                DigitalCraftExtension.OnSelectNothing.Subscribe(_ => OnTargetCleared()),
                DigitalCraftExtension.OnSelectMultiple.Subscribe(_ => OnTargetCleared()),
                DigitalCraftExtension.OnSelectSingleChara.Subscribe(oci => OnTargetChange(oci.charInfo)),
                DigitalCraftExtension.OnSelectSingleItem.Subscribe(OnTargetChange),
                window.OnUpdate.Subscribe(_ => Update())
            ]);
        EditGroup GroupAt(string name, Dictionary<int, EditGroup> groups, int index) =>
            groups.TryGetValue(index, out var group) ? group : groups[index] = new EditGroup(name, ListPanel);
        IEnumerable<EditGroup> AllGroups =>
            [ItemGroup, BodyGroup, FaceGroup, .. HairGroups.Values, .. ClothesGroups.Values, .. AccessoryGroups.Values];
        void Update() => AllGroups.ForEach(group => group.Update());
        void Cleanup() => AllGroups.ForEach(group => Initialize(new (), group));
        void Initialize(Dictionary<string, MaterialWrapper> wrappers, EditGroup group) =>
            group.Initialize(wrappers, Window, EditPanel);
        void OnTargetCleared()
        {
            Target.Dispose();
            Window.Title = "SardineHead";
            Window.Content.SetActive(false);
        }
        void OnTargetChange(DigitalCraft.OCIItem target)
        {
            Target.Dispose();
            Initialize(target.Wrap(), ItemGroup);
            Apply(target);
            Target = [Disposable.Create(F.Apply(Store, target)), Disposable.Create(Cleanup)];
            Window.Content.SetActive(true);
        }
        void OnTargetChange(Human target)
        {
            Target.Dispose();
            OnBodyChange(target.body);
            OnFaceChange(target.face);
            Enumerable.Range(0, target.hair.hairs.Length).ForEach(index => OnHairChange(target.hair, index));
            Enumerable.Range(0, target.cloth.clothess.Length).ForEach(index => OnClothesChange(target.cloth, index));
            Enumerable.Range(0, target.acs.accessories.Length).ForEach(index => OnAccessoryChange(target.acs, index));
            Apply(target);
            Target = [Disposable.Create(F.Apply(Store, target)), Disposable.Create(Cleanup)];
            Window.Content.SetActive(true);
        }
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
        void Apply(Human target) => Apply(Extension<CharaMods, CoordMods>.Humans.NowCoordinate[target]);
        void Store(Human target) => Extension<CharaMods, CoordMods>.Humans.NowCoordinate[target] = new CoordMods()
        {
            Face = FaceGroup.Store(),
            Body = BodyGroup.Store(),
            Hairs = HairGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Clothes = ClothesGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Accessories = AccessoryGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store())
        };

        void Apply(ItemMods mods)
        {
            ItemGroup.Apply(mods.Values);
        }
        void Apply(DigitalCraft.OCIItem target) => Apply(ItemExtension.Values[target]);
        void Store(DigitalCraft.OCIItem target) => ItemExtension.Values[target] = new ItemMods()
        {
            Values = ItemGroup.Store(),
        };

        static EditWindow Instance;
        static IDisposable[] Initialize(WindowConfig config) => [
            DigitalCraftExtension.OnSceneStartup.Subscribe(_ => (Instance = new EditWindow(UI.Window(config))).OnTargetCleared()),
            DigitalCraftExtension.OnSceneDestroy.Subscribe(_ => Instance.Subscriptions.Dispose())
        ];
        internal static IDisposable[] Initialize(Plugin plugin) =>
            Initialize(new WindowConfig(plugin, Plugin.Name, new(30, -80), new KeyboardShortcut(KeyCode.H, KeyCode.LeftAlt)));
    }

    [ItemExtension<ItemMods>(Plugin.Name, "modifications")]
    public class ItemMods
    {
        public Dictionary<string, Modifications> Values { get; set; } = new();
    }
    static partial class Hooks
    {
        internal static IDisposable[] Initialize(Plugin plugin) => [
            ..Extension.Register<CharaMods, CoordMods>(),
            ..Extension.RegisterItem<ItemMods>(),
            Extension.OnLoadScene.Subscribe(Textures.Load),
            Extension.OnPreprocessChara.Select(tuple => tuple.Item2).Subscribe(Textures.Load),
            Extension.OnPreprocessCoord.Select(tuple => tuple.Item2).Subscribe(Textures.Load),
            Extension.OnSaveChara.Subscribe(tuple => Textures.Save(Extension<CharaMods, CoordMods>.Humans[tuple.Human], tuple.Archive)),
            Extension.OnLoadChara.Subscribe(human => new ModApplicator(human)),
            Extension.OnLoadCoord.Subscribe(human => new ModApplicator(human)),
            ItemExtension.OnLoad.Subscribe(entry => entry.Index.Apply(entry.Value.Values)),
            ..EditWindow.Initialize(plugin)
        ];
    }

    [BepInDependency(VarietyOfScales.Plugin.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}