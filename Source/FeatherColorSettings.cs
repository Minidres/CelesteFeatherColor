namespace Celeste.Mod.FeatherColor {
    public enum ColorPreset {
        Default, Custom, RGB, CustomCycle, Red, Orange, Green, Cyan, Blue, Purple, Pink, White, Black, Rainbow,
        Trans, Bisexual, Lesbian
    }

    // Colors for the warning flash. "Default" is vanilla's red.
    public enum FlashPreset {
        Default, Custom, White, Black, Red, Orange, Yellow, Green, Cyan, Blue, Purple, Pink
    }

    public class FeatherColorSettings : EverestModuleSettings {
        public bool Enabled { get; set; } = true;

        // Cycle-style option, so it works from the pause menu inside a level.
        // "Custom" uses the hex value below.
        public ColorPreset Preset { get; set; } = ColorPreset.Default;

        // Text input: Everest only allows editing this from the main menu, not inside a level.
        [SettingMaxLength(6)]
        public string Hex { get; set; } = "FFD65C";

        // Used when Preset is "CustomCycle": up to 4 hex colors, faded through in order and looped.
        // Leave a slot empty to skip it. Main menu only (text input). Invalid entries are skipped.
        // Four short fields instead of one long list, because the menu row shows the typed value
        // next to its label and a long value makes the whole menu wider than the screen.
        [SettingMaxLength(6)]
        public string CycleColor1 { get; set; } = "FF3B3B";

        [SettingMaxLength(6)]
        public string CycleColor2 { get; set; } = "FFFFFF";

        [SettingMaxLength(6)]
        public string CycleColor3 { get; set; } = "3CF0FF";

        [SettingMaxLength(6)]
        public string CycleColor4 { get; set; } = "";

        // Used when Preset is "Sliders". Works inside a level. Hold left/right to scroll fast.
        [SettingRange(0, 255, true)]
        public int Red { get; set; } = 255;

        [SettingRange(0, 255, true)]
        public int Green { get; set; } = 214;

        [SettingRange(0, 255, true)]
        public int Blue { get; set; } = 92;

        // Used by Rainbow, the pride flag presets and Custom Cycle. 1 = slow, 10 = fast.
        [SettingRange(1, 10)]
        public int RainbowSpeed { get; set; } = 4;

        // Also tint the ring around bubble (shielded) feathers.
        public bool RecolorShield { get; set; } = false;

        // Off = the feather keeps its color until it runs out, with no warning flash.
        public bool Flashing { get; set; } = true;

        public FlashPreset FlashColor { get; set; } = FlashPreset.Default;

        // Also main menu only. Used when FlashColor is Custom. Default is vanilla's flash red.
        [SettingMaxLength(6)]
        public string FlashHex { get; set; } = "AC3232";
    }
}