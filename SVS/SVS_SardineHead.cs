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
            HumanCustom.Instance.ThumbnailSelectWindow.transform.parent.parent.parent.gameObject;
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
        internal static void Wrap(this Transform tf, GameObject go) => go.transform.SetParent(tf);
        static GameObject Canvas =>
            new GameObject(Plugin.Name).With(HumanCustom.Instance.transform.Find("UI").Find("Root").Wrap).With(go =>
            {
                go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                go.AddComponent<CanvasScaler>().With(ui =>
                {
                    ui.referenceResolution = new(1920, 1080);
                    ui.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    ui.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                });
                go.AddComponent<GraphicRaycaster>();
                go.AddComponent<ObservableUpdateTrigger>();
            });
        internal static ConfigEntry<float> AnchorX;
        internal static ConfigEntry<float> AnchorY;
        static Action<Unit> UpdateAnchorPosition(RectTransform ui) =>
            _ => (AnchorX.Value, AnchorY.Value) = (ui.anchoredPosition.x, ui.anchoredPosition.y);
        internal static GameObject Window =>
            UnityEngine.Object.Instantiate(ReferenceWindow, Canvas.transform).With(go =>
            {
                UnityEngine.Object.Destroy(go.transform.Find("Category").gameObject);
                UnityEngine.Object.Destroy(go.transform.Find("TabBG").gameObject);
                UnityEngine.Object.Destroy(go.transform.Find("EditWindow").gameObject);
                go.GetComponentInParent<ObservableUpdateTrigger>().UpdateAsObservable().Subscribe(UpdateAnchorPosition(
                go.GetComponent<RectTransform>().With(ui =>
                {
                    ui.anchorMin = new(0.0f, 1.0f);
                    ui.anchorMax = new(0.0f, 1.0f);
                    ui.pivot = new(0.0f, 1.0f);
                    ui.sizeDelta = new(800, 800);
                    ui.anchoredPosition = new(AnchorX.Value, AnchorY.Value);
                })));
                go.GetComponentInChildren<TextMeshProUGUI>().SetText(Plugin.Name);
                go.AddComponent<UI_DragWindow>();
            });
        internal static Tuple<Transform, Transform> Panels(Transform parent) =>
            Panels(new GameObject("Panels").With(parent.Wrap).With(go =>
            {
                go.AddComponent<RectTransform>()
                    .With(ui => (ui.sizeDelta, ui.anchorMin, ui.anchorMax) = (new(800, 750), new(0, 0), new(1, 1)));
                go.AddComponent<LayoutElement>()
                    .With(ui => (ui.preferredWidth, ui.preferredHeight) = (800, 750));
                go.AddComponent<HorizontalLayoutGroup>()
                    .With(ui => (ui.childControlWidth, ui.childControlHeight) = (true, false));
                go.AddComponent<ToggleGroup>().allowSwitchOff = false;
            }));
        static Tuple<Transform, Transform> Panels(GameObject go) =>
            new(ScrollView(go.transform, 300), ScrollView(go.transform, 500));
        internal static Transform ScrollView(Transform parent, float width) =>
            UnityEngine.Object.Instantiate(ReferenceScrollView, parent)
                .With(Resize, width, 750f).With(EnableLayout, width, 750f)
                .transform.With(ResizeScrollbar).With(ResizeViewport, width - 10)
                .Find("Viewport").Find("Content").With(tf =>
                {
                    Enumerable.Range(0, tf.childCount)
                        .Select(tf.GetChild).Select(tf => tf.gameObject)
                        .Do(UnityEngine.Object.Destroy);
                    tf.gameObject.GetComponent<ContentSizeFitter>()
                        .With(ui => (ui.horizontalFit, ui.verticalFit) =
                            (ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize));
                });
        static void Resize(float width, float height, GameObject go) =>
            go.GetComponent<RectTransform>().With(ui =>
                 (ui.anchorMin, ui.anchorMax, ui.sizeDelta) =
                 (new(0, 1), new(0, 1), new(width, height)));
        static void EnableLayout(float width, float height, GameObject go) =>
            go.AddComponent<LayoutElement>().With(ui => (ui.preferredWidth, ui.preferredHeight) = (width, height));
        static void ResizeScrollbar(Transform tf) => tf.Find("Scrollbar Vertical").gameObject.With(go =>
            go.GetComponent<RectTransform>()
                .With(ui => (ui.anchorMin, ui.anchorMax, ui.pivot, ui.offsetMin, ui.offsetMax, ui.sizeDelta) =
                    (new(1, 1), new(1, 1), new(1, 1), new(0, 0), new(-5, 0), new(10, 750))));
        static void ResizeViewport(float width, Transform tf) => tf.Find("Viewport").gameObject.With(go =>
        {
            go.GetComponent<RectMask2D>().enabled = true;
            go.GetComponent<RectTransform>()
                .With(ui => (ui.anchorMin, ui.anchorMax, ui.pivot, ui.sizeDelta, ui.offsetMin, ui.offsetMax) =
                    (new(0, 0), new(1, 1), new(0, 1), new(width, 750), new(0, 0), new(0, 0)));
        });
        internal static GameObject Toggle(string name, Transform parent) =>
            UnityEngine.Object.Instantiate(ReferenceToggle, parent).With(go =>
            {
                UnityEngine.Object.Destroy(go.GetComponent<CategoryKindToggle>());
                go.SetActive(true);
                go.GetComponent<Toggle>().group = parent.GetComponentInParent<ToggleGroup>();
                go.GetComponentInChildren<TextMeshProUGUI>().With(ui =>
                {
                    ui.enableAutoSizing = false;
                    ui.overflowMode = TextOverflowModes.Ellipsis;
                    ui.verticalAlignment = VerticalAlignmentOptions.Top;
                    ui.horizontalAlignment = HorizontalAlignmentOptions.Left;
                    ui.SetText(name);
                });
                go.AddComponent<LayoutElement>().With(ui =>
                    (ui.preferredWidth, ui.preferredHeight) = (290, 30));
            });
        internal static Action<GameObject> Rename(string name) => go => go.name = name;
        internal static Action<GameObject> Active = go => go.SetActive(true);
        internal static Action<GameObject> Inactive = go => go.SetActive(false);
        internal static Action<GameObject> Configure<T>(Action<T> action) where T : Component => go => go.GetComponent<T>().With(action);
        internal static void FitLayout<T>(this GameObject go) where T : HorizontalOrVerticalLayoutGroup
        {
            go.AddComponent<RectTransform>().localScale = new(1, 1);
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
        internal static void Label(int size, string text, GameObject parent) =>
            UnityEngine.Object.Instantiate(ReferenceLabel, parent.transform)
               .With(go => UnityEngine.Object.Destroy(go.GetComponent<Localize.Translate.UIBindData>()))
               .With(go => go.AddComponent<LayoutElement>().With(ui =>
               {
                   ui.preferredWidth = size;
                   ui.preferredHeight = 30;
               }))
               .With(go => go.GetComponent<TextMeshProUGUI>().With(ui =>
               {
                   ui.enableAutoSizing = false;
                   ui.autoSizeTextContainer = false;
                   ui.overflowMode = TextOverflowModes.Ellipsis;
                   ui.verticalAlignment = VerticalAlignmentOptions.Top;
                   ui.horizontalAlignment = HorizontalAlignmentOptions.Left;
                   ui.SetText(text);
               }));
        internal static void Check(string label, GameObject parent) =>
            UnityEngine.Object.Instantiate(ReferenceCheck, parent.transform).With(go =>
            {
                go.AddComponent<LayoutElement>().With(ui => (ui.preferredWidth, ui.preferredHeight) = (250, 30));
                go.GetComponentInChildren<Toggle>().isOn = false;
                go.transform.Find("cb_back").GetComponent<RectTransform>().With(ui =>
                {
                    ui.anchorMin = new(0.0f, 1.0f);
                    ui.anchorMax = new(0.0f, 1.0f);
                    ui.offsetMin = new(0.0f, -24.0f);
                    ui.offsetMax = new(24.0f, 0.0f);
                });
                go.GetComponentInChildren<TextMeshProUGUI>().With(ui =>
                {
                    ui.enableAutoSizing = false;
                    ui.autoSizeTextContainer = false;
                    ui.overflowMode = TextOverflowModes.Ellipsis;
                    ui.verticalAlignment = VerticalAlignmentOptions.Top;
                    ui.horizontalAlignment = HorizontalAlignmentOptions.Left;
                    ui.SetText(label);
                });
            });
        internal static void Input(TMP_InputField.ContentType type, GameObject parent) =>
            UnityEngine.Object.Instantiate(ReferenceInput, parent.transform).With(go =>
            {
                go.AddComponent<LayoutElement>().preferredWidth = 150;
                go.GetComponent<TMP_InputField>().With(ui =>
                {
                    ui.contentType = type;
                    ui.characterLimit = 10;
                    ui.restoreOriginalTextOnEscape = true;
                });
            });
        internal static void Color(GameObject parent) =>
            UnityEngine.Object.Instantiate(ReferenceColor, parent.transform)
                .With(go => go.GetComponent<LayoutElement>().preferredWidth = 100);
        internal static void Slider(GameObject parent) =>
            UnityEngine.Object.Instantiate(ReferenceSlider, parent.transform)
                .With(go => go.AddComponent<LayoutElement>()
                    .With(ui => (ui.preferredWidth, ui.preferredHeight) = (100, 20)));
        internal static void Button(string label, GameObject parent) =>
            UnityEngine.Object.Instantiate(ReferenceButton, parent.transform).With(go =>
            {
                go.AddComponent<LayoutElement>().With(ui =>
                {
                    ui.preferredWidth = 100;
                    ui.preferredHeight = 30;
                });
                go.GetComponentsInChildren<TextMeshProUGUI>().Do(ui =>
                {
                    ui.autoSizeTextContainer = true;
                    ui.SetText(label);
                });
                go.GetComponent<Button>().interactable = true;
            });
    }
    internal abstract class CommonEdit
    {
        protected static GameObject PrepareArchetype(string name, Transform parent) =>
            new GameObject(name).With(parent.Wrap)
                .With(UI.Inactive).With(UI.Check, "PropertyName")
                .With(UI.FitLayout<HorizontalLayoutGroup>)
                .With(UI.Configure<HorizontalLayoutGroup>(ui =>
                    (ui.padding, ui.spacing, ui.childAlignment) = (new(5, 15, 2, 2), 0, TextAnchor.UpperLeft)))
                .With(UI.Configure<LayoutElement>(ui => ui.preferredWidth = 500));
        protected abstract GameObject Archetype { get; }
        protected MaterialWrapper Wrapper;
        protected GameObject Edit;
        CommonEdit(MaterialWrapper wrapper) => Wrapper = wrapper;
        protected CommonEdit(string name, Transform parent, MaterialWrapper wrapper) : this(wrapper) =>
            Edit = UnityEngine.Object.Instantiate(Archetype, parent).With(UI.Active)
                .With(UI.Rename(name)).With(go => go.GetComponentInChildren<TextMeshProUGUI>().With(ui => ui.SetText(name)));
        internal bool Check
        {
            get => Edit.GetComponentInChildren<Toggle>().isOn;
            set => Edit.GetComponentInChildren<Toggle>().isOn = value;
        }
        internal abstract void Store(Modifications mod);
        internal abstract void Apply(Modifications mod);
        internal abstract void Update();
    }
    internal class RenderingEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("BoolEdit", parent.transform).With(UI.Check, "Enabled");
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
        void UpdateGet() =>
            Value.SetIsOnWithoutNotify(Wrapper.Renderer.enabled);
        void UpdateSet() =>
            Wrapper.Renderer.enabled = Value.isOn;
        void UpdateValue() =>
            Check.Either(UpdateGet, UpdateSet);
        internal override void Update() =>
            Label.With(UpdateValue).SetText(Wrapper.Renderer.gameObject.activeInHierarchy ? "Enabled(Active)" : "Enabled(Inactive)");
    }
    internal class IntEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("IntEdit", parent.transform)
                .With(UI.Label, 80, "int:")
                .With(UI.Input, TMP_InputField.ContentType.IntegerNumber);
        TMP_InputField Input => Edit.GetComponentInChildren<TMP_InputField>();
        Action<string> OnChange =>
            input => int.TryParse(input, out var value).Maybe(Edit.name.Curry(value, Wrapper.SetInt));
        internal IntEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper) =>
            Input.onValueChanged.AddListener(OnChange);
        internal override void Store(Modifications mod) =>
            Check.Maybe(() => mod.IntValues[Edit.name] = int.TryParse(Input.text, out var value) ? value : Wrapper.GetInt(Edit.name));
        internal override void Apply(Modifications mod) =>
            (Check = mod.IntValues.TryGetValue(Edit.name, out var value))
                .Maybe(() => Input.With(Edit.name.Curry(value, Wrapper.SetInt)).SetTextWithoutNotify(value.ToString()));
        internal override void Update() =>
            Check.Either(
                () => Input.SetTextWithoutNotify(Wrapper.GetInt(Edit.name).ToString()),
                () => int.TryParse(Input.text, out var value).Maybe(() => Wrapper.SetInt(Edit.name, value)));
    }
    internal class FloatEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("FloatEdit", parent.transform)
                .With(UI.Label, 80, "float:")
                .With(UI.Input, TMP_InputField.ContentType.DecimalNumber);
        TMP_InputField Input => Edit.GetComponentInChildren<TMP_InputField>();
        Action<string> OnChange =>
            input => float.TryParse(input, out float value).Maybe(Edit.name.Curry(value, Wrapper.SetFloat));
        internal FloatEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper) =>
            Input.onValueChanged.AddListener(OnChange);
        internal override void Store(Modifications mod) =>
            Check.Maybe(() => mod.FloatValues[Edit.name] = float.TryParse(Input.text, out var value) ? value : Wrapper.GetFloat(Edit.name));
        internal override void Apply(Modifications mod) =>
            (Check = mod.FloatValues.TryGetValue(Edit.name, out var value))
                .Maybe(() => Input.With(Edit.name.Curry(value, Wrapper.SetFloat)).SetTextWithoutNotify(value.ToString()));
        internal override void Update() =>
            Check.Either(
                () => Input.SetTextWithoutNotify(Wrapper.GetInt(Edit.name).ToString()),
                () => float.TryParse(Input.text, out var value).Maybe(() => Wrapper.SetFloat(Edit.name, value)));
    }
    internal class RangeEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("RangeEdit", parent.transform).With(UI.Label, 80, "").With(UI.Slider);
        Slider Input => Edit.GetComponentInChildren<Slider>();
        TextMeshProUGUI Value => Edit.GetComponentsInChildren<TextMeshProUGUI>()[^1];
        Action<float> OnChange => value => Wrapper.SetRange(Edit.name, value.With(() => Value.SetText(value.ToString())));
        RangeEdit(string name, Transform parent, MaterialWrapper wrapper, Vector2 limits) : base(name, parent, wrapper) =>
            (Input.minValue, Input.maxValue) = (limits.x, limits.y);
        internal RangeEdit(string name, Transform parent, MaterialWrapper wrapper) : this(name, parent, wrapper, wrapper.RangeLimits[name]) =>
             Input.onValueChanged.AddListener(OnChange);
        internal override void Store(Modifications mod) =>
            Check.Maybe(() => mod.RangeValues[Edit.name] = Input.value);
        internal override void Apply(Modifications mod) =>
            (Check = mod.RangeValues.TryGetValue(Edit.name, out var value))
                .Maybe(() => Input.With(Edit.name.Curry(value, Wrapper.SetRange)).SetValueWithoutNotify(value));
        internal override void Update() =>
            Check.Either(
                () => Input.SetValueWithoutNotify(Wrapper.GetRange(Edit.name)),
                () => Wrapper.SetRange(Edit.name, Input.value));
    }
    internal class ColorEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("ColorEdit", parent.transform).With(UI.Color);
        ThumbnailColor Input => Edit.GetComponentInChildren<ThumbnailColor>();
        Func<Color, bool> OnChange => value => true.With(Edit.name.Curry(value, Wrapper.SetColor));
        Func<Color> OnFetch => () => Wrapper.GetColor(Edit.name);
        internal ColorEdit(string name, Transform parent, MaterialWrapper wrapper) : base(name, parent, wrapper) =>
             Input.Initialize(Edit.name, OnFetch, OnChange, true, true);
        internal override void Store(Modifications mod) =>
            Check.Maybe(() => mod.ColorValues[Edit.name] = Input.GetColor());
        internal override void Apply(Modifications mod) =>
            (Check = mod.ColorValues.TryGetValue(Edit.name, out var value))
                .Maybe(() => Input.With(Edit.name.Curry((Color)value, Wrapper.SetColor)).SetColor(value));
        internal override void Update() =>
            Check.Either(
                () => Input.SetColor(Wrapper.GetColor(Edit.name)),
                () => Wrapper.SetColor(Edit.name, Input.GetColor()));
    }
    internal class VectorEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("VectorEdit", parent.transform)
                .With(go => new GameObject("VectorValues").With(go.transform.Wrap)
                .With(UI.FitLayout<VerticalLayoutGroup>).With(vt =>
                {
                    vt.GetComponent<LayoutElement>().preferredWidth = 230;
                    new GameObject("x").With(vt.transform.Wrap)
                        .With(UI.FitLayout<HorizontalLayoutGroup>)
                        .With(UI.Label, 80, "float(x):")
                        .With(UI.Input, TMP_InputField.ContentType.DecimalNumber);
                    new GameObject("y").With(vt.transform.Wrap)
                        .With(UI.FitLayout<HorizontalLayoutGroup>)
                        .With(UI.Label, 80, "float(y):")
                        .With(UI.Input, TMP_InputField.ContentType.DecimalNumber);
                    new GameObject("z").With(vt.transform.Wrap)
                        .With(UI.FitLayout<HorizontalLayoutGroup>)
                        .With(UI.Label, 80, "float(z):")
                        .With(UI.Input, TMP_InputField.ContentType.DecimalNumber);
                    new GameObject("w").With(vt.transform.Wrap)
                        .With(UI.FitLayout<HorizontalLayoutGroup>)
                        .With(UI.Label, 80, "float(w):")
                        .With(UI.Input, TMP_InputField.ContentType.DecimalNumber);
                }));
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
                .Maybe(Edit.name.Curry(Value = new Vector4(value, Value.y, Value.z, Value.w), Wrapper.SetVector));
        Action<string> OnChangeY =>
            input => float.TryParse(input, out float value)
                .Maybe(Edit.name.Curry(Value = new Vector4(Value.x, value, Value.z, Value.w), Wrapper.SetVector));
        Action<string> OnChangeZ =>
            input => float.TryParse(input, out float value)
                .Maybe(Edit.name.Curry(Value = new Vector4(Value.x, Value.y, value, Value.w), Wrapper.SetVector));
        Action<string> OnChangeW =>
            input => float.TryParse(input, out float value)
                .Maybe(Edit.name.Curry(Value = new Vector4(Value.x, Value.y, Value.z, value), Wrapper.SetVector));
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
                .Maybe(() => Wrapper.SetVector(Edit.name, Value.With(Apply)));
        internal override void Update() =>
            Check.Either(
                () => Apply(Wrapper.GetVector(Edit.name)),
                () => Wrapper.SetVector(Edit.name, Value));
    }
    internal class TextureEdit : CommonEdit
    {
        static GameObject Base { get; set; }
        protected override GameObject Archetype => Base;
        internal static void PrepareArchetype(GameObject parent) =>
            Base = PrepareArchetype("TextureEdit", parent.transform)
                .With(go => new GameObject("TextureValues").With(go.transform.Wrap)
                .With(UI.FitLayout<VerticalLayoutGroup>).With(vt =>
                        new GameObject("Buttons").With(vt.With(UI.Label, 200, "").transform.Wrap)
                            .With(UI.FitLayout<HorizontalLayoutGroup>)
                            .With(hr => hr.With(UI.Button, "import").With(UI.Button, "export"))));
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
            (!Check).Maybe(() => Wrapper.GetTexture(Edit.name).With(value =>
                Value.SetText((Export.interactable = value is not null) ? value.name : "")));
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
                .With(parent.Wrap).With(UI.Inactive)
                .With(UI.FitLayout<VerticalLayoutGroup>)
                .With(UI.Configure<VerticalLayoutGroup>(ui => ui.childAlignment = TextAnchor.UpperLeft))) =>
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
                .With(UI.FitLayout<VerticalLayoutGroup>)
                .With(UI.Configure<VerticalLayoutGroup>(ui => ui.padding = new(10, 10, 0, 10)))
                .With(UI.Label, 290, name);
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
            UniTask.NextFrame().ContinueWith((Action)(() => go.active = Status.Value));
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
            Apply(limits.Merge(archive.LoadTextures().LoadCoord(), human.Transform(human.ToArchive().LoadChara())));
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
            new ModApplicator(human.data, human.Transform(limits.Merge(human,
                archive.LoadTextures().LoadCoord(), human.ToArchive().LoadTextures().LoadChara())));
        static void OnPostActorHumanize(SaveData.Actor actor, Human human, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(() => applicator.Cleanup(human.data));
        static void OnPostCoordinateReload(Human human, int type, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(() => applicator.Cleanup(human.data));
        static void OnPostCoordinateDeserialize(Human human, HumanDataCoordinate _, CoordLimit limits, ZipArchive archive) =>
            Current.TryGetValue(human.data, out var applicator).Maybe(() => applicator.Cleanup(human.data));
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