using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Security.Cryptography;
using UniRx;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using Character;
using CharacterCreation;
using Mods = System.Collections.Generic.Dictionary<string, SardineHead.Modifications>;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Fishbone;

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
    public class Modifications
    {
        public Dictionary<string, int> IntValues { get; init; } = new();
        public Dictionary<string, float> FloatValues { get; init; } = new();
        public Dictionary<string, float> RangeValues { get; init; } = new();
        public Dictionary<string, Quad> ColorValues { get; init; } = new();
        public Dictionary<string, Quad> VectorValues { get; init; } = new();
        public Dictionary<string, string> TextureHashes { get; init; } = new();
    }
    public class CharacterModifications
    {
        public Mods Face { get; set; } = new();
        public Mods Eyebrows { get; set; } = new();
        public Mods Eyelines { get; set; } = new();
        public Mods Eyes { get; set; } = new();
        public Mods Tooth { get; set; } = new();
        public Mods Body { get; set; } = new();
        public Mods Nails { get; set; } = new();
        public Dictionary<int, CoordinateModifications> Coordinates { get; set; } = new() { { 0, new() }, { 1, new() }, { 2, new() } };
    }
    public class CoordinateModifications
    {
        public Dictionary<int, Mods> Hair { get; set; } =
            Enumerable.Range(0, 4).ToDictionary<int, int, Mods>(idx => idx, idx => new());
        public Dictionary<int, Mods> Clothes { get; set; } =
            Enumerable.Range(0, 8).ToDictionary<int, int, Mods>(idx => idx, idx => new());
        public Dictionary<int, Mods> Accessory { get; set; } =
            Enumerable.Range(0, ChaFileDefine.AccessorySlotNum).ToDictionary<int, int, Mods>(idx => idx, idx => new());
    }
    internal class MaterialHandle
    {
        internal Material Value { get; init; }
        internal string Label { get; init; }
        internal Mods Mods { get; init; }
        internal Action<int, int> SetInt;
        internal Action<int, float> SetFloat;
        internal Action<int, Color> SetColor;
        internal Action<int, Vector4> SetVector;
        internal Action<int, Texture> SetTexture;
        internal Dictionary<string, Action<int>> SetInts = new();
        internal Dictionary<string, Action<float>> SetFloats = new();
        internal Dictionary<string, Action<Color>> SetColors = new();
        internal Dictionary<string, Action<Vector4>> SetVectors = new();
        internal Dictionary<string, Action<Texture>> SetTextures = new();
        internal Dictionary<string, Action<Transform>> Handles { get; init; } = new();
        internal MaterialHandle(Material material, string label, Mods mods) =>
            ((Value, Label, Mods) = (material, label, mods))
                .With(() => Mods.TryAdd(Label, new())).With(PrepareSetters).With(PopulateHandles);
        private void PrepareSetters() =>
            (SetInt, SetFloat, SetColor, SetVector, SetTexture) =
                (Value.SetInt, Value.SetFloat, Value.SetColor, Value.SetVector, Value.SetTexture);
        private void PopulateHandles() =>
            Enumerable.Range(0, Value.shader.GetPropertyCount()).Do(idx =>
                PopulateHandles(
                    Value.shader.GetPropertyName(idx),
                    Value.shader.GetPropertyNameId(idx),
                    Value.shader.GetPropertyType(idx),
                    () => Value.shader.GetPropertyRangeLimits(idx)));
        private Action<bool> IntToggle(int id, string name) =>
            Toggle(name, SetInts, () => Value.GetInt(id), value => Mods[Label].IntValues[name] = value, () => Mods[Label].IntValues.Remove(name));
        private Action<bool> FloatToggle(int id, string name) =>
            Toggle(name, SetFloats, () => Value.GetFloat(id), value => Mods[Label].FloatValues[name] = value, () => Mods[Label].FloatValues.Remove(name));
        private Action<bool> ColorToggle(int id, string name) =>
            Toggle(name, SetColors, () => Value.GetColor(id), value => Mods[Label].ColorValues[name] = value, () => Mods[Label].ColorValues.Remove(name));
        private Action<bool> VectorToggle(int id, string name) =>
            Toggle(name, SetVectors, () => Value.GetVector(id), value => Mods[Label].VectorValues[name] = value, () => Mods[Label].VectorValues.Remove(name));
        private Action<bool> TextureToggle(int id, string name) =>
            Toggle(name, SetTextures, () => Value.GetTexture(id), value => value.IsExtension()
                .Either(() => Mods[Label].TextureHashes.Remove(name), () => Mods[Label].TextureHashes[name] = value.name), () => Mods[Label].TextureHashes.Remove(name));
        private Action<bool> Toggle<T>(string name, Dictionary<string, Action<T>> actions, Func<T> get, Action<T> set, Action unset) =>
            value => value.Either(() => actions[name] -= set.With(unset), () => actions[name] += set.With(() => set(get())));
        private bool PopulateHandles(string name, int id, ShaderPropertyType shaderType, Func<Vector2> limits) =>
            shaderType switch
            {
                ShaderPropertyType.Int =>
                    SetInts.TryAdd(name, input => SetInt(id, input)) &&
                    Handles.TryAdd(name, go => UnityEngine.Object.Instantiate(UIRef.IntEditBase, go.transform)
                            .With(name.CommonEdit(Mods[Label].IntValues.ContainsKey(name), IntToggle(id, name)))
                            .IntEdit(() => Value.GetInt(id), value => SetInts[name](value))),

                ShaderPropertyType.Float =>
                    SetFloats.TryAdd(name, input => SetFloat(id, input)) &&
                    Handles.TryAdd(name, tf => UnityEngine.Object.Instantiate(UIRef.FloatEditBase, tf)
                        .With(name.CommonEdit(Mods[Label].FloatValues.ContainsKey(name), FloatToggle(id, name)))
                        .FloatEdit(() => Value.GetFloat(id), value => SetFloats[name](value))),

                ShaderPropertyType.Range =>
                    SetFloats.TryAdd(name, input => SetFloat(id, input)) &&
                    Handles.TryAdd(name, tf => UnityEngine.Object.Instantiate(UIRef.RangeEditBase, tf)
                        .With(name.CommonEdit(Mods[Label].FloatValues.ContainsKey(name), FloatToggle(id, name)))
                        .RangeEdit(() => Value.GetFloat(id), value => SetFloats[name](value), limits())),

                ShaderPropertyType.Color =>
                    SetColors.TryAdd(name, input => SetColor(id, input)) &&
                    Handles.TryAdd(name, tf => UnityEngine.Object.Instantiate(UIRef.ColorEditBase, tf)
                        .With(name.CommonEdit(Mods[Label].ColorValues.ContainsKey(name), ColorToggle(id, name)))
                        .ColorEdit(() => Value.GetColor(id), value => SetColors[name](value))),

                ShaderPropertyType.Vector =>
                    SetVectors.TryAdd(name, input => SetVector(id, input)) &&
                    Handles.TryAdd(name, tf => UnityEngine.Object.Instantiate(UIRef.VectorEditBase, tf)
                        .With(name.CommonEdit(Mods[Label].VectorValues.ContainsKey(name), VectorToggle(id, name)))
                        .VectorEdit(() => Value.GetVector(id), value => SetVectors[name](value))),

                ShaderPropertyType.Texture =>
                    SetTextures.TryAdd(name, input => SetTexture(id, input)) &&
                    Handles.TryAdd(name, tf => UnityEngine.Object.Instantiate(UIRef.TextureEditBase, tf)
                        .With(name.CommonEdit(Mods[Label].TextureHashes.ContainsKey(name), TextureToggle(id, name)))
                        .TextureEdit(() => Value.GetTexture(id), value => SetTextures[name](value))),

                _ => false
            };
        internal void Apply() => Mods[Label].With(ApplyInt).With(ApplyFloat).With(ApplyColor).With(ApplyVector).With(ApplyTexture);
        private void ApplyInt(Modifications mods) => mods.IntValues
            .Where(entry => SetInts.ContainsKey(entry.Key)).Do(entry => SetInts[entry.Key](entry.Value));
        private void ApplyFloat(Modifications mods) => mods.FloatValues
            .Where(entry => SetFloats.ContainsKey(entry.Key)).Do(entry => SetFloats[entry.Key](entry.Value));
        private void ApplyColor(Modifications mods) => mods.ColorValues
            .Where(entry => SetColors.ContainsKey(entry.Key)).Do(entry => SetColors[entry.Key](entry.Value));
        private void ApplyVector(Modifications mods) => mods.VectorValues
            .Where(entry => SetVectors.ContainsKey(entry.Key)).Do(entry => SetVectors[entry.Key](entry.Value));
        private void ApplyTexture(Modifications mods) => mods.TextureHashes
            .Where(entry => SetTextures.ContainsKey(entry.Key)).Do(entry => SetTextures[entry.Key](entry.Value.ToTexture()));
    }
    internal class ControlMaterial : MaterialHandle
    {
        internal CustomTextureControl Ctc { get; init; }
        internal ControlMaterial(CustomTextureControl ctc, string name, Mods mods) :
            base(ctc._matCreate, name, mods) =>
                Ctc = ctc.With(OpInt).With(OpFloat).With(OpColor).With(OpVector).With(OpTexture);
        private void OpInt() => SetInt += (_, _) => Ctc.SetNewCreateTexture();
        private void OpFloat() => SetFloat += (_, _) => Ctc.SetNewCreateTexture();
        private void OpColor() => SetColor += (_, _) => Ctc.SetNewCreateTexture();
        private void OpVector() => SetVector += (_, _) => Ctc.SetNewCreateTexture();
        private void OpTexture() => SetTexture += (_, _) => Ctc.SetNewCreateTexture();
    }
    internal class CreateMaterial : MaterialHandle
    {
        internal CustomTextureCreate Ctc { get; init; }
        internal Func<CustomTextureCreate, int, bool> Rebuild { get; init; }
        internal CreateMaterial(CustomTextureCreate ctc, Func<CustomTextureCreate, int, bool> rebuild, string name, Mods mods) :
            base(ctc._matCreate, name, mods) =>
                (Ctc, Rebuild) = (ctc, rebuild).With(OpInt).With(OpFloat).With(OpColor).With(OpVector).With(OpTexture);
        private void OpInt() => SetInt += (id, _) => Rebuild(Ctc, id);
        private void OpFloat() => SetFloat += (id, _) => Rebuild(Ctc, id);
        private void OpColor() => SetColor += (id, _) => Rebuild(Ctc, id);
        private void OpVector() => SetVector += (id, _) => Rebuild(Ctc, id);
        private void OpTexture() => SetTexture += (id, _) => Rebuild(Ctc, id);
    }
    internal static class MaterialHandleExtension
    {
        internal static IEnumerable<Renderer> ToRenderers(this Transform tf) =>
            Enumerable.Range(0, tf.childCount).Select(idx => tf.GetChild(idx).gameObject).SelectMany(ToRenderers);
        internal static IEnumerable<Renderer> ToRenderers(this GameObject go) =>
            go.GetComponents<Renderer>().Concat(ToRenderers(go.transform));
        internal static IEnumerable<MaterialHandle> ToCtcHandles(this HumanFace face, CharacterModifications mods) =>
            face?.customTexCtrlFace == null ? [] : [new ControlMaterial(face.customTexCtrlFace, face.objHead.name, mods.Face)];
        internal static IEnumerable<MaterialHandle> ToCtcHandles(this HumanBody body, CharacterModifications mods) =>
            body?.customTexCtrlBody == null ? [] : [new ControlMaterial(body.customTexCtrlBody, body.objBody.name, mods.Body)];
        internal static IEnumerable<MaterialHandle> ToHandles(this Mods mods, params Renderer[] renderers) =>
            (renderers ?? []).Where(renderer => renderer != null).Distinct()
                .Select(renderer => new MaterialHandle(renderer.material, renderer.name ?? renderer.gameObject.name, mods));
        internal static IEnumerable<MaterialHandle> ToHandles(this HumanFace face, CharacterModifications mods) =>
            [..face.ToFaceHandles(mods), .. face.ToEyebrowsHandles(mods),
             ..face.ToEyelinesHandles(mods), ..face.ToEyesHandles(mods), ..face.ToToothHandles(mods)];
        internal static IEnumerable<MaterialHandle> ToFaceHandles(this HumanFace face, CharacterModifications mods) =>
            mods.Face.ToHandles(face?.rendFace);
        internal static IEnumerable<MaterialHandle> ToEyebrowsHandles(this HumanFace face, CharacterModifications mods) =>
            mods.Eyebrows.ToHandles(face?.rendEyebrow);
        internal static IEnumerable<MaterialHandle> ToEyelinesHandles(this HumanFace face, CharacterModifications mods) =>
            mods.Eyelines.ToHandles(face?.rendEyelid, face?.rendEyeline, face?.rendEyelineUp, face?.rendEyelineDown);
        internal static IEnumerable<MaterialHandle> ToEyesHandles(this HumanFace face, CharacterModifications mods) =>
            mods.Eyes.ToHandles(face?.rendEye);
        internal static IEnumerable<MaterialHandle> ToToothHandles(this HumanFace face, CharacterModifications mods) =>
            mods.Tooth.ToHandles(face?.rendTooth, face?.rendDoubleTooth, face?.rendTongueFace);
        internal static IEnumerable<MaterialHandle> ToHandles(this HumanBody body, CharacterModifications mods) =>
            [.. body.ToBodyHandles(mods), .. body.ToNailsHandles(mods)];
        internal static IEnumerable<MaterialHandle> ToBodyHandles(this HumanBody body, CharacterModifications mods) =>
            mods.Body.ToHandles(body?.rendBody);
        internal static IEnumerable<MaterialHandle> ToNailsHandles(this HumanBody body, CharacterModifications mods) =>
            mods.Nails.ToHandles([.. body?.hand?.nailObject?.obj?.ToRenderers() ?? [], .. body?.leg?.nailObject?.obj?.ToRenderers() ?? []]);
        private static Dictionary<int, Tuple<int, int>> LastHairIds = new();
        private static Dictionary<int, Tuple<int, int>> LastClothesIds = new();
        private static Dictionary<int, Tuple<int, int>> LastAccessoryIds = new();
        private static Action<Mods> Identify(ListInfoBase info, Dictionary<int, Tuple<int, int>> lasts, int index) =>
            info == null ? mods => mods.With(() => lasts.Remove(index)).Clear()
                : !lasts.ContainsKey(index) ? mods => mods.With(() => lasts[index] = new(info.Category, info.Id))
                : (info.Category, info.Id) != (lasts[index].Item1, lasts[index].Item2)
                    ? mods => mods.With(() => lasts[index] = new(info.Category, info.Id)).Clear() : _ => { };
        internal static IEnumerable<MaterialHandle> ToHandles(this HumanHair hair, CoordinateModifications mods, int index) =>
            hair?.hairs?[index]?.ToHandles(mods.Hair[index].With(Identify(hair?.hairs?[index]?.infoHair, LastHairIds, index))) ?? [];
        internal static IEnumerable<MaterialHandle> ToHandles(this HumanHair.Hair hair, Mods mods) =>
            mods.ToHandles([.. hair?.cusHairCmp?.gameObject?.ToRenderers() ?? []]);
        internal static IEnumerable<MaterialHandle> ToHandles(this HumanCloth cloth, CoordinateModifications mods, int index) =>
            cloth?.clothess?[index]?.ToHandles(mods.Clothes[index].With(Identify(cloth?.clothess?[index]?.infoClothes, LastClothesIds, index))) ?? [];
        internal static IEnumerable<MaterialHandle> ToHandles(this HumanCloth.Clothes clothes, Mods mods) =>
            [.. clothes?.ToCtcHandles(mods) ?? [], .. clothes?.cusClothesCmp?.ToHandles(mods) ?? []];
        internal static IEnumerable<MaterialHandle> ToHandles(this ChaClothesComponent cmp, Mods mods) =>
            cmp == null ? [] : mods.ToHandles([.. cmp.gameObject.ToRenderers()]);
        private static Func<CustomTextureCreate, int, bool>[] Rebuilds(ChaClothesComponent cmp) =>
            cmp == null ? [] : [cmp.Rebuild01, cmp.Rebuild02, cmp.Rebuild03, cmp.RebuildAccessory, cmp.RebuildAccessory];
        internal static IEnumerable<MaterialHandle> ToCtcHandles(this HumanCloth.Clothes clothes, Mods mods) =>
            Rebuilds(clothes.cusClothesCmp)
                .Where((_, idx) => idx < clothes.ctCreateClothes.Count && null != clothes.ctCreateClothes[idx])
                .Select((update, idx) => new CreateMaterial(clothes.ctCreateClothes[idx], update, $"{clothes.cusClothesCmp.name}{idx}", mods));
        internal static IEnumerable<MaterialHandle> ToHandles(this HumanAccessory acs, CoordinateModifications mods, int index) =>
            acs?.accessories?[index]?.ToHandles(mods.Accessory[index].With(Identify(acs?.accessories?[index]?.infoAccessory, LastAccessoryIds, index))) ?? [];
        internal static IEnumerable<MaterialHandle> ToHandles(this HumanAccessory.Accessory acs, Mods mods) =>
            mods.ToHandles([.. acs?.cusAcsCmp?.gameObject?.ToRenderers() ?? []]);
        internal static IEnumerable<MaterialHandle> ToHandles(this Human human, CharacterModifications mods) =>
            human.body.ToCtcHandles(mods).Concat(human.body.ToHandles(mods))
                .Concat(human.face.ToCtcHandles(mods)).Concat(human.face.ToHandles(mods))
                .Concat(Enumerable.Range(0, 4)
                    .SelectMany(index => human?.hair?.hairs[index]?.ToHandles(human.Coordinate().Hair[index]) ?? []))
                .Concat(Enumerable.Range(0, 8)
                    .SelectMany(index => (human?.cloth?.clothess[index]?.ToCtcHandles(human.Coordinate().Clothes[index] ?? [])
                        .Concat(human?.cloth?.clothess[index]?.ToHandles(human.Coordinate().Clothes[index] ?? [])))))
                .Concat(Enumerable.Range(0, ChaFileDefine.AccessorySlotNum)
                    .SelectMany(index => human?.acs?.accessories[index]?.ToHandles(human.Coordinate().Accessory[index]) ?? []));
        internal static void Apply(this Human human) => ToHandles(human, human.Modifications()).Do(handle => handle.Apply());
    }
    internal class HandlePaths
    {
        internal HandlePaths(Human target) => (Target, Mods) = (target, target.Modifications());
        internal Human Target { get; init; }
        internal CharacterModifications Mods { get; init; }
        internal CoordinateModifications Coordinate => Mods.Coordinates[Target.data.Status.coordinateType];
        internal IEnumerable<MaterialHandle> Face => Target.face.ToCtcHandles(Mods).Concat(Target.face.ToFaceHandles(Mods));
        internal IEnumerable<MaterialHandle> Eyebrows => Target.face.ToEyebrowsHandles(Mods);
        internal IEnumerable<MaterialHandle> Eyelines => Target.face.ToEyelinesHandles(Mods);
        internal IEnumerable<MaterialHandle> Eyes => Target.face.ToEyesHandles(Mods);
        internal IEnumerable<MaterialHandle> Tooth => Target.face.ToToothHandles(Mods);
        internal IEnumerable<MaterialHandle> Body => Target.body.ToCtcHandles(Mods).Concat(Target.body.ToHandles(Mods));
        internal IEnumerable<MaterialHandle> Hair(int index) => Target.hair.ToHandles(Coordinate, index);
        internal IEnumerable<MaterialHandle> Clothes(int index) => Target.cloth.ToHandles(Coordinate, index);
        internal IEnumerable<MaterialHandle> Accessory(int index) => Target.acs.ToHandles(Coordinate, index);
    }
    internal static partial class TextureExtension
    {
        private static readonly string TexturePath = Path.Combine(Plugin.Guid, "textures");
        private static void Serialize(this string hash, Stream stream) =>
            new BinaryWriter(stream).Write(Binaries[hash]);
        private static Action<Stream> Deserialize(this string hash, int size) =>
            stream => Binaries[hash] = new BinaryReader(stream).ReadBytes(size);
        internal static Action<string> Serialize(ZipArchive archive) => hash =>
            archive.CreateEntry(Path.Combine(TexturePath, hash)).Open().With(hash.Serialize).Close();
        internal static void Deserialize(ZipArchive archive) =>
            archive.Entries
                .Where(entry => entry.FullName.StartsWith(TexturePath))
                .Where(entry => !Binaries.ContainsKey(entry.Name))
                .Do(entry => entry.Open().With(entry.Name.Deserialize((int)entry.Length)).Close());
        private static IEnumerable<string> TexturesNotInUse(this IEnumerable<string> hashes) =>
            Binaries.Keys.Where(hash => !hashes.Contains(hash));
        private static void Cleanup() =>
            ModificationExtension.TexturesInUse.TexturesNotInUse().Do(hash => Binaries.Remove(hash));
        internal static void Initialize()
        {
            Util.Hook<HumanCustom>(Cleanup, Cleanup);
            Util.Hook<SV.SimulationScene>(() => { }, Binaries.Clear);
        }
    }
    internal static partial class ModificationExtension
    {
        private static readonly string ModificationPath = Path.Combine(Plugin.Guid, "modifications.json");
        private static CharacterModifications CustomSceneMods = new();
        private readonly static Dictionary<int, CharacterModifications> Actors = new();
        internal static IEnumerable<string> TexturesInUse =>
            Actors.Values.SelectMany(mods => mods.Aggregate()).SelectMany(mod => mod.TextureHashes.Values).Distinct();
        internal static CharacterModifications Modifications(this Human human) =>
            Manager.Scene.NowData.LevelName.Equals(SceneNames.CustomScene)
                ? CustomSceneMods : human.GetActorIndex().Modifications();
        internal static CoordinateModifications Coordinate(this Human human) =>
            human.Modifications().Coordinates[human.data.Status.coordinateType];
        internal static void Serialize(this ZipArchive archive) =>
            UIFactory.Current.Mods.Serialize(archive);
        internal static void SerializeCoordinate(this ZipArchive archive) =>
            UIFactory.Current.Coordinate.Serialize(archive);
        internal static void Serialize(this ZipArchive archive, int index) =>
            Actors.ContainsKey(index).Maybe(() => Actors[index].Serialize(archive));
        internal static void Deserialize(this ZipArchive archive, CharaLimit limits) =>
            (HumanCustom.Instance?.Human != null).Either(
                () => UniTask.NextFrame().ContinueWith((Action)(() => archive.Deserialize(limits))),
                () => archive.With(TextureExtension.Deserialize)
                        .With(HumanCustom.Instance.Human.Deserialize(limits))
                        .With(HumanCustom.Instance.Human.Apply));
        internal static void DeserializeCoordinate(this ZipArchive archive, Human human, CoordLimit limits) =>
            archive.With(TextureExtension.Deserialize).With(Deserialize(human, limits));
        internal static void Deserialize(this ZipArchive archive, int index) =>
            archive.With(TextureExtension.Deserialize).With(Deserialize(index));
        private static CharacterModifications Modifications(this int index) =>
            CustomSceneMods = Actors.ContainsKey(index) ? Actors[index] : new CharacterModifications();
        private static IEnumerable<Modifications> Aggregate(this CharacterModifications mods) =>
            mods.Coordinates.Values.SelectMany(Aggregate)
                .Concat(mods.Face.Values)
                .Concat(mods.Eyebrows.Values)
                .Concat(mods.Eyelines.Values)
                .Concat(mods.Eyes.Values)
                .Concat(mods.Tooth.Values)
                .Concat(mods.Body.Values)
                .Concat(mods.Nails.Values);
        private static IEnumerable<Modifications> Aggregate(this CoordinateModifications mods) =>
            mods.Hair.Values.SelectMany(values => values.Values)
                .Concat(mods.Clothes.Values.SelectMany(values => values.Values))
                .Concat(mods.Accessory.Values.SelectMany(values => values.Values));
        private static void SerializeTextures(this CoordinateModifications mods, ZipArchive archive) =>
             mods.Aggregate().SelectMany(mod => mod.TextureHashes.Values)
                .Distinct().Do(TextureExtension.Serialize(archive));
        private static void SerializeTextures(this CharacterModifications mods, ZipArchive archive) =>
            mods.Aggregate().SelectMany(mod => mod.TextureHashes.Values)
                .Distinct().Do(TextureExtension.Serialize(archive));
        private static void Serialize(this CoordinateModifications mods, ZipArchive archive) =>
            new StreamWriter(archive.With(mods.SerializeTextures).CreateEntry(ModificationPath).Open())
                .With(stream => stream.Write(JsonSerializer.Serialize(mods))).Close();
        private static void Serialize(this CharacterModifications mods, ZipArchive archive) =>
            new StreamWriter(archive.With(mods.SerializeTextures).CreateEntry(ModificationPath).Open())
                .With(stream => stream.Write(JsonSerializer.Serialize(mods))).Close();
        private static Action<ZipArchive> Deserialize(this Human human, CharaLimit limits) =>
            archive => archive.Deserialize(human.Merge(limits));
        private static Action<ZipArchive> Deserialize(this Human human, CoordLimit limits) =>
            archive => archive.Deserialize(human.Merge(limits));
        private static Action<ZipArchive> Deserialize(int index) =>
            archive => archive.Deserialize((Action<CharacterModifications>)(mods => Actors[index] = mods));
        private static void Deserialize<T>(this ZipArchive archive, Action<T> action) where T : new() =>
            archive.GetEntry(ModificationPath).Deserialize(action);
        private static void Deserialize<T>(this ZipArchiveEntry entry, Action<T> action) where T : new() =>
            (entry != null).Either(() => action(new T()),
                () => entry.Open().With(stream => action(JsonSerializer.Deserialize<T>(stream))).Close());
        private static Action<CharacterModifications> Merge(this Human human, CharaLimit limits) =>
            mods => limits.Merge(mods, new HandlePaths(human).Mods);
        private static Action<CoordinateModifications> Merge(this Human human, CoordLimit limits) =>
            mods => limits.Merge(mods, new HandlePaths(human).Coordinate);
        private static void Merge(this CharaLimit limits, CharacterModifications src, CharacterModifications dst) =>
            limits.With(MergeBody(src, dst)).With(MergeFace(src, dst)).With(MergeHair(src, dst)).With(MergeCoordinate(src, dst));
        private static Action<CharaLimit> MergeBody(CharacterModifications src, CharacterModifications dst) =>
            limits => (dst.Body, dst.Nails) = (CharaLimit.None != (limits & CharaLimit.Body)) ? (src.Body, src.Nails) : (dst.Body, src.Nails);
        private static Action<CharaLimit> MergeFace(CharacterModifications src, CharacterModifications dst) =>
            limits => (dst.Face, dst.Eyebrows, dst.Eyelines, dst.Eyes, dst.Tooth) =
                (CharaLimit.None != (limits & CharaLimit.Face))
                    ? (src.Face, src.Eyebrows, src.Eyelines, src.Eyes, src.Tooth)
                    : (dst.Face, dst.Eyebrows, dst.Eyelines, dst.Eyes, dst.Tooth);
        private static Action<CharaLimit> MergeHair(CharacterModifications src, CharacterModifications dst) =>
            limits => (dst.Coordinates[0].Hair, dst.Coordinates[1].Hair, dst.Coordinates[2].Hair) =
                (CharaLimit.None != (limits & CharaLimit.Hair))
                    ? (src.Coordinates[0].Hair, src.Coordinates[1].Hair, src.Coordinates[2].Hair)
                    : (dst.Coordinates[0].Hair, dst.Coordinates[1].Hair, dst.Coordinates[2].Hair);
        private static Action<CharaLimit> MergeCoordinate(CharacterModifications src, CharacterModifications dst) =>
            limits => dst.Coordinates = (CharaLimit.None != (limits & CharaLimit.Coorde)) ? src.Coordinates : dst.Coordinates;
        private static void Merge(this CoordLimit limits, CoordinateModifications src, CoordinateModifications dst) =>
            limits.With(MergeHair(src, dst)).With(MergeClothes(src, dst)).With(MergeAccessory(src, dst));
        private static Action<CoordLimit> MergeHair(CoordinateModifications src, CoordinateModifications dst) =>
            limits => dst.Hair = (CoordLimit.None != (limits & CoordLimit.Hair)) ? src.Hair : dst.Hair;
        private static Action<CoordLimit> MergeClothes(CoordinateModifications src, CoordinateModifications dst) =>
            limits => dst.Clothes = (CoordLimit.None != (limits & CoordLimit.Clothes)) ? src.Clothes : dst.Clothes;
        private static Action<CoordLimit> MergeAccessory(CoordinateModifications src, CoordinateModifications dst) =>
            limits => dst.Accessory = (CoordLimit.None != (limits & CoordLimit.Accessory)) ? src.Accessory : dst.Accessory;
        internal static void Initialize()
        {
            Util.Hook<HumanCustom>(() => CustomSceneMods = new(), () => CustomSceneMods = new());
            Util.Hook<SV.SimulationScene>(() => { }, Actors.Clear);
        }
    }
    internal static class UIRef
    {
        internal static Transform Window = SV.Config.ConfigWindow.Instance.transform.Find("Canvas").Find("Background").Find("MainWindow");
        internal static Transform Content(this Transform tf) =>
            tf.Find("Settings").Find("Scroll View").Find("Viewport").Find("Content");
        internal static Transform UIRoot => HumanCustom.Instance.gameObject.transform.Find("UI").Find("Root");
        internal static GameObject Text =>
            Window.Content().Find("CameraSetting").Find("Content").Find("Look").Find("Title").gameObject;
        internal static GameObject Input =>
            HumanCustom.Instance.StateMiniSelection.transform.Find("Window").Find("StateWindow")
                .Find("Pose").Find("PosePtn").Find("Layout").Find("ptnSelect").Find("InputField_Integer").gameObject;
        internal static GameObject Color =>
            HumanCustom.Instance.StateMiniSelection.transform
                .Find("Window").Find("StateWindow").Find("Light").Find("grpLighting").Find("colorLight").gameObject;
        internal static GameObject Check =>
            HumanCustom.Instance.StateMiniSelection.transform
                .Find("Window").Find("StateWindow").Find("Clothes").Find("tglVisibleGroup").Find("imgTglCol00").gameObject;
        internal static GameObject Slider =>
            Window.Content().Find("CameraSetting").Find("Content").Find("SensitivityX").Find("Slider").gameObject;
        internal static GameObject Button =>
            HumanCustom.Instance.CustomCharaFile.FileWindow.CharaCategory._btnSelect.gameObject;
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
            UnityEngine.Object.Instantiate(UIRef.Check, go.transform).With(check =>
            {
                check.AddComponent<LayoutElement>().preferredWidth = 200;
                check.transform.Find("cb_back").GetComponent<RectTransform>().With(ui =>
                {
                    ui.anchorMin = new(0.0f, 1.0f);
                    ui.anchorMax = new(0.0f, 1.0f);
                    ui.offsetMin = new(0.0f, -24.0f);
                    ui.offsetMax = new(24.0f, 0.0f);
                });
                check.GetComponentInChildren<TextMeshProUGUI>().With(ui =>
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

        internal static Action<GameObject> NumericEdit(this TMP_InputField.ContentType type, string desc) => go =>
        {
            UnityEngine.Object.Instantiate(Text, go.transform).With(label =>
            {
                label.AddComponent<LayoutElement>().preferredWidth = 80;
                label.GetComponent<TextMeshProUGUI>().SetText(desc);
            });
            UnityEngine.Object.Instantiate(Input, go.transform).With(input =>
            {
                input.AddComponent<LayoutElement>().preferredWidth = 120;
                input.GetComponent<TMP_InputField>().With(ui =>
                {
                    ui.contentType = type;
                    ui.characterLimit = 10;
                    ui.restoreOriginalTextOnEscape = true;
                });
            });
        };
        internal static void RangeEdit(this GameObject go)
        {
            UnityEngine.Object.Instantiate(Text, go.transform).With(label =>
             {
                 label.AddComponent<LayoutElement>().preferredWidth = 100;
                 label.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;
             });
            UnityEngine.Object.Instantiate(Slider, go.transform)
                .AddComponent<LayoutElement>().preferredWidth = 100;
        }
        internal static void ColorEdit(this GameObject go)
        {
            UnityEngine.Object.Instantiate(Color, go.transform)
                .AddComponent<LayoutElement>().preferredWidth = 100;
        }
        internal static void VectorEdit(this GameObject go)
        {
            new GameObject("VectorValues").With(go.transform.Wrap).With(FitLayout<VerticalLayoutGroup>).With(vs =>
            {
                vs.GetComponent<LayoutElement>().preferredWidth = 230;
                new GameObject("x").With(vs.transform.Wrap).With(FitLayout<HorizontalLayoutGroup>)
                    .With(TMP_InputField.ContentType.DecimalNumber.NumericEdit("flaot(x)"));
                new GameObject("y").With(vs.transform.Wrap).With(FitLayout<HorizontalLayoutGroup>)
                    .With(TMP_InputField.ContentType.DecimalNumber.NumericEdit("flaot(y)"));
                new GameObject("z").With(vs.transform.Wrap).With(FitLayout<HorizontalLayoutGroup>)
                    .With(TMP_InputField.ContentType.DecimalNumber.NumericEdit("flaot(z)"));
                new GameObject("w").With(vs.transform.Wrap).With(FitLayout<HorizontalLayoutGroup>)
                    .With(TMP_InputField.ContentType.DecimalNumber.NumericEdit("flaot(w)"));
            });
        }
        internal static void TextureEdit(this GameObject go)
        {
            new GameObject("TextureValues").With(go.transform.Wrap).With(FitLayout<VerticalLayoutGroup>).With(vt =>
            {
                UnityEngine.Object.Instantiate(Text, vt.transform).With(label =>
                {
                    label.AddComponent<LayoutElement>().preferredWidth = 200;
                    label.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;
                });
                new GameObject("Buttons").With(vt.transform.Wrap).With(FitLayout<HorizontalLayoutGroup>).With(hr =>
                {
                    UnityEngine.Object.Instantiate(Button, hr.transform).With(button =>
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
                        button.GetComponent<Button>().interactable = true;
                    });
                    UnityEngine.Object.Instantiate(Button, hr.transform).With(button =>
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
                        button.GetComponent<Button>().interactable = true;
                    });
                });
            });
        }
        internal static GameObject Title { get; set; }
        internal static GameObject IntEditBase { get; set; }
        internal static GameObject FloatEditBase { get; set; }
        internal static GameObject RangeEditBase { get; set; }
        internal static GameObject ColorEditBase { get; set; }
        internal static GameObject VectorEditBase { get; set; }
        internal static GameObject TextureEditBase { get; set; }

        internal static void Setup()
        {
            Title = UnityEngine.Object.Instantiate(Window.Content().Find("CameraSetting").Find("imgTitle").gameObject);
            IntEditBase = new GameObject("IntEditBase").With(FitLayout<HorizontalLayoutGroup>)
                .With(Label).With(TMP_InputField.ContentType.IntegerNumber.NumericEdit("int:"));
            FloatEditBase = new GameObject("IntEditBase").With(FitLayout<HorizontalLayoutGroup>)
                .With(Label).With(TMP_InputField.ContentType.IntegerNumber.NumericEdit("int:"));
            RangeEditBase = new GameObject("RangeEditBase")
                .With(FitLayout<HorizontalLayoutGroup>).With(Label).With(RangeEdit);
            ColorEditBase = new GameObject("ColorEditBase")
                .With(FitLayout<HorizontalLayoutGroup>).With(Label).With(ColorEdit);
            VectorEditBase = new GameObject("VectorEditBase")
                .With(FitLayout<HorizontalLayoutGroup>).With(Label).With(VectorEdit);
            TextureEditBase = new GameObject("TextureEditBase")
                .With(FitLayout<HorizontalLayoutGroup>).With(Label).With(TextureEdit);
        }
        internal static void Dispose()
        {
            UnityEngine.Object.Destroy(Title);
            UnityEngine.Object.Destroy(IntEditBase);
            UnityEngine.Object.Destroy(FloatEditBase);
            UnityEngine.Object.Destroy(RangeEditBase);
            UnityEngine.Object.Destroy(ColorEditBase);
            UnityEngine.Object.Destroy(VectorEditBase);
            UnityEngine.Object.Destroy(TextureEditBase);
        }
        internal static void Initialize() => Util.Hook<HumanCustom>(Setup, Dispose);
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
        }
        internal static void UpdateContent(this IEnumerable<MaterialHandle> handles) =>
            (handles.Count() > 0).Either(Hide, () => ContentRoot.With(Hide)
                .With(Cleanup).With(tf => handles.Do(handle => tf.View(handle))).With(Show));
        internal static void View(this Transform root, MaterialHandle handle) =>
            root.With(handle.Label.Title).With(handle.Value.name.Title)
                .With(() => handle.Handles.Values.Do(action => action(root)));
        internal static void Title(this string name, Transform root) =>
            UnityEngine.Object
                .Instantiate(UIRef.Title, root)
                .GetComponentInChildren<TextMeshProUGUI>().SetText($"{name}");

        internal static Action<GameObject> CommonEdit(this string name, bool state, Action<bool> toggle) => go =>
        {
            go.GetComponentInChildren<TextMeshProUGUI>().SetText(name);
            go.GetComponentInChildren<Toggle>().With(ui =>
            {
                ui.onValueChanged.AddListener(toggle);
                ui.isOn = state;
            });
        };
        internal static void IntEdit(this GameObject go, Func<int> get, Action<int> set)
        {
            go.GetComponentInChildren<TMP_InputField>().With(ui =>
            {
                ui.SetText(get().ToString());
                ui.onValueChanged.AddListener((Action<string>)(value => set(int.TryParse(value, out int result) ? result : 0)));
            });
        }
        internal static void FloatEdit(this GameObject go, Func<float> get, Action<float> set)
        {
            go.GetComponentInChildren<TMP_InputField>().With(ui =>
            {
                ui.SetText(get().ToString());
                ui.onValueChanged.AddListener((Action<string>)(value => set(float.TryParse(value, out float result) ? result : 0.0f)));
            });
        }
        internal static void RangeEdit(this GameObject go, Func<float> get, Action<float> set, Vector2 range)
        {
            go.GetComponentsInChildren<TextMeshProUGUI>()[^1].With(ui =>
            {
                ui.SetText(get().ToString());
                set += value => ui.SetText(value.ToString());
            });
            go.GetComponentInChildren<Slider>().With(ui =>
            {
                ui.value = get();
                ui.minValue = range.x;
                ui.maxValue = range.y;
                ui.onValueChanged.AddListener(set);
            });
        }
        internal static void ColorEdit(this GameObject go, Func<Color> get, Action<Color> set)
        {
            go.GetComponentInChildren<ThumbnailColor>()
                .Initialize(go.name, get, (Func<Color, bool>)(color => true.With(() => set(color))), true, true);
        }
        internal static void VectorEdit(this GameObject go, Func<Vector4> get, Action<Vector4> set)
        {
            new List<Tuple<Func<string>, Action<string>>>() {
                new (() => get().x.ToString(), (x) => set(new(float.Parse(x), get().y, get().z, get().w))),
                new (() => get().y.ToString(), (y) => set(new(get().x, float.Parse(y), get().z, get().w))),
                new (() => get().z.ToString(), (z) => set(new(get().x, get().y, float.Parse(z), get().w))),
                new (() => get().w.ToString(), (w) => set(new(get().x, get().y, get().z, float.Parse(w)))),
            }.Zip(go.GetComponentsInChildren<TMP_InputField>()).Do(item =>
            {
                item.Item2.SetText(item.Item1.Item1());
                item.Item2.onSubmit.AddListener(item.Item1.Item2);
            });
        }
        internal static void TextureEdit(this GameObject go, Func<Texture> get, Action<Texture> set)
        {
            go.GetComponentsInChildren<TextMeshProUGUI>()[1].With(ui =>
            {
                ui.SetText(get()?.name ?? "");
                set += value => ui.SetText(value?.name ?? "");
            });
            go.GetComponentsInChildren<Button>()[0].onClick.AddListener(ImportTexture(get, set));
            go.GetComponentsInChildren<Button>()[1].With(ui =>
            {
                ui.interactable = get() != null;
                ui.onClick.AddListener(ExportTexture(get));
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
        internal static Il2CppSystem.Threading.CancellationTokenSource Canceler = new();
        internal static UniTask RefreshTask = UniTask.CompletedTask;
        internal static ConfigEntry<KeyboardShortcut> Toggle { get; set; }
        internal static ConfigEntry<bool> Status { get; set; }
        internal static void Refresh() => Status.Value.Either(Hide, RefreshOperation);
        internal static void CancelRefresh() =>
            (!RefreshTask.Status.IsCompleted()).Maybe(Canceler.Cancel);
        internal static void ScheduleRefresh() =>
            RefreshTask.Status.IsCompleted().Maybe(() =>
            {
                RefreshTask = UniTask.NextFrame().ContinueWith((Action)Refresh);
            });
        internal static HandlePaths Current => new HandlePaths(HumanCustom.Instance.Human);
        internal static void UpdateContent(int category, int index) =>
             (RefreshOperation = (category, index) switch
             {
                 (0, 1) => () => UpdateContent(Current.Eyebrows),
                 (0, 3) => () => UpdateContent(Current.Eyelines),
                 (0, 4) => () => UpdateContent(Current.Eyes),
                 (0, 5) => () => UpdateContent(Current.Tooth),
                 (0, _) => () => UpdateContent(Current.Face),
                 (1, _) => () => UpdateContent(Current.Body),
                 (2, 0) => () => UpdateContent(Current.Hair(0)),
                 (2, 1) => () => UpdateContent(Current.Hair(1)),
                 (2, 2) => () => UpdateContent(Current.Hair(2)),
                 (2, 3) => () => UpdateContent(Current.Hair(3)),
                 (3, _) => () => UpdateContent(Current.Clothes(index)),
                 (4, _) => () => UpdateContent(Current.Accessory(index)),
                 _ => Hide
             }).With(CancelRefresh).With(ScheduleRefresh);
        internal static Action InputCheck = () =>
            Toggle.Value.IsDown().Maybe(() => (Status.Value = !Status.Value).With(ScheduleRefresh));
        internal static void Setup()
        {
            RefreshOperation = Hide;
            Canvas.preWillRenderCanvases += InputCheck;
            ContentRoot = new GameObject(Plugin.Name).With(UIRef.UIRoot.Wrap).UI().With(ScheduleRefresh);
        }
        internal static void Dispose()
        {
            Canvas.preWillRenderCanvases -= InputCheck;
            Canceler.Cancel();
        }
        internal static void Initialize() => Util.Hook<HumanCustom>(Setup, Dispose);
    }
    internal static partial class TextureExtension
    {
        private static Dictionary<string, byte[]> Binaries = new();
        private static string Hash(this byte[] input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return Convert.ToHexString(sha256.ComputeHash(input)).With(hash => Binaries.TryAdd(hash, input));
            }
        }
        internal static bool IsExtension(this Texture value) => Binaries.ContainsKey(value.name);
        internal static RenderTexture ToTexture(this string value) => Binaries[value].Import();
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
    }
    internal static class Hooks
    {
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.CreateBodyTexture))]
        internal static void HumanBodyCreateBodyTexturePostfix(HumanBody __instance) =>
            __instance.ToCtcHandles(__instance.human.Modifications()).Do(handle => handle.Apply());
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.CreateFaceTexture))]
        internal static void HumanFaceCreateFaceTexturePostfix(HumanFace __instance) =>
            __instance.ToCtcHandles(__instance.human.Modifications()).Do(handle => handle.Apply());
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.CreateClothesTexture))]
        internal static void HumanClothCreateClothesTexturePostfix(HumanCloth __instance, int kind) =>
            __instance.clothess[kind].ToCtcHandles(__instance.human.Coordinate().Clothes[kind]).Do(handle => handle.Apply());
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CustomTextureCreate), nameof(CustomTextureCreate.RebuildTextureAndSetMaterial))]
        internal static void HumanSetRenderMaterialsPostfix(CustomTextureCreate __instance) =>
            __instance._trfParent.GetComponent<HumanComponent>().Human.With(human =>
                ((__instance._matCreate.name.Contains("body_create"), __instance._matCreate.name.Contains("head_create")) switch
                {
                    (true, false) => human.body.ToHandles(human.Modifications()),
                    (false, true) => human.face.ToHandles(human.Modifications()),
                    _ => []
                }).Do(handle => handle.Apply()));
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanHair.Hair), nameof(HumanHair.Hair.SetShader))]
        internal static void HumanHairSetShaderPostfix(HumanHair.Hair __instance) =>
            ((ChaListDefine.CategoryNo)__instance.infoHair.Category switch
            {
                ChaListDefine.CategoryNo.bo_hair_b => 0,
                ChaListDefine.CategoryNo.bo_hair_f => 1,
                ChaListDefine.CategoryNo.bo_hair_o => 2,
                ChaListDefine.CategoryNo.bo_hair_s => 3,
                _ => -1
            }).With(index => (index >= 0).Maybe(() =>
                __instance.objHair.GetComponentInParent<HumanComponent>()
                    .Human.Coordinate().Hair[index].ToHandles(__instance.renderers)
                    .Do(handle => handle.Apply())));
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth.Clothes), nameof(HumanCloth.Clothes.SetShader))]
        internal static void HumanClothSetShaderPostfix(HumanCloth.Clothes __instance) =>
            ((ChaListDefine.CategoryNo)(__instance?.infoClothes?.Category ?? 999) switch
            {
                ChaListDefine.CategoryNo.co_top => 0,
                ChaListDefine.CategoryNo.co_bot => 1,
                ChaListDefine.CategoryNo.co_bra => 2,
                ChaListDefine.CategoryNo.co_shorts => 3,
                ChaListDefine.CategoryNo.co_gloves => 4,
                ChaListDefine.CategoryNo.co_panst => 5,
                ChaListDefine.CategoryNo.co_socks => 6,
                ChaListDefine.CategoryNo.co_shoes => 7,
                _ => -1
            }).With(index => (index >= 0).Maybe(() =>
                __instance.cusClothesCmp.ToHandles(__instance.objClothes
                    .GetComponentInParent<HumanComponent>().Human.Coordinate().Clothes[index])
                    .Do(handle => handle.Apply())));
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanAccessory), nameof(HumanAccessory.SetupAccessoryFK), typeof(int))]
        internal static void HumanAccessorySetupAccessoryFKPostfix(HumanAccessory __instance, int slotNo) =>
            __instance.human.Coordinate().Accessory[slotNo].ToHandles(__instance?.accessories[slotNo].renderers).Do(handle => handle.Apply());
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CategorySelection), nameof(CategorySelection.OpenView), typeof(int))]
        internal static void CategorySelectionSetPostfix(CategorySelection __instance, int index) => UIFactory.UpdateContent(__instance._no, index);
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(ThumbnailButton), nameof(ThumbnailButton.Set))]
        internal static void ThumbnailButtonOpenPostfix() => UIFactory.ScheduleRefresh();
    }
    [BepInProcess(Process)]
    [BepInDependency(Fishbone.Plugin.Guid)]
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        internal static Plugin Instance;
        public const string Process = "SamabakeScramble";
        public const string Name = "SardineHead";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "1.0.1";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(() => Instance = this)
                .With(UIRef.Initialize)
                .With(UIFactory.Initialize)
                .With(ModificationExtension.Initialize)
                .With(TextureExtension.Initialize)
                .With(ListenOnCharacterCreationSerialize)
                .With(ListenOnCharacterCreationDeserialize)
                .With(ListenOnCoordinateSerialize)
                .With(ListenOnCoordinateInitialize)
                .With(ListenOnCoordinateDeserialize)
                .With(ListenOnActorSerialize)
                .With(ListenOnActorDeserialize)
                .With(ConfigToggle)
                .With(ConfigStatus);
        private void ListenOnCharacterCreationSerialize() =>
            Event.OnCharacterCreationSerialize += (archive) => archive.Serialize();
        private void ListenOnCharacterCreationDeserialize() =>
            Event.OnCharacterCreationDeserialize += (limit, archive) => archive.Deserialize(limit);
        private void ListenOnCoordinateSerialize() =>
            Event.OnCoordinateSerialize += (archive) => archive.SerializeCoordinate();
        private void ListenOnCoordinateInitialize() =>
            Event.OnCoordinateInitialize += (index, archive) => archive.Deserialize(index);
        private void ListenOnCoordinateDeserialize() =>
            Event.OnCoordinateDeserialize += (human, limits, archive) => archive.DeserializeCoordinate(human, limits);
        private void ListenOnActorSerialize() =>
            Event.OnActorSerialize += (index, archive) => archive.Serialize(index);
        private void ListenOnActorDeserialize() =>
            Event.OnActorDeserialize += (index, archive) => archive.Deserialize(index);
        private void ConfigToggle() => UIFactory.Toggle = Config
            .Bind("General", "Sardin Head toggle key", new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl));
        private void ConfigStatus() => UIFactory.Status = Config
            .Bind("General", "show Sardin Head (smells fishy)", true);
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}