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
using Fishbone;
using ImportDialog = System.Windows.Forms.OpenFileDialog;
using ExportDialog = System.Windows.Forms.SaveFileDialog;

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
        static GameObject ReferenceToggle =>
            HumanCustom.Instance.SelectionTop.gameObject.transform
                .Find("08_System").Find("Index").Find("Kind_Base(Toggle)").gameObject;
        static GameObject ReferenceCheck =>
            HumanCustom.Instance.StateMiniSelection.transform.Find("Window").Find("StateWindow")
                .Find("Exp").Find("BlinkLayout").Find("tglBlink").gameObject;
        static GameObject ReferenceLabel =>
           HumanCustom.Instance.StateMiniSelection.transform.Find("Window").Find("StateWindow")
               .Find("Exp").Find("BlinkLayout").Find("tglBlink").Find("T02-1").gameObject;
        static GameObject ReferenceInput =>
            HumanCustom.Instance.StateMiniSelection.transform.Find("Window").Find("StateWindow")
                .Find("Exp").Find("EyesPtn").Find("Layout").Find("ptnSelect").Find("InputField_Integer").gameObject;
        static GameObject ReferenceColor =>
            HumanCustom.Instance.StateMiniSelection.transform.Find("Window").Find("StateWindow")
                .Find("Light").Find("grpLighting").Find("colorLight").gameObject;
        static GameObject ReferenceSlider =>
            HumanCustom.Instance.StateMiniSelection.transform.Find("Window").Find("StateWindow")
                .Find("Light").Find("grpLighting").Find("sldLightingPower").Find("Layout").Find("sld_19_000").gameObject;
        static GameObject ReferenceButton =>
            HumanCustom.Instance.CustomCharaFile.FileWindow.CharaCategory._btnSelect.gameObject;
        internal static Action<Action> OnCustomHumanReady =>
            action => (HumanCustom.Instance.Human == null)
                .Either(action, () => UniTask.NextFrame().ContinueWith(OnCustomHumanReady.Apply(action)));
        internal static void Wrap(this Transform tf, GameObject go) => go.transform.SetParent(tf);
        static Action<Transform> DestroyTransform =>
            tf => UnityEngine.Object.Destroy(tf.gameObject);
        static Action<Transform> DestroyChildren(string[] paths) =>
            paths.Length == 0 ? DestroyTransform : tf => DestroyChildren(paths[1..])(tf.Find(paths[0]));
        internal static Action<GameObject> Destroy(params string[] paths) =>
            go => DestroyChildren(paths)(go.transform);
        internal static Action<GameObject> DestroyAll =>
            go => Enumerable.Range(0, go.transform.childCount)
                .Select(go.transform.GetChild).Do(DestroyTransform);
        internal static Action<GameObject> Destroy<T>() where T : Component =>
            go => UnityEngine.Object.Destroy(go.GetComponent<T>());
        internal static Action<GameObject> Component<T>() where T : Component =>
            go => go.GetOrAddComponent<T>();
        internal static Action<GameObject> Component<T>(Action<T> action) where T : Component =>
            go => go.GetOrAddComponent<T>().With(action);
        internal static Action<GameObject> ChildComponent<T>(Action<T> action) where T : Component =>
            go => go.GetComponentInChildren<T>().With(action);
        internal static Action<GameObject> ChildComponent<T>(Index index, Action<T> action) where T : Component =>
            go => go.GetComponentsInChildren<T>()[index].With(action);
        internal static Action<GameObject> ChildObject(string name, Action<GameObject> action) =>
            parent => parent.With(action.Apply(new GameObject(name).With(parent.transform.Wrap)));
        internal static Action<string, GameObject> Name => (name, go) => go.name = name;
        internal static Action<bool, GameObject> Active = (value, go) => go.SetActive(value);
        internal static Action<float, float, GameObject> Size =>
            (width, height, go) => go.GetComponent<RectTransform>()
                .With(ui => (ui.anchorMin, ui.anchorMax, ui.sizeDelta) = (new(0, 1), new(0, 1), new(width, height)));
        internal static Action<float, float, GameObject> LayoutSize =>
            (width, height, go) => go.AddComponent<LayoutElement>().With(ui => (ui.preferredWidth, ui.preferredHeight) = (width, height));
        internal static Func<Action, Action<Unit>> ToUpdateAction => action => _ => action();
        internal static Action<GameObject> OnUpdate(Action action) =>
            go => go.GetComponentInParent<ObservableUpdateTrigger>().UpdateAsObservable().Subscribe(ToUpdateAction(action));
        static GameObject Canvas =>
            new GameObject(Plugin.Name).With(HumanCustom.Instance.transform.Find("UI").Find("Root").Wrap).With(go => go
                .With(Component<Canvas>(ui => ui.renderMode = RenderMode.ScreenSpaceOverlay))
                .With(Component<CanvasScaler>(ui =>
                    (ui.referenceResolution, ui.uiScaleMode, ui.screenMatchMode) =
                        (new(1920, 1080), CanvasScaler.ScaleMode.ScaleWithScreenSize, CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)))
                .With(Component<GraphicRaycaster>())
                .With(Component<ObservableUpdateTrigger>()));
        internal static ConfigEntry<float> AnchorX;
        internal static ConfigEntry<float> AnchorY;
        static Action<RectTransform> UpdateAnchorPosition => ui =>
            (AnchorX.Value, AnchorY.Value) = (ui.anchoredPosition.x, ui.anchoredPosition.y);
        internal static GameObject Window =>
            UnityEngine.Object.Instantiate(ReferenceWindow, Canvas.transform).With(go => go
                .With(Destroy("Title", "btnClose")).With(Destroy("Settings"))
                .With(Component<RectTransform>(ui =>
                    (ui.anchorMin, ui.anchorMax, ui.pivot, ui.sizeDelta, ui.anchoredPosition) =
                        (new(0, 1), new(0, 1), new(0, 1), new(800, 800), new(AnchorX.Value, AnchorY.Value))))
                .With(OnUpdate(UpdateAnchorPosition.Apply(go.GetComponent<RectTransform>())))
                .With(Component<VerticalLayoutGroup>(ui =>
                    (ui.enabled, ui.childControlWidth, ui.childControlHeight, ui.spacing, ui.childAlignment) = (true, true, true, 10, TextAnchor.UpperLeft)))
                .With(Component<UI_DragWindow>()))
                .With(ChildComponent<TextMeshProUGUI>(ui => ui.SetText(Plugin.Name)));
        internal static Func<Transform, Tuple<Transform, Transform>> Panels =>
            parent => ScrollViews(new GameObject("Panels").With(parent.Wrap)
                .With(Component<ToggleGroup>(ui => ui.allowSwitchOff = false))
                .With(Component<RectTransform>(ui => (ui.sizeDelta, ui.anchorMin, ui.anchorMax) = (new(800, 750), new(0, 0), new(1, 1))))
                .With(Component<LayoutElement>(ui => (ui.preferredWidth, ui.preferredHeight) = (800, 750)))
                .With(Component<HorizontalLayoutGroup>(ui => (ui.childControlWidth, ui.childControlHeight) = (true, false))));
        static Func<GameObject, Tuple<Transform, Transform>> ScrollViews =>
            go => new(ScrollView(go.transform, 300), ScrollView(go.transform, 500));
        static Func<Transform, float, Transform> ScrollView =>
            (parent, width) => UnityEngine.Object.Instantiate(ReferenceScrollView, parent)
                .With(Size.Apply(width).Apply(750)).With(LayoutSize.Apply(width).Apply(750))
                .transform.With(SetScrollbarSize.Apply(10)).With(SetViewportSize.Apply(width - 10))
                .Find("Viewport").Find("Content").gameObject.With(DestroyAll)
                .With(Component<ContentSizeFitter>(ui => (ui.horizontalFit, ui.verticalFit) =
                    (ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize))).transform;
        static Action<float, Transform> SetScrollbarSize => (width, tf) => tf.Find("Scrollbar Vertical").gameObject
            .With(go => go.GetComponent<RectTransform>()
                .With(ui => (ui.anchorMin, ui.anchorMax, ui.pivot, ui.offsetMin, ui.offsetMax, ui.sizeDelta) =
                    (new(1, 1), new(1, 1), new(1, 1), new(0, 0), new(0, 0), new(width, 750))));
        static Action<float, Transform> SetViewportSize => (width, tf) => tf.Find("Viewport").gameObject
            .With(go => go.GetComponent<RectMask2D>().enabled = true)
            .With(go => go.GetComponent<RectTransform>()
                .With(ui => (ui.anchorMin, ui.anchorMax, ui.pivot, ui.sizeDelta, ui.offsetMin, ui.offsetMax) =
                    (new(0, 0), new(1, 1), new(0, 1), new(width, 750), new(0, 0), new(0, 0))));
        static Action<string, TextMeshProUGUI> SetTextMeshProUGUI =>
            (value, ui) => (ui.enableAutoSizing, ui.overflowMode, ui.verticalAlignment, ui.horizontalAlignment, ui.m_text) =
                (false, TextOverflowModes.Ellipsis, VerticalAlignmentOptions.Top, HorizontalAlignmentOptions.Left, value);
        internal static Func<string, Transform, GameObject> Toggle =>
            (name, parent) => UnityEngine.Object.Instantiate(ReferenceToggle, parent).With(go => go
                .With(Destroy<CategoryKindToggle>())
                .With(Active.Apply(true))
                .With(Component<Toggle>(ui => ui.group = parent.GetComponentInParent<ToggleGroup>()))
                .With(LayoutSize.Apply(290).Apply(30))
                .With(ChildComponent(SetTextMeshProUGUI.Apply(name)))); 
        internal static Action<float, RectOffset, GameObject> ContentLayout<T>(TextAnchor anchor) where T : HorizontalOrVerticalLayoutGroup =>
            (spacing, offset, go) => go
                .With(Component<RectTransform>(ui => ui.localScale = new(1, 1)))
                .With(Component<LayoutElement>())
                .With(Component<T>(ui => (ui.childControlWidth, ui.childControlHeight, ui.spacing, ui.padding, ui.childAlignment) =
                    (true, true, spacing, offset, anchor)))
                .With(Component<ContentSizeFitter>(ui => (ui.verticalFit, ui.horizontalFit) =
                    (ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)));
        internal static Action<int, string, GameObject> Label =>
            (size, text, parent) => UnityEngine.Object.Instantiate(ReferenceLabel, parent.transform)
                .With(Destroy<Localize.Translate.UIBindData>())
                .With(LayoutSize.Apply(size).Apply(30))
                .With(Component(SetTextMeshProUGUI.Apply(text)));
        internal static Action<string, GameObject> Check =>
            (label, parent) => UnityEngine.Object.Instantiate(ReferenceCheck, parent.transform)
                .With(LayoutSize.Apply(250).Apply(30))
                .With(Component<Toggle>(ui => ui.isOn = false))
                .With(ChildComponent(SetTextMeshProUGUI.Apply(label)))
                .With(ChildComponent<RectTransform>(1, ui =>
                    (ui.anchorMin, ui.anchorMax, ui.offsetMin, ui.offsetMax) = (new (0,1), new(0,1), new(0,-24), new(24,0))));
        internal static Action<TMP_InputField.ContentType, GameObject> Input =>
            (type, parent) => UnityEngine.Object.Instantiate(ReferenceInput, parent.transform)
                .With(Component<LayoutElement>(ui => ui.preferredWidth = 150))
                .With(Component<TMP_InputField>(ui =>
                    (ui.contentType, ui.characterLimit, ui.restoreOriginalTextOnEscape) = (type, 10, true)));
        internal static Action<GameObject> Color =>
            parent => UnityEngine.Object.Instantiate(ReferenceColor, parent.transform)
                .With(Component<LayoutElement>(ui => ui.preferredWidth = 100));
        internal static Action<GameObject> Slider =>
            parent => UnityEngine.Object.Instantiate(ReferenceSlider, parent.transform)
                .With(LayoutSize.Apply(100).Apply(20));
        internal static Action<string, GameObject> Button =>
            (label, parent) => UnityEngine.Object.Instantiate(ReferenceButton, parent.transform)
                .With(LayoutSize.Apply(100).Apply(30))
                .With(Component<Button>(ui => ui.interactable = true))
                .With(ChildComponent<TextMeshProUGUI>(ui => (ui.autoSizeTextContainer, ui.m_text) = (true, label)));
    }
    internal abstract class CommonEdit
    {
        protected static GameObject PrepareArchetype(string name, Transform parent) =>
            new GameObject(name).With(parent.Wrap)
                .With(UI.Active.Apply(false))
                .With(UI.Check.Apply("PropertyName"))
                .With(UI.Component<LayoutElement>(ui => ui.preferredWidth = 500))
                .With(UI.ContentLayout<HorizontalLayoutGroup>(TextAnchor.UpperLeft).Apply(0).Apply(new(5, 15, 2, 2)));
        protected abstract GameObject Archetype { get; }
        protected MaterialWrapper Wrapper;
        protected GameObject Edit;
        CommonEdit(MaterialWrapper wrapper) => Wrapper = wrapper;
        protected CommonEdit(string name, Transform parent, MaterialWrapper wrapper) : this(wrapper) =>
            Edit = UnityEngine.Object.Instantiate(Archetype, parent).With(UI.Active.Apply(true))
                .With(UI.Name.Apply(name)).With(UI.ChildComponent<TextMeshProUGUI>(ui => ui.SetText(name)));
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
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("VectorEdit", parent.transform)
                .With(UI.ChildObject("VectorValues", go => go
                .With(UI.Component<VerticalLayoutGroup>())
                .With(UI.Component<LayoutElement>(ui => ui.preferredWidth = 230))
                .With(UI.ChildObject("x", gox => gox
                    .With(UI.Component<HorizontalLayoutGroup>())
                    .With(UI.Label.Apply(80).Apply("float(x):"))
                    .With(UI.Input.Apply(TMP_InputField.ContentType.DecimalNumber))))
                .With(UI.ChildObject("y", goy => goy
                    .With(UI.Component<HorizontalLayoutGroup>())
                    .With(UI.Label.Apply(80).Apply("float(y):"))
                    .With(UI.Input.Apply(TMP_InputField.ContentType.DecimalNumber))))
                .With(UI.ChildObject("z", goz => goz
                    .With(UI.Component<HorizontalLayoutGroup>())
                    .With(UI.Label.Apply(80).Apply("float(z):"))
                    .With(UI.Input.Apply(TMP_InputField.ContentType.DecimalNumber))))
                .With(UI.ChildObject("w", gow => gow
                    .With(UI.Component<HorizontalLayoutGroup>())
                    .With(UI.Label.Apply(80).Apply("float(w):"))
                    .With(UI.Input.Apply(TMP_InputField.ContentType.DecimalNumber))))));
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
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("TextureEdit", parent.transform)
                .With(UI.ChildObject("TextureValues", go => go
                .With(UI.Component<VerticalLayoutGroup>())
                .With(UI.Label.Apply(200).Apply(""))
                .With(UI.ChildObject("Buttons", buttons => buttons
                    .With(UI.Component<HorizontalLayoutGroup>())
                    .With(UI.Button.Apply("import")).With(UI.Button.Apply("export"))))));
        TextMeshProUGUI Value => Edit.GetComponentsInChildren<TextMeshProUGUI>()[1];
        Button Import => Edit.GetComponentsInChildren<Button>()[0];
        Button Export => Edit.GetComponentsInChildren<Button>()[1];
        bool TryGetFilePath<T>(Func<T> ctor, string subpath, string name, out string path) where T : System.Windows.Forms.FileDialog
        {
            using (var dialog = ctor())
            {
                (dialog.InitialDirectory, dialog.Filter, dialog.FileName) =
                    (Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, subpath), "Texture Sources|*.png", $"{name}.png");
                return (dialog.ShowDialog(), path = dialog.FileName) is (System.Windows.Forms.DialogResult.OK, _);
            }
        }
        Action OnExport => () => Wrapper.GetTexture(Edit.name)
            .With(value => TryGetFilePath(() => new ExportDialog(), "export", value?.name ?? "na.png", out var path)
                .Maybe(() => path.TextureToFile(value)));
        Action OnImport => () => Wrapper.GetTexture(Edit.name)
            .With(value => TryGetFilePath(() => new ImportDialog(), "import", value?.name ?? "na.png", out var path)
                .Maybe(() => Wrapper.SetTexture(Edit.name, path.FileToTexture())));
        internal TextureEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper)
        {
            Import.onClick.AddListener(OnImport);
            Export.onClick.AddListener(OnExport);
        }
        internal override void Store(Modifications mod) =>
           (Check && Value.text.IsExtensionTexture())
            .Maybe(() => mod.TextureHashes[Edit.name] = Wrapper.GetTexture(Edit.name)?.name ?? "");
        internal override void Apply(Modifications mod) =>
            (Check = mod.TextureHashes.TryGetValue(Edit.name, out var value))
                .Maybe(() => Wrapper.SetTexture(Edit.name, (Value.text = value).HashToTexture()));
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
                .With(parent.Wrap).With(UI.Active.Apply(false))
                .With(UI.ContentLayout<VerticalLayoutGroup>(TextAnchor.UpperLeft).Apply(0).Apply(new (5,15,5,5)))) =>
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
            Toggles = new GameObject(name).With(listParent.Wrap)
                .With(UI.ContentLayout<VerticalLayoutGroup>(TextAnchor.UpperLeft).Apply(0).Apply(new(15, 15, 5, 5)))
                .With(UI.Label.Apply(270).Apply(name));
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
            UI.OnCustomHumanReady.Apply(UI.Active.Apply(true).Apply(go));
        Action<Unit> ToggleActive(GameObject go) =>
            _ => Toggle.With(Update).Value.IsDown().Maybe(() => go.SetActive(Status.Value = !Status.Value));
        static ConfigEntry<KeyboardShortcut> Toggle { get; set; }
        static ConfigEntry<bool> Status { get; set; }
        void OnCharacterSerialize(HumanData data, ZipArchive archive) =>
            archive.Save(CoordLimit.All.Merge(data, Store(), archive.LoadChara()));
        void OnCoordinateSerialize(HumanDataCoordinate _, ZipArchive archive) =>
            archive.Save(Store());
        void OnPreCoordinateReload(Human human, int type, ZipArchive archive) =>
            archive.Save(CoordLimit.All.Merge(human, Store(), archive.LoadChara()));
        void OnPostCoordinateReload(Human human, int type, ZipArchive archive) =>
            Apply(human.Transform(archive.LoadChara()));
        void OnCharacterDeserialize(Human human, CharaLimit limits, ZipArchive archive, ZipArchive storage) =>
            Apply(human.Transform(limits.Merge(archive.LoadTextures().LoadChara(), storage.LoadChara()).With(storage.Save)));
        void OnCoordinateDeserialize(Human human, HumanDataCoordinate coord, CoordLimit limits, ZipArchive archive) =>
            human.ReferenceExtension(current => Apply(limits.Merge(archive.LoadTextures().LoadCoord(), human.Transform(current.LoadChara()))));
        void Update(IEnumerable<EditGroup> groups) => groups.Do(group => group.Update());
        void Update() => Update([FaceGroup, BodyGroup, .. HairGroups.Values, .. ClothesGroups.Values, .. AccessoryGroups.Values]);
        internal static EditWindow Instance;
        internal static void Initialize()
        {
            UI.AnchorX = Plugin.Instance.Config.Bind("General", "Window AnchorX", 30.0f);
            UI.AnchorY = Plugin.Instance.Config.Bind("General", "Window AnchorY", -80.0f);
            Status = Plugin.Instance.Config.Bind("General", "Show Sardin Head (smells fishy)", false);
            Toggle = Plugin.Instance.Config.Bind("General", "Sardin Head toggle key", new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl));
            Util.Hook<HumanCustom>(() =>
            {
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
            new ModApplicator(data, actor.charFile.Transform(archive.LoadTextures().LoadChara()));
        static void OnPreCoordinateReload(Human human, int type, ZipArchive archive) =>
            new ModApplicator(human.data, archive.LoadChara().Transform(type));
        static void OnPreCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive) =>
            human.ReferenceExtension(current =>
                new ModApplicator(human.data, human.Transform(limits.Merge(human,
                    archive.LoadTextures().LoadCoord(), current.LoadTextures().LoadChara()))));
        static void OnPostActorHumanize(SaveData.Actor actor, Human human, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human.data));
        static void OnPostCoordinateReload(Human human, int type, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human.data));
        static void OnPostCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(applicator.Cleanup.Apply(human.data));
        internal static void Initialize()
        {
            Event.OnPreActorHumanize += OnPreActorHumanize;
            Event.OnPreCoordinateReload += OnPreCoordinateReload;
            Event.OnPreCoordinateDeserialize += OnPreCoordinateDeserialize;
            Event.OnPostActorHumanize += OnPostActorHumanize;
            Event.OnPostCoordinateReload += OnPostCoordinateReload;
            Event.OnPostCoordinateDeserialize += OnPostCoordinateDeserialize;
            Util.Hook<HumanCustom>(() =>
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