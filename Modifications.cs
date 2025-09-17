using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using HarmonyLib;
using Character;
using Fishbone;
using CoastalSmell;

namespace SardineHead
{
    interface TextureMods
    {
        IEnumerable<string> ToTextures();
    }
    public partial class CharaMods : TextureMods
    {
        public IEnumerable<string> ToTextures() =>
            Face.Values.SelectMany(item => item.TextureHashes.Values)
                .Concat(Body.Values.SelectMany(item => item.TextureHashes.Values))
                .Concat(Hairs.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values))
                .Concat(Clothes.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values))
                .Concat(Accessories.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values));
    }
    public partial class CoordMods : TextureMods
    {
        public IEnumerable<string> ToTextures() =>
            Face.Values.SelectMany(item => item.TextureHashes.Values)
                .Concat(Body.Values.SelectMany(item => item.TextureHashes.Values))
                .Concat(Hairs.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values))
                .Concat(Clothes.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values))
                .Concat(Accessories.Values
                    .SelectMany(item => item.Values)
                    .SelectMany(item => item.TextureHashes.Values));
    }
    internal static partial class Textures
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
    }
    class ModApplicator
    {
        internal static event Action OnApplicationComplete = delegate { };
        static Dictionary<Human, CompositeDisposable> Current = new();
        Human Target;
        CoordMods Mods;
        static void Prepare(Human human) =>
            human.gameObject.GetComponent<ObservableDestroyTrigger>()
                .OnDestroyAsObservable().Subscribe(F.Apply(Dispose, human).Ignoring<Unit>());
        static void Dispose(Human human) =>
            (Current.TryGetValue(human, out var item) && Current.Remove(human)).Maybe(F.Apply(Dispose, item));
        static void Dispose(CompositeDisposable item) =>
            (!item.IsDisposed).Maybe(item.Dispose); 

        internal ModApplicator(Human human)
        {
            (Target, Mods) = (human, Extension.Coord<CharaMods, CoordMods>(human)); 
            Current.TryGetValue(Target, out var item)
                .Either(F.Apply(Prepare, Target), F.Apply(Dispose, item));
            Current[Target] = new CompositeDisposable();
            Hooks.OnFaceReady += OnFaceReady;
            Hooks.OnBodyReady += OnBodyReady;
            Hooks.OnClothesReady += OnClothesReady;
            Hooks.OnReloadingComplete += OnReloadingComplete;
            Current[Target].Add(Disposable.Create((Action)Clean));
        }
        void Clean() {
            Hooks.OnFaceReady -= OnFaceReady;
            Hooks.OnBodyReady -= OnBodyReady;
            Hooks.OnClothesReady -= OnClothesReady;
            Hooks.OnReloadingComplete -= OnReloadingComplete;
        }
        void Apply() =>
            Current[Target].Add(Scheduler.MainThread.Schedule(
                Il2CppSystem.TimeSpan.FromSeconds(0.1),
                F.Apply(Target.Apply, Mods) + OnApplicationComplete));
        void OnFaceReady(HumanFace item) =>
            (item.human == Target).Maybe(F.Apply(item.Apply, Mods));
        void OnBodyReady(HumanBody item) =>
            (item.human == Target).Maybe(F.Apply(item.Apply, Mods));
        void OnClothesReady(HumanCloth item, int index) =>
            (item.human == Target).Maybe(F.Apply(item.clothess[index].Apply, Mods, index));
        void OnReloadingComplete(Human human) =>
            (human == Target).Maybe(Apply);
    }
    static partial class Hooks
    {
        internal static event Action<HumanFace> OnFaceReady = delegate { };
        internal static event Action<HumanBody> OnBodyReady = delegate { };
        internal static event Action<HumanCloth, int> OnClothesReady = delegate { };
        internal static event Action<Human> OnReloadingComplete = delegate { };

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.CreateFaceTexture))]
        internal static void HumanFaceCreateFaceTexturePostfix(HumanFace __instance) =>
            OnFaceReady(__instance);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.CreateBodyTexture))]
        internal static void HumanBodyCreataBodyTexturePostfix(HumanBody __instance) =>
            OnBodyReady(__instance);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.CreateClothesTexture))]
        internal static void HumanClothCreateClothesTexturePostfix(HumanCloth __instance, int kind) =>
            OnClothesReady(__instance, kind);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Create))]
        static void HumanCreatePostfix(Human __result) =>
            OnReloadingComplete(__result);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Reload), [])]
        static void HumanReloadPostfix(Human __instance) =>
            OnReloadingComplete(__instance); 

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), [])]
        static void HumanReloadCoordinatePostfix(Human __instance) =>
            OnReloadingComplete(__instance); 

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human.Reloading), nameof(Human.Reloading.Dispose))]
        internal static void HumanReloadingDisposePostfix(Human.Reloading __instance) =>
            (!__instance._isReloading).Maybe(F.Apply(OnReloadingComplete, __instance._human));
    }
}
