using HarmonyLib;
using BepInEx.Unity.IL2CPP;
using System;
using System.Linq;
using System.IO.Compression;
using System.Collections.Generic;
using UniRx;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Character;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Mods = System.Collections.Generic.Dictionary<string, SardineHead.Modifications>;
using CoastalSmell;
using Fishbone;
using UniRx.Triggers;

namespace SardineHead
{
    public struct Quad : IEquatable<Quad>
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public float w { get; set; }
        public Quad(float v1, float v2, float v3, float v4)
        {
            x = v1;
            y = v2;
            z = v3;
            w = v4;
        }
        public static implicit operator Quad(Vector4 vs) => new(vs.x, vs.y, vs.z, vs.w);
        public static implicit operator Vector4(Quad vs) => new(vs.x, vs.y, vs.z, vs.w);
        public static implicit operator Quad(Color vs) => new(vs.r, vs.g, vs.b, vs.a);
        public static implicit operator Color(Quad vs) => new(vs.x, vs.y, vs.z, vs.w);
        public bool Equals(Quad that) => (x, y, z, w) == (that.x, that.y, that.z, that.w);
    }
    public enum BoolValue
    {
        Unmanaged,
        Enabled,
        Disabled
    }
    public class Modifications
    {
        public BoolValue Rendering { get; set; } = BoolValue.Unmanaged;
        public Dictionary<string, int> IntValues { get; init; } = new();
        public Dictionary<string, float> FloatValues { get; init; } = new();
        public Dictionary<string, float> RangeValues { get; init; } = new();
        public Dictionary<string, Quad> ColorValues { get; init; } = new();
        public Dictionary<string, Quad> VectorValues { get; init; } = new();
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
            F.Apply(Apply, item.body, mods) +
            F.Apply(Apply, item.face, mods) +
            F.Apply(Apply, item.hair, mods) +
            F.Apply(Apply, item.cloth, mods) +
            F.Apply(Apply, item.acs, mods) +
            (() => Plugin.Instance.Log.LogInfo("Delayed application"));
        static void Apply(this HumanBody item, CoordMods mods) =>
            item.Wrap()?.Apply(mods.Body);
        static void Apply(this HumanFace item, CoordMods mods) =>
            item.Wrap()?.Apply(mods.Face);
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
        internal Renderer Renderer;
        internal Material Material;
        internal Shader Shader;
        Action<int, int> CmpSetInt;
        Action<int, float> CmpSetFloat;
        Action<int, Color> CmpSetClor;
        Action<int, Vector4> CmpSetVector;
        Action<int, Texture> CmpSetTexture;
        Dictionary<ShaderPropertyType, Dictionary<string, int>> Ids =
             Enum.GetValues<ShaderPropertyType>().ToDictionary(value => value, value => new Dictionary<string, int>());
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
            Ids[ShaderPropertyType.Int].TryGetValue(name, out var id).Maybe(CmpSetInt.Apply(id).Apply(value));
        internal Action<string, float> SetFloat => (name, value) =>
            Ids[ShaderPropertyType.Float].TryGetValue(name, out var id).Maybe(CmpSetFloat.Apply(id).Apply(value));
        internal Action<string, float> SetRange => (name, value) =>
            Ids[ShaderPropertyType.Range].TryGetValue(name, out var id).Maybe(CmpSetFloat.Apply(id).Apply(value));
        internal Action<string, Color> SetColor => (name, value) =>
            Ids[ShaderPropertyType.Color].TryGetValue(name, out var id).Maybe(CmpSetClor.Apply(id).Apply(value));
        internal Action<string, Vector4> SetVector => (name, value) =>
            Ids[ShaderPropertyType.Vector].TryGetValue(name, out var id).Maybe(CmpSetVector.Apply(id).Apply(value));
        internal Action<string, Texture> SetTexture => (name, value) =>
            Ids[ShaderPropertyType.Texture].TryGetValue(name, out var id).Maybe(CmpSetTexture.Apply(id).Apply(value));
        internal Dictionary<string, ShaderPropertyType> Properties { get; init; } = new();
        internal Dictionary<string, Vector2> RangeLimits { get; init; } = new();
        void PopulateProperties(Shader shader) =>
            Enumerable.Range(0, shader.GetPropertyCount()).Do(index => PopulateProperties(shader, index));
        void PopulateProperties(Shader shader, int index) =>
            PopulateProperties(shader, index, shader.GetPropertyType(index), shader.GetPropertyName(index), shader.GetPropertyNameId(index));
        void PopulateProperties(Shader shader, int index, ShaderPropertyType type, string name, int id) =>
            (type is ShaderPropertyType.Range)
                .With(() => Properties.TryAdd(name, type))
                .With(() => Ids[type].TryAdd(name, id))
                .Maybe(() => RangeLimits.TryAdd(name, shader.GetPropertyRangeLimits(index)));
        MaterialWrapper(Shader shader) => PopulateProperties(Shader = shader);
        MaterialWrapper(Material value) : this(value.shader) =>
            (Material, CmpSetInt, CmpSetFloat, CmpSetClor, CmpSetVector, CmpSetTexture) =
                (value, value.SetInt, value.SetFloat, value.SetColor, value.SetVector, value.SetTexture);
        internal MaterialWrapper(Renderer renderer) : this(renderer.material) => Renderer = renderer;
        internal MaterialWrapper(CustomTextureControl ctc) : this(ctc._matCreate)
        {
            CmpSetInt += (_, _) => ctc.SetNewCreateTexture();
            CmpSetFloat += (_, _) => ctc.SetNewCreateTexture();
            CmpSetClor += (_, _) => ctc.SetNewCreateTexture();
            CmpSetVector += (_, _) => ctc.SetNewCreateTexture();
            CmpSetTexture += (_, _) => ctc.SetNewCreateTexture();
        }
        internal MaterialWrapper(CustomTextureCreate ctc, Func<CustomTextureCreate, int, bool> rebuild) : this(ctc._matCreate)
        {
            CmpSetInt += (id, _) => rebuild(ctc, id);
            CmpSetFloat += (id, _) => rebuild(ctc, id);
            CmpSetClor += (id, _) => rebuild(ctc, id);
            CmpSetVector += (id, _) => rebuild(ctc, id);
            CmpSetTexture += (id, _) => rebuild(ctc, id);
        }
        internal Action<Modifications> Apply => mods =>
            mods.With(ApplyInt)
                .With(ApplyFloat)
                .With(ApplyRange)
                .With(ApplyColor)
                .With(ApplyVector)
                .With(ApplyTexture)
                .With(ApplyRenderer);
        void ApplyInt(Modifications mods) =>
            mods.IntValues.ForEach(entry => SetInt(entry.Key, entry.Value));
        void ApplyFloat(Modifications mods) =>
            mods.FloatValues.ForEach(entry => SetFloat(entry.Key, entry.Value));
        void ApplyRange(Modifications mods) =>
            mods.RangeValues.ForEach(entry => SetRange(entry.Key, entry.Value));
        void ApplyColor(Modifications mods) =>
            mods.ColorValues.ForEach(entry => SetColor(entry.Key, entry.Value));
        void ApplyVector(Modifications mods) =>
            mods.VectorValues.ForEach(entry => SetVector(entry.Key, entry.Value));
        void ApplyTexture(Modifications mods) =>
            mods.TextureHashes.ForEach(entry => SetTexture(entry.Key, Textures.FromHash(entry.Value)));
        Action<Modifications> ApplyRenderer => mods =>
            (Renderer != null).Maybe(() => Renderer.enabled = mods.Rendering switch
            {
                BoolValue.Disabled => false,
                BoolValue.Enabled => true,
                _ => Renderer.enabled,
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
                .GroupBy(renderer => renderer.name ?? renderer.gameObject.name).SelectMany(IdentifyGo.Apply(1))
                .ToDictionary(entry => entry.Item1, entry => new MaterialWrapper(entry.Item2));
        static Func<int, IGrouping<string, Renderer>, IEnumerable<Tuple<string, Renderer>>> IdentifyGo =
            (depth, groups) => groups.Count() == 1
                ? groups.Select<Renderer, Tuple<string, Renderer>>(value => new(groups.Key, value))
                : groups.GroupBy(Identify.Apply(depth).Apply(groups.Key)).SelectMany(IdentifyGo.Apply(depth + 1));
        static Func<int, string, Renderer, string> Identify =
            (depth, path, renderer) => $"IdentifyTf(depth, renderer.transform)/{path}";
        static Func<int, Transform, string> IdentifyTf =
            (depth, tf) => depth == 0 ? tf.name : IdentifyTf(depth - 1, tf.parent);
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
        ModApplicator(HumanData data, CoordMods mods)
        {
            Mods = mods;
            Hooks.OnFaceReady += OnFaceReady;
            Hooks.OnBodyReady += OnBodyReady;
            Hooks.OnClothesReady += OnClothesReady;
            Hooks.OnFaceChange += OnFaceChange;
            Hooks.OnBodyChange += OnBodyChange;
            Hooks.OnHairChange += OnHairChange;
            Hooks.OnClothesChange += OnClothesChange;
            Hooks.OnAccessoryChange += OnAccessoryChange;
            Current.TryAdd(data, this);
        }
        Action<Human> Cleanup => human =>
        {
            Hooks.OnFaceReady -= OnFaceReady;
            Hooks.OnBodyReady -= OnBodyReady;
            Hooks.OnClothesReady -= OnClothesReady;
            Hooks.OnFaceChange -= OnFaceChange;
            Hooks.OnBodyChange -= OnBodyChange;
            Hooks.OnHairChange -= OnHairChange;
            Hooks.OnClothesChange -= OnClothesChange;
            Hooks.OnAccessoryChange -= OnAccessoryChange;
            Current.Remove(human.data);
            Scheduler.MainThread.Schedule(Il2CppSystem.TimeSpan.FromSeconds(0.05), human.Apply(Mods));
        };
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
        void OnHairChange(HumanHair item, int index) =>
            (Current.GetValueOrDefault(item.human.data) == this && Mods.Hairs.ContainsKey(index))
                .Maybe(F.Apply(item.hairs[index].Wrap().Apply, Mods.Hairs.GetValueOrDefault(index, new())));
        void OnClothesChange(HumanCloth item, int index) =>
            (Current.GetValueOrDefault(item.human.data) == this && Mods.Clothes.ContainsKey(index))
                .Maybe(F.Apply(item.clothess[index].WrapCtc().Apply, Mods.Clothes.GetValueOrDefault(index, new())));
        void OnAccessoryChange(HumanAccessory item, int index) =>
            (Current.GetValueOrDefault(item.human.data) == this && Mods.Accessories.ContainsKey(index))
                .Maybe(F.Apply(item.accessories[index].Wrap().Apply, Mods.Accessories.GetValueOrDefault(index, new())));
    }
    static class Hooks
    {
        internal static event Action<HumanFace> OnFaceChange =
            item => Plugin.Instance.Log.LogDebug($"OnFaceChange:{item.human}");
        internal static event Action<HumanBody> OnBodyChange =
            item => Plugin.Instance.Log.LogDebug($"OnBodyChange:{item.human}");
        internal static event Action<HumanHair, int> OnHairChange =
            (item, idx) => Plugin.Instance.Log.LogDebug($"OnHairChange:{item.human},{idx}");
        internal static event Action<HumanCloth, int> OnClothesChange =
            (item, idx) => Plugin.Instance.Log.LogDebug($"OnClothesChange:{item.human},{idx}");
        internal static event Action<HumanAccessory, int> OnAccessoryChange =
            (item, idx) => Plugin.Instance.Log.LogDebug($"OnAccessoryChange:{item.human},{idx}");
        internal static event Action<HumanFace> OnFaceReady =
            item => Plugin.Instance.Log.LogDebug($"OnFaceReady:{item.human}");
        internal static event Action<HumanBody> OnBodyReady =
            item => Plugin.Instance.Log.LogDebug($"OnBodyReady:{item.human}");
        internal static event Action<HumanCloth, int> OnClothesReady =
            (item, idx) => Plugin.Instance.Log.LogDebug($"OnClothesReady:{item.human},{idx}");
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.ChangeHead), typeof(int), typeof(bool))]
        static void ChangeHeadPostfix(HumanFace __instance) =>
            OnFaceChange(__instance);
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.InitBaseCustomTextureBody))]
        static void InitBaseCustomTextureBodyPostfix(HumanBody __instance) =>
            OnBodyChange(__instance);
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
    public partial class Plugin : BasePlugin
    {
        internal static Plugin Instance;
        public const string Name = "SardineHead";
        public const string Version = "1.1.8";
    }
}