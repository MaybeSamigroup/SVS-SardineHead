using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using UniRx;
using UniRx.Triggers;
using Cysharp.Threading.Tasks;
using Character;
using CharacterCreation;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using ImportDialog = System.Windows.Forms.OpenFileDialog;
using ExportDialog = System.Windows.Forms.SaveFileDialog;
using Fishbone;
using CoastalSmell;

namespace SardineHead
{
    internal static partial class UI
    {

       static GameObject ReferenceWindow =>
            SV.Config.ConfigWindow.Instance.transform
                .Find("Canvas").Find("Background").Find("MainWindow").gameObject;
        static GameObject ReferenceScrollView =>
            SV.Config.ConfigWindow.Instance.transform
                .Find("Canvas").Find("Background").Find("MainWindow")
                .Find("Settings").Find("Scroll View").gameObject;
        static GameObject ReferenceSection =>
            SV.Config.ConfigWindow.Instance.transform
                .Find("Canvas").Find("Background").Find("MainWindow")
                .Find("Settings").Find("Scroll View").Find("Viewport").Find("Content")
                .Find("CameraSetting").Find("imgTitle").gameObject;
        static GameObject ReferenceToggle =>
            HumanCustom.Instance.SelectionTop.gameObject.transform
                .Find("08_System").Find("Index").Find("Kind_Base(Toggle)").gameObject;
        static GameObject ReferenceCheck =>
            SV.Config.ConfigWindow.Instance.transform
                .Find("Canvas").Find("Background").Find("MainWindow")
                .Find("Settings").Find("Scroll View").Find("Viewport").Find("Content")
                .Find("GraphicSetting").Find("Content").Find("Effects").Find("tglSSAO").gameObject;
        static GameObject ReferenceLabel =>
            SV.Config.ConfigWindow.Instance.transform
                .Find("Canvas").Find("Background").Find("MainWindow")
                .Find("Settings").Find("Scroll View").Find("Viewport").Find("Content")
                .Find("CameraSetting").Find("Content").Find("SensitivityX").Find("Title").gameObject;
        static GameObject ReferenceInput =>
            HumanCustom.Instance.StateMiniSelection.transform.Find("Window").Find("StateWindow")
                .Find("Exp").Find("EyesPtn").Find("Layout").Find("ptnSelect").Find("InputField_Integer").gameObject;
        static GameObject ReferenceColor =>
            HumanCustom.Instance.StateMiniSelection.transform.Find("Window").Find("StateWindow")
                .Find("Light").Find("grpLighting").Find("colorLight").gameObject;
        static GameObject ReferenceSlider =>
            SV.Config.ConfigWindow.Instance.transform
                .Find("Canvas").Find("Background").Find("MainWindow")
                .Find("Settings").Find("Scroll View").Find("Viewport").Find("Content")
                .Find("CameraSetting").Find("Content").Find("SensitivityX").Find("Slider").gameObject;
        static GameObject ReferenceButton =>
            HumanCustom.Instance.CustomCharaFile.FileWindow.CharaCategory._btnSelect.gameObject;
        internal static Action<Action> OnCustomHumanReady =>
            action => (HumanCustom.Instance.Human == null)
                .Either(action, () => UniTask.NextFrame().ContinueWith(OnCustomHumanReady.Apply(action)));
        internal static Action<GameObject> OnUpdate(Action action) =>
            go => go.GetComponentInParent<ObservableUpdateTrigger>().UpdateAsObservable().Subscribe(action.Ignoring<Unit>());
        static GameObject Canvas =>
            new GameObject(Plugin.Name)
                .With(UGUI.Go(parent: HumanCustom.Instance.transform.Find("UI").Find("Root")))
                .With(UGUI.Cmp(UGUI.Canvas()))
                .With(UGUI.Cmp(UGUI.CanvasScaler(referenceResolution: new(1920, 1080))))
                .With(UGUI.Cmp<GraphicRaycaster>())
                .With(UGUI.Cmp<ObservableUpdateTrigger>());
        internal static ConfigEntry<float> AnchorX;
        internal static ConfigEntry<float> AnchorY;
        static Action<RectTransform> UpdateAnchorPosition => ui =>
            (AnchorX.Value, AnchorY.Value) = (ui.anchoredPosition.x, ui.anchoredPosition.y);
        static Action<GameObject> OnUpdateRecordAnchor = go =>
            OnUpdate(UpdateAnchorPosition.Apply(go.GetComponent<RectTransform>()))(go);
        internal static GameObject Window =>
            UnityEngine.Object.Instantiate(ReferenceWindow, Canvas.transform)
                .With(UGUI.DestroyAt("Title", "btnClose"))
                .With(UGUI.DestroyAt("Settings"))
                .With(UGUI.Cmp(UGUI.Rt(
                    pivot: new(0, 1),
                    anchorMin: new(0, 1),
                    anchorMax: new(0, 1),
                    offsetMin: new(0, 0),
                    offsetMax: new(0, 0),
                    sizeDelta: new(820, 800),
                    anchoredPosition: new(AnchorX.Value, AnchorY.Value))))
                .With(OnUpdateRecordAnchor)
                .With(UGUI.Cmp(
                    UGUI.Behavior<VerticalLayoutGroup>(enabled: true) +
                    UGUI.LayoutGroup<VerticalLayoutGroup>(
                        childControlWidth: true,
                        childControlHeight: true,
                        spacing: 10,
                        childAlignment: TextAnchor.UpperLeft)))
                .With(UGUI.Cmp<UI_DragWindow>())
                .With(UGUI.ModifyAt("Title", "Text (TMP)")(UGUI.Cmp(UGUI.Text(text: Plugin.Name))));
        internal static Func<Transform, Tuple<Transform, Transform>> Panels =
            parent => ScrollViews(new GameObject("Panels")
                .With(UGUI.Go(parent: parent))
                .With(UGUI.Cmp(UGUI.ToggleGroup(allowSwitchOff: false)))
                .With(UGUI.Cmp(UGUI.Rt(
                    anchorMin: new(0, 1),
                    anchorMax: new(0, 1),
                    offsetMin: new(0, 0),
                    offsetMax: new(0, 0),
                    sizeDelta: new(820, 750))))
                .With(UGUI.Cmp(UGUI.Layout(width: 820, height: 750)))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>(childControlWidth: true, childControlHeight: false))));
        static Func<GameObject, Tuple<Transform, Transform>> ScrollViews =
            go => new(ScrollView(go.transform, 300), ScrollView(go.transform, 520));
        static Func<Transform, float, Transform> ScrollView =
            (parent, width) => UnityEngine.Object.Instantiate(ReferenceScrollView, parent)
                .With(UGUI.Cmp(UGUI.Rt(anchorMin: new(0, 1), anchorMax: new(0, 1), sizeDelta: new(width, 750))))
                .With(UGUI.Cmp(UGUI.Layout(width: width, height: 750)))
                .With(UGUI.ModifyAt("Scrollbar Vertical")(
                    UGUI.Cmp(UGUI.Rt(
                        anchorMin: new(1, 1),
                        anchorMax: new(1, 1),
                        offsetMin: new(0, 0),
                        offsetMax: new(0, 0),
                        sizeDelta: new(10, 740),
                        anchoredPosition: new (0, -370)))))
                .With(UGUI.ModifyAt("Viewport")(
                    UGUI.Cmp(UGUI.Rt(
                        anchorMin: new(0, 1),
                        anchorMax: new(0, 1),
                        offsetMin: new(0, 0),
                        offsetMax: new(0, 0),
                        sizeDelta: new(width - 10, 740),
                        anchoredPosition: new (0, 0))) +
                    UGUI.Cmp(UGUI.Behavior<RectMask2D>(enabled: true))))
                .transform.Find("Viewport").Find("Content").gameObject
                .With(UGUI.DestroyChildren)
                .With(UGUI.Cmp(UGUI.Fitter(
                    horizontal: ContentSizeFitter.FitMode.PreferredSize,
                    vertical: ContentSizeFitter.FitMode.PreferredSize)))
                .transform;
        internal static Action<string, GameObject> Section =
            (name, parent) => UnityEngine.Object.Instantiate(ReferenceSection, parent.transform)
                .With(UGUI.Go(name: name))
                .With(UGUI.Cmp(UGUI.Layout(width: 290, height: 30)))
                .With(UGUI.ModifyAt("Title")(UGUI.Cmp(UGUI.Text(
                    enableAutoSizing: false,
                    overflowMode: TextOverflowModes.Ellipsis,
                    verticalAlignment: VerticalAlignmentOptions.Top,
                    horizontalAlignment: HorizontalAlignmentOptions.Left,
                    text: name
                ))));
        internal static Func<string, Transform, GameObject> Toggle =
            (name, parent) => UnityEngine.Object.Instantiate(ReferenceToggle, parent)
                .With(UGUI.Go(name: "Toggle"))
                .With(UGUI.Cmp<CategoryKindToggle>(UnityEngine.Object.Destroy))
                .With(UGUI.Go(active: true))
                .With(UGUI.Cmp(UGUI.Toggle(group: parent.GetComponentInParent<ToggleGroup>(), value: false)))
                .With(UGUI.Cmp(UGUI.Layout(width: 290, height: 30)))
                .With(UGUI.ModifyAt("T02-1")(UGUI.Cmp(UGUI.Text(
                    enableAutoSizing: false,
                    overflowMode: TextOverflowModes.Ellipsis,
                    verticalAlignment: VerticalAlignmentOptions.Top,
                    horizontalAlignment: HorizontalAlignmentOptions.Left,
                    text: name
                ))));
        internal static Action<int, string, GameObject> Label =
            (size, text, parent) => UnityEngine.Object.Instantiate(ReferenceLabel, parent.transform)
                .With(UGUI.Go(name: "Label"))
                .With(UGUI.Cmp<Localize.Translate.UIBindData>(UnityEngine.Object.Destroy))
                .With(UGUI.Cmp(UGUI.Layout(width: size, height: 30)))
                .With(UGUI.Cmp(UGUI.Text(
                    enableAutoSizing: false,
                    overflowMode: TextOverflowModes.Ellipsis,
                    verticalAlignment: VerticalAlignmentOptions.Top,
                    horizontalAlignment: HorizontalAlignmentOptions.Left,
                    text: text
                )));
        internal static Action<string, GameObject> Check =
            (label, parent) => UnityEngine.Object.Instantiate(ReferenceCheck, parent.transform)
                .With(UGUI.Go(name: label))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>(
                    childAlignment: TextAnchor.UpperLeft,
                    childControlWidth: true,
                    childControlHeight: true,
                    childForceExpandWidth: false,
                    childForceExpandHeight: false)))
                .With(UGUI.Cmp(UGUI.Layout(width: 250, height: 30)))
                .With(UGUI.Cmp(UGUI.Toggle(value: false)))
                .With(UGUI.ModifyAt("Background")(UGUI.Cmp(UGUI.Rt(
                    anchorMin: new (0, 1),
                    anchorMax: new (0, 1),
                    sizeDelta: new (24, 24)
                ))))
                .With(UGUI.ModifyAt("Label")(UGUI.Cmp(UGUI.Text(
                    enableAutoSizing: true,
                    overflowMode: TextOverflowModes.Ellipsis,
                    verticalAlignment: VerticalAlignmentOptions.Top,
                    horizontalAlignment: HorizontalAlignmentOptions.Left,
                    text: label
                ))));
        internal static Action<TMP_InputField.ContentType, GameObject> Input =
            (type, parent) => UnityEngine.Object.Instantiate(ReferenceInput, parent.transform)
                .With(UGUI.Go(name: "Input"))
                .With(UGUI.Cmp(UGUI.Layout(width: 150)))
                .With(UGUI.Cmp(UGUI.Input(
                    contentType: type,
                    characterLimit: 10,
                    restoreOriginalTextOnEscape: true)));
        internal static Action<GameObject> Color =
            parent => UnityEngine.Object.Instantiate(ReferenceColor, parent.transform)
                .With(UGUI.Go(name: "Color"))
                .With(UGUI.Cmp(UGUI.Layout(width: 100)));
        internal static Action<GameObject> Slider =
            parent => UnityEngine.Object.Instantiate(ReferenceSlider, parent.transform)
                .With(UGUI.Go(name: "Slider", active: true))
                .With(UGUI.Cmp(UGUI.Layout(width: 80, height: 10)));
        internal static Action<string, GameObject> Button =
            (label, parent) => UnityEngine.Object.Instantiate(ReferenceButton, parent.transform)
                .With(UGUI.Go(name: label))
                .With(UGUI.Cmp(UGUI.Layout(width: 80, height: 30)))
                .With(UGUI.Cmp(UGUI.Selectable<Button>(interactable: true)))
                .With(UGUI.ModifyAt("ST01-2")(UGUI.Cmp(UGUI.Text(
                    autoSizeTextContainer: true,
                    verticalAlignment: VerticalAlignmentOptions.Middle,
                    horizontalAlignment: HorizontalAlignmentOptions.Center,
                    text: label))));
    }
    internal abstract class CommonEdit
    {
        protected static GameObject PrepareArchetype(string name, Transform parent) =>
            new GameObject(name).With(UGUI.Go(parent: parent, active: false))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>(
                    childControlWidth: true,
                    childControlHeight: true,
                    spacing: 0,
                    padding: new(5, 5, 2, 2),
                    childAlignment: TextAnchor.UpperLeft)))
                .With(UGUI.Cmp(UGUI.Layout(width: 500)))
                .With(UGUI.Cmp(UGUI.Fitter(
                    vertical: ContentSizeFitter.FitMode.PreferredSize,
                    horizontal: ContentSizeFitter.FitMode.PreferredSize)))
                .With(UI.Check.Apply("Checkbox"));
        protected abstract GameObject Archetype { get; }
        protected MaterialWrapper Wrapper;
        protected GameObject Edit;
        CommonEdit(MaterialWrapper wrapper) => Wrapper = wrapper;
        protected CommonEdit(string name, Transform parent, MaterialWrapper wrapper) : this(wrapper) =>
            Edit = UnityEngine.Object.Instantiate(Archetype, parent)
                .With(UGUI.Go(name: name, active: true))
                .With(UGUI.ModifyAt("Checkbox", "Label")(UGUI.Cmp(UGUI.Text(text: name))));
        internal bool Check
        {
            get => Edit.GetComponentInChildren<Toggle>().isOn;
            set => Edit.GetComponentInChildren<Toggle>().isOn = value;
        }
        internal abstract void Store(Modifications mod);
        internal abstract void Apply(Modifications mod);
        internal virtual void Update() => Check.Either(UpdateGet, UpdateSet);
        protected abstract void UpdateGet();
        protected abstract void UpdateSet();
    }
    internal class RenderingEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("BoolEdit", parent.transform).With(UI.Check.Apply("Enabled"));
        protected override GameObject Archetype => Base;
        internal RenderingEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper) =>
            Value.onValueChanged.AddListener(OnChange);
        Action<bool> OnChange => value => Wrapper.Renderer.enabled = value;
        Toggle Value => Edit.GetComponentsInChildren<Toggle>()[^1];
        TextMeshProUGUI Label => Edit.GetComponentsInChildren<TextMeshProUGUI>()[^1];
        internal override void Store(Modifications mod) =>
            mod.Rendering = (Check, Value.isOn) switch
            {
                (false, _) => BoolValue.Unmanaged,
                (true, true) => BoolValue.Enabled,
                (true, false) => BoolValue.Disabled
            };
        internal override void Apply(Modifications mod) =>
            (Check, Wrapper.Renderer.enabled) = mod.Rendering switch
            {
                BoolValue.Enabled => (true, true.With(Value.SetIsOnWithoutNotify)),
                BoolValue.Disabled => (true, false.With(Value.SetIsOnWithoutNotify)),
                _ => (false, true.With(Value.SetIsOnWithoutNotify))
            };
        internal override void Update() =>
            Label.With(base.Update).SetText(Wrapper.Renderer.gameObject.activeInHierarchy ? "Enabled(Active)" : "Enabled(Inactive)");
        protected override void UpdateGet() =>
            Value.SetIsOnWithoutNotify(Wrapper.Renderer.enabled);
        protected override void UpdateSet() =>
            Wrapper.Renderer.enabled = Value.isOn;
    }
    internal class IntEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("IntEdit", parent.transform)
                .With(UI.Label.Apply(80).Apply("int:"))
                .With(UI.Input.Apply(TMP_InputField.ContentType.IntegerNumber));
        TMP_InputField Input => Edit.GetComponentInChildren<TMP_InputField>();
        Action<string> OnChange =>
            input => int.TryParse(input, out var value)
                .Maybe(Wrapper.SetInt.Apply(Edit.name).Apply(value));
        internal IntEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper) =>
            Input.onValueChanged.AddListener(OnChange);
        internal override void Store(Modifications mod) =>
            Check.Maybe(() => mod.IntValues[Edit.name] = int.TryParse(Input.text, out var value) ? value : Wrapper.GetInt(Edit.name));
        internal override void Apply(Modifications mod) =>
            (Check = mod.IntValues.TryGetValue(Edit.name, out var value)).Maybe(Wrapper.SetInt.Apply(Edit.name).Apply(value));
        protected override void UpdateGet() =>
            Input.SetTextWithoutNotify(Wrapper.GetInt(Edit.name).ToString());
        protected override void UpdateSet() =>
            int.TryParse(Input.text, out var value).Maybe(Wrapper.SetInt.Apply(Edit.name).Apply(value));
    }
    internal class FloatEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("FloatEdit", parent.transform)
                .With(UI.Label.Apply(80).Apply("float:"))
                .With(UI.Input.Apply(TMP_InputField.ContentType.DecimalNumber));
        TMP_InputField Input => Edit.GetComponentInChildren<TMP_InputField>();
        Action<string> OnChange =>
            input => float.TryParse(input, out float value).Maybe(Wrapper.SetFloat.Apply(Edit.name).Apply(value));
        internal FloatEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper) =>
            Input.onValueChanged.AddListener(OnChange);
        internal override void Store(Modifications mod) =>
            Check.Maybe(() => mod.FloatValues[Edit.name] = float.TryParse(Input.text, out var value) ? value : Wrapper.GetFloat(Edit.name));
        internal override void Apply(Modifications mod) =>
            (Check = mod.FloatValues.TryGetValue(Edit.name, out var value)).Maybe(Wrapper.SetFloat.Apply(Edit.name).Apply(value));
        protected override void UpdateGet() =>
            Input.SetTextWithoutNotify(Wrapper.GetInt(Edit.name).ToString());
        protected override void UpdateSet() =>
            float.TryParse(Input.text, out var value).Maybe(Wrapper.SetFloat.Apply(Edit.name).Apply(value));
    }
    internal class RangeEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("RangeEdit", parent.transform).With(UI.Label.Apply(80).Apply("n/a")).With(UI.Slider);
        Slider Input => Edit.GetComponentInChildren<Slider>();
        TextMeshProUGUI Value => Edit.GetComponentsInChildren<TextMeshProUGUI>()[^1];
        Action<float> OnChange => value => Wrapper.SetRange.Apply(Edit.name).Apply(value);
        RangeEdit(string name, Transform parent, MaterialWrapper wrapper, Vector2 limits) : base(name, parent, wrapper) =>
            (Input.minValue, Input.maxValue) = (limits.x, limits.y);
        internal RangeEdit(string name, Transform parent, MaterialWrapper wrapper) : this(name, parent, wrapper, wrapper.RangeLimits[name]) =>
             Input.onValueChanged.AddListener(OnChange);
        internal override void Store(Modifications mod) =>
            Check.Maybe(() => mod.RangeValues[Edit.name] = Input.value);
        internal override void Apply(Modifications mod) =>
            (Check = mod.RangeValues.TryGetValue(Edit.name, out var value)).Maybe(Wrapper.SetRange.Apply(Edit.name).Apply(value));
        internal override void Update() =>
            Value.With(base.Update).SetText(Input.value.ToString());
        protected override void UpdateGet() =>
            Input.SetValueWithoutNotify(Wrapper.GetRange(Edit.name));
        protected override void UpdateSet() =>
            Wrapper.SetRange(Edit.name, Input.value);
    }
    internal class ColorEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("ColorEdit", parent.transform).With(UI.Color);
        ThumbnailColor Input => Edit.GetComponentInChildren<ThumbnailColor>();
        Func<Color, bool> OnChange => value => true.With(Wrapper.SetColor.Apply(Edit.name).Apply(value));
        internal ColorEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper) =>
             Input.Initialize(Edit.name, Wrapper.GetColor.Apply(Edit.name), OnChange, true, true);
        internal override void Store(Modifications mod) =>
            Check.Maybe(() => mod.ColorValues[Edit.name] = Input.GetColor());
        internal override void Apply(Modifications mod) =>
            (Check = mod.ColorValues.TryGetValue(Edit.name, out var value))
                .Maybe(Wrapper.SetColor.Apply(Edit.name).Apply(value));
        protected override void UpdateGet() =>
            Input.SetColor(Wrapper.GetColor(Edit.name));
        protected override void UpdateSet() =>
            Wrapper.SetColor(Edit.name, Input.GetColor());
    }
    internal class VectorEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        static Func<string, Action<GameObject>> FloatEdit = name =>
            UGUI.AddChild(name)(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>())
                + UI.Label.Apply(80).Apply($"float({name})")
                + UI.Input.Apply(TMP_InputField.ContentType.DecimalNumber));
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("VectorEdit", parent.transform)
                .With(UGUI.AddChild("VectorValues")(
                    UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>())
                    + UGUI.Cmp(UGUI.Layout(width: 230))
                    + FloatEdit("x")
                    + FloatEdit("y")
                    + FloatEdit("z")
                    + FloatEdit("w")));
        TMP_InputField InputX => Edit.GetComponentsInChildren<TMP_InputField>()[0];
        TMP_InputField InputY => Edit.GetComponentsInChildren<TMP_InputField>()[1];
        TMP_InputField InputZ => Edit.GetComponentsInChildren<TMP_InputField>()[2];
        TMP_InputField InputW => Edit.GetComponentsInChildren<TMP_InputField>()[3];
        Vector4 Value;
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
        internal VectorEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper)
        {
            Apply(Wrapper.GetVector(Edit.name));
            InputX.onValueChanged.AddListener(OnChangeX);
            InputY.onValueChanged.AddListener(OnChangeY);
            InputZ.onValueChanged.AddListener(OnChangeZ);
            InputW.onValueChanged.AddListener(OnChangeW);
        }
        internal override void Store(Modifications mod) =>
            Check.Maybe(() => mod.VectorValues[Edit.name] = Value);
        internal override void Apply(Modifications mod) =>
            (Check = mod.VectorValues.TryGetValue(Edit.name, out var value))
                .Maybe(Wrapper.SetVector.Apply(Edit.name).Apply(Value.With(Apply)));
        protected override void UpdateGet() =>
            Apply(Wrapper.GetVector(Edit.name));
        protected override void UpdateSet() =>
            Wrapper.SetVector(Edit.name, Value);
    }
    internal class TextureEdit : CommonEdit
    { static GameObject Base { get; set; } protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("TextureEdit", parent.transform)
                .With(UGUI.AddChild("TextureValues")(
                    UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>())
                    + UI.Label.Apply(200).Apply("")
                    + UGUI.AddChild("Buttons")(
                        UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>())
                        + UI.Button.Apply("import") + UI.Button.Apply("export"))));
        TextMeshProUGUI Value => Edit.GetComponentsInChildren<TextMeshProUGUI>()[1];
        Button Import => Edit.GetComponentsInChildren<Button>()[0];
        Button Export => Edit.GetComponentsInChildren<Button>()[1];
        Texture Texture
        {
            get => Wrapper.GetTexture(Edit.name);
            set => Wrapper.SetTexture(Edit.name, value);
        }
        Action Dialog<T>(string path, Action<string> action) where T : System.Windows.Forms.FileDialog, new() =>
            TryGetFilePath<T>(action).ApplyDisposable(new T()
            {
                InitialDirectory = Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, path),
                Filter = "Texture Sources|*.png",
                FileName = $"{Texture?.name ?? "na"}.png",
            });
        Action<T> TryGetFilePath<T>(Action<string> action) where T : System.Windows.Forms.FileDialog =>
            dialog => (dialog.ShowDialog() is System.Windows.Forms.DialogResult.OK).Maybe(action.Apply(dialog.FileName));
        Action<string> ExportTexture => path => Textures.ToFile(Texture, path);
        Action<string> ImportTexture => path => Texture = Textures.FromFile(path);
        Action OnExport => Dialog<ExportDialog>("export", ExportTexture);
        Action OnImport => Dialog<ImportDialog>("import", ImportTexture); 
        internal TextureEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper)
        {
            Import.onClick.AddListener(OnImport);
            Export.onClick.AddListener(OnExport);
        }
        internal override void Store(Modifications mod) =>
           (Check && Textures.IsExtension(Value.text))
            .Maybe(() => mod.TextureHashes[Edit.name] = Wrapper.GetTexture(Edit.name)?.name ?? "");
        internal override void Apply(Modifications mod) =>
            (Check = mod.TextureHashes.TryGetValue(Edit.name, out var value))
                .Maybe(Wrapper.SetTexture.Apply(Edit.name).Apply(Textures.FromHash(Value.text = value)));
        internal override void Update() =>
            Export.With(base.Update).interactable = Wrapper.GetTexture(Edit.name) is not null;
        protected override void UpdateGet() =>
            Value.SetText(Wrapper.GetTexture(Edit.name)?.name ?? "");
        protected override void UpdateSet() =>
            Value.SetText(Wrapper.GetTexture(Edit.name)?.name ?? "");
    }
    internal class EditView
    {
        MaterialWrapper Wrapper;
        GameObject Toggle;
        GameObject Panel;
        List<CommonEdit> Edits;
        EditView(GameObject toggle, GameObject panel) =>
            toggle.GetComponentInChildren<Toggle>().onValueChanged.AddListener((Action<bool>)panel.SetActive);
        EditView(MaterialWrapper wrapper, GameObject toggle, GameObject panel) : this(toggle, panel) =>
            (Wrapper, Toggle, Panel) = (wrapper, toggle, panel);
        internal EditView(string name, MaterialWrapper wrapper, GameObject toggle, Transform parent) :
            this(wrapper, toggle, new GameObject(name)
                .With(UGUI.Go(parent: parent, active: false))
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>(
                    childControlWidth: true,
                    childControlHeight: true,
                    spacing: 0,
                    padding: new (5, 5, 5, 5),
                    childAlignment: TextAnchor.UpperLeft)))
                .With(UGUI.Cmp(UGUI.Layout(width: 520)))
                .With(UGUI.Cmp(UGUI.Fitter(
                    vertical: ContentSizeFitter.FitMode.PreferredSize,
                    horizontal: ContentSizeFitter.FitMode.PreferredSize)))) =>
            Edits = RendererEdits().Concat(Wrapper.Properties.Select(entry => (CommonEdit)(entry.Value switch
            {
                ShaderPropertyType.Int => new IntEdit(entry.Key, Panel.transform, Wrapper),
                ShaderPropertyType.Float => new FloatEdit(entry.Key, Panel.transform, Wrapper),
                ShaderPropertyType.Range => new RangeEdit(entry.Key, Panel.transform, Wrapper),
                ShaderPropertyType.Color => new ColorEdit(entry.Key, Panel.transform, Wrapper),
                ShaderPropertyType.Vector => new VectorEdit(entry.Key, Panel.transform, Wrapper),
                ShaderPropertyType.Texture => new TextureEdit(entry.Key, Panel.transform, Wrapper),
                _ => throw new NotImplementedException()
            }))).ToList();
        internal IEnumerable<CommonEdit> RendererEdits() =>
            Wrapper.Renderer == null ? [] : [new RenderingEdit("Rendering", Panel.transform, Wrapper)];
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
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>(
                    childControlWidth: true,
                    childControlHeight: true,
                    spacing: 0,
                    padding: new (5, 15, 5, 5),
                    childAlignment: TextAnchor.UpperLeft)))
                .With(UGUI.Cmp(UGUI.Layout(width: 300)))
                .With(UGUI.Cmp(UGUI.Fitter(
                    vertical: ContentSizeFitter.FitMode.PreferredSize,
                    horizontal: ContentSizeFitter.FitMode.PreferredSize)))
                .With(UI.Section.Apply(name));
        internal void Initialize(Dictionary<string, MaterialWrapper> wrappers, Transform editParent) =>
            Toggles.active = (EditViews = wrappers.With(Dispose)
                .Select(entry => new EditView(entry.Key, entry.Value,
                    UI.Toggle(entry.Key, Toggles.transform), editParent)).ToList()).Count > 0;
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
    internal class EditWindow
    {
        Transform ListPanel;
        Transform EditPanel;
        EditGroup FaceGroup;
        EditGroup BodyGroup;
        Dictionary<int, EditGroup> HairGroups = new();
        Dictionary<int, EditGroup> ClothesGroups = new();
        Dictionary<int, EditGroup> AccessoryGroups = new();
        EditWindow(Tuple<Transform, Transform> panels) =>
            (ListPanel, EditPanel) = panels;
        EditWindow(GameObject window) : this(UI.Panels(window.transform)) =>
            window
                .With(IntEdit.PrepareArchetype)
                .With(FloatEdit.PrepareArchetype)
                .With(RangeEdit.PrepareArchetype)
                .With(ColorEdit.PrepareArchetype)
                .With(VectorEdit.PrepareArchetype)
                .With(TextureEdit.PrepareArchetype)
                .With(RenderingEdit.PrepareArchetype)
                .With(SetState)
                .GetComponentInParent<ObservableUpdateTrigger>()
                    .UpdateAsObservable()
                    .Subscribe(ToggleActive(window));
        EditWindow() : this(UI.Window) =>
            (FaceGroup, BodyGroup) =
               (new EditGroup("Face", ListPanel), new EditGroup("Body", ListPanel));
        EditGroup GroupAt(string name, Dictionary<int, EditGroup> groups, int index) =>
            groups.TryGetValue(index, out var group) ? group : groups[index] = new EditGroup(name, ListPanel);
        void Initialize(Dictionary<string, MaterialWrapper> wrappers, EditGroup group) =>
            group.Initialize(wrappers, EditPanel);
        void OnBodyChange(HumanBody item) =>
            Initialize(item.Wrap(), BodyGroup);
        void OnFaceChange(HumanFace item) =>
            Initialize(item.Wrap(), FaceGroup);
        void OnHairChange(HumanHair item, int index) =>
            Initialize(item.Wrap(index), GroupAt($"Hair:{Enum.GetName(typeof(ChaFileDefine.HairKind), index)}", HairGroups, index));
        void OnClothesChange(HumanCloth item, int index) =>
            Initialize(item.Wrap(index), GroupAt($"Clothes:{Enum.GetName(typeof(ChaFileDefine.ClothesKind), index)}", ClothesGroups, index));
        void OnAccessoryChange(HumanAccessory item, int index) =>
            Initialize(item.Wrap(index), GroupAt($"Accessories{index}", AccessoryGroups, index));
        void Apply(CoordMods mods)
        {
            FaceGroup.Apply(mods.Face);
            BodyGroup.Apply(mods.Body);
            HairGroups.Do(entry => entry.Value.Apply(mods.Hairs.GetValueOrDefault(entry.Key, new())));
            ClothesGroups.Do(entry => entry.Value.Apply(mods.Clothes.GetValueOrDefault(entry.Key, new())));
            AccessoryGroups.Do(entry => entry.Value.Apply(mods.Accessories.GetValueOrDefault(entry.Key, new())));
        }
        CoordMods Store() => new()
        {
            Face = FaceGroup.Store(),
            Body = BodyGroup.Store(),
            Hairs = HairGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Clothes = ClothesGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
            Accessories = AccessoryGroups.ToDictionary(entry => entry.Key, entry => entry.Value.Store()),
        };
        void SetState(GameObject go) =>
            UI.OnCustomHumanReady.Apply(UGUI.Go(active: true).Apply(go));
        Action<Unit> ToggleActive(GameObject go) =>
            _ => Toggle.With(Update).Value.IsDown().Maybe(() => go.SetActive(Status.Value = !Status.Value));
        static ConfigEntry<KeyboardShortcut> Toggle { get; set; }
        static ConfigEntry<bool> Status { get; set; }
        void OnCharacterSerialize(HumanData data, ZipArchive archive) =>
            CharaMods.Save(archive, CharaMods.Load(archive).Merge(data)(CoordLimit.All, Store()));
        void OnCoordinateSerialize(HumanDataCoordinate _, ZipArchive archive) =>
            CoordMods.Save(archive, Store());
        void OnPreCoordinateReload(Human human, int type, ZipArchive archive) =>
            CharaMods.Save(archive, CharaMods.Load(archive).Merge(human)(CoordLimit.All, Store()));
        void OnPostCoordinateReload(Human human, int type, ZipArchive archive) =>
            Apply(CharaMods.Load(archive).AsCoord(human));
        void OnCharacterDeserialize(Human human, CharaLimit limits, ZipArchive archive, ZipArchive storage) =>
            Apply(CharaMods.Load(storage).Merge(limits)(CharaMods.Load(archive)).With(CharaMods.Save.Apply(storage)).AsCoord(human));
        void OnCoordinateDeserialize(Human human, HumanDataCoordinate coord, CoordLimit limits, ZipArchive archive, ZipArchive storage) =>
            Apply(CharaMods.Load(storage).Merge(human)(limits, CoordMods.Load(archive)).AsCoord(human));

        void Update(IEnumerable<EditGroup> groups) => groups.Do(group => group.Update());
        void Update() => Update([FaceGroup, BodyGroup, .. HairGroups.Values, .. ClothesGroups.Values, .. AccessoryGroups.Values]);
        internal static EditWindow Instance;
        internal static void Initialize()
        {
            UI.AnchorX = Plugin.Instance.Config.Bind("General", "Window AnchorX", 30.0f);
            UI.AnchorY = Plugin.Instance.Config.Bind("General", "Window AnchorY", -80.0f);
            Status = Plugin.Instance.Config.Bind("General", "Show Sardin Head (smells fishy)", false);
            Toggle = Plugin.Instance.Config.Bind("General", "Sardin Head toggle key", new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl));
            Util<HumanCustom>.Hook(() =>
            {
                Plugin.Instance.Log.LogInfo("CustomInstantiate");
                Instance = new EditWindow();
                Event.OnCharacterSerialize += Instance.OnCharacterSerialize;
                Event.OnCoordinateSerialize += Instance.OnCoordinateSerialize;
                Event.OnPreCoordinateReload += Instance.OnPreCoordinateReload;
                Event.OnPostCoordinateReload += Instance.OnPostCoordinateReload;
                Event.OnPostCharacterDeserialize += Instance.OnCharacterDeserialize;
                Event.OnPostCoordinateDeserialize += Instance.OnCoordinateDeserialize;
                Hooks.OnBodyChange += Instance.OnBodyChange;
                Hooks.OnFaceChange += Instance.OnFaceChange;
                Hooks.OnHairChange += Instance.OnHairChange;
                Hooks.OnClothesChange += Instance.OnClothesChange;
                Hooks.OnAccessoryChange += Instance.OnAccessoryChange;
            }, () =>
            {
                Plugin.Instance.Log.LogInfo("CustomDestroyed");
                Event.OnCharacterSerialize -= Instance.OnCharacterSerialize;
                Event.OnCoordinateSerialize -= Instance.OnCoordinateSerialize;
                Event.OnPreCoordinateReload -= Instance.OnPreCoordinateReload;
                Event.OnPostCoordinateReload -= Instance.OnPostCoordinateReload;
                Event.OnPostCharacterDeserialize -= Instance.OnCharacterDeserialize;
                Event.OnPostCoordinateDeserialize -= Instance.OnCoordinateDeserialize;
                Hooks.OnBodyChange -= Instance.OnBodyChange;
                Hooks.OnFaceChange -= Instance.OnFaceChange;
                Hooks.OnHairChange -= Instance.OnHairChange;
                Hooks.OnClothesChange -= Instance.OnClothesChange;
                Hooks.OnAccessoryChange -= Instance.OnAccessoryChange;
                Instance = null;
            });
        }
    }
    partial class ModApplicator
    {
        static void OnPreActorHumanize(SaveData.Actor actor, HumanData data, ZipArchive archive) =>
            new ModApplicator(data, CharaMods.Load(archive).AsCoord(actor.charFile));
        static void OnPreCoordinateReload(Human human, int type, ZipArchive archive) =>
            new ModApplicator(human.data, CharaMods.Load(archive).AsCoord(type));
        static void OnPreCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive, ZipArchive storage) =>
            new ModApplicator(human.data, CharaMods.Load(storage).Merge(human)(limits, CoordMods.Load(archive)).AsCoord(human));
        static void OnPostActorHumanize(SaveData.Actor actor, Human human, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human.data));
        static void OnPostCoordinateReload(Human human, int type, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human.data));
        static void OnPostCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive, ZipArchive storage) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human.data));
        internal static void Initialize()
        {
            Event.OnPreActorHumanize += OnPreActorHumanize;
            Event.OnPreCoordinateReload += OnPreCoordinateReload;
            Event.OnPreCoordinateDeserialize += OnPreCoordinateDeserialize;
            Event.OnPostActorHumanize += OnPostActorHumanize;
            Event.OnPostCoordinateReload += OnPostCoordinateReload;
            Event.OnPostCoordinateDeserialize += OnPostCoordinateDeserialize;
            Util<HumanCustom>.Hook(() =>
            {
                Event.OnPreActorHumanize -= OnPreActorHumanize;
                Event.OnPreCoordinateReload -= OnPreCoordinateReload;
                Event.OnPreCoordinateDeserialize -= OnPreCoordinateDeserialize;
                Event.OnPostActorHumanize -= OnPostActorHumanize;
                Event.OnPostCoordinateReload -= OnPostCoordinateReload;
                Event.OnPostCoordinateDeserialize -= OnPostCoordinateDeserialize;
            }, () =>
            {
                Event.OnPreActorHumanize += OnPreActorHumanize;
                Event.OnPreCoordinateReload += OnPreCoordinateReload;
                Event.OnPreCoordinateDeserialize += OnPreCoordinateDeserialize;
                Event.OnPostActorHumanize += OnPostActorHumanize;
                Event.OnPostCoordinateReload += OnPostCoordinateReload;
                Event.OnPostCoordinateDeserialize += OnPostCoordinateDeserialize;
            });
        }
    }
    [BepInProcess(Process)]
    [BepInDependency(Fishbone.Plugin.Guid)]
    [BepInPlugin(Guid, Name, Version)]
    public partial class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
        public const string Guid = $"{Process}.{Name}";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(() => Instance = this)
                .With(ModApplicator.Initialize)
                .With(EditWindow.Initialize);
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}