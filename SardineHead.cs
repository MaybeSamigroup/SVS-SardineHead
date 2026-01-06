using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Security.Cryptography;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Character;
using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CoastalSmell;
using Mods = System.Collections.Generic.Dictionary<string, SardineHead.Modifications>;
using MaterialWrappers = System.Collections.Generic.Dictionary<string, SardineHead.MaterialWrapper>;

namespace SardineHead
{
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
            PopulateProperties(shader, index, shader.GetPropertyType(index),
                shader.GetPropertyName(index), shader.GetPropertyNameId(index));
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
            (Renderer != null).Maybe(F.Apply(SetShader, mods.Shader));
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
    internal static class ModificationExtension
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
#if Aicomi
            new() { ["/ct_face"] = new MaterialWrapper(face._customTexCtrlFace) };
#else
            new() { ["/ct_face"] = new MaterialWrapper(face.customTexCtrlFace) };
#endif
        internal static MaterialWrappers WrapCtc(this HumanBody body) =>
#if Aicomi
            new() { ["/ct_body"] = new MaterialWrapper(body._customTexCtrlBody) };
#else
            new() { ["/ct_body"] = new MaterialWrapper(body.customTexCtrlBody) };
#endif
        internal static MaterialWrappers WrapCtc(this HumanCloth.Clothes clothes) =>
            Enumerable.Range(0, clothes?.ctCreateClothes?.Count ?? 0)
                .Where(idx => clothes?.cusClothesCmp != null && clothes?.ctCreateClothes[idx]?._matCreate != null)
                .ToDictionary(idx => $"/{clothes.cusClothesCmp.name}{idx}",
                    idx => new MaterialWrapper(clothes.ctCreateClothes[idx], clothes.cusClothesCmp.Rebuild01));
        internal static MaterialWrappers Wrap(this HumanFace item) =>
            WrapRenderers(RenderersOfGo(item?.objHead)) ?? new();
        internal static MaterialWrappers Wrap(this HumanBody item) =>
#if Aicomi
            WrapRenderers(RenderersOfGo(item?._objBody)) ?? new();
#else
            WrapRenderers(RenderersOfGo(item?.objBody)) ?? new();
#endif
        static MaterialWrappers Wrap(HumanHair.Hair item) =>
            WrapRenderers(RenderersOfGo(item?.cusHairCmp?.gameObject)) ?? new();
        static MaterialWrappers Wrap(HumanCloth.Clothes item) =>
            WrapRenderers(RenderersOfGo(item?.cusClothesCmp?.gameObject)) ?? new();
        static MaterialWrappers Wrap(HumanAccessory.Accessory item) =>
            WrapRenderers(RenderersOfGo(item?.cusAcsCmp?.gameObject)) ?? new();
        internal static MaterialWrappers Wrap(this HumanHair item, int index) =>
            index < item.Hairs.Count ? Wrap(item.Hairs[index]) : new();
        internal static MaterialWrappers Wrap(this HumanCloth item, int index) =>
            index < item.Clothess.Count && index switch
            {
                1 => !item.notBot,
                2 => !item.notBra,
                3 => !item.notShorts,
                _ => true
            } ? Wrap(item.Clothess[index]) : new();
        internal static MaterialWrappers Wrap(this HumanAccessory item, int index) =>
            index < item.Accessories.Count ? Wrap(item.Accessories[index]) : new();
        static void Apply(MaterialWrappers wrappers, Mods mods) =>
            wrappers.Do(entry => entry.Value.Apply(mods.TryGetValue(entry.Key, out var value) ? value : new()));
        static void Apply(MaterialWrappers renderers, MaterialWrappers ctc, Mods mods) =>
            (F.Apply(Apply, ctc, mods) + F.Apply(Apply, renderers, mods) + F.Apply(Apply, ctc, mods)).Invoke();
        internal static void Apply(this Human item, CoordMods mods) => (
            F.Apply(Apply, item.acs, mods) +
            F.Apply(Apply, item.hair, mods) +
            F.Apply(Apply, item.cloth, mods)
        ).Invoke();
        internal static void Apply(this CoordMods mods, HumanFace item) =>
            Apply(item.Wrap(), item.WrapCtc(), mods.Face);
        internal static void Apply(this CoordMods mods, HumanBody item) =>
            Apply(item.Wrap(), item.WrapCtc(), mods.Body);
        internal static void Apply(this HumanCloth.Clothes item, int index, CoordMods mods) =>
            mods.Clothes.TryGetValue(index, out var value)
                .Maybe(F.Apply(Apply, Wrap(item), item.WrapCtc(), value));
        static void Apply(HumanHair item, CoordMods mods) =>
            item.Hairs.ForEachIndex(mods.Apply);
        static void Apply(HumanCloth item, CoordMods mods) =>
            item.Clothess.ForEachIndex(mods.Apply);
        static void Apply(HumanAccessory item, CoordMods mods) =>
            item.Accessories.ForEachIndex(mods.Apply);
        static void Apply(this CoordMods mods, HumanHair.Hair item, int index) =>
            mods.Hairs.TryGetValue(index, out var value).Maybe(F.Apply(Apply, Wrap(item), value));
        static void Apply(this CoordMods mods, HumanCloth.Clothes item, int index) =>
            mods.Clothes.TryGetValue(index, out var value).Maybe(F.Apply(Apply, Wrap(item), value));
        static void Apply(this CoordMods mods, HumanAccessory.Accessory item, int index) =>
            mods.Accessories.TryGetValue(index, out var value).Maybe(F.Apply(Apply, Wrap(item), value));
    }
    internal static class Textures
    {
        static readonly string TexturePath = Path.Combine(Plugin.Name, "textures");
        static Dictionary<string, byte[]> Buffers = new();
        static Action<byte[], string> StoreBuffer =
            (data, hash) => Buffers.TryAdd(hash, data);
        static Func<SHA256, byte[], string> ComputeHashAndStore =
            (sha256, data) => Convert.ToHexString(sha256.ComputeHash(data)).With(StoreBuffer.Apply(data));
        static Action<byte[], RenderTexture> StoreTexture =
            (data, texture) => texture.name = ComputeHashAndStore.ApplyDisposable(SHA256.Create())(data);
        static Action<Texture2D, RenderTexture> GraphicsBlit = Graphics.Blit;
        static Func<byte[], RenderTexture> BytesToTexture =
            data => TryBytesToTexture2D(data, out var t2d)
                ? ToRenderTexture(t2d).With(StoreTexture.Apply(data)) : default;
        static bool TryBytesToTexture2D(byte[] data, out Texture2D t2d) =>
            (t2d = new Texture2D(256, 256)).LoadImage(data);
        static Func<Texture2D, RenderTexture> ToRenderTexture =
            t2d => new RenderTexture(t2d.width, t2d.height, 0).With(GraphicsBlit.Apply(t2d));
        static Func<Texture, Texture2D> TextureToTexture2d =
            tex => new Texture2D(tex.width, tex.height)
                .With(ToTexture2d
                    .Apply(RenderTexture.active)
                    .Apply(RenderTexture.GetTemporary(tex.width, tex.height))
                    .Apply(tex));
        static Action<RenderTexture, RenderTexture, Texture, Texture2D> ToTexture2d =
            (org, tmp, tex, t2d) => t2d
                .With(SwitchActive.Apply(tmp))
                .With(F.Apply(GL.Clear, false, true, new Color()))
                .With(F.Apply(Graphics.Blit, tex, tmp))
                .With(F.Apply(t2d.ReadPixels, new Rect(0, 0, t2d.width, t2d.height), 0, 0))
                .With(SwitchActive.Apply(org))
                .With(F.Apply(RenderTexture.ReleaseTemporary, tmp));
        static Action<RenderTexture> SwitchActive =
            tex => RenderTexture.active = tex;
        static Func<ZipArchiveEntry, bool> IsTextureEntry =
            entry => entry.FullName.StartsWith(TexturePath);
        static Func<ZipArchiveEntry, bool> IsNotBuffered =
            entry => !IsExtension(entry.Name);
        static Action<ZipArchiveEntry> LoadTextures =
            entry => LoadBuffer.Apply(entry.Name).Apply(entry.Length)
                .ApplyDisposable(new BinaryReader(entry.Open())).Try(Plugin.Instance.Log.LogError);
        static Action<string, long, BinaryReader> LoadBuffer =
            (hash, size, reader) => Buffers[hash] = reader.ReadBytes((int)size);
        static Action<ZipArchive, string> SaveTextures =
           (archive, hash) => SaveTextureToArchive.Apply(Buffers[hash])
               .ApplyDisposable(new BinaryWriter(archive.CreateEntry(Path.Combine(TexturePath, hash)).Open()))
               .Try(Plugin.Instance.Log.LogError);
        static Action<byte[], BinaryWriter> SaveTextureToArchive =
            (data, writer) => writer.Write(data);
        internal static Action<ZipArchive> Load =
            archive => archive.Entries.Where(IsTextureEntry).Where(IsNotBuffered).ForEach(LoadTextures);
        internal static Action<TextureMods, ZipArchive> Save =
            (mods, archive) => mods.ToTextures().Distinct().ForEach(SaveTextures.Apply(archive));
        internal static Func<string, bool> IsExtension =
            hash => hash != null && Buffers.ContainsKey(hash);
        internal static Func<string, RenderTexture> FromHash =
            hash => IsExtension(hash) ? BytesToTexture(Buffers[hash]) : default;
        internal static Func<string, RenderTexture> FromFile =
            path => BytesToTexture(File.ReadAllBytes(path));
        internal static Action<Texture, string> ToFile =
            (tex, path) => File.WriteAllBytes(path, TextureToTexture2d(tex).EncodeToPNG());
    }

    [BepInProcess(Process)]
    [BepInDependency(Fishbone.Plugin.Guid)]
    [BepInPlugin(Guid, Name, Version)]
    public partial class Plugin : BasePlugin
    {
        public const string Name = "SardineHead";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "2.2.0";
        internal static Plugin Instance;
        CompositeDisposable Subscriptions;
        IDisposable Initialize() =>
            Disposable.Create(Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks").UnpatchSelf);
        public override void Load() =>
            (Instance, Subscriptions) = (this, [Initialize(), .. Hooks.Initialize(this)]);
        public override bool Unload() =>
            true.With(Subscriptions.Dispose) && base.Unload();
    }
}