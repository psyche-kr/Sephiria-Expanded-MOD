using System;
using System.Collections;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SephiriaExpanded
{
    internal static class GuestReturnToTown
    {
        private const float RetryLimit = 10f;

        private static bool guestSession;
        private static bool pending;

        [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStartClient))]
        private static class StartClientPatch
        {
            [HarmonyPostfix]
            private static void After()
            {
                guestSession = !NetworkServer.active;
            }
        }

        [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.GoToTitleScene))]
        private static class LeavePatch
        {
            [HarmonyPrefix]
            private static bool Before(HorayNetworkManager __instance)
            {
                pending = ModSettings.GuestReturnToTownEnabled && guestSession && SceneManager.GetActiveScene().name != __instance.titleSceneName;
                guestSession = false;
                if (!pending) return true;

                if (!TryStartInPlace(__instance)) return true;

                pending = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(UI_TitleLobby), "Start")]
        private static class TitlePatch
        {
            [HarmonyPostfix]
            private static void After(UI_TitleLobby __instance)
            {
                if (!pending) return;

                pending = false;
                __instance.StartCoroutine(StartOwnGame(__instance));
            }
        }

        private static bool TryStartInPlace(HorayNetworkManager manager)
        {
            try
            {
                string profile = OptionsBinding.Instance.Options.GetString("SelectedProfile", SaveManager.defaultSlotName);
                SaveManager.DeleteFile(profile + "TMP", deleteAllBackups: true);
                SaveManager.CreateNewTMP(profile);
                manager.StartHost();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogWarning("[Sephiria Expanded][GuestReturnToTown] 바로 시작 실패, 타이틀 경로로 대체");
                return false;
            }
        }

        private static IEnumerator StartOwnGame(UI_TitleLobby lobby)
        {
            float titleAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - titleAt < RetryLimit && lobby != null)
            {
                bool busy = ScreenFader.Instance == null || ScreenFader.Instance.IsFading || SteamInvitation.waitForExternalConnect || NetworkServer.active || NetworkClient.active;
                if (!busy)
                {
                    lobby.NetworkHost();
                    yield break;
                }

                yield return null;
            }
        }
    }
}
