using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal sealed class BossBreak : IBossChallenge
    {
        private const float NormalPercent = 0.2f;
        private const float HardPercent = 0.25f;
        private const float StunTime = 3.2f;
        private const float RechargeStep = 4f;
        private const float SendInterval = 0.1f;
        private const float KeepAliveInterval = 1f;
        private const float SmoothSpeed = 12f;
        private const float SnapDistance = 0.002f;
        private const float BarGap = 1f;
        private const float BarThicknessRate = 0.35f;
        private const float BarMinThickness = 2f;
        private const int PruneCount = 8;
        private const int StunHitLevel = 2;
        private const int FallbackHitLevel = 999;
        private const string BarName = "SephiriaExpanded_BossBreakGauge";
        private const string LogTag = "[Sephiria Expanded][BossBreak] ";

        private static readonly Color BarColor = new Color(1f, 0.85f, 0.15f, 1f);
        private static readonly Vector3[] Corners = new Vector3[4];

        private sealed class State
        {
            internal float Fraction = 1f;
            internal bool Stunned;
            internal int Breaks;
            internal float RechargeTime;
            internal float StunStart;
            internal float RecoverAt;
            internal float SentFraction = -1f;
            internal float NextSend;
            internal float NextKeepAlive;
            internal UnitAI_BossBasic Ai;
        }

        private struct GaugeMessage : NetworkMessage
        {
            public uint BossId;
            public float Fraction;
        }

        private sealed class Bar
        {
            internal RectTransform Rect;
            internal float Shown;
        }

        private static readonly AccessTools.FieldRef<UI_BossHPBar, UnitAvatar> BossAvatar =
            AccessTools.FieldRefAccess<UI_BossHPBar, UnitAvatar>("bossAvatar");

        private static readonly AccessTools.FieldRef<UnitAvatar, Timer> StunCooldownTimer =
            AccessTools.FieldRefAccess<UnitAvatar, Timer>("stunCooldownTimer");

        private static readonly MethodInfo ShowDamageParticles = AccessTools.Method(typeof(UnitAvatar), "RpcShowAllDamageParticles");

        private static readonly Dictionary<UnitAvatar, State> States = new Dictionary<UnitAvatar, State>();
        private static readonly Dictionary<uint, float> Fractions = new Dictionary<uint, float>();
        private static readonly Dictionary<UI_BossHPBar, Bar> Bars = new Dictionary<UI_BossHPBar, Bar>();
        private static readonly List<UnitAvatar> Gone = new List<UnitAvatar>();
        private static readonly List<UI_BossHPBar> GoneBars = new List<UI_BossHPBar>();

        internal static void RegisterSerializers()
        {
            Writer<GaugeMessage>.write = (writer, message) =>
            {
                writer.WriteUInt(message.BossId);
                writer.WriteFloat(message.Fraction);
            };
            Reader<GaugeMessage>.read = reader => new GaugeMessage { BossId = reader.ReadUInt(), Fraction = reader.ReadFloat() };
        }

        internal static void RegisterClientHandlers()
        {
            NetworkClient.ReplaceHandler<GaugeMessage>(message =>
            {
                if (message.Fraction < 0f) Fractions.Remove(message.BossId);
                else Fractions[message.BossId] = message.Fraction;
            }, false);
        }

        public void Cleanup()
        {
            States.Clear();
            Fractions.Clear();
            Bars.Clear();
        }

        public void Tick()
        {
            if (!NetworkServer.active || States.Count == 0) return;

            if (!ModSettings.BossBreakEnabled)
            {
                Disable();
                return;
            }

            float now = Time.unscaledTime;
            Gone.Clear();
            foreach (KeyValuePair<UnitAvatar, State> pair in States)
            {
                UnitAvatar unit = pair.Key;
                State state = pair.Value;
                if (unit == null || unit.IsDead)
                {
                    Gone.Add(unit);
                    continue;
                }

                if (state.Ai != null && state.Ai.IsDramaticDying) continue;

                if (state.Stunned && Time.time >= state.RecoverAt && !unit.isInStun)
                {
                    state.Stunned = false;
                    state.Fraction = 1f;
                }

                float fraction = state.Stunned ? Mathf.Clamp01((Time.time - state.StunStart) / state.RechargeTime) : Mathf.Clamp01(state.Fraction);
                bool changed = !Mathf.Approximately(fraction, state.SentFraction);
                if (!(changed && now >= state.NextSend) && now < state.NextKeepAlive) continue;

                state.SentFraction = fraction;
                state.NextSend = now + SendInterval;
                state.NextKeepAlive = now + KeepAliveInterval;
                Fractions[unit.netId] = fraction;
                MoreMapCompatibility.SendToModClients(new GaugeMessage { BossId = unit.netId, Fraction = fraction });
            }

            foreach (UnitAvatar unit in Gone)
            {
                if (unit != null) Fractions.Remove(unit.netId);

                States.Remove(unit);
            }
        }

        private static void Disable()
        {
            foreach (UnitAvatar unit in States.Keys)
            {
                if (unit == null) continue;

                Fractions.Remove(unit.netId);
                MoreMapCompatibility.SendToModClients(new GaugeMessage { BossId = unit.netId, Fraction = -1f });
            }

            States.Clear();
        }

        private static bool Eligible(UnitAvatar unit, out BossChallengeRules rules)
        {
            rules = unit == null ? null : BossChallenge.For(unit.GetType());
            return ModSettings.BossBreakEnabled && rules != null && rules != BossChallenge.MadArmadillo && rules != BossChallenge.RootDemon && rules.Level >= 1 && unit.monsterType == EMonsterType.Boss && !unit.IsDead;
        }

        [HarmonyPatch(typeof(UnitAI_BossBasic), nameof(UnitAI_BossBasic.OnBattleStartServer))]
        private static class BattleStart
        {
            [HarmonyPostfix]
            private static void After(UnitAI_BossBasic __instance)
            {
                BossChallengeRules rules;
                UnitAvatar unit = __instance.Avatar;
                if (!Eligible(unit, out rules)) return;

                States[unit] = new State { Ai = __instance };
            }
        }

        [HarmonyPatch(typeof(UnitAvatar), "ApplyDamage")]
        private static class Hit
        {
            [HarmonyPostfix]
            private static void After(UnitAvatar __instance, DamageInstance __0)
            {
                if (!NetworkServer.active || __0 == null || __0.isSystemDamage || __0.damageResult <= 0) return;

                State state;
                if (!States.TryGetValue(__instance, out state) || state.Stunned || __instance.IsDead || __instance.hp <= 0f) return;

                BossChallengeRules rules;
                if (!Eligible(__instance, out rules) || (state.Ai != null && state.Ai.IsDramaticDying)) return;

                float percent = rules.Level >= 2 ? HardPercent : NormalPercent;
                state.Fraction -= __0.damageResult / (__instance.MaxHp * percent);
                if (state.Fraction <= 0f) Break(__instance, state);
            }
        }

        internal static void Refill(UnitAvatar unit, float duration)
        {
            State state;
            if (unit == null || !States.TryGetValue(unit, out state)) return;

            float current = state.Stunned ? Mathf.Clamp01((Time.time - state.StunStart) / state.RechargeTime) : Mathf.Clamp01(state.Fraction);
            state.Breaks = 0;
            state.Stunned = true;
            state.RechargeTime = duration;
            state.StunStart = Time.time - current * duration;
            state.RecoverAt = state.StunStart + duration;
            state.NextSend = 0f;
        }

        private static void Break(UnitAvatar unit, State state)
        {
            state.Fraction = 0f;
            state.Stunned = true;
            state.StunStart = Time.time;
            state.Breaks++;
            state.RechargeTime = StunTime + RechargeStep * (state.Breaks - 1);
            state.RecoverAt = Time.time + state.RechargeTime;
            state.NextSend = 0f;
            int immunity = unit.stunImmunity;
            unit.stunImmunity = 0;
            unit.stunCooldown = false;
            unit.StartTempStunImmunity(0f);
            Timer cooldown = StunCooldownTimer(unit);
            float previous = cooldown.time;
            bool stunned = unit.StartStun(StunTime);
            cooldown.time = previous;
            unit.stunImmunity = immunity;
            if (stunned) unit.StartHitFeedback(StunHitLevel);
            else unit.StartHitFeedback(FallbackHitLevel, 0f, StunTime);
            unit.RpcCreateSuperArmorBreakFx(unit.transform.position, unit.TopdownActor ? unit.TopdownActor.CenterYPos : 0f);
            ShowBreakText(unit);
        }

        private static void ShowBreakText(UnitAvatar unit)
        {
            if (ShowDamageParticles == null) return;

            foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
            {
                if (player == null || player.PlayerAvatar == null) continue;

                DamageFeedback feedback = new DamageFeedback(unit.transform.position, DamageFeedback.EMsgType.ArmorBreak, 0, BarColor, 0, true, unit, player.PlayerAvatar);
                ShowDamageParticles.Invoke(unit, new object[] { new[] { feedback } });
            }
        }

        [HarmonyPatch(typeof(UI_BossHPBar), "Update")]
        private static class BarUpdate
        {
            [HarmonyPostfix]
            private static void After(UI_BossHPBar __instance)
            {
                ShowBar(__instance);
            }
        }

        private static void ShowBar(UI_BossHPBar hud)
        {
            UnitAvatar boss = BossAvatar(hud);
            float fraction;
            Bar bar;
            Bars.TryGetValue(hud, out bar);
            if (boss == null || hud.barImage == null || !Fractions.TryGetValue(boss.netId, out fraction))
            {
                if (bar != null && bar.Rect != null) bar.Rect.gameObject.SetActive(false);

                return;
            }

            if (bar == null || bar.Rect == null)
            {
                bar = Build(hud);
                if (bar == null) return;

                bar.Shown = fraction;
                Bars[hud] = bar;
                PruneBars();
            }

            bar.Shown = Mathf.Abs(bar.Shown - fraction) < SnapDistance ? fraction : Mathf.Lerp(bar.Shown, fraction, 1f - Mathf.Exp(-SmoothSpeed * Time.unscaledDeltaTime));
            RectTransform root = (RectTransform)hud.transform;
            hud.barImage.rectTransform.GetWorldCorners(Corners);
            Vector3 bottomLeft = root.InverseTransformPoint(Corners[0]);
            Vector3 topLeft = root.InverseTransformPoint(Corners[1]);
            Vector3 bottomRight = root.InverseTransformPoint(Corners[3]);
            float thickness = Mathf.Max(BarMinThickness, (topLeft.y - bottomLeft.y) * BarThicknessRate);
            bar.Rect.sizeDelta = new Vector2((bottomRight.x - bottomLeft.x) * Mathf.Clamp01(bar.Shown), thickness);
            bar.Rect.localPosition = new Vector3(bottomLeft.x, bottomLeft.y - BarGap - thickness * 0.5f, 0f);
            if (!bar.Rect.gameObject.activeSelf) bar.Rect.gameObject.SetActive(true);
        }

        private static Bar Build(UI_BossHPBar hud)
        {
            RectTransform source = hud.barImage.rectTransform;
            GameObject holder = new GameObject(BarName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            holder.GetComponent<LayoutElement>().ignoreLayout = true;
            RectTransform rect = holder.GetComponent<RectTransform>();
            rect.SetParent(hud.transform, false);
            rect.SetAsLastSibling();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.localScale = Vector3.one;
            Image image = holder.GetComponent<Image>();
            image.color = BarColor;
            image.raycastTarget = false;
            Debug.Log(LogTag + "게이지 바 생성: 루트 " + hud.name + ", 체력바 크기 " + source.rect.width + "x" + source.rect.height + ", 경로 " + Ancestry(source, hud.transform));
            return new Bar { Rect = rect };
        }

        private static string Ancestry(Transform from, Transform root)
        {
            string text = string.Empty;
            for (Transform current = from; current != null; current = current.parent)
            {
                text += current.name;
                if (current.GetComponent<Mask>() != null) text += "[Mask]";
                if (current.GetComponent<RectMask2D>() != null) text += "[RectMask2D]";
                if (current.GetComponent<LayoutGroup>() != null) text += "[Layout]";
                if (current.GetComponent<CanvasGroup>() != null) text += "[CanvasGroup]";
                if (current == root) break;

                text += " < ";
            }

            return text;
        }

        private static void PruneBars()
        {
            if (Bars.Count <= PruneCount) return;

            GoneBars.Clear();
            foreach (KeyValuePair<UI_BossHPBar, Bar> pair in Bars)
                if (pair.Key == null || pair.Value.Rect == null) GoneBars.Add(pair.Key);

            foreach (UI_BossHPBar key in GoneBars)
                Bars.Remove(key);
        }
    }
}
