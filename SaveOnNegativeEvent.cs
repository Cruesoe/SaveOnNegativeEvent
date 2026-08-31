using HarmonyLib;
using RimWorld;
using Verse;
using System;
using UnityEngine;

namespace SaveOnNegativeEvent
{
    public class SaveOnNegativeEventSettings : ModSettings
    {
        public bool appendEventLabel = false;
        public int cooldownSeconds = 30;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref appendEventLabel, "appendEventLabel", false);
            Scribe_Values.Look(ref cooldownSeconds, "cooldownSeconds", 30);
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

            listingStandard.CheckboxLabeled("Append event name to save file", ref settings.appendEventLabel, "If enabled, saves as 'Bad Event_Raid'. If disabled, always overwrites 'Bad Event'.");

            listingStandard.Gap();
            listingStandard.Label($"Cooldown between event saves: {settings.cooldownSeconds} seconds");

            settings.cooldownSeconds = (int)listingStandard.Slider(settings.cooldownSeconds, 1f, 200f);

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Save on Negative Event";
        }
    }

    [StaticConstructorOnStartup]
    public static class Patcher
    {
        static Patcher()
        {
            var harmony = new Harmony("com.yourname.saveonnegativeevent");
            harmony.PatchAll();
        }
    }

    // NEW PATCH: Triggers the moment the loading screen finishes
    [HarmonyPatch(typeof(Game), "FinalizeInit")]
    public static class Game_FinalizeInit_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            // Forces the mod into cooldown so it ignores existing letters on load
            ReceiveLetter_Patch.lastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    [HarmonyPatch(typeof(LetterStack), "ReceiveLetter", new Type[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class ReceiveLetter_Patch
    {
        // CHANGED: Made internal so the loading patch above can access and reset it
        internal static long lastSaveTime = 0;

        [HarmonyPostfix]
        public static void Postfix(Letter let)
        {
            if (let.def == LetterDefOf.ThreatBig || let.def == LetterDefOf.NegativeEvent)
            {
                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (currentTime - lastSaveTime < SaveOnNegativeEventMod.settings.cooldownSeconds) return;

                lastSaveTime = currentTime;

                string fileName = "Bad Event";

                if (SaveOnNegativeEventMod.settings.appendEventLabel)
                {
                    string safeLabel = string.Join("_", let.Label.ToString().Split(System.IO.Path.GetInvalidFileNameChars()));
                    fileName += "_" + safeLabel;
                }

                GameDataSaveLoader.SaveGame(fileName);
                Messages.Message("Game Saved: " + fileName, MessageTypeDefOf.SilentInput, false);
            }
        }
    }
}