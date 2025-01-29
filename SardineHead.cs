using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Manager;
using UniRx;
using UniRx.Triggers;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using Character;
using CharacterCreation;

namespace SardineHead
{
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
        internal static GameObject Button
        {
            get => SV.Config.ConfigWindow.Instance.transform
                .Find("Canvas").Find("Background").Find("SubWindow").Find("System").Find("btn").gameObject;
        }
    }
    internal static class UIFactory
    {
        // tweaks to make local namespace clean
        internal static T With<T>(this T input, Action action)
        {
            action();
            return input;
        }
        internal static T With<T>(this T input, Action<T> action)
        {
            action(input);
            return input;
        }
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
                .With(tf => tf.gameObject.Window()).Content().With(Cleanup).With(content =>
                {
                    content.GetComponent<VerticalLayoutGroup>().With(ui =>
                    {
                        ui.childForceExpandWidth = false;
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
        internal static Action UpdateContent(HumanCloth.Clothes[] clothes, int index) =>
            clothes[index].cusClothesCmp != null ? () => UpdateContent(clothes[index]) : Hide;
        internal static void UpdateContent(HumanCloth.Clothes clothes) =>
            ContentRoot.With(Show).With(content =>
            {
                Enumerable.Range(0, clothes.ctCreateClothes.Count)
                    .Where(idx => null != clothes.ctCreateClothes[idx])
                    .Do(idx =>
                    {
                        content.gameObject.View(clothes.ctCreateClothes[idx]._matCreate,
                            clothes.cusClothesCmp.Rebuilds(clothes.ctCreateClothes[idx])[idx]);
                    });
            });
        internal static Func<int, bool>[] Rebuilds(this ChaClothesComponent cmp, CustomTextureCreate ctc) =>
            [id => cmp.Rebuild01(ctc, id), id => cmp.Rebuild02(ctc, id), id => cmp.Rebuild03(ctc, id), id => cmp.RebuildAccessory(ctc, id)];
        internal static void UpdateContent(CustomTextureControl ctc) =>
            ContentRoot.With(Show).With(content =>
            {
                content.gameObject.View(ctc._matCreate, id => { ctc._shaderID = id; return ctc.SetNewCreateTexture(); });
                content.gameObject.View(ctc._matDraw, id => { ctc._shaderID = id; return ctc.SetNewCreateTexture(); });
            });
        internal static Action UpdateContent(HumanHair.Hair[] list, int index) =>
            list[index].cusHairCmp != null ? () => UpdateContent(list[index].cusHairCmp) : Hide;
        internal static Action UpdateContent(HumanAccessory.Accessory[] list, int index) =>
            list[index].cusAcsCmp != null ? () => UpdateContent(list[index].cusAcsCmp) : Hide;
        internal static void UpdateContent(ChaCustomHairComponent cmp) =>
            UpdateContent(Enumerable.Concat(cmp.rendHair, Enumerable.Concat(cmp.rendLines, cmp.rendAccessory)));
        internal static void UpdateContent(ChaAccessoryComponent cmp) =>
            UpdateContent(Enumerable.Concat(cmp?.rendNormal, Enumerable.Concat(cmp.rendAlpha, cmp.rendHair)));

        internal static void UpdateContent(IEnumerable<Renderer> renderers) =>
            ContentRoot.With(Show).With(content =>
            {
                renderers.Where(rend => rend != null && rend.material != null).Do(rend =>
                {
                    content.gameObject.View(rend.material, id => true);
                });
            });
        internal static void View(this GameObject go, Material mat, Func<int, bool> update)
        {
            UnityEngine.Object
                .Instantiate(UIRef.SectionTitle, go.transform)
                .GetComponentInChildren<TextMeshProUGUI>().SetText($"{mat.name}/{mat.shader.name}");
            go.Edit(mat, mat.shader, update);
        }
        internal static void Edit(this GameObject go, Material prop, Shader shdr, Func<int, bool> update) =>
            Enumerable.Range(0, shdr.GetPropertyCount()).Do(index =>
                new GameObject(shdr.GetPropertyName(index)).With(go.transform.Wrap).With(Label)
                    .Edit(index, shdr.GetPropertyNameId(index), prop, shdr, update));
        internal static void Label(this GameObject go)
        {
            go.AddComponent<RectTransform>().With(ui =>
            {
                ui.localScale = new(1.0f, 1.0f);
                ui.anchorMin = new(0.0f, 1.0f);
                ui.anchorMax = new(0.0f, 1.0f);
                ui.pivot = new(0.0f, 1.0f);
                ui.sizeDelta = new(470, 20);
            });
            go.AddComponent<HorizontalLayoutGroup>().With(ui =>
            {
                ui.padding = new(10, 10, 2, 2);
                ui.childControlWidth = true;
            });
            go.AddComponent<ContentSizeFitter>().With(ui =>
            {
                ui.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ui.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            });
            UnityEngine.Object.Instantiate(UIRef.Label, go.transform).With(label =>
            {
                label.AddComponent<LayoutElement>().preferredWidth = 180;
                label.GetComponent<TextMeshProUGUI>().With(ui =>
                {
                    ui.fontSize = 12;
                    ui.SetText(go.name);
                });
            });
        }
        internal static GameObject Edit(this GameObject go, int index, int id, Material prop, Shader shdr, Func<int, bool> update) =>
            shdr.GetPropertyType(index) switch
            {
                ShaderPropertyType.Int =>
                    go.IntEdit(value =>
                    {
                        prop.SetInt(id, value);
                        update(id);
                    }, prop.GetInt(id)),
                ShaderPropertyType.Float =>
                    go.FloatEdit(value =>
                    {
                        prop.SetFloat(id, value);
                        update(id);
                    }, prop.GetFloat(id)),
                ShaderPropertyType.Range =>
                    go.RangeEdit(value =>
                    {
                        prop.SetFloat(id, value);
                        update(id);
                    }, prop.GetFloat(id), shdr.GetPropertyRangeLimits(index)),
                ShaderPropertyType.Color =>
                    go.ColorEdit(value =>
                    {
                        prop.SetColor(id, value);
                        return update(id);
                    }, prop.GetColor(id)),
                ShaderPropertyType.Texture =>
                    go.TextureEdit(value =>
                    {
                        prop.SetTexture(id, value);
                        update(id);
                    }, prop.GetTexture(id)),
                _ => go.CannotEdit()
            };

        internal static Action<S> Compose<S, T>(this Action<T> action, Func<S, T> func) => input => action(func(input));
        internal static GameObject CannotEdit(this GameObject go)
        {
            UnityEngine.Object.Instantiate(UIRef.Label, go.transform).With(label =>
            {
                label.AddComponent<LayoutElement>().preferredWidth = 180;
                label.GetComponent<TextMeshProUGUI>().With(ui =>
                {
                    ui.fontSize = 12;
                    ui.SetText("Can't edit now");
                });
            });
            return go;
        }
        internal static GameObject IntEdit(this GameObject go, Action<int> update, int value)
        {
            UnityEngine.Object.Instantiate(UIRef.Label, go.transform).With(label =>
            {
                label.AddComponent<LayoutElement>().preferredWidth = 80;
                label.GetComponent<TextMeshProUGUI>().With(ui =>
                {
                    ui.fontSize = 12;
                    ui.SetText("int:");
                });
            });
            UnityEngine.Object.Instantiate(UIRef.Input, go.transform).With(input =>
            {
                input.AddComponent<LayoutElement>().preferredWidth = 150;
                input.GetComponent<TMP_InputField>().With(ui =>
                {
                    ui.contentType = TMP_InputField.ContentType.IntegerNumber;
                    ui.characterLimit = 10;
                    ui.onValueChanged.AddListener(update.Compose<string, int>(int.Parse));
                    ui.restoreOriginalTextOnEscape = true;
                    ui.SetText(value.ToString());
                    ui.textComponent.With(txt =>
                    {
                        txt.fontSize = 12;
                        txt.alignment = TextAlignmentOptions.Right;
                    });
                });
            });
            return go;
        }
        internal static GameObject FloatEdit(this GameObject go, Action<float> update, float value)
        {
            UnityEngine.Object.Instantiate(UIRef.Label, go.transform).With(label =>
            {
                label.AddComponent<LayoutElement>().preferredWidth = 80;
                label.GetComponent<TextMeshProUGUI>().With(ui =>
                {
                    ui.fontSize = 12;
                    ui.SetText("float:");
                });
            });
            UnityEngine.Object.Instantiate(UIRef.Input, go.transform).With(input =>
            {
                input.AddComponent<LayoutElement>().preferredWidth = 150;
                input.GetComponent<TMP_InputField>().With(ui =>
                {
                    ui.contentType = TMP_InputField.ContentType.DecimalNumber;
                    ui.characterLimit = 10;
                    ui.onValueChanged.AddListener(update.Compose<string, float>(float.Parse));
                    ui.restoreOriginalTextOnEscape = true;
                    ui.SetText(value.ToString());
                    ui.textComponent.With(txt =>
                    {
                        txt.fontSize = 12;
                        txt.alignment = TextAlignmentOptions.Right;
                    });
                });
                input.GetComponentInChildren<TextMeshProUGUI>().With(ui =>
                {
                    ui.alignment = TextAlignmentOptions.Right;
                });
            });
            return go;
        }
        internal static GameObject RangeEdit(this GameObject go, Action<float> update, float value, Vector2 range)
        {
            UnityEngine.Object.Instantiate(UIRef.Label, go.transform).With(label =>
            {
                label.AddComponent<LayoutElement>().preferredWidth = 80;
                label.GetComponent<TextMeshProUGUI>().With(ui =>
                {
                    ui.fontSize = 12;
                    ui.SetText(value.ToString());
                    Action<float> onUpdate = input => ui.SetText(input.ToString());
                    UnityEngine.Object.Instantiate(UIRef.Slider, go.transform).With(input =>
                    {
                        input.AddComponent<LayoutElement>().preferredWidth = 150;
                        input.GetComponent<Slider>().With(ui =>
                        {
                            ui.minValue = range.x;
                            ui.maxValue = range.y;
                            ui.onValueChanged.AddListener(update);
                            ui.onValueChanged.AddListener(onUpdate);
                        });
                    });
                });
            });
            return go;
        }
        internal static GameObject ColorEdit(this GameObject go, Func<Color, bool> update, Color value)
        {
            UnityEngine.Object.Instantiate(UIRef.Color, go.transform).With(input =>
            {
                input.AddComponent<LayoutElement>().preferredWidth = 150;
                input.GetComponent<ThumbnailColor>().With(ui =>
                {
                    Func<Color> dummy = () => value;
                    ui.Initialize(go.name, dummy, update, true, true);
                    ui.SetColor(value);
                });
            });
            return go;
        }
        internal static GameObject TextureEdit(this GameObject go, Action<Texture> update, Texture value)
        {
            UnityEngine.Object.Instantiate(UIRef.Input, go.transform).With(input =>
            {
                input.AddComponent<LayoutElement>().preferredWidth = 150;
                input.GetComponent<TMP_InputField>().With(ui =>
                {
                    ui.contentType = TMP_InputField.ContentType.Custom;
                    ui.characterValidation = TMP_InputField.CharacterValidation.None;
                    ui.characterLimit = 256;
                    ui.restoreOriginalTextOnEscape = true;
                    ui.textComponent.With(txt =>
                    {
                        txt.fontSize = 12;
                        txt.alignment = TextAlignmentOptions.Left;
                    });
                    ui.readOnly = false;
                    ui.SetText(value?.name ?? "");
                    ui.onValueChanged.AddListener((Action<string>)(input => value.name = input));
                });
            });
            UnityEngine.Object.Instantiate(UIRef.Button, go.transform).With(button =>
            {
                button.AddComponent<LayoutElement>().With(ui =>
                {
                    ui.preferredWidth = 60;
                    ui.preferredHeight = 25;
                });
                button.GetComponentInChildren<TextMeshProUGUI>().With(ui =>
                {
                    ui.autoSizeTextContainer = true;
                    ui.fontSize = 12;
                    ui.SetText("import");
                });
                button.GetComponent<Button>().With(ui =>
                {
                    ui.onClick.AddListener(ImportTexture(value, update));
                });
                button.active = true;
            });
            UnityEngine.Object.Instantiate(UIRef.Button, go.transform).With(button =>
            {
                button.AddComponent<LayoutElement>().With(ui =>
                {
                    ui.preferredWidth = 60;
                    ui.preferredHeight = 25;
                });
                button.GetComponentInChildren<TextMeshProUGUI>().With(ui =>
                {
                    ui.autoSizeTextContainer = true;
                    ui.fontSize = 12;
                    ui.SetText("export");
                });
                button.GetComponent<Button>().With(ui =>
                {
                    ui.enabled = value != null;
                    ui.onClick.AddListener(ExportTexture(value));
                });
                button.active = true;
            });
            return go;
        }
        internal static string ImportPath(this string name) =>
            Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, "import", $"{name}.png");
        internal static string ExportPath(this string name) =>
           Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, "export", $"{name}.png");
        internal static Action ImportTexture(Texture texture, Action<Texture> update)
        {
            return () =>
            {
                if (File.Exists(texture.name.ImportPath()))
                {
                    new Texture2D(texture.width, texture.height).With(t2d =>
                    {
                        ImageConversion.LoadImage(t2d, File.ReadAllBytes(texture.name.ImportPath()));
                        RenderTexture raw = new RenderTexture(texture.width, texture.height, 0);
                        Graphics.Blit(t2d, raw);
                        update(raw);
                    });
                }
            };
        }
        internal static Action ExportTexture(Texture texture)
        {
            return () =>
            {
                new Texture2D(texture.width, texture.height).With(t2d =>
                {
                    RenderTexture.GetTemporary(texture.width, texture.height, 0).With(raw =>
                    {
                        RenderTexture.active = RenderTexture.active.With(_ =>
                        {
                            RenderTexture.active = raw;
                            GL.Clear(false, true, new Color());
                            Graphics.Blit(texture, raw);
                            t2d.ReadPixels(new Rect(0, 0, raw.width, raw.height), 0, 0);
                            File.WriteAllBytes(texture.name.ExportPath(), t2d.EncodeToPNG().ToArray());
                        });
                        RenderTexture.ReleaseTemporary(raw);
                    });
                });
            };
        }
        internal static void Show() =>
             ContentRoot?.With(Cleanup)?.parent?.parent?.parent?.parent?.gameObject?.SetActive(true);
        internal static void Hide() =>
            ContentRoot?.With(Cleanup)?.parent?.parent?.parent?.parent?.gameObject?.SetActive(false);
        internal static Il2CppSystem.Threading.CancellationTokenSource RefreshCanceler = new Il2CppSystem.Threading.CancellationTokenSource();
        internal static UniTask.Awaiter RefreshAwaiter = UniTask.CompletedTask.GetAwaiter();
        internal static Action Refresh = Hide;
        internal static void CancelRefresh() =>
            Enumerable.Range(0, RefreshAwaiter.IsCompleted ? 0 : 1).Do(_ => RefreshCanceler.Cancel());
        internal static void ScheduleRefresh() =>
            RefreshAwaiter = RefreshAwaiter.task.Status.IsCompleted() ?
                UniTask.NextFrame(RefreshCanceler.Token, true).GetAwaiter().With(await =>
                {
                    await.UnsafeOnCompleted(Refresh);
                }) : RefreshAwaiter;
        internal static Action Initialize = () =>
            ContentRoot = new GameObject(Plugin.Name)
            .UI(Scene.ActiveScene.GetRootGameObjects()
                .Where(item => "CustomScene".Equals(item.name))
                .First().transform.Find("UI").Find("Root"))
                .With(CancelRefresh).With(ScheduleRefresh);
        internal static Action Dispose = () => {
            RefreshCanceler.Cancel();
            Refresh = Hide;
        };
    }
    internal static class Extension
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CategorySelection), nameof(CategorySelection.OpenView), typeof(int))]
        internal static void CategorySelectionSetPostfix(CategorySelection __instance, int index)
        {
            (UIFactory.Refresh = __instance._no switch
            {
                0 => () => UIFactory.UpdateContent(Human.list[0].face.customTexCtrlFace),
                1 => () => UIFactory.UpdateContent(Human.list[0].body.customTexCtrlBody),
                2 => index < Human.list[0].hair.hairs.Count ? () => UIFactory.UpdateContent(Human.list[0].hair.hairs, index)() : UIFactory.Hide,
                3 => index < Human.list[0].cloth.clothess.Count ? () => UIFactory.UpdateContent(Human.list[0].cloth.clothess, index)() : UIFactory.Hide,
                4 => index < Human.list[0].acs.accessories.Count ? () => UIFactory.UpdateContent(Human.list[0].acs.accessories, index)() : UIFactory.Hide,
                _ => UIFactory.Hide
            }).With(UIFactory.CancelRefresh).With(UIFactory.ScheduleRefresh);
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ThumbnailButton), nameof(ThumbnailButton.Set))]
        internal static void ThumbnailButtonOpenPostfix() => UIFactory.ScheduleRefresh();
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Scene), nameof(Scene.LoadStart), typeof(Scene.Data), typeof(bool))]
        internal static void SceneLoadStartPostfix(Scene.Data data, ref UniTask __result) =>
            __result = "CustomScene".Equals(data.LevelName) ? __result.ContinueWith(UIFactory.Initialize) : __result;
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
        public const string Name = "SardineHead";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "0.1.0";
        private Harmony[] Patches;
        public override void Load()
        {
            Patches = new[]
            {
                Harmony.CreateAndPatchAll(typeof(Extension), $"{Name}.Hooks")
            };
        }
        public override bool Unload()
        {
            Patches.Do(patch => patch.UnpatchSelf());
            return base.Unload();
        }
    }
}