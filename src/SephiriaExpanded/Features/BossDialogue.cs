using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class BossDialogue
    {
        private struct ColoredChatMessage : NetworkMessage
        {
            public string ColoredLine;
        }

        private sealed class DeathOverride
        {
            internal string BossName;
            internal string[] VanillaKeys;
            internal string CustomLine;
            internal Func<int> Level;
        }

        private static readonly Dictionary<Type, DeathOverride> DeathOverrides = new Dictionary<Type, DeathOverride>();
        private static readonly HashSet<UnitAI_NewBasic> DeathLineShown = new HashSet<UnitAI_NewBasic>();
        private sealed class EncounterHold
        {
            internal float Start;
            internal float Until;
            internal UnitAI_NewBasic Speaker;
        }

        private const float EncounterMinHold = 0.5f;
        private const float EncounterMaxHold = 6f;
        private const float DeathMaxWait = 8f;
        private static readonly Dictionary<UnitAI_NewBasic, EncounterHold> EncounterHolds = new Dictionary<UnitAI_NewBasic, EncounterHold>();
        private static readonly Dictionary<Type, Action<UnitAI_BossBasic>> EncounterSayers = new Dictionary<Type, Action<UnitAI_BossBasic>>();
        private static readonly HashSet<UnitAI_NewBasic> EnterSaid = new HashSet<UnitAI_NewBasic>();
        private static readonly HashSet<UnitAI_BossBasic> SkippedDeaths = new HashSet<UnitAI_BossBasic>();
        private static UnitAI_NewBasic enterSpeaker;
        private static bool inEnter;
        private static bool completingDeath;
        private static readonly string[] HpKeys = { "Hp0", "Hp1", "Hp2", "Hp3", "Hp4", "Hp5", "Hp6", "Hp7" };
        private const string UnusedPrefix = "Line/";
        private static readonly List<string> UnusedLines = new List<string>();
        private static readonly Dictionary<UnitAI_NewBasic, HashSet<string>> ThresholdFired = new Dictionary<UnitAI_NewBasic, HashSet<string>>();

        internal static void Say(UnitAI_NewBasic ai, string bossName, string text, int level, float time = 3f)
        {
            if (ai == null || !ai.isServer) return;

            string color = level >= 2 ? "red" : "yellow";
            string wrapped = "<color=" + color + ">" + text + "</color>";
            Speak(ai, bossName, wrapped, wrapped, time);
        }

        internal static bool SayEncounter(UnitAI_NewBasic ai, string bossName, string text, int level)
        {
            if (ai == null || !ai.isServer || EnterSaid.Remove(ai)) return false;

            Say(ai, bossName, text, level);
            if (inEnter)
            {
                EnterSaid.Add(ai);
                enterSpeaker = ai;
                return false;
            }

            Hold(ai, ai);
            return true;
        }

        internal static void RegisterEncounter(Type aiType, Action<UnitAI_BossBasic> sayer)
        {
            EncounterSayers[aiType] = sayer;
        }

        [HarmonyPatch]
        private static class EnterWait
        {
            [HarmonyTargetMethods]
            private static IEnumerable<System.Reflection.MethodBase> Targets()
            {
                foreach (Type type in typeof(UnitAI_BossBasic).Assembly.GetTypes())
                {
                    if (!typeof(UnitAI_BossBasic).IsAssignableFrom(type)) continue;

                    System.Reflection.MethodInfo method = AccessTools.DeclaredMethod(type, nameof(UnitAI_BossBasic.EnterAction));
                    if (method != null && !method.IsAbstract) yield return method;
                }
            }

            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance, ref System.Collections.IEnumerator __result)
            {
                if (__instance != null && __instance.isServer && EncounterSayers.ContainsKey(__instance.GetType())) __result = WaitEncounter(__result, __instance);
            }
        }

        private static System.Collections.IEnumerator WaitEncounter(System.Collections.IEnumerator inner, UnitAI_BossBasic ai)
        {
            while (inner != null && inner.MoveNext())
                yield return inner.Current;

            Action<UnitAI_BossBasic> sayer;
            if (ai == null || !EncounterSayers.TryGetValue(ai.GetType(), out sayer)) yield break;

            enterSpeaker = null;
            inEnter = true;
            try
            {
                sayer(ai);
            }
            finally
            {
                inEnter = false;
            }

            UnitAI_NewBasic speaker = enterSpeaker;
            if (speaker == null) yield break;

            float start = Time.time;
            yield return new WaitForSeconds(EncounterMinHold);
            while (speaker != null && NetworkClient.active && speaker.IsSpeaking() && Time.time - start < EncounterMaxHold)
                yield return null;
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.SkipDramaticDie))]
        private static class DeathSkip
        {
            [HarmonyPrefix]
            private static void Before(UnitAI_BossBasic __instance)
            {
                SkippedDeaths.Add(__instance);
            }
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.DramaticDieCompleted))]
        private static class DeathWait
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_BossBasic __instance)
            {
                if (completingDeath || SkippedDeaths.Remove(__instance) || !__instance.isServer || !NetworkClient.active || __instance.Avatar == null) return true;

                BossChallengeRules rules = BossChallenge.For(__instance.Avatar.GetType());
                if (rules == null || rules.Level < 1 || !__instance.IsSpeaking()) return true;

                MonoBehaviour runner = __instance.Environment != null ? (MonoBehaviour)__instance.Environment : __instance;
                runner.StartCoroutine(FinishDeathAfterSpeech(__instance));
                return false;
            }
        }

        private static System.Collections.IEnumerator FinishDeathAfterSpeech(UnitAI_BossBasic ai)
        {
            float start = Time.time;
            while (ai != null && NetworkClient.active && ai.IsSpeaking() && Time.time - start < DeathMaxWait)
                yield return null;

            completingDeath = true;
            try
            {
                ai.DramaticDieCompleted();
            }
            finally
            {
                completingDeath = false;
            }
        }

        internal static void Hold(UnitAI_NewBasic ai, UnitAI_NewBasic speaker)
        {
            if (ai != null && speaker != null) EncounterHolds[ai] = new EncounterHold { Start = Time.time, Speaker = speaker };
        }

        internal static void HoldFor(UnitAI_NewBasic ai, float seconds)
        {
            if (ai != null) EncounterHolds[ai] = new EncounterHold { Start = Time.time, Until = Time.time + seconds };
        }

        internal static void SayColored(UnitAI_NewBasic ai, string bossName, string text, Color color, float time = 3f)
        {
            if (ai == null || !ai.isServer) return;

            string hex = ColorUtility.ToHtmlStringRGB(color);
            string wrapped = "<color=#" + hex + ">" + text + "</color>";
            Speak(ai, bossName, wrapped, wrapped, time);
        }

        internal static void SayRaw(UnitAI_NewBasic ai, string bossName, string bubbleText, string plainText, string flatColorName, float time = 3f)
        {
            if (ai == null || !ai.isServer) return;

            string chatColored = "<color=" + flatColorName + ">" + StripTags(plainText) + "</color>";
            Speak(ai, bossName, bubbleText, chatColored, time);
        }

        internal static void Announce(string text, string color)
        {
            string line = "<color=" + color + ">" + text + "</color>";
            if (GameLogWriter.Instance != null) GameLogWriter.Instance.WriteLog(line, Color.white);
            if (NetworkServer.active)
                MoreMapCompatibility.SendToModClients(new ColoredChatMessage { ColoredLine = line });
        }

        private static void Speak(UnitAI_NewBasic ai, string bossName, string bubbleColored, string chatColored, float time)
        {
            ai.CreateSpeechBubble(bubbleColored, false, time);

            string coloredLine = bossName + " : \"" + chatColored + "\"";

            if (GameLogWriter.Instance != null) GameLogWriter.Instance.WriteLog(coloredLine, Color.white);
            if (NetworkServer.active)
                MoreMapCompatibility.SendToModClients(new ColoredChatMessage { ColoredLine = coloredLine });
        }

        private static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", string.Empty);
        }

        internal static void RegisterSerializers()
        {
            Writer<ColoredChatMessage>.write = (writer, message) => writer.WriteString(message.ColoredLine);
            Reader<ColoredChatMessage>.read = reader => new ColoredChatMessage { ColoredLine = reader.ReadString() };
        }

        internal static void RegisterClientHandlers()
        {
            NetworkClient.ReplaceHandler<ColoredChatMessage>(message =>
            {
                if (GameLogWriter.Instance != null) GameLogWriter.Instance.WriteLog(message.ColoredLine, Color.white);
            }, false);
        }

        internal static bool TryFireThreshold(UnitAI_NewBasic ai, string thresholdKey)
        {
            if (ai == null) return false;

            HashSet<string> fired;
            if (!ThresholdFired.TryGetValue(ai, out fired))
            {
                fired = new HashSet<string>();
                ThresholdFired[ai] = fired;
            }

            if (fired.Contains(thresholdKey)) return false;

            fired.Add(thresholdKey);
            return true;
        }

        internal static bool SayUnused(UnitAI_NewBasic ai, string bossName, string[] lines, int level)
        {
            if (ai == null || !ai.isServer || lines == null || lines.Length == 0) return false;

            HashSet<string> fired;
            ThresholdFired.TryGetValue(ai, out fired);
            UnusedLines.Clear();
            foreach (string line in lines)
                if (fired == null || !fired.Contains(UnusedPrefix + line)) UnusedLines.Add(line);

            if (UnusedLines.Count == 0) return false;

            string picked = UnusedLines[UnityEngine.Random.Range(0, UnusedLines.Count)];
            TryFireThreshold(ai, UnusedPrefix + picked);
            Say(ai, bossName, picked, level);
            return true;
        }

        internal static void CheckHpLines(UnitAI_BossBasic ai, string bossName, float[] ratios, string[] lines, int level)
        {
            if (ai == null || !ai.isServer || ai.IsDramaticDying) return;

            UnitAvatar unit = ai.Avatar;
            if (unit == null || unit.IsDead || unit.MaxHp <= 0f) return;

            float ratio = unit.hp / unit.MaxHp;
            int step = 0;
            while (step < ratios.Length && ratio < ratios[step])
                step++;

            if (step == 0) return;

            bool announce = false;
            for (int i = 0; i < step; i++)
                announce = TryFireThreshold(ai, HpKeys[i]) && i == step - 1;

            if (announce) Say(ai, bossName, lines[step - 1], level);
        }

        internal static void ClearThresholds(UnitAI_NewBasic ai)
        {
            if (ai != null) ThresholdFired.Remove(ai);
        }

        internal static void RegisterDeathOverride(Type bossType, string bossName, string[] vanillaKeys, string customLine, Func<int> level)
        {
            DeathOverrides[bossType] = new DeathOverride { BossName = bossName, VanillaKeys = vanillaKeys, CustomLine = customLine, Level = level };
        }

        internal static void ResetDeathLine(UnitAI_NewBasic ai)
        {
            if (ai != null) DeathLineShown.Remove(ai);
        }

        [HarmonyPatch(typeof(UnitAI_NewBasic), "OnAIUpdate")]
        private static class EncounterWait
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_NewBasic __instance)
            {
                EncounterHold hold;
                if (EncounterHolds.Count == 0 || !EncounterHolds.TryGetValue(__instance, out hold)) return true;

                float elapsed = Time.time - hold.Start;
                bool speaking = hold.Speaker != null && NetworkClient.active && hold.Speaker.IsSpeaking();
                bool speakerHold = hold.Speaker != null && elapsed < EncounterMaxHold && (elapsed < EncounterMinHold || speaking);
                if (Time.time < hold.Until || speakerHold)
                {
                    if (__instance.Avatar != null) __instance.Avatar.Stop();
                    return false;
                }

                EncounterHolds.Remove(__instance);
                return true;
            }
        }

        [HarmonyPatch(typeof(UnitAI_NewBasic), nameof(UnitAI_NewBasic.CreateSpeechBubble),
            new[] { typeof(string), typeof(bool), typeof(float), typeof(bool), typeof(Loc.KeywordValue[]) })]
        private static class InterceptDeathSpeech
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_NewBasic __instance, string key)
            {
                DeathOverride over;
                if (__instance == null || key == null || !DeathOverrides.TryGetValue(__instance.GetType(), out over)) return true;
                if (over.Level() < 1) return true;

                bool matches = false;
                foreach (string vanillaKey in over.VanillaKeys)
                {
                    if (vanillaKey == key) { matches = true; break; }
                }

                Debug.Log("[Sephiria Expanded][Dialogue] 말풍선 key='" + key + "' 보스=" + __instance.GetType().Name + " 일치=" + matches + " 이미 출력=" + DeathLineShown.Contains(__instance));

                if (!matches) return true;

                if (!DeathLineShown.Contains(__instance))
                {
                    DeathLineShown.Add(__instance);
                    Say(__instance, over.BossName, over.CustomLine, over.Level());
                }

                return false;
            }
        }
    }
}
