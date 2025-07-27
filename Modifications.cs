using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using Fishbone;
using CoastalSmell;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;

namespace SardineHead
{
    public partial class LegacyCharaMods
    {
        public static implicit operator CharaMods(LegacyCharaMods mods) => new()
        {
            Face = mods.Face
                .Select(entry => entry.Key.StartsWith("p_") ? new("/ct_face", entry.Value) : entry)
                .Concat(mods.Eyebrows)
                .Concat(mods.Eyelines)
                .Concat(mods.Eyes)
                .Concat(mods.Tooth)
                .DistinctBy(entry => entry.Key)
                .ToDictionary(entry => entry.Key, entry => entry.Value),
            Body = mods.Body
                .Select(entry => entry.Key.StartsWith("p_") ? new("/ct_body", entry.Value) : entry)
                .Concat(mods.Nails)
                .DistinctBy(entry => entry.Key)
                .ToDictionary(entry => entry.Key, entry => entry.Value),
            Hairs = mods.Coordinates
                .ToDictionary(entry => entry.Key, entry => entry.Value.Hair),
            Clothes = mods.Coordinates
                .ToDictionary(entry => entry.Key, entry => entry.Value.Clothes
                    .ToDictionary(entry => entry.Key, entry => entry.Value
                        .ToDictionary(entry => entry.Key.StartsWith("ct_") ? $"/{entry.Key}" : entry.Key, entry => entry.Value))),
            Accessories = mods.Coordinates
                .ToDictionary(entry => entry.Key, entry => entry.Value.Accessory),
        };
    }
    public partial class LegacyCoordMods
    {
        public static implicit operator CoordMods(LegacyCoordMods mods) => new()
        {
            Face = new(),
            Body = new(),
            Hairs = mods.Hair,
            Clothes = mods.Clothes.ToDictionary(entry => entry.Key, entry => entry.Value
                .ToDictionary(entry => entry.Key.StartsWith("ct_") ? $"/{entry.Key}" : entry.Key, entry => entry.Value)),
            Accessories = mods.Accessory
        };
    }
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
        internal partial CoordMods AsCoord(int index) => new()
        {
            Face = Face,
            Body = Body,
            Hairs = Hairs.TryGetValue(index, out var hairs) ? hairs : new(),
            Clothes = Clothes.TryGetValue(index, out var clothes) ? clothes : new(),
            Accessories = Accessories.TryGetValue(index, out var accessories) ? accessories : new(),
        };
        internal partial Func<CharaMods, CharaMods> Merge(CharaLimit limits) => mods => new()
        {
            Face = (limits & CharaLimit.Face) == CharaLimit.None ? Face : mods.Face,
            Body = (limits & CharaLimit.Body) == CharaLimit.None ? Body : mods.Body,
            Hairs = (limits & CharaLimit.Hair) == CharaLimit.None ? Hairs : mods.Hairs,
            Clothes = (limits & CharaLimit.Coorde) == CharaLimit.None ? Clothes : mods.Clothes,
            Accessories = (limits & CharaLimit.Coorde) == CharaLimit.None ? Accessories : mods.Accessories,
        };
        internal partial Func<CoordLimit, CoordMods, CharaMods> Merge(int index) => (limits, mods) => new()
        {
            Face = (limits & CoordLimit.FaceMakeup) == CoordLimit.None ? Face : mods.Face,
            Body = (limits & CoordLimit.BodyMakeup) == CoordLimit.None ? Body : mods.Body,
            Hairs = (limits & CoordLimit.Hair) == CoordLimit.None ? Hairs :
                Hairs.Where(entry => entry.Key != index).Concat([new(index, mods.Hairs)])
                    .ToDictionary(entry => entry.Key, entry => entry.Value),
            Clothes = (limits & CoordLimit.Clothes) == CoordLimit.None ? Clothes :
                Clothes.Where(entry => entry.Key != index).Concat([new(index, mods.Clothes)])
                    .ToDictionary(entry => entry.Key, entry => entry.Value),
            Accessories = (limits & CoordLimit.Accessory) == CoordLimit.None ? Accessories :
                Accessories.Where(entry => entry.Key != index).Concat([new(index, mods.Accessories)])
                    .ToDictionary(entry => entry.Key, entry => entry.Value),
        };
        static CharaMods()
        {
            Load = archive =>
                BonesToStuck<CharaMods>.Load(archive.With(Textures.Load), out var mods) ? mods :
                    BonesToStuck<LegacyCharaMods>.Load(archive, out var legacy) ? legacy : new();
            Save = (archive, mods) =>
                BonesToStuck<CharaMods>.Save(archive.With(Textures.Save.Apply(mods)), mods);
        }
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
        internal partial Func<CoordMods, CoordMods> Merge(CoordLimit limits) => mods => new()
        {
            Face = (limits & CoordLimit.FaceMakeup) == CoordLimit.None ? Face : mods.Face,
            Body = (limits & CoordLimit.BodyMakeup) == CoordLimit.None ? Body : mods.Body,
            Hairs = (limits & CoordLimit.Hair) == CoordLimit.None ? Hairs : mods.Hairs,
            Clothes = (limits & CoordLimit.Clothes) == CoordLimit.None ? Clothes : mods.Clothes,
            Accessories = (limits & CoordLimit.Accessory) == CoordLimit.None ? Accessories : mods.Accessories
        };
        static CoordMods()
        {
            Load = archive =>
                BonesToStuck<CoordMods>.Load(archive.With(Textures.Load), out var mods) ? mods :
                    BonesToStuck<LegacyCoordMods>.Load(archive, out var legacy) ? legacy : new();
            Save = (archive, mods) =>
                BonesToStuck<CoordMods>.Save(archive.With(Textures.Save.Apply(mods)), mods);
        }
    }
    internal static partial class Textures
    {
        static readonly string LegacyPath = Path.Combine(Plugin.Guid, "textures");
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
            entry => entry.FullName.StartsWith(TexturePath) || entry.FullName.StartsWith(LegacyPath);
        static Func<ZipArchiveEntry, bool> IsNotBuffered =
            entry => !IsExtension(entry.Name);
        static Action<ZipArchiveEntry> LoadTexture =
            entry => LoadBuffer.Apply(entry.Name).Apply(entry.Length)
                .ApplyDisposable(new BinaryReader(entry.Open())).Try(Plugin.Instance.Log.LogError);
        static Action<string, long, BinaryReader> LoadBuffer =
            (hash, size, reader) => Buffers[hash] = reader.ReadBytes((int)size);
        static Action<ZipArchive, string> SaveTexture =
           (archive, hash) => SaveTextureToArchive.Apply(Buffers[hash])
               .ApplyDisposable(new BinaryWriter(archive.CreateEntry(Path.Combine(TexturePath, hash)).Open()))
               .Try(Plugin.Instance.Log.LogError);
        static Action<byte[], BinaryWriter> SaveTextureToArchive =
            (data, writer) => writer.Write(data);
        static Func<ZipArchiveEntry, bool> IsExtensionEntry =
            entry => entry.FullName.StartsWith(Plugin.Guid) || entry.FullName.StartsWith(Plugin.Name);
        static Action<ZipArchiveEntry> Delete =
            entry => F.Try(entry.Delete, Plugin.Instance.Log.LogError);
        internal static Action<ZipArchive> Clean =
            archive => archive.Entries.Where(IsExtensionEntry).ForEach(Delete);
        static Textures()
        {
            IsExtension =
               hash => hash != null && Buffers.ContainsKey(hash);
            FromHash =
               hash => IsExtension(hash) ? BytesToTexture(Buffers[hash]) : default;
            FromFile =
                path => BytesToTexture(File.ReadAllBytes(path));
            ToFile =
                (tex, path) => File.WriteAllBytes(path, TextureToTexture2d(tex).EncodeToPNG());
            Load =
                archive => archive.Entries.Where(IsTextureEntry).Where(IsNotBuffered).ForEach(LoadTexture);
            Save =
                (mods, archive) => mods.ToTextures().Distinct().ForEach(SaveTexture.Apply(archive.With(Clean)));
        }
    }
}
