using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class CutsceneSkip
    {
        private sealed class State
        {
            internal bool ServerStarted;
            internal bool ServerCompleted;
            internal bool ClientCompleted;
        }

        private static readonly ConditionalWeakTable<UnitAI_BossBasic, State> States = new ConditionalWeakTable<UnitAI_BossBasic, State>();
        private static readonly FieldInfo ChangeDarkColor = AccessTools.Field(typeof(Unit_QBoss), "changeDarkColor");

        private static readonly FieldInfo CurrentEnterCoroutineField = AccessTools.Field(typeof(BossSpawner), "currentEnterCoroutine");
        private static readonly FieldInfo SkippableField = AccessTools.Field(typeof(BossSpawner), "skippable");
        private static readonly FieldInfo SkippedField = AccessTools.Field(typeof(BossSpawner), "skipped");
        private static readonly MethodInfo RpcHelloEndMethod = AccessTools.Method(typeof(BossSpawner), "RpcHelloEnd");
        private static readonly MethodInfo RpcStartBattleMethod = AccessTools.Method(typeof(BossSpawner), "RpcStartBattle");

        private struct StatusMessage : NetworkMessage
        {
            public bool Allowed;
        }

        private static bool sessionLocked;
        private static bool sessionAllowed;
        private static bool hostAllowsSkip;
        private static bool wasClientActive;
        private static bool hasSent;
        private static bool lastSentAllowed;

        private static bool Enabled
        {
            get
            {
                if (NetworkServer.active) return ModSettings.CutsceneSkipEnabled && SessionAllowsSkip();
                if (NetworkClient.active) return hostAllowsSkip;
                return ModSettings.CutsceneSkipEnabled;
            }
        }

        private static bool SessionAllowsSkip()
        {
            if (!sessionLocked)
            {
                sessionLocked = true;
                sessionAllowed = MoreMapCompatibility.AllConnectionsHaveMod();
                BroadcastStatus();
            }

            return sessionAllowed;
        }

        internal static bool IsOptionLocked()
        {
            return NetworkServer.active && sessionLocked && !sessionAllowed;
        }

        internal static void Initialize()
        {
            Writer<StatusMessage>.write = (writer, message) => writer.WriteBool(message.Allowed);
            Reader<StatusMessage>.read = reader => new StatusMessage { Allowed = reader.ReadBool() };
            NetworkClient.ReplaceHandler<StatusMessage>(ReceiveStatus, false);
        }

        internal static void Tick()
        {
            if (NetworkClient.active && !wasClientActive)
                hostAllowsSkip = false;
            else if (!NetworkClient.active && wasClientActive)
                hostAllowsSkip = false;
            wasClientActive = NetworkClient.active;

            if (!NetworkServer.active) return;

            bool allowed = ComputeAllowed();
            if (hasSent && allowed == lastSentAllowed) return;

            hasSent = true;
            lastSentAllowed = allowed;
            MoreMapCompatibility.SendToModClients(new StatusMessage { Allowed = allowed });
        }

        internal static void SendStatusTo(NetworkConnectionToClient connection)
        {
            if (connection == null || !NetworkServer.active) return;

            connection.Send(new StatusMessage { Allowed = ComputeAllowed() }, Channels.Reliable);
        }

        private static bool ComputeAllowed()
        {
            return sessionLocked ? ModSettings.CutsceneSkipEnabled && sessionAllowed : ModSettings.CutsceneSkipEnabled && MoreMapCompatibility.AllConnectionsHaveMod();
        }

        private static void BroadcastStatus()
        {
            bool allowed = ComputeAllowed();
            hasSent = true;
            lastSentAllowed = allowed;
            MoreMapCompatibility.SendToModClients(new StatusMessage { Allowed = allowed });
        }

        private static void ReceiveStatus(StatusMessage message)
        {
            hostAllowsSkip = message.Allowed;
        }

        internal static void Cleanup()
        {
            sessionLocked = false;
            sessionAllowed = false;
            hostAllowsSkip = false;
            wasClientActive = false;
            hasSent = false;
        }

        [HarmonyPatch(typeof(HorayNetworkManager), "NewGame")]
        private static class NewGameReset
        {
            [HarmonyPrefix]
            private static void Before()
            {
                sessionLocked = false;
                sessionAllowed = false;
                hasSent = false;
            }
        }

        private static State GetState(UnitAI_BossBasic ai)
        {
            return States.GetValue(ai, _ => new State());
        }

        private static void ForceSkipEnterAction(BossSpawner spawner, PlayerAvatar challenger)
        {
            if (spawner == null) return;

            Coroutine enterCoroutine = CurrentEnterCoroutineField == null ? null : CurrentEnterCoroutineField.GetValue(spawner) as Coroutine;
            if (enterCoroutine != null) spawner.StopCoroutine(enterCoroutine);
            if (CurrentEnterCoroutineField != null) CurrentEnterCoroutineField.SetValue(spawner, null);
            if (SkippedField != null) SkippedField.SetValue(spawner, true);
            if (SkippableField != null) SkippableField.SetValue(spawner, false);

            UnitAI_BossBasic ai = spawner.NetworkbossAI;
            UnitAvatar bossObj = spawner.NetworkbossObject;
            if (ai == null || bossObj == null) return;

            ai.DestroyCurrentSpeechBubble();
            ai.SkipEnterAction(challenger);
            bossObj.NetworkcanBeTarget = (sbyte)(bossObj.canBeTarget + 1);
            if (RpcHelloEndMethod != null && spawner.bossSocialID != null)
                RpcHelloEndMethod.Invoke(spawner, new object[] { true, spawner.bossSocialID.name });
            ai.enabled = true;
            bossObj.StopInvulnerable();
            ai.OnBattleStartServer();
            if (RpcStartBattleMethod != null) RpcStartBattleMethod.Invoke(spawner, null);
        }

        [HarmonyPatch(typeof(UnitAI_QBossAdv), "RpcChapter5ClearCutSceneStart")]
        private static class Floor5Rpc
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_QBossAdv __instance, out bool __state)
            {
                __state = false;
                if (!Enabled || !__instance.isServer) return true;

                State state = GetState(__instance);
                if (state.ServerStarted) return false;

                state.ServerStarted = true;
                __state = true;
                return true;
            }

            [HarmonyPostfix]
            private static void After(UnitAI_QBossAdv __instance, bool __state)
            {
                if (!__state || !__instance.isServer) return;

                State state = GetState(__instance);
                if (state.ServerCompleted) return;

                state.ServerCompleted = true;
                CompleteFloor5(__instance);
            }
        }

        [HarmonyPatch(typeof(UnitAI_QBossAdv), "UserCode_RpcChapter5ClearCutSceneStart")]
        private static class Floor5Client
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_QBossAdv __instance)
            {
                if (!Enabled) return true;

                State state = GetState(__instance);
                if (state.ClientCompleted) return false;

                state.ClientCompleted = true;
                RestoreLocalView();
                EnableFloor5Exit(__instance.Environment as BossEnvironment_QBossAdv ?? BossEnvironment_QBossAdv.Current);
                return false;
            }
        }

        private static void CompleteFloor5(UnitAI_QBossAdv ai)
        {
            try
            {
                Unit_QBoss boss = ai.Avatar as Unit_QBoss;
                BossEnvironment_QBossAdv env = ai.Environment as BossEnvironment_QBossAdv ?? BossEnvironment_QBossAdv.Current;

                ai.CancelAction();
                ai.DestroyCurrentSpeechBubble();

                if (boss != null)
                {
                    boss.activeWipeAttack = false;
                    boss.DestroyWipeSignal();
                    boss.NetworktrueDiePhase = 5;
                }

                ai.DieTruly();
                ai.DramaticDieCompleted();
                EnableFloor5Exit(env);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[CutsceneSkip] CompleteFloor5 실패\n" + ex);
            }
        }

        private static void EnableFloor5Exit(BossEnvironment_QBossAdv env)
        {
            if (env == null || !DungeonManager.IsFullReleasePhase) return;
            if (env.nextStage_hard != null) env.nextStage_hard.SetActive(true);
        }

        [HarmonyPatch(typeof(UnitAI_QQBoss), "UserCode_RpcDramaticDie")]
        private static class Phase1Client
        {
            [HarmonyPrefix]
            private static bool Before(UnitAI_QQBoss __instance)
            {
                if (!Enabled) return true;

                State state = GetState(__instance);
                if (state.ClientCompleted) return false;
                state.ClientCompleted = true;

                Unit_QQBoss boss = __instance.Avatar as Unit_QQBoss;
                if (boss != null) boss.TriggerDramaticDieHide();

                if (NetworkServer.active)
                {
                    __instance.DramaticDieCompleted();

                    BossEnvironment_QQBoss env = __instance.Environment as BossEnvironment_QQBoss ?? BossEnvironment_QQBoss.Current;
                    BossSpawner qqqSpawner = env == null ? null : env.qqqBossSpawner;
                    PlayerAvatar observer = GameCamera.Instance == null ? null : GameCamera.Instance.Observer;

                    if (qqqSpawner != null && observer != null)
                    {
                        if (env.qqqBossPoint != null)
                            observer.ReqSetPosition(env.qqqBossPoint.position, true);

                        qqqSpawner.StartBattleExternal(observer);
                        ForceSkipEnterAction(qqqSpawner, observer);
                    }
                }

                RestoreLocalView();
                return false;
            }
        }

        [HarmonyPatch(typeof(Unit_QBoss), "OnTrueDiePhaseChanged")]
        private static class InstantDeathVisual
        {
            [HarmonyPrefix]
            private static bool Before(Unit_QBoss __instance, int __1)
            {
                if (!Enabled || __1 != 5) return true;
                if (__instance.GetComponent<UnitAI_QBoss>() == null && __instance.GetComponent<UnitAI_QBossAdv>() == null) return true;

                if (ChangeDarkColor != null) ChangeDarkColor.SetValue(__instance, false);

                if (__instance.TopdownActor != null)
                {
                    if (__instance.TopdownActor.bodyRenderer != null) __instance.TopdownActor.bodyRenderer.enabled = false;
                    __instance.TopdownActor.DestoryShadow();
                }

                if (__instance.TopdownRigidbody != null) __instance.TopdownRigidbody.enabled = false;
                if (__instance.dieScreamSound != null) __instance.dieScreamSound.Stop();

                return false;
            }
        }

        private static void RestoreLocalView()
        {
            if (SoundManager.Instance != null) SoundManager.Instance.StopBGM();

            if (GameCamera.Instance != null && GameCamera.Instance.targetTracker != null)
            {
                GameCamera.Instance.targetTracker.ResetTempTarget();
                GameCamera.Instance.targetTracker.ResetZoom(0f);
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetAllowPausePanelOpening(true);
                UIManager.Instance.SetAllowGiveUp(true);
            }
        }
    }
}
