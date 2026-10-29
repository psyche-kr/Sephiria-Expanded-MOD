using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal static class BossChallengeBadge
    {
        private const string Tag = "[강화]";
        private const string NormalPrefix = "<color=yellow>[강화]</color> ";
        private const int TagLength = 4;
        private const string HardPrefix = "<color=red>[강화]</color> ";
        private static readonly Color OverheatColor = new Color32(0x99, 0x00, 0x85, 0xFF);

        private const float TintKeepAlive = 1f;

        private struct TintMessage : NetworkMessage
        {
            public uint BossId;
            public bool Tinted;
        }

        private struct TintSent
        {
            internal bool Tinted;
            internal float NextSend;
        }

        private static readonly HashSet<uint> ClientTints = new HashSet<uint>();
        private static readonly Dictionary<uint, TintSent> SentTints = new Dictionary<uint, TintSent>();
        private static readonly HashSet<TMPGlitchEffect> TagOnly = new HashSet<TMPGlitchEffect>();
        private static readonly Dictionary<Image, Color> OverheatOriginal = new Dictionary<Image, Color>();
        private static readonly AccessTools.FieldRef<UI_BossHPBar, UnitAI_BossBasic> BossAi = AccessTools.FieldRefAccess<UI_BossHPBar, UnitAI_BossBasic>("bossAI");

        private static bool IsEnhanced(UnitAI_BossBasic ai)
        {
            return BossChallenge.For(ai.Avatar.GetType()) != null;
        }

        private static bool Enraged(UnitAI_BossBasic ai)
        {
            if (ai is UnitAI_MoleBigBomb)
            {
                Unit_MoleBigBomb mole = ai.Avatar as Unit_MoleBigBomb;
                return mole != null && BossChallenge_MoleBigBomb.Overheated(mole);
            }

            if (ai is UnitAI_LibraryGuard)
            {
                Unit_LibraryGuard guard = ai.Avatar as Unit_LibraryGuard;
                return guard != null && BossChallenge_LibraryGuard.Annihilating(guard);
            }

            if (ai.Avatar is Unit_QBoss qBoss) return BossChallenge_QBoss.Recolored(qBoss);

            return false;
        }

        internal static void RegisterSerializers()
        {
            Writer<TintMessage>.write = (writer, message) =>
            {
                writer.WriteUInt(message.BossId);
                writer.WriteBool(message.Tinted);
            };
            Reader<TintMessage>.read = reader => new TintMessage { BossId = reader.ReadUInt(), Tinted = reader.ReadBool() };
        }

        internal static void RegisterClientHandlers()
        {
            NetworkClient.ReplaceHandler<TintMessage>(message =>
            {
                if (message.Tinted) ClientTints.Add(message.BossId);
                else ClientTints.Remove(message.BossId);
            }, false);
        }

        private static bool Tinted(UnitAI_BossBasic ai)
        {
            uint bossId = ai.Avatar.netId;
            if (!NetworkServer.active) return ClientTints.Contains(bossId);

            bool tinted = Enraged(ai);
            float now = Time.unscaledTime;
            TintSent sent;
            if (!SentTints.TryGetValue(bossId, out sent) || sent.Tinted != tinted || now >= sent.NextSend)
            {
                SentTints[bossId] = new TintSent { Tinted = tinted, NextSend = now + TintKeepAlive };
                MoreMapCompatibility.SendToModClients(new TintMessage { BossId = bossId, Tinted = tinted });
            }

            return tinted;
        }

        [HarmonyPatch(typeof(UI_BossHPBar), nameof(UI_BossHPBar.OnOpened))]
        private static class Opened
        {
            [HarmonyPostfix]
            private static void After(UI_BossHPBar __instance)
            {
                int level = BossChallengeNet.GetDisplayLevel(BossAi(__instance));
                if (level < 1 || __instance.nameText == null || !IsEnhanced(BossAi(__instance))) return;

                TextMeshProUGUI text = __instance.nameText;
                if (text.text.IndexOf(Tag) < 0) text.text = (level >= 2 ? HardPrefix : NormalPrefix) + text.text;
                if (level < 2) return;

                TMPGlitchEffect glitch = text.GetComponent<TMPGlitchEffect>() ?? text.gameObject.AddComponent<TMPGlitchEffect>();
                TagOnly.RemoveWhere(item => item == null);
                TagOnly.Add(glitch);
            }
        }

        [HarmonyPatch(typeof(UI_BossHPBar), "Update")]
        private static class OverheatColorTint
        {
            [HarmonyPostfix]
            private static void After(UI_BossHPBar __instance)
            {
                UnitAI_BossBasic ai = BossAi(__instance);
                if (ai == null || __instance.barImage == null) return;

                Image bar = __instance.barImage;
                if (Tinted(ai))
                {
                    if (!OverheatOriginal.ContainsKey(bar)) OverheatOriginal[bar] = bar.color;
                    bar.color = OverheatColor;
                    return;
                }

                Color original;
                if (OverheatOriginal.TryGetValue(bar, out original))
                {
                    bar.color = original;
                    OverheatOriginal.Remove(bar);
                }
            }
        }

        [HarmonyPatch(typeof(TMPGlitchEffect), "GlitchChar")]
        private static class TagGlitch
        {
            [HarmonyPrefix]
            private static bool Before(TMPGlitchEffect __instance, int idx)
            {
                return idx < TagLength || !TagOnly.Contains(__instance);
            }
        }
    }
}
