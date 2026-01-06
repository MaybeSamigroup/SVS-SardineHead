using BepInEx;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using Cysharp.Threading.Tasks;
#if Aicomi
using ILLGAMES.Unity.UI.ColorPicker;
using ILLGAMES.Extensions;
#else
using ILLGames.Unity.UI.ColorPicker;
using ILLGames.Extensions;
#endif
using ImportDialog = System.Windows.Forms.OpenFileDialog;
using ExportDialog = System.Windows.Forms.SaveFileDialog;
using CoastalSmell;

namespace SardineHead
{
    public class ShaderDefinitions
    {
        public List<string> BuiltIn { get; set; } = new ();
    }
    static class UI
    {
        static void Templates(Window window) =>
            window.Background.With("Templates".AsChild(
                UGUI.GameObject(active: false) +
                ShaderEdit.PrepareTemplate +
                RenderingEdit.PrepareTemplate +
                IntEdit.PrepareTemplate +
                FloatEdit.PrepareTemplate +
                RangeEdit.PrepareTemplate +
                ColorEdit.PrepareTemplate +
                VectorEdit.PrepareTemplate +
                TextureEdit.PrepareTemplate));

        static void Configure(Window window) =>
            window.Content
                .With(UGUI.LayoutH())
                .With(UGUI.ToggleGroup(allowSwitchOff: false));

        internal static Window Window(WindowConfig config) =>
            config.Create(810, 800, Plugin.Name).With(Configure).With(Templates);
    }
    
    internal abstract class CommonEdit
    {
        protected MaterialWrapper Wrapper;
        protected GameObject Root;
        protected Toggle Edit;
        CommonEdit(MaterialWrapper wrapper) => Wrapper = wrapper;
        protected CommonEdit(string name, Transform parent, MaterialWrapper wrapper, GameObject template) : this(wrapper) =>
            Root = UnityEngine.Object.Instantiate(template, parent)
                .With(UGUI.GameObject(name: name) +
                    UGUI.Text(text: name).At("Name") +
                    UGUI.Component<Toggle>(cmp => Edit = cmp).At("Edit"));
        internal abstract void Store(Modifications mod);
        internal abstract void Apply(Modifications mod);
        internal virtual void Update() => Edit.isOn.Either(UpdateGet, UpdateSet);
        protected abstract void UpdateGet();
        protected abstract void UpdateSet();
    }
    internal class ShaderEdit : CommonEdit
    {
        static string[] ShaderNames = Json<ShaderDefinitions>
            .Load(Plugin.Instance.Log.LogError,
                File.OpenRead(Path.Combine(Util.UserDataPath, "plugins", Plugin.Name, "shaders.json")))
            .BuiltIn.Where(name => Shader.Find(name) != null).ToArray();
        internal static UIAction PrepareTemplate =
            "Shaders".AsChild(
                new UIAction(go => Template = go) +
                UGUI.LayoutH() +
                UGUI.Fitter() +
                UGUI.Size(width: 500) +
                "Edit".AsChild(UGUI.Check(24, 24)) +
                "Name".AsChild(UGUI.Label(106, 24)) +
                "Shaders".AsChild(UGUI.Dropdown(330, 24, UGUI.Identity) +
                    UGUI.Component<TMP_Dropdown>(cmp => cmp.AddOptions(ShaderNames.AsIl2Cpp()))));

        static GameObject Template { get; set; }

        TMP_Dropdown Shaders;
        ShaderEdit(string name, Transform parent, MaterialWrapper wrapper, GameObject template) : base(name, parent, wrapper, template) =>
            Root.With(UGUI.Component<TMP_Dropdown>(ui => Shaders = ui).At("Shaders"));
        internal ShaderEdit(string name, Transform parent, MaterialWrapper wrapper, EditView view) :
            this(name, parent, wrapper, Template) =>
                Shaders.OnValueChangedAsObservable().Subscribe(OnValueChanged(view.Populate));
        Action<int> OnValueChanged(Action<MaterialWrapper> action) =>
            value => (F.Apply(Wrapper.SetShader, ShaderNames[value]) + action.Apply(Wrapper)).Invoke();
        internal override void Store(Modifications mod) =>
            Edit.isOn.Maybe(() => mod.Shader = Wrapper.GetShader());
        internal override void Apply(Modifications mod) =>
            Edit.isOn = (mod.Shader != default).With(F.Apply(Wrapper.SetShader, mod.Shader));
        protected override void UpdateGet() =>
            Shaders.captionText.SetText(Wrapper.GetShader());
        protected override void UpdateSet() =>
            Shaders.captionText.SetText(Wrapper.GetShader());
    }
    internal class RenderingEdit : CommonEdit
    {
        internal static UIAction PrepareTemplate =
            "Toggle".AsChild(
                new UIAction(go => Template = go) +
                UGUI.LayoutH() +
                UGUI.Fitter() +
                UGUI.Size(width: 500) +
                "Edit".AsChild(UGUI.Check(24, 24)) +
                "Name".AsChild(UGUI.Label(216, 24)) +
                "Check".AsChild(UGUI.Check(24, 24)) +
                "Value".AsChild(UGUI.Label(150, 24)));
        static GameObject Template { get; set; }
        Toggle Value;
        TextMeshProUGUI Label;

        RenderingEdit(string name, Transform parent, MaterialWrapper wrapper, GameObject template) : base(name, parent, wrapper, template) =>
            Root.With(UGUI.Component<Toggle>(cmp => Value = cmp).At("Check") + UGUI.Component<TextMeshProUGUI>(cmp => Label = cmp).At("Value"));

        internal RenderingEdit(string name, Transform parent, MaterialWrapper wrapper) :
            this(name, parent, wrapper, Template) =>
                Value.OnValueChangedAsObservable().Subscribe(value => Wrapper.Renderer.enabled = value); 

        internal override void Store(Modifications mod) =>
            mod.Rendering = (Edit.isOn, Value.isOn) switch
            {
                (false, _) => BoolValue.Unmanaged,
                (true, true) => BoolValue.Enabled,
                (true, false) => BoolValue.Disabled
            };
        internal override void Apply(Modifications mod) =>
            Edit.isOn = mod.Rendering switch
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
        internal static UIAction PrepareTemplate =
            "IntEdit".AsChild(
                new UIAction(go => Template = go) +
                UGUI.LayoutH() +
                UGUI.Fitter() +
                UGUI.Size(width: 500) +
                "Edit".AsChild(UGUI.Check(24, 24)) +
                "Name".AsChild(UGUI.Label(216, 24)) +
                "Label".AsChild(UGUI.Label(100, 24) + UGUI.Text(text: "int:")) +
                "Value".AsChild(UGUI.Input(120, 24, UGUI.Identity) + UGUI.InputField(contentType: TMP_InputField.ContentType.IntegerNumber)));
        static GameObject Template { get; set; }
        TMP_InputField Value;

        IntEdit(string name, Transform parent, MaterialWrapper wrapper, GameObject template) : base(name, parent, wrapper, template) =>
            Root.With(UGUI.Component<TMP_InputField>(cmp => Value = cmp));
        internal IntEdit(string name, Transform parent, MaterialWrapper wrapper) : this(name, parent, wrapper, Template) =>
            Value.OnValueChangedAsObservable().Where(input => int.TryParse(input, out var _)).Select(input => int.Parse(input)).Subscribe(Wrapper.SetInt.Apply(Root.name));
        internal override void Store(Modifications mod) =>
            Edit.isOn.Maybe(() => mod.IntValues[Root.name] = int.TryParse(Value.text, out var value) ? value : Wrapper.GetInt(Root.name));
        internal override void Apply(Modifications mod) =>
            (Edit.isOn = mod.IntValues.TryGetValue(Root.name, out var value))
                .Maybe(F.Apply(Value.SetText, value.ToString(), false));
        protected override void UpdateGet() =>
            Value.SetTextWithoutNotify(Wrapper.GetInt(Root.name).ToString());
        protected override void UpdateSet() =>
            int.TryParse(Value.text, out var value).Maybe(Wrapper.SetInt.Apply(Root.name).Apply(value));
    }
    internal class FloatEdit : CommonEdit
    {
        internal static UIAction PrepareTemplate =
            "FloatEdit".AsChild(
                new UIAction(go => Template = go) +
                UGUI.LayoutH() +
                UGUI.Fitter() +
                UGUI.Size(width: 500) +
                "Edit".AsChild(UGUI.Check(24, 24)) +
                "Name".AsChild(UGUI.Label(216, 24)) +
                "Label".AsChild(UGUI.Label(100, 24) + UGUI.Text(text: "float:")) +
                "Value".AsChild(UGUI.Input(120, 24, UGUI.Identity) + UGUI.InputField(contentType: TMP_InputField.ContentType.DecimalNumber)));
        static GameObject Template { get; set; }
        TMP_InputField Value;
        FloatEdit(string name, Transform parent, MaterialWrapper wrapper, GameObject template) : base(name, parent, wrapper, template) =>
            Root.With(UGUI.Component<TMP_InputField>(cmp => Value = cmp).At("Value")); 
        internal FloatEdit(string name, Transform parent, MaterialWrapper wrapper) : this(name, parent, wrapper, Template) =>
            Value.OnValueChangedAsObservable().Where(input => float.TryParse(input, out float _)).Select(input => float.Parse(input)).Subscribe(Wrapper.SetFloat.Apply(Root.name));
        internal override void Store(Modifications mod) =>
            Edit.isOn.Maybe(() => mod.FloatValues[Root.name] = float.TryParse(Value.text, out var value) ? value : Wrapper.GetFloat(Root.name));
        internal override void Apply(Modifications mod) =>
            (Edit.isOn = mod.FloatValues.TryGetValue(Root.name, out var value))
                .Maybe(F.Apply(Value.SetText, value.ToString(), false));
        protected override void UpdateGet() =>
            Value.SetTextWithoutNotify(Wrapper.GetInt(Root.name).ToString());
        protected override void UpdateSet() =>
            float.TryParse(Value.text, out var value).Maybe(Wrapper.SetFloat.Apply(Root.name).Apply(value));
    }
    internal class RangeEdit : CommonEdit
    {
        internal static UIAction PrepareTemplate =
            "RangeEdit".AsChild(
                new UIAction(go => Template = go) +
                UGUI.LayoutH() +
                UGUI.Fitter() +
                UGUI.Size(width: 500) +
                "Edit".AsChild(UGUI.Check(24, 24)) +
                "Name".AsChild(UGUI.Label(216, 24)) +
                "Label".AsChild(UGUI.Label(100, 24)) +
                "Value".AsChild(UGUI.Slider(120, 24)));
        static GameObject Template { get; set; }
        TextMeshProUGUI Label;
        Slider Value;

        RangeEdit(string name, Transform parent, MaterialWrapper wrapper, GameObject template) : base(name, parent, wrapper, template) =>
            Root.With(UGUI.Component<TextMeshProUGUI>(cmp => Label = cmp).At("Label") + UGUI.Component<Slider>(cmp => Value = cmp).At("Value"));

        RangeEdit(string name, Transform parent, MaterialWrapper wrapper, Vector2 limits) : this(name, parent, wrapper, Template) =>
            (Value.minValue, Value.maxValue) = (limits.x, limits.y);
        internal RangeEdit(string name, Transform parent, MaterialWrapper wrapper) : this(name, parent, wrapper, wrapper.RangeLimits[name]) =>
            Value.OnValueChangedAsObservable().Subscribe(Wrapper.SetRange.Apply(Root.name));
        internal override void Store(Modifications mod) =>
            Edit.isOn.Maybe(() => mod.RangeValues[Root.name] = Value.value);
        internal override void Apply(Modifications mod) =>
            (Edit.isOn = mod.RangeValues.TryGetValue(Root.name, out var value))
                .Maybe(F.Apply(Value.Set, value, false));
        internal override void Update() =>
            Label.With(base.Update).SetText(Value.value.ToString());
        protected override void UpdateGet() =>
            Value.SetValueWithoutNotify(Wrapper.GetRange(Root.name));
        protected override void UpdateSet() =>
            Wrapper.SetRange(Root.name, Value.value);
    }
    internal class ColorEdit : CommonEdit
    {
        internal static UIAction PrepareTemplate =
            "ColorEdit".AsChild(
                new UIAction(go => Template = go) +
                UGUI.LayoutH() +
                UGUI.Fitter() +
                UGUI.Size(width: 500) +
                "Edit".AsChild(UGUI.Check(24, 24)) +
                "Name".AsChild(UGUI.Label(216, 24)) +
                "Value".AsChild(UGUI.Color(100, 24)));
        static GameObject Template { get; set; }
        ThumbnailColor Value;
        ColorEdit(string name, Transform parent, MaterialWrapper wrapper, GameObject template) : base(name, parent, wrapper, template) =>
            Root.With(UGUI.Component<ThumbnailColor>(cmp => Value = cmp).At("Value"));
        internal ColorEdit(string name, Transform parent, MaterialWrapper wrapper) : this(name, parent, wrapper, Template) =>
            Root.With(UGUI.ThumbnailColor(name, Wrapper.GetColor.Apply(name), Wrapper.SetColor.Apply(name)).At("Value"));
        internal override void Store(Modifications mod) =>
            Edit.isOn.Maybe(() => mod.ColorValues[Root.name] = Value.GetColor());
        internal override void Apply(Modifications mod) =>
            (Edit.isOn = mod.ColorValues.TryGetValue(Root.name, out var value)).Maybe(UpdateGet);
        protected override void UpdateGet() =>
            Value.SetColor(Wrapper.GetColor(Root.name));
        protected override void UpdateSet() =>
            Wrapper.SetColor(Root.name, Value.GetColor());
    }
    internal class VectorEdit : CommonEdit
    {
        internal static UIAction PrepareTemplate =
            "VectorEdit".AsChild(
                new UIAction(go => Template = go) +
                UGUI.LayoutH() +
                UGUI.Fitter() +
                UGUI.Size(width: 500) +
                "Edit".AsChild(UGUI.Check(24, 24)) +
                "Name".AsChild(UGUI.Label(216, 24)) +
                "Axis".AsChild(
                    UGUI.LayoutV() +
                    UGUI.Size(width: 220) +
                    "EditX".AsChild(
                        UGUI.LayoutH() +
                        "LabelX".AsChild(UGUI.Label(100, 24) + UGUI.Text(text: "floatX:")) +
                        "ValueX".AsChild(UGUI.Input(120, 24, UGUI.Identity) + UGUI.InputField(contentType: TMP_InputField.ContentType.DecimalNumber))) +
                    "EditY".AsChild(
                        UGUI.LayoutH() +
                        "LabelY".AsChild(UGUI.Label(100, 24) + UGUI.Text(text: "floatY:")) +
                        "ValueY".AsChild(UGUI.Input(120, 24, UGUI.Identity) + UGUI.InputField(contentType: TMP_InputField.ContentType.DecimalNumber))) +
                    "EditZ".AsChild(
                        UGUI.LayoutH() +
                        "LabelZ".AsChild(UGUI.Label(100, 24) + UGUI.Text(text: "floatZ:")) +
                        "ValueZ".AsChild(UGUI.Input(120, 24, UGUI.Identity) + UGUI.InputField(contentType: TMP_InputField.ContentType.DecimalNumber))) +
                    "EditW".AsChild(
                        UGUI.LayoutH() +
                        "LabelW".AsChild(UGUI.Label(100, 24) + UGUI.Text(text: "floatW:")) +
                        "ValueW".AsChild(UGUI.Input(120, 24, UGUI.Identity) + UGUI.InputField(contentType: TMP_InputField.ContentType.DecimalNumber)))));
        static GameObject Template { get; set; }
        TMP_InputField ValueX;
        TMP_InputField ValueY;
        TMP_InputField ValueZ;
        TMP_InputField ValueW;
        Vector4 Value;
        internal VectorEdit(string name, Transform parent, MaterialWrapper wrapper, GameObject template) : base(name, parent, wrapper, template) =>
            Root.With(
                UGUI.Component<TMP_InputField>(ui => ValueX = ui).At("Axis", "EditX", "ValueX") +
                UGUI.Component<TMP_InputField>(ui => ValueY = ui).At("Axis", "EditY", "ValueY") +
                UGUI.Component<TMP_InputField>(ui => ValueZ = ui).At("Axis", "EditZ", "ValueZ") +
                UGUI.Component<TMP_InputField>(ui => ValueW = ui).At("Axis", "EditW", "ValueW"));
        internal VectorEdit(string name, Transform parent, MaterialWrapper wrapper) : this(name, parent, wrapper, Template) =>
            (_, _, _, _) = (
                ValueX.OnValueChangedAsObservable().Where(input => float.TryParse(input, out var _))
                    .Select(float.Parse).Select(value => Value = new Vector4(value, Value.y, Value.z, Value.w)).Subscribe(Wrapper.SetVector.Apply(name)),
                ValueY.OnValueChangedAsObservable().Where(input => float.TryParse(input, out var _))
                    .Select(float.Parse).Select(value => Value = new Vector4(Value.x, value, Value.z, Value.w)).Subscribe(Wrapper.SetVector.Apply(name)),
                ValueZ.OnValueChangedAsObservable().Where(input => float.TryParse(input, out var _))
                    .Select(float.Parse).Select(value => Value = new Vector4(Value.x, Value.y, value, Value.w)).Subscribe(Wrapper.SetVector.Apply(name)),
                ValueW.OnValueChangedAsObservable().Where(input => float.TryParse(input, out var _))
                    .Select(float.Parse).Select(value => Value = new Vector4(Value.x, Value.y, Value.z, value)).Subscribe(Wrapper.SetVector.Apply(name)));

        void Apply(Vector4 value)
        {
            Value = value;
            ValueX.SetTextWithoutNotify(value.x.ToString());
            ValueY.SetTextWithoutNotify(value.y.ToString());
            ValueZ.SetTextWithoutNotify(value.z.ToString());
            ValueW.SetTextWithoutNotify(value.w.ToString());
        }
        internal override void Store(Modifications mod) =>
            Edit.isOn.Maybe(() => mod.VectorValues[Root.name] = Value);
        internal override void Apply(Modifications mod) =>
            (Edit.isOn = mod.VectorValues.TryGetValue(Root.name, out var value)).Maybe(F.Apply(Apply, (Vector4)value));
        protected override void UpdateGet() =>
            Apply(Wrapper.GetVector(Root.name));
        protected override void UpdateSet() =>
            Wrapper.SetVector(Root.name, Value);
    }
    internal class TextureEdit : CommonEdit
    {
        internal static UIAction PrepareTemplate =
            "TextureEdit".AsChild(
                new UIAction(go => Template = go) +
                UGUI.LayoutH() +
                UGUI.Fitter() +
                UGUI.Size(width: 500) +
                "Edit".AsChild(UGUI.Check(24, 24)) +
                "Name".AsChild(UGUI.Label(216, 24)) +
                "Content".AsChild(
                    UGUI.LayoutV() +
                    "Value".AsChild(UGUI.Label(220, 24)) +
                    "Import".AsChild(UGUI.Button(100, 24, UGUI.Text(text: "import"))) +
                    "Export".AsChild(UGUI.Button(100, 24, UGUI.Text(text: "export")))));
        static GameObject Template { get; set; }
        Button Import;
        Button Export;
        TextMeshProUGUI Value;
        TextureEdit(string name, Transform parent, MaterialWrapper wrapper, GameObject template) : base(name, parent, wrapper, template) =>
            Root.With(
                UGUI.Component<Button>(cmp => Import = cmp).At("Content", "Import") +
                UGUI.Component<Button>(cmp => Export = cmp).At("Content", "Export") +
                UGUI.Component<TextMeshProUGUI>(cmp => Value = cmp).At("Content", "Value"));

        internal TextureEdit(string name, Transform parent, MaterialWrapper wrapper) : this(name, parent, wrapper, Template) =>
            (_, _) = (
                Import.OnClickAsObservable().Subscribe(OnImport),
                Export.OnClickAsObservable().Subscribe(OnExport));

        Action<Unit> OnExport =>
            _ => Dialog<ExportDialog>("export", ExportTexture);
        Action<Unit> OnImport =>
            _ => Dialog<ImportDialog>("import", ImportTexture);
        Action<string> ExportTexture =>
            path => Textures.ToFile(Wrapper.GetTexture(Root.name), path);
        Action<string> ImportTexture =>
            path => Wrapper.SetTexture(Root.name, Textures.FromFile(path));
        void Dialog<T>(string path, Action<string> action) where T : System.Windows.Forms.FileDialog, new() =>
            TryGetFilePath<T>(action).ApplyDisposable(new T()
            {
                InitialDirectory = Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Name, path),
                Filter = "Texture Sources|*.png",
                FileName = $"{Wrapper.GetTexture(Root.name)?.name ?? "na"}.png",
            })();
        Action<T> TryGetFilePath<T>(Action<string> action) where T : System.Windows.Forms.FileDialog =>
            dialog => (dialog.ShowDialog() is System.Windows.Forms.DialogResult.OK).Maybe(action.Apply(dialog.FileName));
        internal override void Store(Modifications mod) =>
           (Edit.isOn && Textures.IsExtension(Value.text))
            .Maybe(() => mod.TextureHashes[Root.name] = Wrapper.GetTexture(Root.name)?.name ?? "");
        internal override void Apply(Modifications mod) =>
            (Edit.isOn = mod.TextureHashes.TryGetValue(Root.name, out var value)).Maybe(F.Apply(Value.SetText, value, false));
        internal override void Update() =>
            Export.With(base.Update).interactable = Wrapper.GetTexture(Root.name) is not null;
        protected override void UpdateGet() =>
            Value.SetText(Wrapper.GetTexture(Root.name)?.name ?? "");
        protected override void UpdateSet() =>
            Value.SetText(Wrapper.GetTexture(Root.name)?.name ?? "");
    }
    internal class EditView
    {
        GameObject Panel;
        List<CommonEdit> Edits;
        IObservable<bool> ToggleState;
        CompositeDisposable Subscriptions;
        EditView(GameObject panel, GameObject toggle) =>
            (Panel, ToggleState) = (panel, toggle.GetComponent<Toggle>().OnValueChangedAsObservable());
        EditView(Window window, GameObject toggle, GameObject panel) : this(panel, toggle) =>
            Subscriptions = [
                ToggleState.Subscribe(panel.SetActive),
                ToggleState.Where(value => value)
                    .Subscribe(_ => window.Title = $"{Plugin.Name}:{Panel.name}"),
                Disposable.Create(F.Apply(UnityEngine.Object.Destroy, panel)),
                Disposable.Create(F.Apply(UnityEngine.Object.Destroy, toggle)),
            ];

        internal EditView(Window window, string name, MaterialWrapper wrapper, GameObject toggle, Transform parent) :
            this(window, toggle,
                new GameObject(name).With(parent.AsParent() + UGUI.GameObject(active: false) + UGUI.LayoutV(spacing: 5) + UGUI.Size(width: 500))) =>
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
            wrapper.Renderer == null ? [] : [
                new ShaderEdit("Shader", Panel.transform, wrapper, this),
                new RenderingEdit("Rendering", Panel.transform, wrapper)
            ];
        internal void Cleanup() =>
            Enumerable.Range(0, Panel.transform.childCount)
                .Select(Panel.transform.GetChild)
                .Select(tf => tf.gameObject)
                .ForEach(UnityEngine.Object.Destroy);
        internal void Populate(MaterialWrapper wrapper) =>
            Edits = RendererEdits(wrapper.With(Cleanup))
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
            Edits.ForEach(edit => edit.Store(mod));
        void Apply(Modifications mod) =>
            Edits.ForEach(edit => edit.Apply(mod));
        internal void Store(Dictionary<string, Modifications> mods) =>
            mods[Panel.name] = new Modifications().With(Store);
        internal void Apply(Dictionary<string, Modifications> mods) =>
            Apply(mods.GetValueOrDefault(Panel.name, new Modifications()));
        internal void Update() =>
            Panel.active.Maybe(() => Edits.ForEach(edit => edit.Update()));
        internal void Dispose() => Subscriptions.Dispose();
    }
    internal class EditGroup
    {
        List<EditView> EditViews = [];
        GameObject Toggles;
        internal EditGroup(string name, Transform listParent) =>
            listParent.With(name.AsChild(
                new UIAction(go => Toggles = go) +
                UGUI.LayoutV(spacing: 5) +
                UGUI.Size(width: 300) +
                UGUI.Fitter() +
                "Section".AsChild(UGUI.Section(300, 24, new(0.3f, 0.3f, 0.3f, 0.8f), UGUI.Text(text: name)))));

        internal void Initialize(Dictionary<string, MaterialWrapper> wrappers, Window window, Transform editParent) =>
            Toggles.active = (EditViews = wrappers.With(Dispose)
                .Select(entry => new EditView(window, entry.Key, entry.Value,
                    new GameObject(entry.Key)
                        .With(UGUI.Toggle(300, 24, UGUI.Text(text: entry.Key)) + Toggles.AsParent() +
                            UGUI.Component<Toggle, ToggleGroup>((ui, group) => ui.group = group)), editParent)).ToList()).Count > 0;
        void Store(Dictionary<string, Modifications> mods) =>
            EditViews.ForEach(edits => edits.Store(mods));
        internal Dictionary<string, Modifications> Store() =>
            new Dictionary<string, Modifications>().With(Store);
        internal void Apply(Dictionary<string, Modifications> mods) =>
            EditViews.ForEach(edits => edits.Apply(mods));
        internal void Update() =>
            EditViews.ForEach(edits => edits.Update());
        internal void Dispose() =>
            EditViews.ForEach(edits => edits.Dispose());
    }
}