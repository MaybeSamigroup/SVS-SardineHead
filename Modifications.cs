using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
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
            Face = Face,
            Body = Body,
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
        internal static event Action OnApplicationComplete = delegate { };
        static Dictionary<Human, CompositeDisposable> Current = new();
        Human Target;
        CoordMods Mods;
        static void Prepare(Human human) =>
            human.gameObject.GetComponent<ObservableDestroyTrigger>()
                .OnDestroyAsObservable().Subscribe(F.Apply(Dispose, human).Ignoring<Unit>());
        static void Dispose(Human human) =>
            (Current.TryGetValue(human, out var item) && Current.Remove(human)).Maybe(F.Apply(Dispose, item));
        static void Dispose(CompositeDisposable item) =>
            (!item.IsDisposed).Maybe(item.Dispose);
        internal ModApplicator(Human human)
        {
            (Target, Mods) = (human, Extension.Coord<CharaMods, CoordMods>(human));
            Current.TryGetValue(Target, out var item)
                .Either(F.Apply(Prepare, Target), F.Apply(Dispose, item));
            Current[Target] = new CompositeDisposable();
            Hooks.OnFaceReady += OnFaceReady;
            Hooks.OnBodyReady += OnBodyReady;
            Hooks.OnClothesReady += OnClothesReady;
            Hooks.OnReloadingComplete += OnReloadingComplete;
            Current[Target].Add(Disposable.Create((Action)Clean));
        }
        void Clean()
        {
            Hooks.OnFaceReady -= OnFaceReady;
            Hooks.OnBodyReady -= OnBodyReady;
            Hooks.OnClothesReady -= OnClothesReady;
            Hooks.OnReloadingComplete -= OnReloadingComplete;
        }

        void NotifyComplete() =>
            OnApplicationComplete();

        void Prepare(CancellationTokenSource cts) =>
            UniTask.DelayFrame(10, PlayerLoopTiming.Update, cts.Token)
                .ContinueWith(F.Apply(Target.Apply, Mods) + NotifyComplete);
        void Apply() =>
            Current[Target].With(Clean)
                .Add(Disposable.Create((Action)new CancellationTokenSource().With(Prepare).Cancel));

        void OnFaceReady(HumanFace item) =>
            (item.human == Target).Maybe(F.Apply(item.Apply, Mods));
        void OnBodyReady(HumanBody item) =>
            (item.human == Target).Maybe(F.Apply(item.Apply, Mods));
        void OnClothesReady(HumanCloth item, int index) =>
            (item.human == Target).Maybe(F.Apply(item.clothess[index].Apply, Mods, index));
        void OnReloadingComplete(Human human) =>
            (human == Target).Maybe(Apply);
    }
    static partial class Hooks
    {
        internal static event Action<HumanFace> OnFaceReady = delegate { };
        internal static event Action<HumanBody> OnBodyReady = delegate { };
        internal static event Action<HumanCloth, int> OnClothesReady = delegate { };
        internal static event Action<Human> OnReloadingComplete = delegate { };

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.CreateFaceTexture))]
        internal static void HumanFaceCreateFaceTexturePostfix(HumanFace __instance) =>
            OnFaceReady(__instance);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.CreateBodyTexture))]
        internal static void HumanBodyCreataBodyTexturePostfix(HumanBody __instance) =>
            OnBodyReady(__instance);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.CreateClothesTexture))]
        internal static void HumanClothCreateClothesTexturePostfix(HumanCloth __instance, int kind) =>
            OnClothesReady(__instance, kind);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Create))]
        static void HumanCreatePostfix(Human __result) =>
            OnReloadingComplete(__result);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Reload), [])]
        static void HumanReloadPostfix(Human __instance) =>
            OnReloadingComplete(__instance);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), [])]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), typeof(Human.ReloadFlags))]
        static void HumanReloadCoordinatePostfix(Human __instance) =>
            OnReloadingComplete(__instance);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human.Reloading), nameof(Human.Reloading.Dispose))]
        internal static void HumanReloadingDisposePostfix(Human.Reloading __instance) =>
            (!__instance._isReloading).Maybe(F.Apply(OnReloadingComplete, __instance._human));
    }
}
