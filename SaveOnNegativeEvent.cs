using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace SaveOnNegativeEvent
{
    public class SaveOnNegativeEventSettings : ModSettings
    {
        public const int DefaultCooldownSeconds = 30;
        public const int MinimumCooldownSeconds = 1;
        public const int MaximumCooldownSeconds = 200;

        public bool appendEventLabel = false;
        public int cooldownSeconds = DefaultCooldownSeconds;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref appendEventLabel, "appendEventLabel", false);
            Scribe_Values.Look(ref cooldownSeconds, "cooldownSeconds", DefaultCooldownSeconds);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                cooldownSeconds = Math.Max(MinimumCooldownSeconds, Math.Min(MaximumCooldownSeconds, cooldownSeconds));
            }
        }
    }

    public class SaveOnNegativeEventMod : Mod
    {
        public static SaveOnNegativeEventSettings settings;

        public SaveOnNegativeEventMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<SaveOnNegativeEventSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.CheckboxLabeled(
                "SaveOnNegativeEvent.AppendEventLabel.Label".Translate(),
                ref settings.appendEventLabel,
                "SaveOnNegativeEvent.AppendEventLabel.Tooltip".Translate());

            listingStandard.Gap();
            listingStandard.Label("SaveOnNegativeEvent.Cooldown.Label".Translate(settings.cooldownSeconds));

            settings.cooldownSeconds = (int)listingStandard.Slider(
                settings.cooldownSeconds,
                SaveOnNegativeEventSettings.MinimumCooldownSeconds,
                SaveOnNegativeEventSettings.MaximumCooldownSeconds);

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "SaveOnNegativeEvent.SettingsCategory".Translate();
        }
    }

    [StaticConstructorOnStartup]
    public static class Patcher
    {
        static Patcher()
        {
            var harmony = new Harmony("cruesoe.save.negative");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(LetterStack), "ReceiveLetter", new Type[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class ReceiveLetter_Patch
    {
        private const string BaseSaveName = "Bad Event";
        private const int MaximumEventLabelLength = 80;
        private static readonly char[] InvalidFileNameCharacters = Path.GetInvalidFileNameChars();
        private static float lastSaveTime = float.NegativeInfinity;

        [HarmonyPostfix]
        public static void Postfix(Letter let)
        {
            if (Current.ProgramState != ProgramState.Playing ||
                (let.def != LetterDefOf.ThreatBig && let.def != LetterDefOf.NegativeEvent))
            {
                return;
            }

            SaveOnNegativeEventSettings settings = SaveOnNegativeEventMod.settings;
            float currentTime = Time.realtimeSinceStartup;
            if (settings == null || currentTime - lastSaveTime < settings.cooldownSeconds)
            {
                return;
            }

            string fileName = BuildSaveName(let, settings.appendEventLabel);

            try
            {
                GameDataSaveLoader.SaveGame(fileName);
                lastSaveTime = currentTime;
                Messages.Message(
                    "SaveOnNegativeEvent.SaveSucceeded".Translate(fileName),
                    MessageTypeDefOf.SilentInput,
                    false);
            }
            catch (Exception exception)
            {
                Log.Error($"[Save on Negative Event] Failed to save '{fileName}': {exception}");
            }
        }

        internal static string BuildSaveName(Letter letter, bool appendEventLabel)
        {
            if (!appendEventLabel)
            {
                return BaseSaveName;
            }

            string safeLabel = SanitizeEventLabel(letter.Label.ToString());
            return safeLabel.Length == 0 ? BaseSaveName : BaseSaveName + "_" + safeLabel;
        }

        internal static string SanitizeEventLabel(string label)
        {
            StringBuilder result = new StringBuilder(Math.Min(label.Length, MaximumEventLabelLength));
            bool previousCharacterWasReplacement = false;

            foreach (char character in label)
            {
                bool replaceCharacter = Array.IndexOf(InvalidFileNameCharacters, character) >= 0;
                if (replaceCharacter)
                {
                    if (!previousCharacterWasReplacement && result.Length > 0)
                    {
                        result.Append('_');
                    }

                    previousCharacterWasReplacement = true;
                }
                else
                {
                    result.Append(character);
                    previousCharacterWasReplacement = false;
                }

                if (result.Length >= MaximumEventLabelLength)
                {
                    break;
                }
            }

            return result.ToString().Trim(' ', '.', '_');
        }
    }
}
