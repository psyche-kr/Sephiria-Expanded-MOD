using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mirror;
using TMPro;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class BossChallengeNotice
    {
        private const string SampleId = "MORE_HP";
        private const string Body = "플레이어의 회복 효과가 30% 감소합니다.";
        private static readonly Regex ColorTag = new Regex("</?color[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex TextRun = new Regex("(?<=^|>)[^<>]+(?=<|$)");

        private struct NoticeMessage : NetworkMessage
        {
            public string BossName;
            public int Level;
        }

        internal static void RegisterSerializers()
        {
            Writer<NoticeMessage>.write = (writer, message) =>
            {
                writer.WriteString(message.BossName);
                writer.WriteInt(message.Level);
            };
            Reader<NoticeMessage>.read = reader => new NoticeMessage { BossName = reader.ReadString(), Level = reader.ReadInt() };
        }

        internal static void RegisterClientHandlers()
        {
            NetworkClient.ReplaceHandler<NoticeMessage>(message => Show(message.BossName, message.Level), false);
        }

        internal static void Announce(UnitAvatar boss, int level)
        {
            if (boss == null || level < 1) return;

            string name = KeywordDatabase.Convert(boss.Name, useColor: false, useSprite: false);
            Show(name, level);
            MoreMapCompatibility.SendToModClients(new NoticeMessage { BossName = name, Level = level });
        }

        private static void Show(string bossName, int level)
        {
            if (!NetworkClient.active || UIManager.Instance == null) return;

            UI_HardBattleWarningHUD template = UIManager.Instance.GetElement<UI_HardBattleWarningHUD>();
            if (template == null) return;

            UI_HardBattleWarningHUD hud = Object.Instantiate(template, template.transform.parent, false);
            hud.name = "SephiriaExpanded_HardBattleWarning";
            hud.hasControl = false;

            TextMeshProUGUI header = FindHeader(hud);
            if (header != null)
            {
                foreach (UI_LocalizationStringText localization in header.GetComponentsInChildren<UI_LocalizationStringText>(true))
                    localization.enabled = false;

                header.text = Header(bossName, level);
                hud.Open(Body);
            }
            else
            {
                hud.Open(Compose(bossName, level));
            }

            UIManager.Instance.StartCoroutine(DestroyWhenClosed(hud));
        }

        private static TextMeshProUGUI FindHeader(UI_HardBattleWarningHUD hud)
        {
            List<TextMeshProUGUI> others = new List<TextMeshProUGUI>();
            foreach (TextMeshProUGUI candidate in hud.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (candidate != hud.text) others.Add(candidate);

            if (others.Count == 1) return others[0];

            return others.Find(candidate => candidate.text != null && candidate.text.Contains("교란"));
        }

        private static string Header(string bossName, int level)
        {
            bool hard = level >= 2;
            return "<color=" + (hard ? "red" : "yellow") + ">" + bossName + (hard ? " 대폭 강화" : " 강화") + "</color>";
        }

        private static IEnumerator DestroyWhenClosed(UI_HardBattleWarningHUD hud)
        {
            yield return null;
            while (hud != null && hud.IsOpened)
                yield return null;

            if (hud != null) Object.Destroy(hud.gameObject);
        }

        private static string Compose(string bossName, int level)
        {
            string header = Header(bossName, level);

            string sample = null;
            HardBattleData data = CombatManager.Instance == null ? null : CombatManager.Instance.FindHardBattleData(SampleId);
            if (data != null && data.text != null) sample = data.text.ToString();

            string[] lines = string.IsNullOrEmpty(sample) ? null : sample.Replace("\r", string.Empty).Split(new[] { '\n' }, 2);
            if (lines == null || lines.Length < 2) return header + "\n" + Body;

            return Replace(ColorTag.Replace(lines[0], string.Empty), header) + "\n" + Replace(lines[1], Body);
        }

        private static string Replace(string line, string text)
        {
            bool done = false;
            string result = TextRun.Replace(line, match =>
            {
                if (done || string.IsNullOrWhiteSpace(match.Value)) return match.Value;

                done = true;
                return text;
            });

            return done ? result : text;
        }
    }
}
