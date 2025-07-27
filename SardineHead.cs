using BepInEx;
using HarmonyLib;
using BepInEx.Unity.IL2CPP;
using System;
using System.Linq;
using System.IO.Compression;
using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Character;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Mods = System.Collections.Generic.Dictionary<string, SardineHead.Modifications>;
using CoastalSmell;
using Fishbone;

namespace SardineHead
{
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
    [BonesToStuck(Plugin.Guid, "modifications.json")]
    public partial class LegacyCharaMods
    {
        public Mods Face { get; set; } = new();
        public Mods Eyebrows { get; set; } = new();
        public Mods Eyelines { get; set; } = new();
        public Mods Eyes { get; set; } = new();
        public Mods Tooth { get; set; } = new();
        public Mods Body { get; set; } = new();
        public Mods Nails { get; set; } = new();
        public Dictionary<int, LegacyCoordMods> Coordinates { get; set; } = new();
    }
    [BonesToStuck(Plugin.Guid, "modifications.json")]
    public partial class LegacyCoordMods
    {
        public Dictionary<int, Mods> Hair { get; set; } = new();
        public Dictionary<int, Mods> Clothes { get; set; } = new();
        public Dictionary<int, Mods> Accessory { get; set; } = new();
    }
    [BonesToStuck(Plugin.Name, "modifications.json")]
    public partial class CharaMods
    {
        public Mods Face { get; set; } = new();
        public Mods Body { get; set; } = new();
        public Dictionary<int, Dictionary<int, Mods>> Hairs { get; set; } = new();
        public Dictionary<int, Dictionary<int, Mods>> Clothes { get; set; } = new();
        public Dictionary<int, Dictionary<int, Mods>> Accessories { get; set; } = new();
        internal CoordMods AsCoord(Human human) => AsCoord(human.data);
        internal CoordMods AsCoord(HumanData data) => AsCoord(data.Status.coordinateType);
        internal partial CoordMods AsCoord(int index);
        internal partial Func<CharaMods, CharaMods> Merge(CharaLimit limits);
        internal Func<CoordLimit, CoordMods, CharaMods> Merge(Human human) => Merge(human.data);
        internal Func<CoordLimit, CoordMods, CharaMods> Merge(HumanData data) => Merge(data.Status.coordinateType);
        internal partial Func<CoordLimit, CoordMods, CharaMods> Merge(int index);
        internal static Func<ZipArchive, CharaMods> Load;
        internal static Action<ZipArchive, CharaMods> Save;
    }
    [BonesToStuck(Plugin.Name, "modifications.json")]
    public partial class CoordMods
    {
        public Mods Face { get; set; } = new();
        public Mods Body { get; set; } = new();
        public Dictionary<int, Mods> Hairs { get; set; } = new();
        public Dictionary<int, Mods> Clothes { get; set; } = new();
        public Dictionary<int, Mods> Accessories { get; set; } = new();
        internal partial Func<CoordMods, CoordMods> Merge(CoordLimit limits);
        internal static Func<ZipArchive, CoordMods> Load;
        internal static Action<ZipArchive, CoordMods> Save;
    }
    internal static class ModificationExtensions
    {
        internal static void Apply(this Dictionary<string, MaterialWrapper> wrappers, Mods mods) =>
            wrappers.Do(entry => entry.Value.Apply(mods.TryGetValue(entry.Key, out var value) ? value : new()));
        internal static Action Apply(this Human item, CoordMods mods) =>
            F.Apply(Apply, item.hair, mods) +
            F.Apply(Apply, item.cloth, mods) +
            F.Apply(Apply, item.acs, mods);
        static void Apply(this HumanHair item, CoordMods mods) =>
           item.hairs.ForEachIndex(mods.Apply);
        static void Apply(this HumanCloth item, CoordMods mods) =>
            item.clothess.ForEachIndex(mods.Apply);
        static void Apply(this HumanAccessory item, CoordMods mods) =>
            item.accessories.ForEachIndex(mods.Apply); 
        static void Apply(this CoordMods mods, HumanHair.Hair item, int index) =>
            mods.Hairs.TryGetValue(index, out var value).Maybe(F.Apply(item.Wrap().Apply, value));
        static void Apply(this CoordMods mods, HumanCloth.Clothes item, int index) =>
            mods.Clothes.TryGetValue(index, out var value).Maybe(F.Apply(item.Wrap().Apply, value));
        static void Apply(this CoordMods mods, HumanAccessory.Accessory item, int index) =>
            mods.Accessories.TryGetValue(index, out var value).Maybe(F.Apply(item.Wrap().Apply, value));
        internal static bool NotEmpty(this Modifications mods) =>
            mods.IntValues.Count + mods.FloatValues.Count + mods.RangeValues.Count +
            mods.ColorValues.Count + mods.VectorValues.Count + mods.TextureHashes.Count > 0;
        internal static bool NotEmpty(this Mods mods) =>
            mods.Values.Any(NotEmpty);
    }
    internal class MaterialWrapper
    {
        static Dictionary<ShaderPropertyType, Dictionary<string, int>> EmptyIds =>
            Enum.GetValues<ShaderPropertyType>().ToDictionary(value => value, value => new Dictionary<string, int>());
        internal Renderer Renderer;
        Material Material;
        Shader Shader;
        Action<int> UpdateProperty = F.DoNothing.Ignoring<int>();
        Dictionary<ShaderPropertyType, Dictionary<string, int>> Ids = EmptyIds;
        internal Func<string, int> GetInt => name =>
            Ids[ShaderPropertyType.Int].TryGetValue(name, out var id) ? Material.GetInt(id) : default;
        internal Func<string, float> GetFloat => name =>
            Ids[ShaderPropertyType.Float].TryGetValue(name, out var id) ? Material.GetFloat(id) : default;
        internal Func<string, float> GetRange => name =>
            Ids[ShaderPropertyType.Range].TryGetValue(name, out var id) ? Material.GetFloat(id) : default;
        internal Func<string, Color> GetColor => name =>
            Ids[ShaderPropertyType.Color].TryGetValue(name, out var id) ? Material.GetColor(id) : default;
        internal Func<string, Vector4> GetVector => name =>
            Ids[ShaderPropertyType.Vector].TryGetValue(name, out var id) ? Material.GetVector(id) : default;
        internal Func<string, Texture> GetTexture => name =>
            Ids[ShaderPropertyType.Texture].TryGetValue(name, out var id) ? Material.GetTexture(id) : default;
        internal Action<string, int> SetInt => (name, value) =>
            Ids[ShaderPropertyType.Int].TryGetValue(name, out var id)
                .Maybe(UpdateProperty.Apply(id) + F.Apply(Material.SetInt, id, value));
        internal Action<string, float> SetFloat => (name, value) =>
            Ids[ShaderPropertyType.Float].TryGetValue(name, out var id)
                .Maybe(UpdateProperty.Apply(id) + F.Apply(Material.SetFloat, id, value));
        internal Action<string, float> SetRange => (name, value) =>
            Ids[ShaderPropertyType.Range].TryGetValue(name, out var id)
                .Maybe(UpdateProperty.Apply(id) + F.Apply(Material.SetFloat, id, value));
        internal Action<string, Color> SetColor => (name, value) =>
            Ids[ShaderPropertyType.Color].TryGetValue(name, out var id)
                .Maybe(UpdateProperty.Apply(id) + F.Apply(Material.SetColor, id, value));
        internal Action<string, Vector4> SetVector => (name, value) =>
            Ids[ShaderPropertyType.Vector].TryGetValue(name, out var id)
                .Maybe(UpdateProperty.Apply(id) + F.Apply(Material.SetVector, id, value));
        internal Action<string, Texture> SetTexture => (name, value) =>
            Ids[ShaderPropertyType.Texture].TryGetValue(name, out var id)
                .Maybe(UpdateProperty.Apply(id) + F.Apply(Material.SetTexture, id, value)); 
        internal Dictionary<string, ShaderPropertyType> Properties { get; init; } = new();
        internal Dictionary<string, Vector2> RangeLimits { get; init; } = new();
        MaterialWrapper(Material value) =>
            ((Material, Shader) = (value, value.shader)).With(PopulateProperties);
        internal MaterialWrapper(Renderer renderer) : this(renderer.material) =>
            Renderer = renderer;
        internal MaterialWrapper(CustomTextureControl ctc) : this(ctc._matCreate) =>
            UpdateProperty = _ => ctc.SetNewCreateTexture();
        internal MaterialWrapper(CustomTextureCreate ctc, Func<CustomTextureCreate, int, bool> rebuild) : this(ctc._matCreate) =>
            UpdateProperty = id => rebuild(ctc, id);
        void PopulateProperties() =>
            Enumerable.Range(0, Shader.GetPropertyCount()).Do(index => PopulateProperties(Shader, index));
        void PopulateProperties(Shader shader, int index) =>
            PopulateProperties(shader, index, shader.GetPropertyType(index), shader.GetPropertyName(index), shader.GetPropertyNameId(index));
        void PopulateProperties(Shader shader, int index, ShaderPropertyType type, string name, int id) =>
            (type is ShaderPropertyType.Range)
                .With(F.Apply(Properties.TryAdd, name, type).Ignoring())
                .With(F.Apply(Ids[type].TryAdd, name, id).Ignoring())
                .Maybe(F.Apply(PopulateRangeLimits, shader, name, index));
        void PopulateRangeLimits(Shader shader, string name, int index) =>
            RangeLimits.TryAdd(name, shader.GetPropertyRangeLimits(index));
        internal string GetShader() =>
            Shader.name;
        internal void SetShader(string name) =>
            (name != null && Shader.name != name).Maybe(F.Apply(SetShader, Shader.Find(name)));
        void SetShader(Shader shader) =>
            (shader != null).Maybe(F.Apply(SetShaderInternal, shader));
        void SetShaderInternal(Shader shader) =>
            ((Shader, Ids) = (Material.shader = shader, EmptyIds))
                .With(Properties.Clear).With(RangeLimits.Clear).With(PopulateProperties);
        internal Action<Modifications> Apply =>
            ApplyShader + ApplyInt + ApplyFloat + ApplyRange +
            ApplyColor + ApplyVector + ApplyTexture + ApplyRenderer;
        Action<Modifications> ApplyShader => mods =>
            SetShader(mods.Shader);
        Action <Modifications> ApplyInt => mods =>
            mods.IntValues.ForEach(entry => SetInt(entry.Key, entry.Value));
        Action<Modifications> ApplyFloat => mods =>
            mods.FloatValues.ForEach(entry => SetFloat(entry.Key, entry.Value));
        Action<Modifications> ApplyRange => mods =>
            mods.RangeValues.ForEach(entry => SetRange(entry.Key, entry.Value));
        Action<Modifications> ApplyColor => mods =>
            mods.ColorValues.ForEach(entry => SetColor(entry.Key, entry.Value));
        Action<Modifications> ApplyVector => mods =>
            mods.VectorValues.ForEach(entry => SetVector(entry.Key, entry.Value));
        Action<Modifications> ApplyTexture => mods =>
            mods.TextureHashes.ForEach(entry => SetTexture(entry.Key, Textures.FromHash(entry.Value)));
        Action<Modifications> ApplyRenderer => mods =>
            (Renderer != null).Maybe(mods.Rendering switch
            {
                BoolValue.Disabled =>
                    () => Renderer.enabled = false,
                BoolValue.Enabled =>
                    () => Renderer.enabled = true,
                _ =>
                    F.DoNothing
            });
    }
    internal static class MaterialExtension
    {
        static Func<GameObject, IEnumerable<Renderer>> RenderersOfGo =
            go => go?.GetComponents<Renderer>().Concat(RenderersOfTf(go.transform)) ?? [];
        static Func<Transform, IEnumerable<Renderer>> RenderersOfTf =
            tf => tf == null ? [] : Enumerable.Range(0, tf.childCount)
                .Select(idx => tf.GetChild(idx).gameObject).SelectMany(RenderersOfGo);
        static Func<IEnumerable<Renderer>, Dictionary<string, MaterialWrapper>> WrapRenderers =
            renderers => (renderers ?? []).Where(renderer => renderer != null && renderer.material != null)
                .GroupBy(renderer => renderer.name ?? renderer.gameObject.name)
                .SelectMany(groups => Identify(groups, 1))
                .ToDictionary(entry => entry.Item1, entry => new MaterialWrapper(entry.Item2));
        static IEnumerable<Tuple<string, Renderer>> Identify(IGrouping<string, Renderer> groups, int depth) =>
            groups.Count() == 1
                ? groups.Select<Renderer, Tuple<string, Renderer>>(value => new(groups.Key, value))
                : groups.GroupBy(renderer => $"{Identify(renderer.gameObject.transform, depth)}/{groups.Key}")
                    .SelectMany(groups => Identify(groups, depth + 1));
        static string Identify(this Transform tf, int depth) =>
            depth == 0 ? tf.name : tf.parent.Identify(depth - 1);
        internal static Dictionary<string, MaterialWrapper> WrapCtc(this HumanFace face) =>
            new (){ ["/ct_face"] = new MaterialWrapper(face.customTexCtrlFace) };
        internal static Dictionary<string, MaterialWrapper> WrapCtc(this HumanBody body) =>
            new() { ["/ct_body"] = new MaterialWrapper(body.customTexCtrlBody) };
        internal static Dictionary<string, MaterialWrapper> WrapCtc(this HumanCloth.Clothes clothes) =>
            Enumerable.Range(0, clothes?.ctCreateClothes?.Count ?? 0)
                .Where(idx => clothes?.cusClothesCmp != null && clothes?.ctCreateClothes[idx]?._matCreate != null)
                .ToDictionary(idx => $"/{clothes.cusClothesCmp.name}{idx}",
                    idx => new MaterialWrapper(clothes.ctCreateClothes[idx], clothes.cusClothesCmp.Rebuild01));
        internal static Dictionary<string, MaterialWrapper> Wrap(this HumanFace item) =>
            WrapRenderers(RenderersOfGo(item?.objHead)) ?? new();
        internal static Dictionary<string, MaterialWrapper> Wrap(this HumanBody item) =>
            WrapRenderers(RenderersOfGo(item?.objBody)) ?? new();
        internal static Dictionary<string, MaterialWrapper> Wrap(this HumanHair.Hair item) =>
            WrapRenderers(RenderersOfGo(item?.cusHairCmp?.gameObject)) ?? new();
        internal static Dictionary<string, MaterialWrapper> Wrap(this HumanCloth.Clothes item) =>
            WrapRenderers(RenderersOfGo(item?.cusClothesCmp?.gameObject)) ?? new();
        internal static Dictionary<string, MaterialWrapper> Wrap(this HumanAccessory.Accessory item) =>
            WrapRenderers(RenderersOfGo(item?.cusAcsCmp?.gameObject)) ?? new();
        internal static Dictionary<string, MaterialWrapper> Wrap(this HumanHair item, int index) =>
            index < item.hairs.Count ? item.hairs[index].Wrap() : new();
        internal static Dictionary<string, MaterialWrapper> Wrap(this HumanCloth item, int index) =>
            index < item.clothess.Count && index switch
            {
                1 => !item.notBot,
                2 => !item.notBra,
                3 => !item.notShorts,
                _ => true
            } ? item.clothess[index].Wrap() : new();
        internal static Dictionary<string, MaterialWrapper> Wrap(this HumanAccessory item, int index) =>
            index < item.accessories.Count ? item.accessories[index].Wrap() : new();
    }
    internal static partial class Textures
    {
        internal static Func<string, bool> IsExtension;
        internal static Func<string, RenderTexture> FromHash;
        internal static Func<string, RenderTexture> FromFile;
        internal static Action<Texture, string> ToFile;
        internal static Action<ZipArchive> Load;
        internal static Action<TextureMods, ZipArchive> Save;
    }
    partial class ModApplicator
    {
        static Dictionary<HumanData, ModApplicator> Current = new();
        CoordMods Mods;
        CompositeDisposable Disposables;
        ModApplicator(HumanData data, CoordMods mods)
        {
            Mods = mods;
            Hooks.OnFaceReady += OnFaceChange;
            Hooks.OnBodyReady += OnBodyChange;
            Hooks.OnClothesReady += OnClothesChange;
            Hooks.OnFaceReady += OnFaceReady;
            Hooks.OnBodyReady += OnBodyReady;
            Hooks.OnClothesReady += OnClothesReady;
            Disposables = new CompositeDisposable();
            Current.TryGetValue(data, out var item).Maybe(F.Apply(Dispose, item));
            Disposables.Add(Disposable.Create(F.Apply(Current.Remove, data).Ignoring()));
            Current.TryAdd(data, this);
        }
        Action<Human> Cleanup => human =>
        {
            Hooks.OnFaceReady -= OnFaceChange;
            Hooks.OnBodyReady -= OnBodyChange;
            Hooks.OnClothesReady -= OnClothesChange;
            Hooks.OnFaceReady -= OnFaceReady;
            Hooks.OnBodyReady -= OnBodyReady;
            Hooks.OnClothesReady += OnClothesReady;
            Disposables.Add(Scheduler.MainThread
                .Schedule(Il2CppSystem.TimeSpan.FromSeconds(0.05), human.Apply(Mods) + Disposables.Dispose));
            human.gameObject.GetComponent<ObservableDestroyTrigger>()
                .OnDestroyAsObservable().Subscribe(F.Ignoring<Unit>(Disposables.Dispose));
        };
        static void Dispose(ModApplicator ma) =>
            ma.Disposables.Dispose(); 
        void OnFaceReady(HumanFace item) =>
            (Current.GetValueOrDefault(item.human.data) == this)
                .Maybe(F.Apply(item.WrapCtc().Apply, Mods.Face));
        void OnBodyReady(HumanBody item) =>
            (Current.GetValueOrDefault(item.human.data) == this)
                .Maybe(F.Apply(item.WrapCtc().Apply, Mods.Body));
        void OnClothesReady(HumanCloth item, int index) =>
            (Current.GetValueOrDefault(item.human.data) == this)
                .Maybe(F.Apply(item.clothess[index].WrapCtc().Apply, Mods.Clothes.GetValueOrDefault(index, new())));
        void OnFaceChange(HumanFace item) =>
            (Current.GetValueOrDefault(item.human.data) == this)
                .Maybe(F.Apply(item.Wrap().Apply, Mods.Face));
        void OnBodyChange(HumanBody item) =>
            (Current.GetValueOrDefault(item.human.data) == this)
                .Maybe(F.Apply(item.Wrap().Apply, Mods.Body));
        void OnClothesChange(HumanCloth item, int index) =>
            (Current.GetValueOrDefault(item.human.data) == this && Mods.Clothes.ContainsKey(index))
                .Maybe(F.Apply(item.clothess[index].WrapCtc().Apply, Mods.Clothes.GetValueOrDefault(index, new())));
    }
    static class Hooks
    {
        internal static event Action<HumanFace> OnFaceChange = delegate { };
        internal static event Action<HumanBody> OnBodyChange = delegate { };
        internal static event Action<HumanHair, int> OnHairChange = delegate { };
        internal static event Action<HumanCloth, int> OnClothesChange = delegate { };
        internal static event Action<HumanAccessory, int> OnAccessoryChange = delegate { };
        internal static event Action<HumanFace> OnFaceReady = delegate { };
        internal static event Action<HumanBody> OnBodyReady = delegate { };
        internal static event Action<HumanCloth, int> OnClothesReady = delegate { };
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
    }
    [BepInDependency(Fishbone.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        public const string Name = "SardineHead";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "1.1.10";
        internal static Plugin Instance;
        private Harmony Patch;
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}