using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using Cysharp.Threading.Tasks;
using Il2CppSystem.Threading;
using HarmonyLib;
using Character;
using Fishbone;
using CoastalSmell;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Mods = System.Collections.Generic.Dictionary<string, SardineHead.Modifications>;

namespace SardineHead
{
    interface TextureMods
    {
        IEnumerable<string> ToTextures();
    }
    public enum BoolValue
    {
        Unmanaged,
        Enabled,
        Disabled
    }
    public class Modifications
    {
        public string Shader { get; set; } = null;
        public BoolValue Rendering { get; set; } = BoolValue.Unmanaged;
        public Dictionary<string, int> IntValues { get; init; } = new();
        public Dictionary<string, float> FloatValues { get; init; } = new();
        public Dictionary<string, float> RangeValues { get; init; } = new();
        public Dictionary<string, Float4> ColorValues { get; init; } = new();
        public Dictionary<string, Float4> VectorValues { get; init; } = new();
        public Dictionary<string, string> TextureHashes { get; init; } = new();
    }
    [Extension<CharaMods, CoordMods>(Plugin.Name, "modifications.json")]
    public class CharaMods : CharacterExtension<CharaMods>, ComplexExtension<CharaMods, CoordMods>, TextureMods
    {
        public Mods Face { get; set; } = new();
        public Mods Body { get; set; } = new();
        public Dictionary<int, Dictionary<int, Mods>> Hairs { get; set; } = new();
        public Dictionary<int, Dictionary<int, Mods>> Clothes { get; set; } = new();
        public Dictionary<int, Dictionary<int, Mods>> Accessories { get; set; } = new();
        public CoordMods Get(int coordinateType) => new()
        {
            Face = Face,
            Body = Body,
            Hairs = Hairs.TryGetValue(coordinateType, out var hairs) ? hairs : new(),
            Clothes = Clothes.TryGetValue(coordinateType, out var clothes) ? clothes : new(),
            Accessories = Accessories.TryGetValue(coordinateType, out var accessories) ? accessories : new(),
        };
        public CharaMods Merge(CharaLimit limit, CharaMods mods) => new()
        {
            Face = (limit & CharaLimit.Face) == CharaLimit.None ? Face : mods.Face,
            Body = (limit & CharaLimit.Body) == CharaLimit.None ? Body : mods.Body,
            Hairs = (limit & CharaLimit.Hair) == CharaLimit.None ? Hairs : mods.Hairs,
            Clothes = (limit & CharaLimit.Coorde) == CharaLimit.None ? Clothes : mods.Clothes,
            Accessories = (limit & CharaLimit.Coorde) == CharaLimit.None ? Accessories : mods.Accessories,
        };
        public CharaMods Merge(int coordinateType, CoordMods mods) => new()
        {
            Face = mods.Face,
            Body = mods.Body,
            Hairs = Hairs.Merge(coordinateType, mods.Hairs),
            Clothes = Clothes.Merge(coordinateType, mods.Clothes),
            Accessories = Accessories.Merge(coordinateType, mods.Accessories),
        };
        public IEnumerable<string> ToTextures() =>
            Face.Values.SelectMany(item => item.TextureHashes.Values)
                .Concat(Body.Values.SelectMany(item => item.TextureHashes.Values))
                .Concat(Hairs.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values))
                .Concat(Clothes.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values))
                .Concat(Accessories.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values));
    }
    public class CoordMods : CoordinateExtension<CoordMods>, TextureMods
    {
        public Mods Face { get; set; } = new();
        public Mods Body { get; set; } = new();
        public Dictionary<int, Mods> Hairs { get; set; } = new();
        public Dictionary<int, Mods> Clothes { get; set; } = new();
        public Dictionary<int, Mods> Accessories { get; set; } = new();
        public CoordMods Merge(CoordLimit limit, CoordMods mods) => new()
        {
            Face = (limit & CoordLimit.FaceMakeup) == CoordLimit.None ? Face : mods.Face,
            Body = (limit & CoordLimit.BodyMakeup) == CoordLimit.None ? Body : mods.Body,
            Hairs = (limit & CoordLimit.Hair) == CoordLimit.None ? Hairs : mods.Hairs,
            Clothes = (limit & CoordLimit.Clothes) == CoordLimit.None ? Clothes : mods.Clothes,
            Accessories = (limit & CoordLimit.Accessory) == CoordLimit.None ? Accessories : mods.Accessories
        };
        public IEnumerable<string> ToTextures() =>
            Face.Values.SelectMany(item => item.TextureHashes.Values)
                .Concat(Body.Values.SelectMany(item => item.TextureHashes.Values))
                .Concat(Hairs.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values))
                .Concat(Clothes.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values))
                .Concat(Accessories.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values));
    }
    class ModApplicator
    {
        internal static Subject<Unit> OnApplicationComplete = new(); 
        static Dictionary<Human, CompositeDisposable> Current = new(Il2CppEquals.Instance);
        static void Prepare(Human human) =>
            human.component.OnDestroyAsObservable().Subscribe(_ => Cleanup(human));
        static void Cleanup(Human human) =>
            Current.Remove(human, out var subscriptions).Maybe(subscriptions.Dispose);
        Human Target;
        CoordMods Mods;
        internal ModApplicator(Human human)
        {
            (Target, Mods) = (human, Extension<CharaMods, CoordMods>.Humans.NowCoordinate[human]);
            Current.ContainsKey(Target).Either(F.Apply(Prepare, Target), F.Apply(Cleanup, Target));
            Current[Target] = [
                Hooks.OnFaceReady.Where(Il2CppEquals.Apply(Target.face)).Subscribe(Mods.Apply),
                Hooks.OnBodyReady.Where(Il2CppEquals.Apply(Target.body)).Subscribe(Mods.Apply),
                Hooks.OnClothesReady
                    .Where(pair => Il2CppEquals.Apply(Target.cloth, pair.Item))
                    .Subscribe(pair => pair.Item.Clothess[pair.Index].Apply(pair.Index, Mods)),
                Hooks.OnReloadingComplete.Where(Il2CppEquals.Apply(Target)).Subscribe(Complete)
            ];
        }
        Action<CancellationTokenSource> Prepare(Action action) => cts =>
            UniTask.DelayFrame(10, PlayerLoopTiming.Update, cts.Token)
                .ContinueWith(action + F.Apply(OnApplicationComplete.OnNext, Unit.Default));
        void Complete(Human human) =>
            Current[Target.With(Cleanup)] = [
                Disposable.Create(new CancellationTokenSource()
                    .With(Prepare(F.Apply(human.Apply, Mods))).Cancel)
            ];
    }
    static partial class Hooks
    {
        internal static IObservable<HumanFace> OnFaceReady => FaceReady.AsObservable(); 
        internal static IObservable<HumanBody> OnBodyReady => BodyReady.AsObservable(); 
        internal static IObservable<(HumanCloth Item, int Index)> OnClothesReady => ClothesReady.AsObservable();
        internal static IObservable<Human> OnReloadingComplete => ReloadingComplete.AsObservable();

        static Subject<HumanFace> FaceReady = new();
        static Subject<HumanBody> BodyReady = new();
        static Subject<(HumanCloth, int)> ClothesReady = new();
        static Subject<Human> ReloadingComplete = new();

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.CreateFaceTexture))]
        internal static void HumanFaceCreateFaceTexturePostfix(HumanFace __instance) => FaceReady.OnNext(__instance);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.CreateBodyTexture))]
        internal static void HumanBodyCreataBodyTexturePostfix(HumanBody __instance) => BodyReady.OnNext(__instance);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.CreateClothesTexture))]
        internal static void HumanClothCreateClothesTexturePostfix(HumanCloth __instance, int kind) => ClothesReady.OnNext((__instance, kind));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Create))]
        [HarmonyPatch(typeof(Human), nameof(Human.CreateCustom))]
        static void HumanCreatePostfix(Human __result) => ReloadingComplete.OnNext(__result);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Reload), [])]
        static void HumanReloadPostfix(Human __instance) => ReloadingComplete.OnNext(__instance);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), [])]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), typeof(Human.ReloadFlags))]
        static void HumanReloadCoordinatePostfix(Human __instance) => ReloadingComplete.OnNext(__instance);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human.Reloading), nameof(Human.Reloading.Dispose))]
        internal static void HumanReloadingDisposePostfix(Human.Reloading __instance) =>
            (!__instance._isReloading).Maybe(F.Apply(ReloadingComplete.OnNext, __instance._human));
    }
}
