using HarmonyLib;
using BepInEx;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using UniRx;
using Cysharp.Threading.Tasks;
using ILLGames.Unity.UI.ColorPicker;
using ImportDialog = System.Windows.Forms.OpenFileDialog;
using ExportDialog = System.Windows.Forms.SaveFileDialog;
using CoastalSmell;
using ILLGames.Extensions;

namespace SardineHead
{
    internal static class UI
    {
        internal static GameObject Window(WindowHandle handle) =>
            UGUI.Window(856, 824, Plugin.Name, handle)
                .With(UGUI.Cmp(UGUI.ToggleGroup(allowSwitchOff: false)))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>()));
        internal static Func<GameObject, Tuple<Transform, Transform>> Panels =
            go => new(
                UGUI.ScrollView(328, 812, "Menus", go).With(ScrollView).transform,
                UGUI.ScrollView(528, 812, "Edits", go).With(ScrollView).transform);
        static Action<GameObject> ScrollView = go => go
            .With(UGUI.Cmp(UGUI.Fitter()))
            .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>(padding: new(6, 6, 6, 6))));
        internal static Action<string, GameObject> Section =
            (name, parent) => UGUI.Section(300, 24, name, new(0.3f, 0.3f, 0.3f, 0.8f), parent);
        internal static Func<string, GameObject, GameObject> Toggle =
            (name, parent) => UGUI.Toggle(300, 24, name, parent);
        internal static Action<int, string, GameObject> Label =
            (size, name, parent) => UGUI.Label(size, 24, name, parent);
        internal static Action<TMP_InputField.ContentType, GameObject> Input =
            (type, parent) => UGUI.Input(120, 24, "Input", parent)
                .With(UGUI.Cmp(UGUI.InputField(contentType: type)));
        internal static Action<string, GameObject> Check =
            (name, parent) => UGUI.Check(24, 24, name, parent);
        internal static Action<GameObject> Color =
            parent => UGUI.Color(100, 24, "Color", parent);
        internal static Action<GameObject> Range =
            parent => UGUI.Slider(120, 24, "Range", parent);
        internal static Action<string, GameObject> Button =
            (name, parent) => UGUI.Button(80, 24, name, parent);
        internal static Action<string, GameObject> Choice =
            (name, parent) => UGUI.Choice(220, 24, name, parent);
        internal static readonly string[] ShaderNames = {
            "Hidden/InternalErrorShader",
            "Custom/monoTone",
            "ItohikiShader_URP",
            "Legacy Shaders/VertexLit",
            "Legacy Shaders/Particles/Alpha Blended",
            "Legacy Shaders/Particles/Alpha Blended Premultiply",
            "ProBuilder/UnlitVertexColor",
            "ProBuilder/Standard Vertex Color",
            "LIF/lif_create_main_cloth",
            "LIF/lif_create_main_skin_body",
            "LIF/lif_create_main_skin_head",
            "LIF/lif_main_acs",
            "LIF/lif_main_acs_alpha",
            "LIF/lif_main_acs_low",
            "LIF/lif_main_cloth",
            "LIF/lif_main_cloth_low",
            "LIF/lif_main_cloth_alpha",
            "LIF/lif_main_cloth_alpha_low",
            "LIF/lif_main_cloth_socks",
            "LIF/lif_main_cloth_socks_alpha",
            "LIF/lif_main_eye",
            "LIF/lif_main_eye_low",
            "LIF/lif_main_eyebrow",
            "LIF/lif_main_eyelash_up",
            "LIF/lif_main_eyelid",
            "LIF/lif_main_hair",
            "LIF/lif_main_hair_low",
            "LIF/lif_main_hair_outline",
            "LIF/lif_main_nail",
            "LIF/lif_main_nail_low",
            "LIF/lif_main_skin_body",
            "LIF/lif_main_skin_body_low",
            "LIF/lif_main_skin_head",
            "LIF/lif_main_skin_head_low",
            "LIF/lif_namida",
            "LIF/lif_silhouette",
            "LIF/lif_sub_mnpb_urp",
            "LIF/lif_unlit2d",
            "LIF/MoveAxis",
            "LIF/MoveAxis_Alpha",
            "LIF/sub_texture_animation",
            "lif_shadow_map",
            "Lux URP/FX/Box Volume",
            "Lux URP/FX/Lightbeam",
            "Lux URP/Water",
            "Obi/DistanceFieldSlice",
            "Obi/URP/Fluid/FluidShading",
            "Obi/URP/Particles",
            "Obi/URP/Simple Particles",
            "Obi/Fluid/Colors/FluidColorsBlend",
            "Obi/Fluid/FluidShading",
            "Obi/Simple Particles",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Nature/SpeedTree8",
            "Universal Render Pipeline/Particles/Unlit",
            "Unlit/Color",
            "Unlit/Texture",
            "Unlit/Transparent",
            "Standard (Vertex Colors)",
            "Standard (Specular setup)",
            "Standard",
        };
        internal static ChoiceList ShaderChoices;
        internal static void PrepareChoicesList() => ShaderChoices = new(300, 24, "Shaders", ShaderNames);
    }
    internal abstract class CommonEdit
    {
        protected static GameObject PrepareArchetype(string name, Transform parent) =>
            new GameObject(name).With(UGUI.Go(parent: parent, active: false))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>()))
                .With(UGUI.Cmp(UGUI.Layout(width: 500)))
                .With(UGUI.Cmp(UGUI.Fitter()))
                .With(UI.Check.Apply("Target"))
                .With(UI.Label.Apply(216).Apply("Label"));
        protected MaterialWrapper Wrapper;
        protected GameObject Edit;
        protected Toggle Target;
        CommonEdit(MaterialWrapper wrapper) => Wrapper = wrapper;
        protected CommonEdit(string name, Transform parent, MaterialWrapper wrapper, GameObject archetype) : this(wrapper) =>
            Edit = UnityEngine.Object.Instantiate(archetype, parent)
                .With(UGUI.Go(name: name, active: true))
                .With(UGUI.ModifyAt("Label")(UGUI.Cmp(UGUI.Text(text: name))))
                .With(UGUI.ModifyAt("Background.Target", "Target")(UGUI.Cmp<Toggle>(ui => Target = ui)));
        internal abstract void Store(Modifications mod);
        internal abstract void Apply(Modifications mod);
        internal virtual void Update() => Target.isOn.Either(UpdateGet, UpdateSet);
        protected abstract void UpdateGet();
        protected abstract void UpdateSet();
    }
    internal class ShaderEdit : CommonEdit
    {
        internal static void PrepareArchetype(GameObject parent) =>
            Archetype = PrepareArchetype("ChoiceEdit", parent.transform)
                .With(UI.Choice.Apply("Choice"));
        static GameObject Archetype { get; set; }
        Toggle State;
        TextMeshProUGUI Value;
        internal ShaderEdit(string name, Transform parent, MaterialWrapper wrapper, EditView view) :
            base(name, parent, wrapper, Archetype) => Edit
                .With(UGUI.ModifyAt("Choice", "Choice.State", "Choice.Label")(UGUI.Cmp<TextMeshProUGUI>(ui => Value = ui)))
                .With(UGUI.ModifyAt("Choice")(UGUI.Cmp<Toggle>(ui => State = ui) + UI.ShaderChoices.Assign))
                .With(() => State.OnValueChangedAsObservable().Subscribe(OnValueChanged(view.Populate)));
        Action<bool> OnValueChanged(Action<MaterialWrapper> action) =>
            value => (!value && UI.ShaderNames.Contains(Value.text))
                .Maybe(F.Apply(Wrapper.SetShader, Value.text) + action.Apply(Wrapper));
        internal override void Store(Modifications mod) =>
            Target.isOn.Maybe(() => mod.Shader = Wrapper.GetShader());
        internal override void Apply(Modifications mod) =>
            Target.isOn = (mod.Shader != default).With(F.Apply(Wrapper.SetShader, mod.Shader));
        protected override void UpdateGet() =>
            Value.SetText(Wrapper.GetShader());
        protected override void UpdateSet() =>
            Value.SetText(Wrapper.GetShader());
    }
    internal class RenderingEdit : CommonEdit
    {
        internal static void PrepareArchetype(GameObject parent) =>
            Archetype = PrepareArchetype("BoolEdit", parent.transform)
                .With(UI.Check.Apply("Rendering")).With(UI.Label.Apply(150).Apply("State"));
        static GameObject Archetype { get; set; }
        Toggle Value;
        TextMeshProUGUI Label;
        Action<bool> OnChange => value => Wrapper.Renderer.enabled = value;
        internal RenderingEdit(string name, Transform parent, MaterialWrapper wrapper) :
            base(name, parent, wrapper, Archetype) => Edit
                .With(UGUI.ModifyAt("State")(UGUI.Cmp<TextMeshProUGUI>(ui => Label = ui)))
                .With(UGUI.ModifyAt("Background.Rendering", "Rendering")
                    (UGUI.Cmp<Toggle>(ui => (Value = ui).Set(wrapper.Renderer.enabled, false))))
                .With(() => Value.OnValueChangedAsObservable().Subscribe(OnChange));
        internal override void Store(Modifications mod) =>
            mod.Rendering = (Target.isOn, Value.isOn) switch
            {
                (false, _) => BoolValue.Unmanaged,
                (true, true) => BoolValue.Enabled,
                (true, false) => BoolValue.Disabled
            };
        internal override void Apply(Modifications mod) =>
            Target.isOn = mod.Rendering switch
            {
                BoolValue.Enabled => true.With(F.Apply(Value.Set, true, true)),
                BoolValue.Disabled => true.With(F.Apply(Value.Set, false, true)),
                _ => false
            };
        internal override void Update() =>
            Label.SetText(Wrapper.Renderer.gameObject.activeInHierarchy ? "Enabled(Active)" : "Enabled(Inactive)");
        protected override void UpdateGet() { }
        protected override void UpdateSet() { }
    }
    internal class IntEdit : CommonEdit
    {
        internal static void PrepareArchetype(GameObject parent) =>
            Archetype = PrepareArchetype("IntEdit", parent.transform)
                .With(UI.Label.Apply(100).Apply("int:"))
                .With(UI.Input.Apply(TMP_InputField.ContentType.IntegerNumber));
        static GameObject Archetype { get; set; }
        TMP_InputField Input;
        internal IntEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper, Archetype) =>
            Edit.With(UGUI.ModifyAt("Input")(UGUI.Cmp<TMP_InputField>(ui => (Input = ui).onValueChanged.AddListener(OnChange))));
        Action<string> OnChange =>
            input => int.TryParse(input, out var value).Maybe(Wrapper.SetInt.Apply(Edit.name).Apply(value));
        internal override void Store(Modifications mod) =>
            Target.isOn.Maybe(() => mod.IntValues[Edit.name] = int.TryParse(Input.text, out var value) ? value : Wrapper.GetInt(Edit.name));
        internal override void Apply(Modifications mod) =>
            (Target.isOn = mod.IntValues.TryGetValue(Edit.name, out var value))
                .Maybe(F.Apply(Input.SetText, value.ToString(), false));
        protected override void UpdateGet() =>
            Input.SetTextWithoutNotify(Wrapper.GetInt(Edit.name).ToString());
        protected override void UpdateSet() =>
            int.TryParse(Input.text, out var value).Maybe(Wrapper.SetInt.Apply(Edit.name).Apply(value));
    }
    internal class FloatEdit : CommonEdit
    {
        internal static void PrepareArchetype(GameObject parent) =>
            Archetype = PrepareArchetype("FloatEdit", parent.transform)
                .With(UI.Label.Apply(100).Apply("float:"))
                .With(UI.Input.Apply(TMP_InputField.ContentType.DecimalNumber));
        static GameObject Archetype { get; set; }
        TMP_InputField Input;
        internal FloatEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper, Archetype) =>
            Edit.With(UGUI.ModifyAt("Input")(UGUI.Cmp<TMP_InputField>(ui => (Input = ui).onValueChanged.AddListener(OnChange))));
        Action<string> OnChange =>
            input => float.TryParse(input, out float value).Maybe(Wrapper.SetFloat.Apply(Edit.name).Apply(value));
        internal override void Store(Modifications mod) =>
            Target.isOn.Maybe(() => mod.FloatValues[Edit.name] = float.TryParse(Input.text, out var value) ? value : Wrapper.GetFloat(Edit.name));
        internal override void Apply(Modifications mod) =>
            (Target.isOn = mod.FloatValues.TryGetValue(Edit.name, out var value))
                .Maybe(F.Apply(Input.SetText, value.ToString(), false));
        protected override void UpdateGet() =>
            Input.SetTextWithoutNotify(Wrapper.GetInt(Edit.name).ToString());
        protected override void UpdateSet() =>
            float.TryParse(Input.text, out var value).Maybe(Wrapper.SetFloat.Apply(Edit.name).Apply(value));
    }
    internal class RangeEdit : CommonEdit
    {
        internal static void PrepareArchetype(GameObject parent) =>
            Archetype = PrepareArchetype("RangeEdit", parent.transform).With(UI.Label.Apply(100).Apply("Value")).With(UI.Range);
        static GameObject Archetype { get; set; }
        Slider Range;
        TextMeshProUGUI Value;
        RangeEdit(string name, Transform parent, MaterialWrapper wrapper, Vector2 limits) : base(name, parent, wrapper, Archetype) =>
            Edit.With(UGUI.ModifyAt("Value")(UGUI.Cmp<TextMeshProUGUI>(ui => Value = ui)))
                .With(UGUI.ModifyAt("Range")(UGUI.Cmp<Slider>(ui => (Range = ui).OnValueChangedAsObservable().Subscribe(OnChange))))
                .With(() => (Range.minValue, Range.maxValue) = (limits.x, limits.y));
        internal RangeEdit(string name, Transform parent, MaterialWrapper wrapper) : this(name, parent, wrapper, wrapper.RangeLimits[name]) { }
        Action<float> OnChange => value => Wrapper.SetRange.Apply(Edit.name).Apply(value);
        internal override void Store(Modifications mod) =>
            Target.isOn.Maybe(() => mod.RangeValues[Edit.name] = Range.value);
        internal override void Apply(Modifications mod) =>
            (Target.isOn = mod.RangeValues.TryGetValue(Edit.name, out var value))
                .Maybe(F.Apply(Range.Set, value, false));
        internal override void Update() =>
            Value.With(base.Update).SetText(Range.value.ToString());
        protected override void UpdateGet() =>
            Range.SetValueWithoutNotify(Wrapper.GetRange(Edit.name));
        protected override void UpdateSet() =>
            Wrapper.SetRange(Edit.name, Range.value);
    }
    internal class ColorEdit : CommonEdit
    {
        internal static void PrepareArchetype(GameObject parent) =>
            Archetype = PrepareArchetype("ColorEdit", parent.transform).With(UI.Color);
        static GameObject Archetype { get; set; }
        ThumbnailColor Input;
        internal ColorEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper, Archetype) =>
            Edit.With(UGUI.ModifyAt("Color")(UGUI.Cmp<ThumbnailColor>(ui =>
                (Input = ui).With(UGUI.ThumbnailColor(Edit.name, Wrapper.GetColor.Apply(Edit.name), Wrapper.SetColor.Apply(Edit.name))))));
        internal override void Store(Modifications mod) =>
            Target.isOn.Maybe(() => mod.ColorValues[Edit.name] = Input.GetColor());
        internal override void Apply(Modifications mod) =>
            (Target.isOn = mod.ColorValues.TryGetValue(Edit.name, out var value)).Maybe(UpdateGet);
        protected override void UpdateGet() =>
            Input.SetColor(Wrapper.GetColor(Edit.name));
        protected override void UpdateSet() =>
            Wrapper.SetColor(Edit.name, Input.GetColor());
    }
    internal class VectorEdit : CommonEdit
    {
        static Func<string, Action<GameObject>> FloatEdit = name =>
            UGUI.Content(name)(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>()) +
                UI.Label.Apply(100).Apply($"float({name})") +
                UI.Input.Apply(TMP_InputField.ContentType.DecimalNumber));
        internal static void PrepareArchetype(GameObject parent) =>
            Archetype = PrepareArchetype("VectorEdit", parent.transform)
                .With(UGUI.Content("VectorValues")(
                    UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>())
                    + UGUI.Cmp(UGUI.Layout(width: 220))
                    + FloatEdit("X")
                    + FloatEdit("Y")
                    + FloatEdit("Z")
                    + FloatEdit("W")));
        static GameObject Archetype { get; set; }
        TMP_InputField InputX;
        TMP_InputField InputY;
        TMP_InputField InputZ;
        TMP_InputField InputW;
        Vector4 Value;
        internal VectorEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper, Archetype) =>
            Edit.With(UGUI.ModifyAt("VectorValues", "X", "Input")(UGUI.Cmp<TMP_InputField>(ui => (InputX = ui).onValueChanged.AddListener(OnChangeX))))
                .With(UGUI.ModifyAt("VectorValues", "Y", "Input")(UGUI.Cmp<TMP_InputField>(ui => (InputY = ui).onValueChanged.AddListener(OnChangeY))))
                .With(UGUI.ModifyAt("VectorValues", "Z", "Input")(UGUI.Cmp<TMP_InputField>(ui => (InputZ = ui).onValueChanged.AddListener(OnChangeZ))))
                .With(UGUI.ModifyAt("VectorValues", "W", "Input")(UGUI.Cmp<TMP_InputField>(ui => (InputW = ui).onValueChanged.AddListener(OnChangeW))));
        void Apply(Vector4 value)
        {
            Value = value;
            InputX.SetTextWithoutNotify(value.x.ToString());
            InputY.SetTextWithoutNotify(value.y.ToString());
            InputZ.SetTextWithoutNotify(value.z.ToString());
            InputW.SetTextWithoutNotify(value.w.ToString());
        }
        Action<string> OnChangeX =>
            input => float.TryParse(input, out float value)
                .Maybe(Wrapper.SetVector.Apply(Edit.name)
                .Apply(Value = new Vector4(value, Value.y, Value.z, Value.w)));
        Action<string> OnChangeY =>
            input => float.TryParse(input, out float value)
                .Maybe(Wrapper.SetVector.Apply(Edit.name)
                .Apply(Value = new Vector4(Value.x, value, Value.z, Value.w)));
        Action<string> OnChangeZ =>
            input => float.TryParse(input, out float value)
                .Maybe(Wrapper.SetVector.Apply(Edit.name)
                .Apply(Value = new Vector4(Value.x, Value.y, value, Value.w)));
        Action<string> OnChangeW =>
            input => float.TryParse(input, out float value)
                .Maybe(Wrapper.SetVector.Apply(Edit.name)
                .Apply(Value = new Vector4(Value.x, Value.y, Value.z, value)));
        internal override void Store(Modifications mod) =>
            Target.isOn.Maybe(() => mod.VectorValues[Edit.name] = Value);
        internal override void Apply(Modifications mod) =>
            (Target.isOn = mod.VectorValues.TryGetValue(Edit.name, out var value)).Maybe(F.Apply(Apply, (Vector4)value));
        protected override void UpdateGet() =>
            Apply(Wrapper.GetVector(Edit.name));
        protected override void UpdateSet() =>
            Wrapper.SetVector(Edit.name, Value);
    }
    internal class TextureEdit : CommonEdit
    {
        static GameObject Archetype { get; set; }
        internal static void PrepareArchetype(GameObject parent) =>
            Archetype = PrepareArchetype("TextureEdit", parent.transform)
                .With(UGUI.Content("TextureValues")(
                    UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>()) +
                    UI.Label.Apply(220).Apply("TextureName") +
                    UGUI.Content("Buttons")(
                        UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>()) +
                        UI.Button.Apply("import") + UI.Button.Apply("export"))));
        Button Import;
        Button Export;
        TextMeshProUGUI Value;
        internal TextureEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper, Archetype) =>
            Edit.With(UGUI.ModifyAt("TextureValues", "Buttons", "import")(UGUI.Cmp<Button>(ui => Import = ui)))
                .With(UGUI.ModifyAt("TextureValues", "Buttons", "export")(UGUI.Cmp<Button>(ui => Export = ui)))
                .With(UGUI.ModifyAt("TextureValues", "TextureName")(UGUI.Cmp<TextMeshProUGUI>(ui => Value = ui)))
                .With(() => Import.OnClickAsObservable().Subscribe(OnImport))
                .With(() => Export.OnClickAsObservable().Subscribe(OnExport));
        Action<Unit> OnExport =>
            _ => Dialog<ExportDialog>("export", ExportTexture);
        Action<Unit> OnImport =>
            _ => Dialog<ImportDialog>("import", ImportTexture);
        Action<string> ExportTexture =>
            path => Textures.ToFile(Wrapper.GetTexture(Edit.name), path);
        Action<string> ImportTexture =>
            path => Wrapper.SetTexture(Edit.name, Textures.FromFile(path));
        void Dialog<T>(string path, Action<string> action) where T : System.Windows.Forms.FileDialog, new() =>
            TryGetFilePath<T>(action).ApplyDisposable(new T()
            {
                InitialDirectory = Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Name, path),
                Filter = "Texture Sources|*.png",
                FileName = $"{Wrapper.GetTexture(Edit.name)?.name ?? "na"}.png",
            })();
        Action<T> TryGetFilePath<T>(Action<string> action) where T : System.Windows.Forms.FileDialog =>
            dialog => (dialog.ShowDialog() is System.Windows.Forms.DialogResult.OK).Maybe(action.Apply(dialog.FileName));
        internal override void Store(Modifications mod) =>
           (Target.isOn && Textures.IsExtension(Value.text))
            .Maybe(() => mod.TextureHashes[Edit.name] = Wrapper.GetTexture(Edit.name)?.name ?? "");
        internal override void Apply(Modifications mod) =>
            (Target.isOn = mod.TextureHashes.TryGetValue(Edit.name, out var value)).Maybe(F.Apply(Value.SetText, value, false));
        internal override void Update() =>
            Export.With(base.Update).interactable = Wrapper.GetTexture(Edit.name) is not null;
        protected override void UpdateGet() =>
            Value.SetText(Wrapper.GetTexture(Edit.name)?.name ?? "");
        protected override void UpdateSet() =>
            Value.SetText(Wrapper.GetTexture(Edit.name)?.name ?? "");
    }
    internal class EditView
    {
        GameObject Toggle;
        GameObject Panel;
        Action<bool> ToggleActive;
        List<CommonEdit> Edits;
        EditView(WindowHandle handle, GameObject panel) =>
            (Panel, ToggleActive) = (panel, value => (panel.active = value)
                .Maybe(F.Apply(handle.Title.SetText, $"{Plugin.Name}:{panel.name}", false)));
        EditView(WindowHandle handle, GameObject toggle, GameObject panel) : this(handle, panel) =>
            (Toggle = toggle).GetComponentInChildren<Toggle>().OnValueChangedAsObservable().Subscribe(ToggleActive);
        internal EditView(WindowHandle handle, string name, MaterialWrapper wrapper, GameObject toggle, Transform parent) :
            this(handle, toggle, new GameObject(name)
                .With(UGUI.Go(parent: parent, active: false))
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>(spacing: 5)))
                .With(UGUI.Cmp(UGUI.Layout(width: 500)))) =>
            Edits = RendererEdits(wrapper).Concat(wrapper.Properties.Select(entry => (CommonEdit)(entry.Value switch
            {
                ShaderPropertyType.Int => new IntEdit(entry.Key, Panel.transform, wrapper),
                ShaderPropertyType.Float => new FloatEdit(entry.Key, Panel.transform, wrapper),
                ShaderPropertyType.Range => new RangeEdit(entry.Key, Panel.transform, wrapper),
                ShaderPropertyType.Color => new ColorEdit(entry.Key, Panel.transform, wrapper),
                ShaderPropertyType.Vector => new VectorEdit(entry.Key, Panel.transform, wrapper),
                ShaderPropertyType.Texture => new TextureEdit(entry.Key, Panel.transform, wrapper),
                _ => throw new NotImplementedException()
            }))).ToList();
        IEnumerable<CommonEdit> RendererEdits(MaterialWrapper wrapper) =>
            wrapper.Renderer == null ? [
                new ShaderEdit("Shader", Panel.transform, wrapper, this)
            ] : [
                new ShaderEdit("Shader", Panel.transform, wrapper, this),
                new RenderingEdit("Rendering", Panel.transform, wrapper)
            ];
        internal void Populate(MaterialWrapper wrapper) =>
            Edits = RendererEdits(wrapper.With(UGUI.DestroyChildren.Apply(Panel)))
                .Concat(wrapper.Properties.Select(ToEdit(wrapper))).ToList();
        Func<KeyValuePair<string, ShaderPropertyType>, CommonEdit> ToEdit(MaterialWrapper wrapper) =>
            entry => entry.Value switch
            {
                ShaderPropertyType.Int => new IntEdit(entry.Key, Panel.transform, wrapper),
                ShaderPropertyType.Float => new FloatEdit(entry.Key, Panel.transform, wrapper),
                ShaderPropertyType.Range => new RangeEdit(entry.Key, Panel.transform, wrapper),
                ShaderPropertyType.Color => new ColorEdit(entry.Key, Panel.transform, wrapper),
                ShaderPropertyType.Vector => new VectorEdit(entry.Key, Panel.transform, wrapper),
                ShaderPropertyType.Texture => new TextureEdit(entry.Key, Panel.transform, wrapper),
                _ => throw new NotImplementedException()
            };
        void Store(Modifications mod) =>
            Edits.Do(edit => edit.Store(mod));
        void Apply(Modifications mod) =>
            Edits.Do(edit => edit.Apply(mod));
        internal void Store(Dictionary<string, Modifications> mods) =>
            mods[Panel.name] = new Modifications().With(Store);
        internal void Apply(Dictionary<string, Modifications> mods) =>
            Apply(mods.GetValueOrDefault(Panel.name, new Modifications()));
        internal void Update() =>
            Panel.active.Maybe(() => Edits.Do(edit => edit.Update()));
        internal void Dispose()
        {
            UnityEngine.Object.Destroy(Toggle);
            UnityEngine.Object.Destroy(Panel);
        }
    }
    internal class EditGroup
    {
        List<EditView> EditViews = [];
        GameObject Toggles;
        internal EditGroup(string name, Transform listParent) =>
            Toggles = new GameObject(name)
                .With(UGUI.Go(parent: listParent))
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>(spacing: 5)))
                .With(UGUI.Cmp(UGUI.Layout(width: 300)))
                .With(UGUI.Cmp(UGUI.Fitter()))
                .With(UI.Section.Apply(name));
        internal void Initialize(Dictionary<string, MaterialWrapper> wrappers, WindowHandle handle, Transform editParent) =>
            Toggles.active = (EditViews = wrappers.With(Dispose)
                .Select(entry => new EditView(handle, entry.Key, entry.Value, UI.Toggle(entry.Key, Toggles)
                    .With(UGUI.Cmp<Toggle, ToggleGroup>((ui, group) => ui.group = group)), editParent)).ToList()).Count > 0;
        void Store(Dictionary<string, Modifications> mods) =>
            EditViews.Do(edits => edits.Store(mods));
        internal Dictionary<string, Modifications> Store() =>
            new Dictionary<string, Modifications>().With(Store);
        internal void Apply(Dictionary<string, Modifications> mods) =>
            EditViews.Do(edits => edits.Apply(mods));
        internal void Update() =>
            EditViews.Do(edits => edits.Update());
        internal void Dispose() =>
            EditViews.Do(edits => edits.Dispose());
    }
}