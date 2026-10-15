using UnityEngine;

namespace RuleRunner.View
{
    /// <summary>Grey-box colors shared by the map, the runner and the UI legend.</summary>
    public static class Palette
    {
        public static readonly Color Background = new Color32(0x1d, 0x23, 0x2e, 0xff);
        public static readonly Color Ground = new Color32(0x8a, 0x93, 0xa3, 0xff);
        public static readonly Color GroundDark = new Color32(0x5c, 0x64, 0x73, 0xff);
        public static readonly Color PitHole = new Color32(0x0d, 0x10, 0x16, 0xff);
        public static readonly Color Spike = new Color32(0xe0, 0x4f, 0x4f, 0xff);
        public static readonly Color LowWall = new Color32(0xc2, 0x8f, 0x5a, 0xff);
        public static readonly Color HighWall = new Color32(0x3d, 0x45, 0x55, 0xff);
        public static readonly Color TrapUp = new Color32(0xf0, 0x9a, 0x2c, 0xff);
        public static readonly Color TrapDown = new Color32(0x7a, 0x5a, 0x33, 0xff);
        public static readonly Color Orb = new Color32(0x4f, 0xd6, 0xe8, 0xff);
        public static readonly Color Finish = new Color32(0x5c, 0xc9, 0x6b, 0xff);
        public static readonly Color Runner = new Color32(0xf5, 0xd0, 0x3b, 0xff);
        public static readonly Color RunnerEye = new Color32(0x1d, 0x23, 0x2e, 0xff);
        public static readonly Color Dead = new Color32(0xe0, 0x4f, 0x4f, 0xff);
    }
}
