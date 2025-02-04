using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Security.Cryptography;
using UniRx;
using UniRx.Triggers;
using Cysharp.Threading.Tasks;
using Il2CppSystem.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using Manager;
using Character;
using CharacterCreation;
using CharacterCreation.UI;
using ILLGames.Component;
using SaveFlgs = Character.HumanData.SaveFileInfo.Flags;
using LoadFlgs = Character.HumanData.LoadFileInfo.Flags;
using Mods = System.Collections.Generic.Dictionary<string, SardineHead.Modifications>;

namespace SardineHead
{
    internal delegate void Either(Action a, Action b);
    internal static class FunctionalExtension
    {
        internal static Either Either(bool value) => value ? (left, right) => right() : (left, right) => left();
        internal static void Either(this bool value, Action left, Action right) => Either(value)(left, right);
        internal static void Maybe(this bool value, Action maybe) => value.Either(() => { }, maybe);
        internal static Action Apply<T>(this T input, Action<T> action) => () => action(input);
        internal static T With<T>(this T input, Action sideEffect)
        {
            sideEffect();
            return input;
        }
        internal static T With<T>(this T input, Action<T> sideEffect)
        {
            sideEffect(input);
            return input;
        }
    }
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
    public class Modifications
    {
       public Dictionary<string, int> IntValues { get; init; } = new();
        public Dictionary<string, float> FloatValues { get; init; } = new();
        public Dictionary<string, float> RangeValues { get; init; } = new();
        public Dictionary<string, Quad> VectorValues { get; init; } = new();
        public Dictionary<string, Quad> ColorValues { get; init; } = new();
        public Dictionary<string, string> TextureHashes { get; init; } = new();
    }
    internal static partial class ModificationExtension {
        internal static Modifications Difference(this Modifications mods, Modifications refs) => new() {
            IntValues = mods.IntValues.Difference(refs.IntValues),
            FloatValues = mods.FloatValues.Difference(refs.FloatValues),
            RangeValues = mods.RangeValues.Difference(refs.RangeValues),
            VectorValues = mods.VectorValues.Difference(refs.VectorValues),
            ColorValues = mods.ColorValues.Difference(refs.ColorValues),
            TextureHashes = mods.TextureHashes.Difference(refs.TextureHashes)
        };
        internal static Dictionary<string, T> Difference<T>(
            this Dictionary<string, T> mods, Dictionary<string, T> refs) where T: IEquatable<T> =>
                mods.Where(entry => !refs.ContainsKey(entry.Key) || !entry.Value.Equals(refs[entry.Key]))
                    .ToDictionary(entry => entry.Key, entry => entry.Value);
        // transform current material shader properties into (material owner name) => (modifications)
        internal static Mods ToMods(this IEnumerable<MaterialHandle> handles) => 
            handles.ToDictionary(handle => handle.Label, handle => new Modifications().With(handle.Store));
        // apply stored (material owner name => modifications) to current materials
        internal static void Apply(this Mods mods, IEnumerable<MaterialHandle> handles) =>
            mods.Do(entry => handles.Where(handle => handle.Label.Equals(entry.Key)).FirstOrDefault()?.Apply(entry.Value));
    }
    internal class MaterialHandle
    {
        internal string Label { get; init; }
        internal Material Value { get; init; }
        internal Dictionary<string, Action<GameObject>> Handles { get; init; } = new();
        internal Action<Modifications> Apply;
        internal Action<Modifications> Store;
        internal MaterialHandle(Material material, string label) : this(material) => Label = label;
        private MaterialHandle(Material material) => Value = material.With(() => PopulateHandles(material, material.shader));
        private void PopulateHandles(Material material, Shader shader) =>
            Enumerable.Range(0, material.shader.GetPropertyCount())
                .Do(idx => PopulateHandles(material,
                    shader.GetPropertyName(idx),
                    shader.GetPropertyNameId(idx),
                    shader.GetPropertyType(idx),
                    () => shader.GetPropertyRangeLimits(idx)));
        private bool PopulateHandles(Material material, string name, int id, ShaderPropertyType shaderType, Func<Vector2> limits) =>
            shaderType switch
            {
                ShaderPropertyType.Int =>
                    Handles.TryAdd(name, go => go.NumericEdit("int:", () => material.GetInt(id).ToString(), input => Set(id, int.Parse(input)), TMP_InputField.ContentType.IntegerNumber))
                        .With(() => Store += mods => mods.IntValues[name] = material.GetInt(id))
                        .With(() => Apply += mods => mods.IntValues.ContainsKey(name).Maybe(() => Set(id, mods.IntValues[name]))),
                ShaderPropertyType.Float =>
                    Handles.TryAdd(name, go => go.NumericEdit("float:", () => material.GetFloat(id).ToString(), input => Set(id, float.Parse(input)), TMP_InputField.ContentType.DecimalNumber))
                        .With(() => Store += mods => mods.FloatValues[name] = material.GetInt(id))
                        .With(() => Apply += mods => mods.FloatValues.ContainsKey(name).Maybe(() => Set(id, mods.FloatValues[name]))),
                ShaderPropertyType.Range =>
                    Handles.TryAdd(name, go => go.RangeEdit(() => material.GetFloat(id), input => Set(id, input), limits()))
                        .With(() => Store += mods => mods.RangeValues[name] = material.GetFloat(id))
                        .With(() => Apply += mods => mods.RangeValues.ContainsKey(name).Maybe(() => Set(id, mods.RangeValues[name]))),
                ShaderPropertyType.Color =>
                    Handles.TryAdd(name, go => go.ColorEdit(() => material.GetColor(id), (input) => Set(id, input)))
                        .With(() => Store += mods => mods.ColorValues[name] = material.GetColor(id))
                        .With(() => Apply += mods => mods.ColorValues.ContainsKey(name).Maybe(() => Set(id, (Color)mods.ColorValues[name]))),
                ShaderPropertyType.Vector =>
                    Handles.TryAdd(name, go => go.VectorEdit(() => material.GetVector(id), (input) => Set(id, input)))
                        .With(() => Store += mods => mods.VectorValues[name] = material.GetVector(id))
                        .With(() => Apply += mods => mods.VectorValues.ContainsKey(name).Maybe(() => Set(id, (Vector4)mods.VectorValues[name]))),
                ShaderPropertyType.Texture =>
                    Handles.TryAdd(name, go => go.TextureEdit(() => material.GetTexture(id), (input) => Set(id, input)))
                        .With(() => Store += mods => material.GetTexture(id)?.Store()?.With(hash => mods.TextureHashes[name] = hash))
                        .With(() => Apply += mods => mods.TextureHashes.ContainsKey(name).Maybe(() => Set(id, mods.TextureHashes[name].Apply()))),
                _ => false
            };
        internal virtual bool Set(int id, float value) =>
            true.With(_ => Value.SetFloat(id, value));
        internal virtual bool Set(int id, int value) =>
            true.With(_ => Value.SetInt(id, value));
        internal virtual bool Set(int id, Color value) =>
            true.With(_ => Value.SetColor(id, value));
        internal virtual bool Set(int id, Vector4 value) =>
             true.With(_ => Value.SetVector(id, value));
        internal virtual bool Set(int id, Texture value) =>
             true.With(_ => Value.SetTexture(id, value));
        internal static IEnumerable<MaterialHandle> Of(Renderer renderer) =>
            renderer != null ? [new MaterialHandle(renderer.material, renderer.name ?? renderer.gameObject.name)] : [];
        internal static IEnumerable<MaterialHandle> Of(GameObject go) =>
            go?.GetComponentsInChildren<Renderer>()?.SelectMany(Of) ?? [];
        internal static IEnumerable<MaterialHandle> Of(ChaClothesComponent cmpClothes) =>
            Of(cmpClothes?.rendAccessory)
                .Concat(cmpClothes?.rendNormal01?.SelectMany(Of) ?? [])
                .Concat(cmpClothes?.rendNormal02?.SelectMany(Of) ?? [])
                .Concat(cmpClothes?.rendNormal03?.SelectMany(Of) ?? [])
                .Concat(Of(cmpClothes?.rendEmblem01))
                .Concat(Of(cmpClothes?.rendEmblem02))
                .Concat(cmpClothes?.exRendEmblem01?.SelectMany(Of) ?? [])
                .Concat(cmpClothes?.exRendEmblem02?.SelectMany(Of) ?? []);
    }
    internal class ControlMaterial : MaterialHandle
    {
        internal CustomTextureControl Ctc { get; init; }
        internal override bool Set(int id, int value) => base.Set(id, value) && Ctc.SetNewCreateTexture();
        internal override bool Set(int id, float value) => base.Set(id, value) && Ctc.SetNewCreateTexture();
        internal override bool Set(int id, Color value) => base.Set(id, value) && Ctc.SetNewCreateTexture();
        internal override bool Set(int id, Vector4 value) => base.Set(id, value) && Ctc.SetNewCreateTexture();
        internal override bool Set(int id, Texture value) => base.Set(id, value) && Ctc.SetNewCreateTexture();
        private ControlMaterial(CustomTextureControl ctc, string name) : base(ctc._matCreate, name) => Ctc = ctc;
        internal static IEnumerable<MaterialHandle> Of(HumanBody body) =>
            body?.customTexCtrlBody == null ? [] : [new ControlMaterial(body.customTexCtrlBody, body.objBody.name)];
        internal static IEnumerable<MaterialHandle> Of(HumanFace face) =>
            face?.customTexCtrlFace == null ? [] : [new ControlMaterial(face.customTexCtrlFace, face.objHead.name)];
    }
    internal class CreateMaterial : MaterialHandle
    {
        internal CustomTextureCreate Ctc { get; init; }
        internal Func<CustomTextureCreate, int, bool> Rebuild { get; init; }
        internal override bool Set(int id, int value) => base.Set(id, value) && Rebuild(Ctc, id);
        internal override bool Set(int id, float value) => base.Set(id, value) && Rebuild(Ctc, id);
        internal override bool Set(int id, Color value) => base.Set(id, value) && Rebuild(Ctc, id);
        internal override bool Set(int id, Vector4 value) => base.Set(id, value) && Rebuild(Ctc, id);
        internal override bool Set(int id, Texture value) => base.Set(id, value) && Rebuild(Ctc, id);
        private CreateMaterial(CustomTextureCreate ctc, Func<CustomTextureCreate, int, bool> rebuild, string name) : this(ctc, name) => Rebuild = rebuild;
        private CreateMaterial(CustomTextureCreate ctc, string name) : base(ctc._matCreate, name) => Ctc = ctc;
        private static Func<CustomTextureCreate, int, bool>[] Rebuilds(ChaClothesComponent cmp) =>
            cmp == null ? [] : [cmp.Rebuild01, cmp.Rebuild02, cmp.Rebuild03, cmp.RebuildAccessory, cmp.RebuildAccessory];
        internal static IEnumerable<MaterialHandle> Of(HumanCloth.Clothes clothes) =>
            Rebuilds(clothes.cusClothesCmp)
                .Where((_, idx) => idx < clothes.ctCreateClothes.Count && null != clothes.ctCreateClothes[idx])
                .Select((update, idx) => new CreateMaterial(clothes.ctCreateClothes[idx], update, $"{clothes.cusClothesCmp.name}{idx}"));
    }
    public class CharacterModifications
    {
        private static CharacterModifications ReferenceMale = JsonSerializer.Deserialize<CharacterModifications>
            (File.ReadAllText(Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, "default", "male.json")));
        private static CharacterModifications ReferenceFemale = JsonSerializer.Deserialize<CharacterModifications>
            (File.ReadAllText(Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, "default", "female.json")));
        private static CharacterModifications Reference { get => UIFactory.Current.Target.sex == 0 ? ReferenceMale : ReferenceFemale; }
        public Mods Face { get; set; } = new();
        public Mods Eyebrows { get; set; } = new();
        public Mods Eyelines { get; set; } = new();
        public Mods Eyes { get; set; } = new();
        public Mods Tooth { get; set; } = new();
        public Mods Body { get; set; } = new();
        public Mods Nails { get; set; } = new();
        public Dictionary<int, CoordinateModifications> Coordinates { get; set; } = new() { { 0, new() }, { 1, new() }, { 2, new() } };
        internal void Store(HandlePaths paths) =>
            paths.With(StoreFace).With(StoreEyebrows).With(StoreEyelines)
                .With(StoreEyes).With(StoreTooth).With(StoreBody).With(StoreNails)
                .With(Coordinates[paths.Target.data.Status.coordinateType].Store);
        private void StoreFace(HandlePaths paths) => Face = paths.Face.ToMods().Difference(Reference.Face);
        private void StoreEyebrows(HandlePaths paths) => Eyebrows = paths.Eyebrows.ToMods().Difference(Reference.Eyebrows);
        private void StoreEyelines(HandlePaths paths) => Eyelines = paths.Eyelines.ToMods().Difference(Reference.Eyelines);
        private void StoreEyes(HandlePaths paths) => Eyes = paths.Eyes.ToMods().Difference(Reference.Eyes);
        private void StoreTooth(HandlePaths paths) => Tooth = paths.Tooth.ToMods().Difference(Reference.Tooth);
        private void StoreBody(HandlePaths paths) => Body = paths.Body.ToMods().Difference(Reference.Body);
        private void StoreNails(HandlePaths paths) => Nails = paths.Nails.ToMods().Difference(Reference.Nails);
        internal void Apply(HandlePaths paths) =>
            paths.With(ApplyFace).With(ApplyEyebrows).With(ApplyEyelines)
                .With(ApplyEyes).With(ApplyTooth).With(ApplyBody).With(ApplyNails)
                .With(Coordinates[paths.Target.data.Status.coordinateType].Apply);
        private void ApplyFace(HandlePaths paths) => Face.Apply(paths.Face);
        private void ApplyEyebrows(HandlePaths paths) => Eyebrows.Apply(paths.Eyebrows);
        private void ApplyEyelines(HandlePaths paths) => Eyelines.Apply(paths.Eyelines);
        private void ApplyEyes(HandlePaths paths) => Eyes.Apply(paths.Eyes);
        private void ApplyTooth(HandlePaths paths) => Tooth.Apply(paths.Tooth);
        private void ApplyBody(HandlePaths paths) => Body.Apply(paths.Body);
        private void ApplyNails(HandlePaths paths) => Nails.Apply(paths.Nails);
        internal void Clear() => new List<Mods>() { Face, Eyebrows, Eyelines, Eyes, Tooth, Body, Nails }.Do(item => item.Clear());
    }
    public class CoordinateModifications
    {
        public Dictionary<int, Mods> Hair { get; set; } = new();
        public Dictionary<int, Mods> Clothes { get; set; } = new();
        public Dictionary<int, Mods> Accessory { get; set; } = new();
        private Dictionary<int, Mods> ReferenceHair { get; set; } = new ();
        private Dictionary<int, Mods> ReferenceClothes { get; set; } = new ();
        private Dictionary<int, Mods> ReferenceAccessory { get; set; } = new ();
        internal void Store(HandlePaths paths) => paths.With(StoreHair).With(StoreClothes).With(StoreAccessory);
        private void StoreHair(HandlePaths paths) =>
           Enumerable.Range(0, 4).Do(index => index.StoreDifference(ReferenceHair, Hair, paths.Hair(index).ToMods()));
        private void StoreClothes(HandlePaths paths) =>
            Enumerable.Range(0,8).Do(index => index.StoreDifference(ReferenceClothes, Clothes, paths.Clothes(index).ToMods()));
        private void StoreAccessory(HandlePaths paths) =>
            Enumerable.Range(0,20).Do(index => index.StoreDifference(ReferenceAccessory, Accessory, paths.Accessory(index).ToMods()));
        internal void Apply(HandlePaths paths) => paths.With(ApplyHair).With(ApplyClothes).With(ApplyAccessory);
        private void ApplyHair(HandlePaths paths) =>
           Enumerable.Range(0, 4).Do(index => Hair.GetValueOrDefault(index)?.Apply(paths.Hair(index)));
        private void ApplyClothes(HandlePaths paths) =>
           Enumerable.Range(0, 8).Do(index => Clothes.GetValueOrDefault(index)?.Apply(paths.Clothes(index)));
        private void ApplyAccessory(HandlePaths paths) =>
           Enumerable.Range(0, 20).Do(index => Accessory.GetValueOrDefault(index)?.Apply(paths.Accessory(index)));
        internal void Clear() => new List<Dictionary<int, Mods>>() {
            Hair, Clothes, Accessory, ReferenceHair, ReferenceClothes, ReferenceAccessory
        }.Do(item => item.Clear());
    }
    internal static partial class ModificationExtension {
        // If keys different from reference, discard previous and keep next as new reference
        internal static void StoreDifference(this int index, Dictionary<int, Mods> refs, Dictionary<int, Mods> prev, Mods next) =>
            (refs.ContainsKey(index) && Equals(refs[index].Keys, next.Keys)).Either(
                () => prev.With(() => refs[index] = next)[index] = new (),
                () => prev[index] = next.Difference(refs[index]));
        internal static bool Equals(IEnumerable<string> refs, IEnumerable<string> mods) =>
            Enumerable.Range(0, Math.Max(refs.Count(), mods.Count()))
                .All(index => string.Equals(refs.ElementAtOrDefault(index), mods.ElementAtOrDefault(index)));
        internal static Mods Difference(this Mods mods, Mods refs) =>
            mods.ToDictionary(entry => entry.Key, entry => entry.Value.Difference(refs[entry.Key]));
    }
    internal static class TextureExtension
    {
        private static string TexturePath(string hash) =>
            Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, "textures", hash);
        private static Dictionary<string, byte[]> Binaries = new();
        private static string Hash(this byte[] input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return Convert.ToHexString(sha256.ComputeHash(input)).With(hash => Binaries.TryAdd(hash, input));
            }
        }
        internal static string Store(this Texture value) => Binaries.ContainsKey(value.name) ? value.name : null;
        internal static RenderTexture Apply(this string value) =>
            Binaries.GetValueOrDefault(value)?.Import() ?? TexturePath(value).Import();
        internal static RenderTexture Import(this string path) => File.ReadAllBytes(path).Import();
        private static RenderTexture Import(this byte[] input) =>
            new Texture2D(256, 256)
                .With(t2d => ImageConversion.LoadImage(t2d, input))
                .Import().With(texture => texture.name = Hash(input));
        private static RenderTexture Import(this Texture2D input) =>
            new RenderTexture(input.width, input.height, 0)
                .With(tex => Graphics.Blit(input, tex));
        internal static void Export(this string path, Texture tex) =>
            File.WriteAllBytes(path, new Texture2D(tex.width, tex.height)
                .With(t2d => t2d.Export(tex, RenderTexture.GetTemporary(tex.width, tex.height)))
                .EncodeToPNG().ToArray());
        private static void Export(this Texture2D t2d, Texture tex, RenderTexture tmp) =>
            RenderTexture.active = RenderTexture.active.With(() =>
            {
                RenderTexture.active = tmp;
                GL.Clear(false, true, new Color());
                Graphics.Blit(tex, tmp);
                t2d.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            }).With(() => RenderTexture.ReleaseTemporary(tmp));
        internal static void Dispose() => Binaries.Clear();
        internal static void Serialize(string hash) =>
            (!File.Exists(TexturePath(hash))).Maybe(() => File.WriteAllBytes(TexturePath(hash), Binaries[hash]));
    }
    internal class HandlePaths
    {
        internal static HandlePaths Of(HumanData data) =>
            Of(Human.Find((Func<Human, bool>)(human => human.data == data)));
        internal static HandlePaths Of(Human human) => human == null ? null : new HandlePaths(human);
        internal HandlePaths(Human target) => Target = target.With(ModificationExtension.Register);
        internal Human Target { get; init; }
        internal IEnumerable<MaterialHandle> Face
        {
            get => ControlMaterial.Of(Target?.face)
                .Concat(MaterialHandle.Of(Target?.face?.rendFace));
        }
        internal IEnumerable<MaterialHandle> Eyebrows
        {
            get => MaterialHandle.Of(Target?.face?.rendEyebrow);
        }
        internal IEnumerable<MaterialHandle> Eyelines
        {
            get => MaterialHandle.Of(Target?.face?.rendEyelid)
                .Concat(MaterialHandle.Of(Target?.face?.rendEyeline));
        }
        internal IEnumerable<MaterialHandle> Eyes
        {
            get => MaterialHandle.Of(Target?.face?.rendEye?[0])
                .Concat(MaterialHandle.Of(Target?.face?.rendEye?[1]));
        }
        internal IEnumerable<MaterialHandle> Tooth
        {
            get => MaterialHandle.Of(Target?.face?.rendTooth)
                .Concat(MaterialHandle.Of(Target?.face?.rendDoubleTooth))
                .Concat(MaterialHandle.Of(Target?.face?.rendTongueFace));
        }
        internal IEnumerable<MaterialHandle> Body
        {
            get => ControlMaterial.Of(Target?.body)
                .Concat(MaterialHandle.Of(Target?.body?.rendBody));
        }
        internal IEnumerable<MaterialHandle> Nails
        {
            get => MaterialHandle.Of(Target?.body?.hand?.nailObject?.obj)
                .Concat(MaterialHandle.Of(Target?.body?.leg?.nailObject?.obj));
        }
        internal IEnumerable<MaterialHandle> Hair(int index) =>
            Target?.hair?.hairs?[index]?.renderers?.SelectMany(MaterialHandle.Of) ?? [];
        internal IEnumerable<MaterialHandle> Clothes(int index) =>
            CreateMaterial.Of(Target?.cloth?.clothess?[index]).Concat(MaterialHandle.Of(Target?.cloth?.clothess?[index]?.cusClothesCmp)) ?? [];
        internal IEnumerable<MaterialHandle> Accessory(int index) =>
            Target?.acs?.accessories?[index]?.renderers?.SelectMany(MaterialHandle.Of) ?? [];
        internal void Store() => Target.Modifications().Store(this);
        internal void Apply() => Target.Modifications().Apply(this);
        internal void Clear() => Target.Modifications().Clear();
        internal void Serialize(string name) => Target.Serialize(name);
        internal void Deserialize(string name) =>
            Target.With(Store).With(() => UniTask.NextFrame().ContinueWith((Action)Apply)).Deserialize(name);
    }
    internal static partial class ModificationExtension
    {
        private static Dictionary<Human, CharacterModifications> Instances = new();
        internal static CharacterModifications Modifications(this Human human) => Instances[human];
        internal static void Register(this Human human) => Instances.TryAdd(human, new CharacterModifications());
        internal static void Dispose() => Instances.Clear();
        internal static IEnumerable<Modifications> Aggregate(this CharacterModifications mods) =>
            mods.Coordinates.Values.SelectMany(Aggregate)
                .Concat(mods.Face.Values).Concat(mods.Eyebrows.Values).Concat(mods.Eyelines.Values)
                .Concat(mods.Eyes.Values).Concat(mods.Tooth.Values).Concat(mods.Body.Values).Concat(mods.Nails.Values);
        internal static IEnumerable<Modifications> Aggregate(this CoordinateModifications mods) =>
            mods.Hair.Values.SelectMany(values => values.Values)
                .Concat(mods.Clothes.Values.SelectMany(values => values.Values))
                .Concat(mods.Accessory.Values.SelectMany(values => values.Values));
        internal static string Gender(this Human human) => human.sex == 0 ? "male" : "female";
        private static string CharacterPath(this Human human, string name) =>
            Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, "chara", human.Gender(), $"{name}.json");
        private static void Serialize(CharacterModifications mods) =>
            mods.Aggregate().SelectMany(mod => mod.TextureHashes.Values).Do(TextureExtension.Serialize);
        internal static void Serialize(this Human human, string path) =>
            File.WriteAllText(human.CharacterPath(path),
                JsonSerializer.Serialize(Instances[human].With(Serialize), new JsonSerializerOptions { WriteIndented = true }));
        internal static CharacterModifications Deserialize(this Human human, string path) =>
            Instances[human] = File.Exists(human.CharacterPath(path)) ?
                JsonSerializer.Deserialize<CharacterModifications>(File.ReadAllText(human.CharacterPath(path))) : new CharacterModifications();
        internal static void InitializeModifications(this Human human) =>
            (!Instances.ContainsKey(human)).Maybe(() => Deserialize(human, human.data.CharaFileName));
        internal static void Delete(string name) =>
            File.Delete(UIFactory.Current.Target.CharacterPath(name));
    }
    internal static class UIRef
    {
        internal static Transform Window
        {
            get => SV.Config.ConfigWindow.Instance.transform
                .Find("Canvas").Find("Background").Find("MainWindow");
        }
        internal static Transform Content(this Transform tf) =>
            tf.Find("Settings").Find("Scroll View").Find("Viewport").Find("Content");
        internal static Transform SectionTitle
        {
            get => Window.Content().Find("CameraSetting").Find("imgTitle");
        }
        internal static GameObject Label
        {
            get => Window.Content().Find("CameraSetting").Find("Content").Find("Look").Find("Title").gameObject;
        }
        internal static GameObject Input
        {
            get => HumanCustom.Instance.StateMiniSelection.transform.Find("Window")
                .Find("StateWindow").Find("Pose")
                .Find("PosePtn").Find("Layout").Find("ptnSelect").Find("InputField_Integer").gameObject;
        }
        internal static GameObject Color
        {
            get => HumanCustom.Instance.StateMiniSelection.transform.Find("Window")
                .Find("StateWindow").Find("Light")
                .Find("grpLighting").Find("colorLight").gameObject;
        }
        internal static GameObject Slider
        {
            get => Window.Content().Find("CameraSetting").Find("Content").Find("SensitivityX").Find("Slider").gameObject;
        }
        internal static Transform UIRoot {
            get => Scene.ActiveScene.GetRootGameObjects()
                .Where(item => "CustomScene".Equals(item.name))
                .First().transform.Find("UI").Find("Root");
        }
        internal static GameObject Import
        {
            get => UIRoot.Find("Cvs_Category_FileList")
                .GetChild(0).Find("CustomFileWindow").Find("BasePanel").Find("WinRect").Find("Load").Find("btnLoad").gameObject;
        }
        internal static GameObject Export
        {
            get => UIRoot.Find("Cvs_Category_FileList")
                .GetChild(0).Find("CustomFileWindow").Find("BasePanel").Find("WinRect").Find("Save").Find("btnSave").gameObject;
        }
        internal static Toggle Casual
        {
            get => UIRoot.Find("Cvs_Coorde").Find("BG").Find("CoordinateType").Find("01_Plain").GetComponent<Toggle>();
        }
        internal static Toggle Job
        {
            get => UIRoot.Find("Cvs_Coorde").Find("BG").Find("CoordinateType").Find("02_Roomwear").GetComponent<Toggle>();
        }
        internal static Toggle Swim
        {
            get => UIRoot.Find("Cvs_Coorde").Find("BG").Find("CoordinateType").Find("03_Bathing").GetComponent<Toggle>();
        }
    }
    internal static class UIFactory
    {
        internal static void Wrap(this Transform tf, GameObject go) => go.transform.SetParent(tf);
        internal static Transform ContentRoot;
        internal static void Cleanup(Transform tf) =>
            Enumerable.Range(0, tf.childCount).Select(tf.GetChild)
                .Select(tf => tf.gameObject).Do(UnityEngine.Object.Destroy);
        internal static Transform UI(this GameObject go)
        {
            go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().With(ui =>
            {
                ui.referenceResolution = new(1920, 1080);
                ui.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                ui.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            });
            go.AddComponent<GraphicRaycaster>();
            return UnityEngine.Object.Instantiate(UIRef.Window, go.transform)
                .With(tf => tf.gameObject.Window()).Content().With(Cleanup).With(tf =>
                {
                    tf.GetComponent<VerticalLayoutGroup>().With(ui =>
                    {
                        ui.childControlWidth = false;
                        ui.childControlHeight = true;
                        ui.childForceExpandWidth = true;
                        ui.childForceExpandHeight = false;
                    });
                });
        }
        internal static void Window(this GameObject go)
        {
            go.GetComponent<RectTransform>().With(ui =>
            {
                ui.anchorMin = new(0.0f, 1.0f);
                ui.anchorMax = new(0.0f, 1.0f);
                ui.pivot = new(0.0f, 1.0f);
                ui.anchoredPosition = new(1000, -120);
                ui.sizeDelta = new(480, 644);
            });
            go.transform.Find("Title").gameObject.With(title =>
            {
                title.GetComponentInChildren<TextMeshProUGUI>().SetText(Plugin.Name);
                UnityEngine.Object.Destroy(title.transform.Find("btnClose").gameObject);
            });
            go.transform.Find("Settings").gameObject.With(settings =>
            {
                settings.GetComponent<LayoutElement>().preferredHeight = 600;
                settings.GetComponentInChildren<RectMask2D>().enabled = true;
            });
            go.AddComponent<UI_DragWindow>();
            go.AddComponent<ObservableDestroyTrigger>()
                .AddDisposableOnDestroy(Disposable.Create(Dispose));
        }
        internal static void UpdateContent(this IEnumerable<MaterialHandle> handles) =>
            (handles.Count() > 0).Either(Hide, () => ContentRoot.With(Hide)
                .With(Cleanup).With(tf => handles.Do(handle => tf.View(handle))).With(Show));
        internal static void View(this Transform root, MaterialHandle handle) =>
            root.With(handle.Label.Title).With(handle.Value.name.Title)
                .With(() => handle.Handles.Do(entry => new GameObject(entry.Key)
                    .With(root.Wrap).With(FitLayout<HorizontalLayoutGroup>).With(Label).With(entry.Value)));
        internal static void Title(this string name, Transform root) =>
            UnityEngine.Object
                .Instantiate(UIRef.SectionTitle, root)
                .GetComponentInChildren<TextMeshProUGUI>().SetText($"{name}");
        internal static void FitLayout<T>(this GameObject go) where T : HorizontalOrVerticalLayoutGroup
        {
            go.AddComponent<RectTransform>().localScale = new(1.0f, 1.0f);
            go.AddComponent<LayoutElement>();
            go.AddComponent<T>().With(ui =>
            {
                ui.childControlWidth = true;
                ui.childControlHeight = true;
            });
            go.AddComponent<ContentSizeFitter>().With(ui =>
            {
                ui.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ui.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            });
        }
        internal static void Label(this GameObject go)
        {
            go.GetComponent<RectTransform>();
            go.GetComponent<HorizontalLayoutGroup>().With(ui =>
            {
                ui.padding = new(10, 0, 2, 2);
                ui.spacing = 10;
                ui.childAlignment = TextAnchor.UpperLeft;
            });
            UnityEngine.Object.Instantiate(UIRef.SectionTitle.gameObject, go.transform).With(label =>
            {
                label.AddComponent<LayoutElement>().preferredWidth = 200;
                label.GetComponentInChildren<TextMeshProUGUI>().With(ui =>
                {
                    ui.maxVisibleLines = 4;
                    ui.enableWordWrapping = true;
                    ui.overflowMode = TextOverflowModes.Ellipsis;
                    ui.verticalAlignment = VerticalAlignmentOptions.Top;
                    ui.horizontalAlignment = HorizontalAlignmentOptions.Left;
                    ui.SetText(go.name.Replace('_', ' ').Trim());
                });
            });
        }
        internal static void NumericEdit(this GameObject go, string desc, Func<string> get, Action<string> set, TMP_InputField.ContentType type)
        {
            UnityEngine.Object.Instantiate(UIRef.Label, go.transform).With(label =>
            {
                label.AddComponent<LayoutElement>().preferredWidth = 80;
                label.GetComponent<TextMeshProUGUI>().SetText(desc);
            });
            UnityEngine.Object.Instantiate(UIRef.Input, go.transform).With(input =>
            {
                input.AddComponent<LayoutElement>().preferredWidth = 120;
                input.GetComponent<TMP_InputField>().With(ui =>
                {
                    ui.contentType = type;
                    ui.characterLimit = 10;
                    ui.onSubmit.AddListener(set);
                    ui.restoreOriginalTextOnEscape = true;
                    ui.SetText(get());
                });
            });
        }
        internal static void RangeEdit(this GameObject go, Func<float> get, Action<float> set, Vector2 range)
        {
            UnityEngine.Object.Instantiate(UIRef.Label, go.transform).With(label =>
             {
                 label.AddComponent<LayoutElement>().preferredWidth = 100;
                 label.GetComponent<TextMeshProUGUI>().With(text =>
                 {
                     text.overflowMode = TextOverflowModes.Ellipsis;
                     text.SetText(get().ToString());
                     UnityEngine.Object.Instantiate(UIRef.Slider, go.transform).With(input =>
                     {
                         input.AddComponent<LayoutElement>().preferredWidth = 100;
                         input.GetComponent<Slider>().With(ui =>
                         {
                             ui.value = get();
                             ui.minValue = range.x;
                             ui.maxValue = range.y;
                             ui.onValueChanged.AddListener(set);
                             ui.onValueChanged.AddListener((Action<float>)(input => text.SetText(input.ToString())));
                         });
                     });
                 });
             });
        }
        internal static void ColorEdit(this GameObject go, Func<Color> get, Func<Color, bool> set)
        {
            UnityEngine.Object.Instantiate(UIRef.Color, go.transform).With(input =>
            {
                input.AddComponent<LayoutElement>().preferredWidth = 100;
                input.GetComponent<ThumbnailColor>().Initialize(go.name, get, set, true, true);
            });
        }
        internal static void VectorEdit(this GameObject go, Func<Vector4> get, Action<Vector4> set)
        {
            new GameObject("VectorValues").With(go.transform.Wrap).With(FitLayout<VerticalLayoutGroup>).With(vs =>
            {
                vs.GetComponent<LayoutElement>().preferredWidth = 230;
                new GameObject("x").With(vs.transform.Wrap).With(FitLayout<HorizontalLayoutGroup>)
                    .NumericEdit("float(x):", () => get().x.ToString(),
                         (input) => set(new(float.Parse(input), get().y, get().z, get().w)), TMP_InputField.ContentType.DecimalNumber);
                new GameObject("y").With(vs.transform.Wrap).With(FitLayout<HorizontalLayoutGroup>)
                    .NumericEdit("float(y):", () => get().y.ToString(),
                        (input) => set(new(get().x, float.Parse(input), get().z, get().w)), TMP_InputField.ContentType.DecimalNumber);
                new GameObject("z").With(vs.transform.Wrap).With(FitLayout<HorizontalLayoutGroup>)
                    .NumericEdit("float(z):", () => get().z.ToString(),
                        (input) => set(new(get().x, get().y, float.Parse(input), get().w)), TMP_InputField.ContentType.DecimalNumber);
                new GameObject("w").With(vs.transform.Wrap).With(FitLayout<HorizontalLayoutGroup>)
                    .NumericEdit("float(w):", () => get().w.ToString(),
                        (input) => set(new(get().x, get().y, get().z, float.Parse(input))), TMP_InputField.ContentType.DecimalNumber);
            });
        }
        internal static void TextureEdit(this GameObject go, Func<Texture> get, Action<Texture> set)
        {
            new GameObject("VectorValues").With(go.transform.Wrap).With(FitLayout<VerticalLayoutGroup>).With(vt =>
            {
                UnityEngine.Object.Instantiate(UIRef.Label, vt.transform).With(label =>
                {
                    label.AddComponent<LayoutElement>().preferredWidth = 200;
                    label.GetComponent<TextMeshProUGUI>().SetText(get()?.name ?? "");
                });
                new GameObject("Buttons").With(vt.transform.Wrap).With(FitLayout<HorizontalLayoutGroup>).With(hr =>
                {
                    UnityEngine.Object.Instantiate(UIRef.Import, hr.transform).With(button =>
                    {
                        button.AddComponent<LayoutElement>().With(ui =>
                        {
                            ui.preferredWidth = 100;
                            ui.preferredHeight = 30;
                        });
                        button.GetComponentsInChildren<TextMeshProUGUI>().Do(ui =>
                        {
                            ui.autoSizeTextContainer = true;
                            ui.SetText("import");
                        });
                        button.GetComponent<Button>().With(ui =>
                        {
                            ui.onClick.AddListener(ImportTexture(get, set));
                            ui.interactable = true;
                        });
                    });
                    UnityEngine.Object.Instantiate(UIRef.Export, hr.transform).With(button =>
                    {
                        button.AddComponent<LayoutElement>().With(ui =>
                        {
                            ui.preferredWidth = 100;
                            ui.preferredHeight = 30;
                        });
                        button.GetComponentsInChildren<TextMeshProUGUI>().Do(ui =>
                        {
                            ui.autoSizeTextContainer = true;
                            ui.SetText("export");
                        });
                        button.GetComponent<Button>().With(ui =>
                        {
                            ui.onClick.AddListener(ExportTexture(get));
                            ui.interactable = get() != null;
                        });
                    });
                });
            });
        }
        internal static Action ImportTexture(Func<Texture> get, Action<Texture> set)
        {
            return () =>
            {
                using (System.Windows.Forms.OpenFileDialog dialog = new())
                {
                    get().With(tex =>
                    {
                        dialog.InitialDirectory = Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, "import");
                        dialog.Filter = "Texture Sources|*.png";
                        dialog.FileName = $"{tex.name}.png";
                        (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            .Maybe(() => set(dialog.FileName.Import()));
                    });
                }
            };
        }
        internal static Action ExportTexture(Func<Texture> get)
        {
            return () =>
            {
                using (System.Windows.Forms.SaveFileDialog dialog = new())
                {
                    get().With(tex =>
                    {
                        dialog.InitialDirectory = Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, "export");
                        dialog.Filter = "Texture Sources|*.png";
                        dialog.FileName = $"{tex.name}.png";
                        (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            .Maybe(() => dialog.FileName.Export(tex));
                    });
                }
            };
        }
        internal static void Show() =>
            ContentRoot?.parent?.parent?.parent?.parent?.gameObject?.SetActive(true);
        internal static void Hide() =>
            ContentRoot?.parent?.parent?.parent?.parent?.gameObject?.SetActive(false);
        internal static Action RefreshOperation = Hide;
        internal static CancellationTokenSource RefreshCanceler = new();
        internal static UniTask RefreshTask = UniTask.CompletedTask;
        internal static ConfigEntry<KeyboardShortcut> Toggle { get; set; }
        internal static ConfigEntry<bool> Status { get; set; }
        internal static void Refresh() => Status.Value.Either(Hide, RefreshOperation);
        internal static void CancelRefresh() =>
            (!RefreshTask.Status.IsCompleted()).Maybe(RefreshCanceler.Cancel);
        internal static void ScheduleRefresh() =>
            RefreshTask.Status.IsCompleted().Maybe(() =>
            {
                RefreshTask = UniTask.NextFrame().ContinueWith((Action)Refresh);
            });
        internal static HandlePaths Current { get => Human.list.TryGet(0, out Human human) ? new HandlePaths(human) : null; }
        internal static Action<bool> ReserveCoordinate = (bool input) =>
            input.Maybe(Current.With(() => UniTask.NextFrame().ContinueWith((Action)(() => Current.Apply()))).Store); 
        internal static Action InputCheck = () =>
            Toggle.Value.IsDown().Maybe(() => (Status.Value = !Status.Value).With(ScheduleRefresh));
        internal static void Listening()
        {
            Canvas.preWillRenderCanvases += InputCheck;
            UIRef.Casual.onValueChanged.AddListener(ReserveCoordinate);
            UIRef.Job.onValueChanged.AddListener(ReserveCoordinate);
            UIRef.Swim.onValueChanged.AddListener(ReserveCoordinate);
        }
        internal static Action Initialize = () =>
            ContentRoot = new GameObject(Plugin.Name).With(UIRef.UIRoot.Wrap).UI()
                .With(Listening).With(Current.Store).With(CancelRefresh).With(ScheduleRefresh);
        internal static Action Dispose = () =>
        {
            Canvas.preWillRenderCanvases -= InputCheck;
            RefreshCanceler.Cancel();
            RefreshOperation = Hide;
            TextureExtension.Dispose();
            ModificationExtension.Dispose();
        };
       internal static void UpdateContent(int category, int index) =>
            (RefreshOperation = (category, index) switch
            {
                (0, 1) => () => UpdateContent(Current.Eyebrows),
                (0, 3) => () => UpdateContent(Current.Eyelines),
                (0, 4) => () => UpdateContent(Current.Eyes),
                (0, 5) => () => UpdateContent(Current.Tooth),
                (0, _) => () => UpdateContent(Current.Face),
                (1, 8) => () => UpdateContent(Current.Nails),
                (1, _) => () => UpdateContent(Current.Body),
                (2, 0) => () => UpdateContent(Current.Hair(1)),
                (2, 1) => () => UpdateContent(Current.Hair(0)),
                (2, 2) => () => UpdateContent(Current.Hair(2)),
                (2, 3) => () => UpdateContent(Current.Hair(3)),
                (3, _) => () => UpdateContent(Current.Clothes(index)),
                (4, _) => () => UpdateContent(Current.Accessory(index)),
                _ => Hide
            }).With(CancelRefresh).With(ScheduleRefresh);
    }
    internal class HookImpl
    {
        internal Action<Human> ApplyModifications = delegate { };
        internal Action<Human> InitializeModifications = delegate { };
        internal Action<HumanData, LoadFlgs> LoadPrefix = delegate { };
        internal Action<HumanData, LoadFlgs> LoadPostfix = delegate { };
        internal Action<HumanData, SaveFlgs> SavePostfix = delegate { };
        internal static HookImpl Application = new HookImpl() {
            ApplyModifications = human => HandlePaths.Of(human).Apply(),
            InitializeModifications = human => human.InitializeModifications()
        };
        internal static HookImpl CharacterCreation = new HookImpl() {
            LoadPrefix = (data, flags) => flags = flags switch {
                    LoadFlgs.Default => flags,
                    LoadFlgs.FileView => flags,
                    _ => flags.With(UIFactory.Current.Clear)
                },
            LoadPostfix = (data, flags) => flags = flags switch {
                    LoadFlgs.Default => flags,
                    LoadFlgs.FileView => flags,
                    _ => flags.With(data.CharaFileName.Apply(UIFactory.Current.Deserialize))
                },
            SavePostfix = (data, flags) =>
                UIFactory.Current.With(UIFactory.Current.Store).Serialize(data.CharaFileName)
        };
        internal static HookImpl Default = new HookImpl();
    }
    internal static class Hooks
    {
        internal static HookImpl Impl;
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.CreateClothesTexture))]
        internal static void HumanClothCreateClothesTexturePostfix(HumanCloth __instance) => Impl.ApplyModifications(__instance.human);
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Human), nameof(Human.SetCreateTexture))]
        internal static void HumanSetCreateTexturePostfix(Human __instance) => Impl.InitializeModifications(__instance);
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppSystem.IO.BinaryReader), typeof(LoadFlgs))]
        internal static void HumanDataLoadFilePrefix(HumanData __instance, LoadFlgs flags) => Impl.LoadPrefix(__instance, flags);
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppSystem.IO.BinaryReader), typeof(LoadFlgs))]
        internal static void HumanDataLoadFilePostfix(HumanData __instance, LoadFlgs flags) => Impl.LoadPostfix(__instance, flags);
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveFile), typeof(Il2CppSystem.IO.Stream), typeof(SaveFlgs))]
        internal static void HumanDataSaveFilePostfix(HumanData __instance, SaveFlgs flags) => Impl.SavePostfix(__instance, flags);
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CustomCharaFile), nameof(CustomCharaFile.DeleteCharaFile))]
        internal static void CustomCharaFileDeleteCharaFilePrefix(CustomCharaFile __instance) =>
            ModificationExtension.Delete(__instance.ListCtrl.GetSelectTopInfo().FileName);
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CategorySelection), nameof(CategorySelection.OpenView), typeof(int))]
        internal static void CategorySelectionSetPostfix(CategorySelection __instance, int index) =>
            UIFactory.UpdateContent(__instance._no, index);
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(ThumbnailButton), nameof(ThumbnailButton.Set))]
        internal static void ThumbnailButtonOpenPostfix() => UIFactory.ScheduleRefresh();
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Scene), nameof(Scene.LoadStart), typeof(Scene.Data), typeof(bool))]
        internal static void SceneLoadStartPostfix(Scene.Data data, ref UniTask __result) =>
            __result = data.LevelName switch
            {
                "CustomScene" => __result.ContinueWith(UIFactory.Initialize)
                    .With(() => Impl = HookImpl.CharacterCreation),
                "Simulation" => __result
                    .With(() => Impl = HookImpl.Application),
                _ => __result
                    .With(() => Impl = HookImpl.Default),
            };
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        internal static Plugin Instance;
        public const string Process = "SamabakeScramble";
        public const string Name = "SardineHead";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "0.3.0";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(ConfigToggle).With(ConfigStatus).With(() => Instance = this);
        private void ConfigToggle() => UIFactory.Toggle = Config
            .Bind("General", "Sardin Head toggle key", new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl));
        private void ConfigStatus() => UIFactory.Status = Config
            .Bind("General", "show Sardin Head (smells fishy)", true);
        public override bool Unload() =>
            true.With(() => Patch.UnpatchSelf()) && base.Unload();
    }
}