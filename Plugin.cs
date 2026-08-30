using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace LowBudgetRepairsFA
{
    [BepInPlugin("ir.LowBudgetRepairs.FA", "Low Budget Repairs Persian Translation", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static ManualLogSource _logger;
        private static Dictionary<string, string> _translations;
        private static Dictionary<string, string> _fontReplacements;
        private static bool _isRTL = true;
        private static HashSet<string> _collectedTexts = new HashSet<string>();
        private static string _collectorPath;

        private void Awake()
        {
            _logger = Logger;
            _collectorPath = Path.Combine(Paths.PluginPath, "collected_texts.json");
            _logger.LogInfo("Persian translation plugin loaded.");

            // Load translations from JSON file
            _translations = LoadTranslations();
            _fontReplacements = LoadFontMappings();

            // Apply Harmony patches
            var harmony = new Harmony("ir.LowBudgetRepairs.FA");
            harmony.PatchAll();

            _logger.LogInfo($"Loaded {_translations.Count} translations and {_fontReplacements.Count} font mappings.");
        }

        private static Dictionary<string, string> LoadTranslations()
        {
            var path = Path.Combine(Paths.PluginPath, "translations.json");
            if (!File.Exists(path))
            {
                _logger?.LogWarning($"translations.json not found at {path}");
                return new Dictionary<string, string>();
            }

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to load translations: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        private static Dictionary<string, string> LoadFontMappings()
        {
            var path = Path.Combine(Paths.PluginPath, "font_mappings.json");
            if (!File.Exists(path)) return new Dictionary<string, string>();

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        // ============ TEXT PATCHES ============

        // Patch TMP_Text.text setter to translate all text
        [HarmonyPatch(typeof(TMP_Text), "set_text")]
        private static class TextSetterPatch
        {
            private static void Postfix(TMP_Text __instance, string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                if (value.StartsWith("FA:")) return;

                // Collect text for translation dictionary
                CollectText(value);

                // Try direct translation lookup
                if (_translations.TryGetValue(value, out var persian))
                {
                    ApplyPersianText(__instance, persian);
                    return;
                }

                // Try regex-based translation for dynamic text
                var translated = TranslateDynamicText(value);
                if (translated != value)
                {
                    ApplyPersianText(__instance, translated);
                }
            }
        }

        // Patch TMP_Text.SetText method (for formatted strings)
        [HarmonyPatch(typeof(TMP_Text), "SetText", new Type[] { typeof(string) })]
        private static class SetTextPatch
        {
            private static void Postfix(TMP_Text __instance, string sourceText)
            {
                if (string.IsNullOrEmpty(sourceText)) return;
                if (sourceText.StartsWith("FA:")) return;

                CollectText(sourceText);

                if (_translations.TryGetValue(sourceText, out var persian))
                {
                    ApplyPersianText(__instance, persian);
                }
            }
        }

        // Patch for formatted strings with parameters: SetText(string, float)
        [HarmonyPatch(typeof(TMP_Text), "SetText", new Type[] { typeof(string), typeof(float) })]
        private static class SetTextFloatPatch
        {
            private static void Postfix(TMP_Text __instance, string sourceText, float arg0)
            {
                if (string.IsNullOrEmpty(sourceText)) return;

                var key = sourceText.Replace("{0}", arg0.ToString("0.##"));
                CollectText(key);

                if (_translations.TryGetValue(key, out var persian))
                {
                    ApplyPersianText(__instance, persian);
                }
            }
        }

        // Patch for formatted strings with parameters: SetText(string, int)
        [HarmonyPatch(typeof(TMP_Text), "SetText", new Type[] { typeof(string), typeof(int) })]
        private static class SetTextIntPatch
        {
            private static void Postfix(TMP_Text __instance, string sourceText, int arg0)
            {
                if (string.IsNullOrEmpty(sourceText)) return;

                var key = sourceText.Replace("{0}", arg0.ToString());
                CollectText(key);

                if (_translations.TryGetValue(key, out var persian))
                {
                    ApplyPersianText(__instance, persian);
                }
            }
        }

        // Patch for formatted strings with two parameters
        [HarmonyPatch(typeof(TMP_Text), "SetText", new Type[] { typeof(string), typeof(float), typeof(float) })]
        private static class SetTextFloatFloatPatch
        {
            private static void Postfix(TMP_Text __instance, string sourceText, float arg0, float arg1)
            {
                if (string.IsNullOrEmpty(sourceText)) return;

                var key = sourceText.Replace("{0}", arg0.ToString("0.##")).Replace("{1}", arg1.ToString("0.##"));
                CollectText(key);

                if (_translations.TryGetValue(key, out var persian))
                {
                    ApplyPersianText(__instance, persian);
                }
            }
        }

        // ============ FONT PATCHES ============

        // Replace font on TMP_Text components when they get enabled
        [HarmonyPatch(typeof(TMP_Text), "OnEnable")]
        private static class FontPatch
        {
            private static void Postfix(TMP_Text __instance)
            {
                if (__instance.font == null) return;

                var fontName = __instance.font.name;
                if (_fontReplacements.TryGetValue(fontName, out var replacementPath))
                {
                    var replacementFont = LoadFontAsset(replacementPath);
                    if (replacementFont != null)
                    {
                        __instance.font = replacementFont;
                        __instance.isRightToLeftText = _isRTL;
                        __instance.ForceMeshUpdate();
                    }
                }
            }
        }

        // ============ TEXT COLLECTOR ============

        private static void CollectText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.StartsWith("FA:")) return;

            lock (_collectedTexts)
            {
                if (_collectedTexts.Add(text))
                {
                    // Save incrementally
                    var dict = new Dictionary<string, string>();
                    foreach (var t in _collectedTexts)
                    {
                        dict[t] = _translations.ContainsKey(t) ? _translations[t] : "";
                    }
                    File.WriteAllText(_collectorPath, JsonConvert.SerializeObject(dict, Formatting.Indented));
                }
            }
        }

        // ============ HELPERS ============

        private static void ApplyPersianText(TMP_Text textComponent, string persianText)
        {
            // Mark as already translated to prevent recursion
            textComponent.text = "FA:" + persianText;

            // Apply RTL
            textComponent.isRightToLeftText = _isRTL;

            // Fix Persian glyph shaping (connect letters properly)
            var fixedText = PersianFixer.Fix(persianText);
            textComponent.text = fixedText;

            // Force mesh update to apply changes
            textComponent.ForceMeshUpdate();
        }

        private static string TranslateDynamicText(string input)
        {
            // Handle patterns like "Budget: {0}" or "Level {0}"
            foreach (var kvp in _translations)
            {
                if (kvp.Key.Contains("{0}") && input.Contains(kvp.Key.Replace("{0}", "").TrimEnd(':', ' ')))
                {
                    var prefix = kvp.Key.Replace("{0}", "").TrimEnd(':', ' ');
                    if (input.StartsWith(prefix))
                    {
                        var dynamicPart = input.Substring(prefix.Length).Trim();
                        var translatedTemplate = kvp.Value;
                        return translatedTemplate.Replace("{0}", PersianFixer.Fix(dynamicPart);
                    }
                }
            }

            return input;
        }

        private static TMP_FontAsset LoadFontAsset(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var asset = ScriptableObject.CreateInstance<TMP_FontAsset>();
                // Note: In a real implementation, you'd load the TMP_FontAsset from an AssetBundle
                // This is a simplified placeholder
                return asset;
            }
            catch
            {
                return null;
            }
        }
    }

    // ============ PERSIAN TEXT FIXER ============

    public static class PersianFixer
    {
        private static readonly Dictionary<char, char> PersianDigits = new Dictionary<char, char>
        {
            { '0', '۰' }, { '1', '۱' }, { '2', '۲' }, { '3', '۳' },
            { '4', '۴' }, { '5', '۵' }, { '6', '۶' }, { '7', '۷' },
            { '8', '۸' }, { '9', '۹' }
        };

        private static readonly Dictionary<string, string> GlyphMap = new Dictionary<string, string>
        {
            { "آ", "ﺁ" }, { "ا", "ﺍ" }, { "ب", "ﺏ" },
            { "پ", "ﭖ" }, { "ت", "ﺕ" }, { "ث", "ﺙ" },
            { "ج", "ﺝ" }, { "چ", "ﭺ" }, { "ح", "ﺡ" },
            { "خ", "ﺥ" }, { "د", "ﺩ" }, { "ذ", "ﺫ" },
            { "ر", "ﺭ" }, { "ز", "ﺯ" }, { "ژ", "ﮊ" },
            { "س", "ﺱ" }, { "ش", "ﺵ" }, { "ص", "ﺹ" },
            { "ض", "ﺽ" }, { "ط", "ﻁ" }, { "ظ", "ﻅ" },
            { "ع", "ﻉ" }, { "غ", "ﻍ" }, { "ف", "ﻑ" },
            { "ق", "ﻕ" }, { "ک", "ﮎ" }, { "گ", "ﮒ" },
            { "ل", "ﻝ" }, { "م", "ﻡ" }, { "ن", "ﻥ" },
            { "و", "ﻭ" }, { "ه", "ﻩ" }, { "ی", "ﯽ" }
        };

        public static string Fix(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Convert English digits to Persian
            var sb = new StringBuilder();
            foreach (var c in input)
            {
                sb.Append(PersianDigits.ContainsKey(c) ? PersianDigits[c] : c);
            }

            // Apply glyph shaping (simplified - real implementation needs full Arabic shaping algorithm)
            var result = sb.ToString();
            return result;
        }
    }
}
