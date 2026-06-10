using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
        (Func<object, bool> Check, Action Apply, Action Store) CurrentTarget;
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
                window.With(() => Plugin.Instance.Log.LogInfo(Manager.Scene.NowData.LevelName)),
                new EditGroup("Item", ListPanel),
                new EditGroup("Face", ListPanel),
                new EditGroup("Body", ListPanel), [
                Extension.OnPrepareSaveChara.Subscribe(_ => CurrentTarget.Store()),
                ModApplicator.OnApplicationComplete
                    .Where(human => CurrentTarget.Check(human))
                    .Subscribe(_ => CurrentTarget.Apply()),
                Hooks.OnTargetCleared.Subscribe(_ => OnTargetCleared()),
                Hooks.OnTargetChangedToItem.Subscribe(oci => OnTargetChange(oci.itemComponent)),
                Hooks.OnTargetChangedToChar.Subscribe(oci => OnTargetChange(oci.charInfo)),
                window.OnUpdate.Subscribe(_ => Update())
            ]);

        EditGroup GroupAt(string name, Dictionary<int, EditGroup> groups, int index) =>
            groups.TryGetValue(index, out var group) ? group : groups[index] = new EditGroup(name, ListPanel);
        void Initialize(Dictionary<string, MaterialWrapper> wrappers, EditGroup group) =>
            group.Initialize(wrappers, Window, EditPanel);
        void OnTargetCleared()
        {
            CurrentTarget = (_ => false, F.DoNothing, F.DoNothing);
            Initialize(new(), ItemGroup);
            Initialize(new(), BodyGroup);
            Initialize(new(), FaceGroup);
            HairGroups.Values.ForEach(group => Initialize(new(), group));
            ClothesGroups.Values.ForEach(group => Initialize(new(), group));
            AccessoryGroups.Values.ForEach(group => Initialize(new(), group));
            Window.Title = "SardineHead";
        }
        void OnTargetChange(DigitalCraft.ItemComponent target)
        {
            CurrentTarget = (target.Equals, F.DoNothing, F.DoNothing);
            Initialize(target.Wrap(), ItemGroup);
            Initialize(new(), BodyGroup);
            Initialize(new(), FaceGroup);
            HairGroups.Values.ForEach(group => Initialize(new(), group));
            ClothesGroups.Values.ForEach(group => Initialize(new(), group));
            AccessoryGroups.Values.ForEach(group => Initialize(new(), group));
        }
        void OnTargetChange(Human target)
        {
            CurrentTarget = (target.Equals, F.Apply(ApplyToHuman, target), F.Apply(StoreToHuman, target));
            Initialize(new(), ItemGroup);
            OnBodyChange(target.body);
            OnFaceChange(target.face);
            Enumerable.Range(0, target.hair.hairs.Length).ForEach(index => OnHairChange(target.hair, index));
            Enumerable.Range(0, target.cloth.clothess.Length).ForEach(index => OnClothesChange(target.cloth, index));
            Enumerable.Range(0, target.acs.accessories.Length).ForEach(index => OnAccessoryChange(target.acs, index));
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
        void ApplyToHuman(Human target) => Apply(Extension<CharaMods, CoordMods>.Humans.NowCoordinate[target]);
        void StoreToHuman(Human target) => Extension<CharaMods, CoordMods>.Humans.NowCoordinate[target] = new CoordMods()
        {
            Face = FaceGroup.Store(),
            Body = BodyGroup.Store(),
            Hairs = HairGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Clothes = ClothesGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Accessories = AccessoryGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store())
        };
        void Update(IEnumerable<EditGroup> groups) => groups.Do(group => group.Update());
        void Update() =>
            Update([ItemGroup, FaceGroup, BodyGroup, .. HairGroups.Values, .. ClothesGroups.Values, .. AccessoryGroups.Values]);
        static EditWindow Instance;
        static IDisposable[] Initialize(WindowConfig config) => [
            DigitalCraftExtension.OnSceneStartup.Subscribe(_ => (Instance = new EditWindow(UI.Window(config))).OnTargetCleared()),
            DigitalCraftExtension.OnSceneDestroy.Subscribe(_ => Instance.Subscriptions.Dispose())
        ];
        internal static IDisposable[] Initialize(Plugin plugin) =>
            Initialize(new WindowConfig(plugin, Plugin.Name, new(30, -80), new KeyboardShortcut(KeyCode.H, KeyCode.LeftAlt)));
    }

    static partial class Hooks
    {
        internal static IObservable<Unit> OnInitialize =>
            OnTargetChangedToChar.Select(_ => Unit.Default)
                .Merge(OnTargetChangedToItem.Select(_ => Unit.Default))
                    .Merge(OnTargetCleared).FirstAsync();
        internal static IObservable<Unit> OnTargetCleared =>
            TargetCleared.AsObservable();
        internal static IObservable<DigitalCraft.OCIItem> OnTargetChangedToItem =>
            TargetChangedToItem.AsObservable().Where(oci => oci.itemComponent != null);
        internal static IObservable<DigitalCraft.OCIChar> OnTargetChangedToChar =>
            TargetChangedToChar.AsObservable().Where(oci => oci.charInfo != null);

        static Subject<Unit> TargetCleared = new ();
        static Subject<DigitalCraft.OCIItem> TargetChangedToItem = new();
        static Subject<DigitalCraft.OCIChar> TargetChangedToChar = new();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(DigitalCraft.OCIItem), nameof(DigitalCraft.OCIItem.OnSelect), [typeof(bool)])]
        static void OCIItemOnSelectPostfix(DigitalCraft.OCIItem __instance, bool _select) =>
            _select.Either(F.Apply(TargetChangedToItem.OnNext, __instance), F.Apply(TargetCleared.OnNext, Unit.Default)); 

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(DigitalCraft.OCIChar), nameof(DigitalCraft.OCIChar.OnSelect), [typeof(bool)])]
        static void OCICharOnSelectPostfix(DigitalCraft.OCIChar __instance, bool _select) =>
            _select.Either(F.Apply(TargetChangedToChar.OnNext, __instance), F.Apply(TargetCleared.OnNext, Unit.Default)); 

        internal static IDisposable[] Initialize(Plugin plugin) => [
            ..Extension.Register<CharaMods, CoordMods>(),
            Extension.OnPreprocessChara.Select(tuple => tuple.Item2).Subscribe(Textures.Load),
            Extension.OnPreprocessCoord.Select(tuple => tuple.Item2).Subscribe(Textures.Load),
            Extension.OnSaveChara.Subscribe(tuple => Textures.Save(Extension<CharaMods, CoordMods>.Humans[tuple.Human], tuple.Archive)),
            Extension.OnLoadChara.Subscribe(human => new ModApplicator(human)),
            Extension.OnLoadCoord.Subscribe(human => new ModApplicator(human)),
            ..EditWindow.Initialize(plugin)
        ];
    }

    [BepInDependency(VarietyOfScales.Plugin.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}