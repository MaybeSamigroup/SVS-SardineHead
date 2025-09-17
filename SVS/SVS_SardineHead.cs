using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using Character;
using CharacterCreation;
using HarmonyLib;
using BepInEx;
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
            Initialize(item.clothess[index].WrapCtc().Concat(item.Wrap(index)).ToDictionary(),
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
        void Store(CharaMods mods, int coordinateType) => (
            mods.Face,
            mods.Body,
            mods.Hairs[coordinateType],
            mods.Clothes[coordinateType],
            mods.Accessories[coordinateType]
        ) = (
            FaceGroup.Store(),
            BodyGroup.Store(),
            HairGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            ClothesGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            AccessoryGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store())
        );
        void Apply() => Apply(HumanExtension<CharaMods, CoordMods>.Coord);
        void Store() => Store(HumanExtension<CharaMods, CoordMods>.Chara, HumanCustom.Instance.Human.data.Status.coordinateType);
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

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.InitBaseCustomTextureBody))]
        internal static void InitBaseCustomTextureBodyPostfix(HumanBody __instance) =>
            OnBodyChange(__instance);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.ChangeHead), typeof(int), typeof(bool))]
        static void ChangeHeadPostfix(HumanFace __instance) =>
            OnFaceChange(__instance);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanHair), nameof(HumanHair.ChangeHair), typeof(int), typeof(int), typeof(bool))]
        static void ChangeHairPostfix(HumanHair __instance, int kind) =>
            OnHairChange(__instance, kind);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesBot), typeof(int), typeof(bool))]
        static void ChangeClothesBotPostfix(HumanCloth __instance) =>
            OnClothesChange(__instance, 1);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesGloves), typeof(int), typeof(bool))]
        static void ChangeClothesGlovesPostfix(HumanCloth __instance) =>
            OnClothesChange(__instance, 4);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesPanst), typeof(int), typeof(bool))]
        static void ChangeClothesPanstPostfix(HumanCloth __instance) =>
            OnClothesChange(__instance, 5);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesSocks), typeof(int), typeof(bool))]
        static void ChangeClothesSocksPostfix(HumanCloth __instance) =>
            OnClothesChange(__instance, 6);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesShoes), typeof(int), typeof(bool))]
        static void ChangeClothesShoesPostfix(HumanCloth __instance) =>
            OnClothesChange(__instance, 7);

        static readonly int[] BraOnly = [2];
        static readonly int[] ShortsOnly = [3];
        static readonly int[] BraAndShorts = [2, 3];

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesBra), typeof(int), typeof(bool))]
        static void ChangeClothesBraPrefix(HumanCloth __instance, ref bool __state) =>
            __state = __instance.notShorts;

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesBra), typeof(int), typeof(bool))]
        static void ChangeClothesBraPostfix(HumanCloth __instance, bool __state) =>
            ((__state == __instance.notShorts) ? BraOnly : BraAndShorts).Do(kind => OnClothesChange(__instance, kind));

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesShorts), typeof(int), typeof(bool))]
        static void ChangeClothesShortsPrefix(HumanCloth __instance, ref bool __state) =>
            __state = __instance.notBra;

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesShorts), typeof(int), typeof(bool))]
        static void ChangeClothesShortsPostfix(HumanCloth __instance, bool __state) =>
            ((__state == __instance.notShorts) ? ShortsOnly : BraAndShorts).Do(kind => OnClothesChange(__instance, kind));
        static readonly int[] TopOnly = [0];
        static readonly int[] TopAndBot = [0, 1];

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesTop),
            [typeof(HumanCloth.TopResultData), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool)],
            [ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal])]
        static void ChangeClothesTopPrefix(HumanCloth __instance, ref bool __state) =>
            __state = __instance.notBot;

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesTop),
            [typeof(HumanCloth.TopResultData), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool)],
            [ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal])]
        static void ChangeClothesTopPostfix(HumanCloth __instance, bool __state) =>
            ((__state == __instance.notBot) ? TopOnly : TopAndBot).Do(kind => OnClothesChange(__instance, kind));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanAccessory), nameof(HumanAccessory.ChangeAccessory),
            typeof(int), typeof(int), typeof(int), typeof(ChaAccessoryDefine.AccessoryParentKey), typeof(bool))]
        static void ChangeAccessoryPostfix(HumanAccessory __instance, int slotNo) =>
            OnAccessoryChange(__instance, slotNo);

        internal static void OnCustomLoaded() =>
            OnReloadingComplete(HumanCustom.Instance.Human);

        internal static void OnActorLoaded(SaveData.Actor _, Human human) =>
            OnReloadingComplete(human);
    }

    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public partial class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
        public override void Load()
        {
            (Instance, Patch) = (this, Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks"));
            Extension.Register<CharaMods, CoordMods>();
            Extension.OnPreprocessChara += (_, archive) => Textures.Load(archive);
            Extension.OnPreprocessCoord += (_, archive) => Textures.Load(archive);
            Extension.OnSaveChara += (archive) =>
                Textures.Save(HumanExtension<CharaMods, CoordMods>.Chara, archive);
            Extension.OnSaveCoord += (archive) =>
                Textures.Save(HumanExtension<CharaMods, CoordMods>.Coord, archive);
            Extension.OnSaveActor += (actor, archive) =>
                Textures.Save(ActorExtension<CharaMods, CoordMods>.Chara(actor), archive);
            Extension.OnLoadChara += human => new ModApplicator(human);
            Extension.OnLoadCoord += human => new ModApplicator(human);
            Extension.OnLoadActorChara += Hooks.OnActorLoaded; 
            Extension.OnLoadActorCoord += Hooks.OnActorLoaded; 
            EditWindow.Initialize();
        }
    }
}