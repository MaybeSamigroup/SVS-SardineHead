using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UniRx;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Character;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Mods = System.Collections.Generic.Dictionary<string, SardineHead.Modifications>;
using MaterialWrappers = System.Collections.Generic.Dictionary<string, SardineHead.MaterialWrapper>;
using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Fishbone;
using CoastalSmell;

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
    [Extension<CharaMods, CoordMods>(Plugin.Name, "modifications.json")]
    public partial class CharaMods : CharacterExtension<CharaMods>, ComplexExtension<CharaMods, CoordMods>
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
    }
    public partial class CoordMods : CoordinateExtension<CoordMods>
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
    }
    internal static class ModificationExtensions
    {
        internal static Dictionary<int, T> Merge<T>(this Dictionary<int, T> mods, int index, T mod) =>
            mods.Where(entry => entry.Key != index)
                .Select(entry => new Tuple<int, T>(entry.Key, entry.Value))
                .Append(new Tuple<int, T>(index, mod)).ToDictionary();
        static void Apply(MaterialWrappers wrappers, Mods mods) =>
            wrappers.Do(entry => entry.Value.Apply(mods.TryGetValue(entry.Key, out var value) ? value : new()));
        static void Apply(MaterialWrappers ctc, MaterialWrappers renderers, Mods mods) =>
            (F.Apply(Apply, ctc, mods) + F.Apply(Apply, renderers, mods) + F.Apply(Apply, ctc, mods)).Invoke();
        internal static void Apply(this Human item, CoordMods mods) => (
            F.Apply(Apply, item.hair, mods) +
            F.Apply(Apply, item.cloth, mods) +
            F.Apply(Apply, item.acs, mods)
        ).Invoke();
        internal static void Apply(this HumanFace item, CoordMods mods) =>
            Apply(item.WrapCtc(), item.Wrap(), mods.Face);
        internal static void Apply(this HumanBody item, CoordMods mods) =>
            Apply(item.WrapCtc(), item.Wrap(), mods.Body);
        internal static void Apply(this HumanCloth.Clothes item, int index, CoordMods mods) =>
            mods.Clothes.TryGetValue(index, out var value)
                .Maybe(F.Apply(Apply, item.WrapCtc(), item.Wrap(), value));
        static void Apply(this HumanHair item, CoordMods mods) =>
            item.hairs.ForEachIndex(mods.Apply);
        static void Apply(this HumanCloth item, CoordMods mods) =>
            item.clothess.ForEachIndex(mods.Apply);
        static void Apply(this HumanAccessory item, CoordMods mods) =>
            item.accessories.ForEachIndex(mods.Apply);
        static void Apply(this CoordMods mods, HumanHair.Hair item, int index) =>
            mods.Hairs.TryGetValue(index, out var value).Maybe(F.Apply(Apply, item.Wrap(), value));
        static void Apply(this CoordMods mods, HumanCloth.Clothes item, int index) =>
            mods.Clothes.TryGetValue(index, out var value).Maybe(F.Apply(Apply, item.Wrap(), value));
        static void Apply(this CoordMods mods, HumanAccessory.Accessory item, int index) =>
            mods.Accessories.TryGetValue(index, out var value).Maybe(F.Apply(Apply, item.Wrap(), value));
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
        Action<Modifications> ApplyInt => mods =>
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
        static Func<IEnumerable<Renderer>, MaterialWrappers> WrapRenderers =
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
        internal static MaterialWrappers WrapCtc(this HumanFace face) =>
            new() { ["/ct_face"] = new MaterialWrapper(face.customTexCtrlFace) };
        internal static MaterialWrappers WrapCtc(this HumanBody body) =>
            new() { ["/ct_body"] = new MaterialWrapper(body.customTexCtrlBody) };
        internal static MaterialWrappers WrapCtc(this HumanCloth.Clothes clothes) =>
            Enumerable.Range(0, clothes?.ctCreateClothes?.Count ?? 0)
                .Where(idx => clothes?.cusClothesCmp != null && clothes?.ctCreateClothes[idx]?._matCreate != null)
                .ToDictionary(idx => $"/{clothes.cusClothesCmp.name}{idx}",
                    idx => new MaterialWrapper(clothes.ctCreateClothes[idx], clothes.cusClothesCmp.Rebuild01));
        internal static MaterialWrappers Wrap(this HumanFace item) =>
            WrapRenderers(RenderersOfGo(item?.objHead)) ?? new();
        internal static MaterialWrappers Wrap(this HumanBody item) =>
            WrapRenderers(RenderersOfGo(item?.objBody)) ?? new();
        internal static MaterialWrappers Wrap(this HumanHair.Hair item) =>
            WrapRenderers(RenderersOfGo(item?.cusHairCmp?.gameObject)) ?? new();
        internal static MaterialWrappers Wrap(this HumanCloth.Clothes item) =>
            WrapRenderers(RenderersOfGo(item?.cusClothesCmp?.gameObject)) ?? new();
        internal static MaterialWrappers Wrap(this HumanAccessory.Accessory item) =>
            WrapRenderers(RenderersOfGo(item?.cusAcsCmp?.gameObject)) ?? new();
        internal static MaterialWrappers Wrap(this HumanHair item, int index) =>
            index < item.hairs.Count ? item.hairs[index].Wrap() : new();
        internal static MaterialWrappers Wrap(this HumanCloth item, int index) =>
            index < item.clothess.Count && index switch
            {
                1 => !item.notBot,
                2 => !item.notBra,
                3 => !item.notShorts,
                _ => true
            } ? item.clothess[index].Wrap() : new();
        internal static MaterialWrappers Wrap(this HumanAccessory item, int index) =>
            index < item.accessories.Count ? item.accessories[index].Wrap() : new();
    }
    internal static partial class Textures
    {
        internal static Func<string, bool> IsExtension =
            hash => hash != null && Buffers.ContainsKey(hash);
        internal static Func<string, RenderTexture> FromHash =
            hash => IsExtension(hash) ? BytesToTexture(Buffers[hash]) : default;
        internal static Func<string, RenderTexture> FromFile =
            path => BytesToTexture(File.ReadAllBytes(path));
        internal static Action<Texture, string> ToFile =
            (tex, path) => File.WriteAllBytes(path, TextureToTexture2d(tex).EncodeToPNG());
    }
    [BepInDependency(Fishbone.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        public const string Name = "SardineHead";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "2.0.0";
        internal static Plugin Instance;
        private Harmony Patch;
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}