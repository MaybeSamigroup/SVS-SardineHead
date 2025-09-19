using System.Linq;
using System.Collections.Generic;
using Mods = System.Collections.Generic.Dictionary<string, SardineHead.Modifications>;

namespace SardineHead
{
    public class LegacyCharaMods
    {
        public Mods Face { get; set; } = new();
        public Mods Eyebrows { get; set; } = new();
        public Mods Eyelines { get; set; } = new();
        public Mods Eyes { get; set; } = new();
        public Mods Tooth { get; set; } = new();
        public Mods Body { get; set; } = new();
        public Mods Nails { get; set; } = new();
        public Dictionary<int, LegacyCoordMods> Coordinates { get; set; } = new();

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
    public class LegacyCoordMods
    {
        public Dictionary<int, Mods> Hair { get; set; } = new();
        public Dictionary<int, Mods> Clothes { get; set; } = new();
        public Dictionary<int, Mods> Accessory { get; set; } = new();

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
}
 