using HarmonyLib;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using System;
using System.IO;
using System.Linq;
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
using System.Text.Json;
using SaveFlgs = Character.HumanData.SaveFileInfo.Flags;
using LoadFlgs = Character.HumanData.LoadFileInfo.Flags;
namespace SardineHead
{
    internal delegate void Either(Action a, Action b);
    internal delegate void Either<A, B>(Action<A> a, Action<B> b);
    internal static class FunctionalExtension
    {
        internal static Either Either(bool value) => value ? (left, right) => right() : (left, right) => left();
        internal static void Either(this bool value, Action left, Action right) => Either(value)(left, right);
        internal static void Maybe(this bool value, Action maybe) => value.Either(() => { }, maybe);
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
    public struct Quad
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
        internal static string Save(this Texture value) => Binaries.ContainsKey(value.name) ? value.name : null;
        internal static RenderTexture Load(this string value) =>
            Binaries.GetValueOrDefault(value)?.Import() ?? TexturePath(value).Import();
        internal static RenderTexture Import(this string path) => File.ReadAllBytes(path).Import();
        private static RenderTexture Import(this byte[] input) =>
            new Texture2D(256, 256)
                .With(t2d => ImageConversion.LoadImage(t2d, input))
                .Import().With(texture => texture.name = Hash(input));
        private static RenderTexture Import(this Texture2D input) =>
            new RenderTexture(input.width, input.height, 0).With(tex => Graphics.Blit(input, tex));
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
    internal class MaterialHandle
    {
        internal string Label { get; init; }
        internal Material Value { get; init; }
        internal Dictionary<string, Action<GameObject>> Handles { get; init; } = new();
        internal Action<Modifications> Load;
        internal Action<Modifications> Save;
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
                        .With(() => Save += mods => mods.IntValues[name] = material.GetInt(id))
                        .With(() => Load += mods => mods.IntValues.ContainsKey(name).Maybe(() => Set(id, mods.IntValues[name]))),
                ShaderPropertyType.Float =>
                    Handles.TryAdd(name, go => go.NumericEdit("float:", () => material.GetFloat(id).ToString(), input => Set(id, float.Parse(input)), TMP_InputField.ContentType.DecimalNumber))
                        .With(() => Save += mods => mods.FloatValues[name] = material.GetInt(id))
                        .With(() => Load += mods => mods.FloatValues.ContainsKey(name).Maybe(() => Set(id, mods.FloatValues[name]))),
                ShaderPropertyType.Range =>
                    Handles.TryAdd(name, go => go.RangeEdit(() => material.GetFloat(id), input => Set(id, input), limits()))
                        .With(() => Save += mods => mods.RangeValues[name] = material.GetFloat(id))
                        .With(() => Load += mods => mods.RangeValues.ContainsKey(name).Maybe(() => Set(id, mods.RangeValues[name]))),
                ShaderPropertyType.Color =>
                    Handles.TryAdd(name, go => go.ColorEdit(() => material.GetColor(id), (input) => Set(id, input)))
                        .With(() => Save += mods => mods.ColorValues[name] = material.GetColor(id))
                        .With(() => Load += mods => mods.ColorValues.ContainsKey(name).Maybe(() => Set(id, (Color)mods.ColorValues[name]))),
                ShaderPropertyType.Vector =>
                    Handles.TryAdd(name, go => go.VectorEdit(() => material.GetVector(id), (input) => Set(id, input)))
                        .With(() => Save += mods => mods.VectorValues[name] = material.GetVector(id))
                        .With(() => Load += mods => mods.VectorValues.ContainsKey(name).Maybe(() => Set(id, (Vector4)mods.VectorValues[name]))),
                ShaderPropertyType.Texture =>
                    Handles.TryAdd(name, go => go.TextureEdit(() => material.GetTexture(id), (input) => Set(id, input)))
                        .With(() => Save += mods => material.GetTexture(id)?.Save()?.With(hash => mods.TextureHashes[name] = hash))
                        .With(() => Load += mods => mods.TextureHashes.ContainsKey(name).Maybe(() => Set(id, mods.TextureHashes[name].Load()))),
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
        internal void Save() => Save(Target.Modifications());
        internal void Serialize(string name) => Target.With(Save).Serialize(name);
        private void Save(CharacterModifications mods)
        {
            mods.Face = Face.ToDictionary(item => item.Label, Save);
            mods.Eyebrows = Eyebrows.ToDictionary(item => item.Label, Save);
            mods.Eyelines = Eyelines.ToDictionary(item => item.Label, Save);
            mods.Eyes = Eyes.ToDictionary(item => item.Label, Save);
            mods.Tooth = Tooth.ToDictionary(item => item.Label, Save);
            mods.Body = Body.ToDictionary(item => item.Label, Save);
            mods.Nails = Nails.ToDictionary(item => item.Label, Save);
            Save(mods.Coorinates[Target.data.Status.coordinateType]);
        }
        private void Save(CoordinateModifications mods)
        {
            Enumerable.Range(0, 4).Do(index => mods.Hair[index] = Hair(index).ToDictionary(item => item.Label, Save));
            Enumerable.Range(0, 8).Do(index => mods.Clothes[index] = Clothes(index).ToDictionary(item => item.Label, Save));
            Enumerable.Range(0, 20).Do(index => mods.Accessory[index] = Accessory(index).ToDictionary(item => item.Label, Save));
        }
        private Modifications Save(MaterialHandle handle) => new Modifications().With(handle.Save);
        internal void Deserialize(string name) =>
            Target.With(() => UniTask.NextFrame().ContinueWith((Action)Load)).Deserialize(name);
        internal void Load() => Load(Target.Modifications());
        private void Load(CharacterModifications mods)
        {
            Load(Face, mods.Face);
            Load(Eyebrows, mods.Eyebrows);
            Load(Eyelines, mods.Eyelines);
            Load(Eyes, mods.Eyes);
            Load(Tooth, mods.Tooth);
            Load(Body, mods.Body);
            Load(Nails, mods.Nails);
            Load(mods.Coorinates[Target.data.Status.coordinateType]);
        }
        private void Load(CoordinateModifications mods)
        {
            mods.Hair.Do(entry => Load(Hair(entry.Key), entry.Value));
            mods.Clothes.Do(entry => Load(Clothes(entry.Key), entry.Value));
            mods.Accessory.Do(entry => Load(Accessory(entry.Key), entry.Value));
        }
        private void Load(IEnumerable<MaterialHandle> handles, Dictionary<string, Modifications> mods) =>
            mods.Do(entry => handles.Where(handle => handle.Label.Equals(entry.Key)).FirstOrDefault()?.Load(entry.Value));
        internal void RestoreDefaults() => Load(Target.RestoreDefaults());
    }
    internal static class ModificationExtension
    {
        private static Dictionary<Human, CharacterModifications> Instances = new();
        internal static CharacterModifications Modifications(this Human human) => Instances[human];
        internal static void Register(this Human human) => Instances.TryAdd(human, new CharacterModifications());
        internal static void Dispose() => Instances.Clear();
        internal static IEnumerable<Modifications> Aggregate(this CharacterModifications mods) =>
            mods.Coorinates.Values.SelectMany(Aggregate)
                .Concat(mods.Face.Values).Concat(mods.Eyebrows.Values).Concat(mods.Eyelines.Values)
                .Concat(mods.Eyes.Values).Concat(mods.Tooth.Values).Concat(mods.Body.Values).Concat(mods.Nails.Values);
        internal static IEnumerable<Modifications> Aggregate(this CoordinateModifications mods) =>
            mods.Hair.Values.SelectMany(values => values.Values)
                .Concat(mods.Clothes.Values.SelectMany(values => values.Values))
                .Concat(mods.Accessory.Values.SelectMany(values => values.Values));
        internal static string Gender(this Human human) => human.sex == 0 ? "male" : "female";
        private static string CharacterPath(this Human human, string name) =>
            Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, "chara", human.Gender(), $"{name}.json");
        private static string DefaultsPath(this Human human) =>
            Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, "default", $"{human.Gender()}.json");
        private static void Serialize(CharacterModifications mods) =>
            mods.Aggregate().SelectMany(mod => mod.TextureHashes.Values).Do(TextureExtension.Serialize);
        internal static void Serialize(this Human human, string path) =>
            File.WriteAllText(human.CharacterPath(path),
                JsonSerializer.Serialize(Instances[human].With(Serialize), new JsonSerializerOptions { WriteIndented = true }));
        internal static CharacterModifications Deserialize(this Human human, string path) =>
            Instances[human] = File.Exists(human.CharacterPath(path)) ?
                JsonSerializer.Deserialize<CharacterModifications>(File.ReadAllText(human.CharacterPath(path))) : new CharacterModifications();
        internal static CharacterModifications RestoreDefaults(this Human human) =>
            Instances[human] = JsonSerializer.Deserialize<CharacterModifications>(File.ReadAllText(human.DefaultsPath()));
        internal static void InitializeModifications(this Human human) =>
            (!Instances.ContainsKey(human)).Maybe(() => Deserialize(human, human.data.CharaFileName));
        internal static void Delete(string name) =>
            File.Delete(UIFactory.Current.Target.CharacterPath(name));
    }
    public class CharacterModifications
    {
        public Dictionary<string, Modifications> Face { get; set; } = new();
        public Dictionary<string, Modifications> Eyebrows { get; set; } = new();
        public Dictionary<string, Modifications> Eyelines { get; set; } = new();
        public Dictionary<string, Modifications> Eyes { get; set; } = new();
        public Dictionary<string, Modifications> Tooth { get; set; } = new();
        public Dictionary<string, Modifications> Body { get; set; } = new();
        public Dictionary<string, Modifications> Nails { get; set; } = new();
        public Dictionary<int, CoordinateModifications> Coorinates { get; set; } = new() { { 0, new() }, { 1, new() }, { 2, new() } };
    }
    public class CoordinateModifications
    {
        public Dictionary<int, Dictionary<string, Modifications>> Hair { get; set; } = new();
        public Dictionary<int, Dictionary<string, Modifications>> Clothes { get; set; } = new();
        public Dictionary<int, Dictionary<string, Modifications>> Accessory { get; set; } = new();
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
        internal static GameObject Import
        {
            get => HumanCustom.Instance.transform.Find("UI").Find("Root").Find("Cvs_Category_FileList")
                .GetChild(0).Find("CustomFileWindow").Find("BasePanel").Find("WinRect").Find("Load").Find("btnLoad").gameObject;
        }
        internal static GameObject Export
        {
            get => HumanCustom.Instance.transform.Find("UI").Find("Root").Find("Cvs_Category_FileList")
                .GetChild(0).Find("CustomFileWindow").Find("BasePanel").Find("WinRect").Find("Save").Find("btnSave").gameObject;
        }
        internal static Toggle Casual
        {
            get => HumanCustom.Instance.transform.Find("UI").Find("Root").Find("Cvs_Coorde")
                    .Find("BG").Find("CoordinateType").Find("01_Plain").GetComponent<Toggle>();
        }
        internal static Toggle Job
        {
            get => HumanCustom.Instance.transform.Find("UI").Find("Root").Find("Cvs_Coorde")
                .Find("BG").Find("CoordinateType").Find("02_Roomwear").GetComponent<Toggle>();
        }
        internal static Toggle Swim
        {
            get => HumanCustom.Instance.transform.Find("UI").Find("Root").Find("Cvs_Coorde")
                .Find("BG").Find("CoordinateType").Find("03_Bathing").GetComponent<Toggle>();
        }
    }
    internal static class UIFactory
    {
        internal static void Wrap(this Transform tf, GameObject go) => go.transform.SetParent(tf);
        internal static Transform ContentRoot;
        internal static void Cleanup(Transform tf) =>
            Enumerable.Range(0, tf.childCount).Select(tf.GetChild)
                .Select(tf => tf.gameObject).Do(UnityEngine.Object.Destroy);
        internal static Transform UI(this GameObject go, Transform canvas)
        {
            go.transform.SetParent(canvas);
            go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().With(ui =>
            {
                ui.referenceResolution = new(1920, 1080);
                ui.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                ui.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            });
            go.AddComponent<GraphicRaycaster>();
            go.GetComponent<RectTransform>();
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
        internal static void Refresh() => RefreshOperation();
        internal static void CancelRefresh() =>
            (!RefreshTask.Status.IsCompleted()).Maybe(RefreshCanceler.Cancel);
        internal static void ScheduleRefresh() =>
            RefreshTask.Status.IsCompleted().Maybe(() =>
            {
                RefreshTask = UniTask.NextFrame().ContinueWith((Action)Refresh);
            });
        internal static HandlePaths Current { get => Human.list.TryGet(0, out Human human) ? new HandlePaths(human) : null; }
        internal static Action<bool> ReserveCoordinate = (bool _) =>
            Current.With(() => UniTask.NextFrame().ContinueWith((Action)(() => Current?.Load())))?.Save();
        internal static void Listening()
        {
            UIRef.Casual.onValueChanged.AddListener(ReserveCoordinate);
            UIRef.Job.onValueChanged.AddListener(ReserveCoordinate);
            UIRef.Swim.onValueChanged.AddListener(ReserveCoordinate);
        }
        internal static Action Initialize = () =>
            ContentRoot = new GameObject(Plugin.Name)
                .UI(Scene.ActiveScene.GetRootGameObjects()
                .Where(item => "CustomScene".Equals(item.name))
                .First().transform.Find("UI").Find("Root"))
                .With(Listening).With(CancelRefresh).With(ScheduleRefresh);
        internal static Action Dispose = () =>
        {
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
            ApplyModifications = human => HandlePaths.Of(human).Load(),
            InitializeModifications = human => human.InitializeModifications()
        };
        internal static HookImpl CharacterCreation = new HookImpl() {
            LoadPrefix = (data, flags) =>
                ((flags & LoadFlgs.Fusion) != LoadFlgs.None)
                    .Maybe(() => UIFactory.Current?.RestoreDefaults()),
            LoadPostfix = (data, flags) =>
                ((flags & LoadFlgs.Fusion) != LoadFlgs.None)
                   .Maybe(() => UIFactory.Current?.Deserialize(data.CharaFileName)),
            SavePostfix = (data, flags) =>
                UIFactory.Current?.Serialize(data.CharaFileName)
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
        public const string Process = "SamabakeScramble";
        public const string Name = "SardineHead";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "0.2.0";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks");
        public override bool Unload() =>
            true.With(() => Patch.UnpatchSelf()) && base.Unload();
    }
}