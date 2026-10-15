using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.FeatherColor {
    public class FeatherColorModule : EverestModule {
        public static FeatherColorModule Instance { get; private set; }
        public override Type SettingsType => typeof(FeatherColorSettings);
        public static FeatherColorSettings Settings => (FeatherColorSettings) Instance._Settings;

        private static readonly Color VanillaStarFly = Calc.HexToColor("FFD65C");
        private static (Color c1, Color c2)? origFlying, origCollect;
        private static Color? appliedParticleColor; // null = vanilla particles in place

        // Hex parse cache
        private static string cachedHex;
        private static Color cachedHexColor = VanillaStarFly;
        private static string cachedPalette;
        private static Color[] cachedPaletteColors = new Color[0];
        private static string cachedFlashHex;
        private static Color cachedFlashHexColor; // filled on first use (avoids touching Player statics at load)

        public FeatherColorModule() { Instance = this; }

        public override void Load() {
            On.Celeste.Player.StartStarFly += OnStartStarFly;
            On.Celeste.Player.Update += OnPlayerUpdate;
            On.Celeste.FlyFeather.Update += OnFeatherUpdate;
            On.Celeste.FlyFeather.Render += OnFeatherRender;
            On.Celeste.FlyFeather.ctor_Vector2_bool_bool += OnFeatherCtor;
            On.Celeste.TextMenu.Update += OnMenuUpdate;
            On.Celeste.TextMenu.Render += OnMenuRender;
        }

        public override void Unload() {
            On.Celeste.Player.StartStarFly -= OnStartStarFly;
            On.Celeste.Player.Update -= OnPlayerUpdate;
            On.Celeste.FlyFeather.Update -= OnFeatherUpdate;
            On.Celeste.FlyFeather.Render -= OnFeatherRender;
            On.Celeste.FlyFeather.ctor_Vector2_bool_bool -= OnFeatherCtor;
            On.Celeste.TextMenu.Update -= OnMenuUpdate;
            On.Celeste.TextMenu.Render -= OnMenuRender;
            RestoreParticles();
            appliedParticleColor = null;
        }

        // Cheap enough to call every frame, so changes from the pause menu apply instantly.
        private static Color GetColor() {
            var s = Settings;
            if (!s.Enabled) return VanillaStarFly;

            switch (s.Preset) {
                case ColorPreset.RGB: return new Color(s.Red, s.Green, s.Blue);
                case ColorPreset.Default: return VanillaStarFly;
                case ColorPreset.Red: return Calc.HexToColor("FF3B3B");
                case ColorPreset.Orange: return Calc.HexToColor("FF6400");
                case ColorPreset.Green: return Calc.HexToColor("4CFF6A");
                case ColorPreset.Cyan: return Calc.HexToColor("3CF0FF");
                case ColorPreset.Blue: return Calc.HexToColor("4C7BFF");
                case ColorPreset.Purple: return Calc.HexToColor("A455FF");
                case ColorPreset.Pink: return Calc.HexToColor("FF6FD8");
                case ColorPreset.White: return Color.White;
                case ColorPreset.Black: return Calc.HexToColor("2B2B2B");
                case ColorPreset.Rainbow: return Rainbow();
                case ColorPreset.Trans: return Cycle(TransFlag);
                case ColorPreset.Bisexual: return Cycle(BisexualFlag);
                case ColorPreset.Lesbian: return Cycle(LesbianFlag);
                case ColorPreset.CustomCycle: {
                    string text = s.CycleColor1 + "," + s.CycleColor2 + "," + s.CycleColor3 + "," + s.CycleColor4;
                    if (text != cachedPalette) {
                        cachedPalette = text;
                        cachedPaletteColors = ParsePalette(text);
                    }
                    return cachedPaletteColors.Length == 0 ? VanillaStarFly : Cycle(cachedPaletteColors);
                }
                default: return CustomHexColor(s.Hex);
            }
        }

        private static Color CustomHexColor(string hex) {
            hex ??= "";
            if (hex != cachedHex) {
                cachedHex = hex;
                cachedHexColor = ParseHex(hex, VanillaStarFly);
            }
            return cachedHexColor;
        }

        private static Color ParseHex(string hex, Color fallback) {
            try {
                string h = hex.TrimStart('#');
                if (h.Length == 6) return Calc.HexToColor(h);
            } catch (Exception) { }
            return fallback;
        }

        // The color the sprite shows on the "off" beats of the low-timer warning flash.
        private static Color GetFlashColor() {
            var s = Settings;
            switch (s.FlashColor) {
                case FlashPreset.White: return Color.White;
                case FlashPreset.Black: return Color.Black;
                case FlashPreset.Red: return Calc.HexToColor("FF3B3B");
                case FlashPreset.Orange: return Calc.HexToColor("FF9A3C");
                case FlashPreset.Yellow: return VanillaStarFly;
                case FlashPreset.Green: return Calc.HexToColor("4CFF6A");
                case FlashPreset.Cyan: return Calc.HexToColor("3CF0FF");
                case FlashPreset.Blue: return Calc.HexToColor("4C7BFF");
                case FlashPreset.Purple: return Calc.HexToColor("A455FF");
                case FlashPreset.Pink: return Calc.HexToColor("FF6FD8");
                case FlashPreset.Custom: {
                    string hex = s.FlashHex ?? "";
                    if (hex != cachedFlashHex) {
                        cachedFlashHex = hex;
                        cachedFlashHexColor = ParseHex(hex, Player.NormalHairColor);
                    }
                    return cachedFlashHexColor;
                }
                default: return Player.NormalHairColor;
            }
        }

        // Pride flag palettes. The feather fades smoothly from stripe to stripe, top to bottom, then loops.
        private static readonly Color[] TransFlag = Hexes("00B8FF", "D04798", "FFFFFF", "D04798", "00B8FF");
        private static readonly Color[] BisexualFlag = Hexes("D60270", "9B4F96", "0038A8");
        private static readonly Color[] LesbianFlag = Hexes("FF3D00", "FF6500", "FF8533", "FFFFFF", "D04798", "B83485", "A30262");

        // "FF0000, 00ff00;#0000FF" -> three colors. Anything that isn't exactly 6 hex digits is skipped.
        private static Color[] ParsePalette(string text) {
            var colors = new List<Color>();
            foreach (string part in text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                string h = part.TrimStart('#');
                if (h.Length != 6) continue;
                bool valid = true;
                foreach (char c in h) {
                    if (!Uri.IsHexDigit(c)) { valid = false; break; }
                }
                if (valid) colors.Add(Calc.HexToColor(h));
            }
            return colors.ToArray();
        }

        private static Color[] Hexes(params string[] hex) {
            var colors = new Color[hex.Length];
            for (int i = 0; i < hex.Length; i++) colors[i] = Calc.HexToColor(hex[i]);
            return colors;
        }

        // Each stripe lasts about as long as one rainbow hue step (8s / speed / 6), so the pace feels
        // the same whichever animated preset is chosen. Wraps from the last stripe back to the first.
        private static Color Cycle(Color[] palette) {
            long period = (8000 / Math.Max(1, Settings.RainbowSpeed)) * palette.Length / 6;
            period = Math.Max(period, 1);
            float t = (Environment.TickCount64 % period) / (float) period * palette.Length;
            int i = (int) t;
            float f = t - i;
            return Color.Lerp(palette[i % palette.Length], palette[(i + 1) % palette.Length], f);
        }

        // One full hue cycle takes 8s / RainbowSpeed (speed 4 = 2s). Saturation and value fixed at 1.
        private static Color Rainbow() {
            long period = 8000 / Math.Max(1, Settings.RainbowSpeed);
            float h = (Environment.TickCount64 % period) / (float) period * 6f;
            int i = (int) h;
            float f = h - i, q = 1f - f;
            switch (i) {
                case 0: return new Color(1f, f, 0f);
                case 1: return new Color(q, 1f, 0f);
                case 2: return new Color(0f, 1f, f);
                case 3: return new Color(0f, q, 1f);
                case 4: return new Color(f, 0f, 1f);
                default: return new Color(1f, 0f, q);
            }
        }

        private static bool OnStartStarFly(On.Celeste.Player.orig_StartStarFly orig, Player self) {
            Color color = GetColor();
            new DynamicData(self).Set("starFlyColor", color);
            ApplyParticles(color);
            return orig(self);
        }

        private static void OnPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self) {
            Color color = GetColor();
            var data = new DynamicData(self);
            data.Set("starFlyColor", color);
            ApplyParticles(color);
            orig(self);

            if (self.StateMachine.State == Player.StStarFly && !data.Get<bool>("starFlyTransforming")) {
                if (data.Get<float>("starFlyTimer") >= 0.5f) {
                    // Normal flight: keep the sprite and hair tinted with the current color.
                    self.Sprite.Color = color;
                    self.Hair.Color = color;
                } else if (Settings.Enabled && self.Sprite.Color == Player.NormalHairColor) {
                    // Low-timer warning. Vanilla alternates Sprite.Color between starFlyColor and
                    // NormalHairColor (hardcoded red) every 0.05s. We swap the red beat for our
                    // flash color, or for the feather color itself, which means no visible flash.
                    // The next vanilla toggle then sees "not starFlyColor" and flips back, so the
                    // alternation keeps working.
                    self.Sprite.Color = Settings.Flashing ? GetFlashColor() : color;
                    // Vanilla's UpdateHair already copied the red into Hair.Color this frame
                    // (it runs after the state machine), so fix the hair as well.
                    self.Hair.Color = self.Sprite.Color;
                }
            }
        }

        private static void TintFeather(FlyFeather self) {
            var data = new DynamicData(self);
            Color tint = GetColor();
            data.Get<Sprite>("sprite").Color = tint;
            data.Get<VertexLight>("light").Color = tint;
        }

        // Tint at creation, so a feather in a new room is already the right color on its first frame.
        // Entities in the incoming room don't Update until the screen transition ends,
        // which is why they show the vanilla color for a moment.
        private static void OnFeatherCtor(On.Celeste.FlyFeather.orig_ctor_Vector2_bool_bool orig,
                FlyFeather self, Vector2 position, bool shielded, bool singleUse) {
            orig(self, position, shielded, singleUse);
            TintFeather(self);
        }

        private static void OnFeatherUpdate(On.Celeste.FlyFeather.orig_Update orig, FlyFeather self) {
            orig(self);
            TintFeather(self);
        }

        // Vanilla draws the bubble ring in Color.White inside Render. We draw the same circle again
        // in our color on top, with identical position, radius and resolution, so it covers the white.
        private static void OnFeatherRender(On.Celeste.FlyFeather.orig_Render orig, FlyFeather self) {
            TintFeather(self);
            orig(self);
            if (!Settings.Enabled || !Settings.RecolorShield) return;

            var data = new DynamicData(self);
            if (!data.Get<bool>("shielded")) return;
            var sprite = data.Get<Sprite>("sprite");
            if (!sprite.Visible) return;
            var wiggle = data.Get<Wiggler>("shieldRadiusWiggle");
            Draw.Circle(self.Position + sprite.Position, 10f - wiggle.Value * 2f, GetColor(), 3);
        }

        // ---- Mod Options menu: group the rarely used rows into dropdowns ----
        // Everest builds the page from the settings class. Once our page exists, we pull some of its rows out
        // and put them inside TextMenuExt.SubMenu dropdowns, the same kind of expandable section other mods
        // use, which animate open and closed by themselves.

        // Menus we have already looked at, so each one is only rearranged once.
        private static readonly ConditionalWeakTable<TextMenu, object> checkedMenus = new();

        private static void OnMenuUpdate(On.Celeste.TextMenu.orig_Update orig, TextMenu self) {
            GroupMenuRows(self);
            orig(self);
        }

        // Render can run before the first Update, so do it here too, otherwise the ungrouped rows
        // would show for one frame when the menu opens.
        private static void OnMenuRender(On.Celeste.TextMenu.orig_Render orig, TextMenu self) {
            GroupMenuRows(self);
            orig(self);
        }

        private static void GroupMenuRows(TextMenu menu) {
            if (checkedMenus.TryGetValue(menu, out _)) return;
            checkedMenus.Add(menu, new object());

            // Only touch our own page: it has both of these rows.
            if (FindMenuItem(menu, Dialog.Clean("MODOPTIONS_FEATHERCOLOR_PRESET")) == null
                    || FindMenuItem(menu, Dialog.Clean("MODOPTIONS_FEATHERCOLOR_ENABLED")) == null)
                return;

            // The Custom Color dropdown goes right after the Recolor Bubble Ring row.
            // Inside it: hex, RGB sliders, speed, then the four cycle colors.
            Group(menu, "MODOPTIONS_FEATHERCOLOR_CUSTOMMENU", "Custom Color Settings", "RECOLORSHIELD",
                "HEX", "RED", "GREEN", "BLUE", "RAINBOWSPEED",
                "CYCLECOLOR1", "CYCLECOLOR2", "CYCLECOLOR3", "CYCLECOLOR4");

            Group(menu, "MODOPTIONS_FEATHERCOLOR_FLASHMENU", "Warning Flash Settings", null,
                "FLASHING", "FLASHCOLOR", "FLASHHEX");
        }

        // Moves the rows named by these dialog keys (MODOPTIONS_FEATHERCOLOR_<key>) into one dropdown,
        // placed right after the row named by insertAfterKey, or where the first of them used to be if null.
        private static void Group(TextMenu menu, string titleKey, string fallbackTitle, string insertAfterKey, params string[] rowKeys) {
            var rows = new List<TextMenu.Item>();
            foreach (string key in rowKeys) {
                var item = FindMenuItem(menu, Dialog.Clean("MODOPTIONS_FEATHERCOLOR_" + key));
                if (item != null) rows.Add(item);
            }
            if (rows.Count == 0) return;

            int index = rows.Min(row => menu.Items.IndexOf(row));
            foreach (var row in rows) menu.Items.Remove(row);

            if (insertAfterKey != null) {
                // Look the anchor up after the rows were removed, so its index is current.
                var anchor = FindMenuItem(menu, Dialog.Clean("MODOPTIONS_FEATHERCOLOR_" + insertAfterKey));
                int anchorIndex = anchor == null ? -1 : menu.Items.IndexOf(anchor);
                if (anchorIndex >= 0) index = anchorIndex + 1;
            }
            index = Math.Min(index, menu.Items.Count);

            string title = Dialog.Has(titleKey) ? Dialog.Clean(titleKey) : fallbackTitle;
            var sub = new TextMenuExt.SubMenu(title, false);
            menu.Insert(index, sub);          // add the dropdown to the menu first, so it has a Container
            foreach (var row in rows) sub.Add(row);
        }

        // Finds a menu row by its label. Menu rows keep their text in a public "Label" field; text-input
        // rows show "Label: value", so a match on the label followed by a colon counts too.
        private static TextMenu.Item FindMenuItem(TextMenu menu, string label) {
            foreach (TextMenu.Item item in menu.Items) {
                var field = item.GetType().GetField("Label", BindingFlags.Public | BindingFlags.Instance);
                if (field?.GetValue(item) is string text && (text == label || text.StartsWith(label + ":")))
                    return item;
            }
            return null;
        }

        private static void ApplyParticles(Color color) {
            if (FlyFeather.P_Flying == null || FlyFeather.P_Collect == null) return;
            origFlying ??= (FlyFeather.P_Flying.Color, FlyFeather.P_Flying.Color2);
            origCollect ??= (FlyFeather.P_Collect.Color, FlyFeather.P_Collect.Color2);

            if (!Settings.Enabled) {
                if (appliedParticleColor != null) { RestoreParticles(); appliedParticleColor = null; }
                return;
            }
            if (appliedParticleColor == color) return;
            appliedParticleColor = color;

            Color lighter = Color.Lerp(color, Color.White, 0.5f);
            FlyFeather.P_Flying.Color = color;
            FlyFeather.P_Flying.Color2 = lighter;
            FlyFeather.P_Collect.Color = color;
            FlyFeather.P_Collect.Color2 = lighter;
        }

        private static void RestoreParticles() {
            if (origFlying is { } f && FlyFeather.P_Flying != null) {
                FlyFeather.P_Flying.Color = f.c1;
                FlyFeather.P_Flying.Color2 = f.c2;
            }
            if (origCollect is { } c && FlyFeather.P_Collect != null) {
                FlyFeather.P_Collect.Color = c.c1;
                FlyFeather.P_Collect.Color2 = c.c2;
            }
        }
    }
}