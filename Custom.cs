using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
#if Aicomi
using R3;
using R3.Triggers;
#else
using UniRx;
using UniRx.Triggers;
#endif
using Character;
using CharacterCreation;
using HarmonyLib;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;
using Fishbone;
using CoastalSmell;

namespace SardineHead
{
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
        EditWindow(GameObject window) : this(UI.Panels(window)) => window
            .With(UI.PrepareChoicesList)
            .With(ShaderEdit.PrepareArchetype)
            .With(RenderingEdit.PrepareArchetype)
            .With(IntEdit.PrepareArchetype)
            .With(FloatEdit.PrepareArchetype)
            .With(RangeEdit.PrepareArchetype)
            .With(ColorEdit.PrepareArchetype)
            .With(VectorEdit.PrepareArchetype)
            .With(TextureEdit.PrepareArchetype)
            .GetComponentInParent<ObservableUpdateTrigger>()
                .UpdateAsObservable().Subscribe(F.Ignoring<Unit>(Update));
        EditWindow() : this(UI.Window(Handle)) =>
            (FaceGroup, BodyGroup) = (new EditGroup("Face", ListPanel), new EditGroup("Body", ListPanel));
        EditGroup GroupAt(string name, Dictionary<int, EditGroup> groups, int index) =>
            groups.TryGetValue(index, out var group) ? group : groups[index] = new EditGroup(name, ListPanel);
        void Initialize(Dictionary<string, MaterialWrapper> wrappers, EditGroup group) =>
            group.Initialize(wrappers, Handle, EditPanel);
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
        void Apply() => Apply(Extension.Coord<CharaMods, CoordMods>());
        void Store() => Extension.Coord<CharaMods, CoordMods>(new CoordMods()
        {
            Face = FaceGroup.Store(),
            Body = BodyGroup.Store(),
            Hairs = HairGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Clothes = ClothesGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Accessories = AccessoryGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store())
        });
        void Update(IEnumerable<EditGroup> groups) => groups.Do(group => group.Update());
        void Update() =>
            Update([FaceGroup, BodyGroup, .. HairGroups.Values, .. ClothesGroups.Values, .. AccessoryGroups.Values]);
        static WindowHandle Handle;
        static EditWindow Instance;
        internal static void Initialize()
        {
            Handle = new WindowHandle(Plugin.Instance, Plugin.Name, new(30, -80), new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl));
            Util<HumanCustom>.Hook(() =>
            {
                Instance = new EditWindow();
                Hooks.OnBodyChange += Instance.OnBodyChange;
                Hooks.OnFaceChange += Instance.OnFaceChange;
                Hooks.OnHairChange += Instance.OnHairChange;
                Hooks.OnClothesChange += Instance.OnClothesChange;
                Hooks.OnAccessoryChange += Instance.OnAccessoryChange;
                Extension.PrepareSaveChara += Instance.Store;
                Extension.PrepareSaveCoord += Instance.Store;
                ModApplicator.OnApplicationComplete += Instance.Apply;
                Util.OnCustomHumanReady(Hooks.OnCustomLoaded);
            }, () =>
            {
                Hooks.OnBodyChange -= Instance.OnBodyChange;
                Hooks.OnFaceChange -= Instance.OnFaceChange;
                Hooks.OnHairChange -= Instance.OnHairChange;
                Hooks.OnClothesChange -= Instance.OnClothesChange;
                Hooks.OnAccessoryChange -= Instance.OnAccessoryChange;
                Extension.PrepareSaveChara -= Instance.Store;
                Extension.PrepareSaveCoord -= Instance.Store;
                ModApplicator.OnApplicationComplete -= Instance.Apply;
                Instance = null;
            });
        }
    }
    static partial class Hooks
    {
        internal static event Action<HumanFace> OnFaceChange = delegate { };
        internal static event Action<HumanBody> OnBodyChange = delegate { };
        internal static event Action<HumanHair, int> OnHairChange = delegate { };
        internal static event Action<HumanCloth, int> OnClothesChange = delegate { };
        internal static event Action<HumanAccessory, int> OnAccessoryChange = delegate { };
        internal static void OnCustomLoaded() =>
            OnReloadingComplete(HumanCustom.Instance.Human);
    }

    public partial class Plugin : BasePlugin
    {
        public override void Load()
        {
            (Instance, Patch) = (this, Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks"));
            Extension.OnPreprocessChara += (_, archive) => Textures.Load(archive);
            Extension.OnPreprocessCoord += (_, archive) => Textures.Load(archive);

            #if SamabakeScrable
            Extension.OnPreprocessChara += Extension<CharaMods, CoordMods>
                .Translate<LegacyCharaMods>(Path.Combine(Guid, "modifications.json"), mods => mods);
            Extension.OnPreprocessCoord += Extension<CharaMods, CoordMods>
                .Translate<LegacyCoordMods>(Path.Combine(Guid, "modifications.json"), mods => mods);
            #endif


            Extension.Register<CharaMods, CoordMods>();
            Extension.OnLoadChara += human => new ModApplicator(human);
            Extension.OnLoadCoord += human => new ModApplicator(human);

            Extension.OnSaveChara += (archive) =>
                Textures.Save(Extension.Chara<CharaMods, CoordMods>(), archive);
            Extension.OnSaveCoord += (archive) =>
                Textures.Save(Extension.Coord<CharaMods, CoordMods>(), archive);
            Extension.OnSaveActor += (actor, archive) =>
                Textures.Save(Extension.Chara<CharaMods, CoordMods>(actor), archive);
            EditWindow.Initialize();
        }
    }
}