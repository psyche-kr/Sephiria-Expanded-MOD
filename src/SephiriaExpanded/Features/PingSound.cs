using HarmonyLib;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class PingSound
    {
        internal static readonly string[] Labels = { "없음", "3초에 한번", "5초에 한번", "소리 끄기" };

        private const int MuteMode = 3;
        private static readonly float[] Cooldowns = { 0f, 3f, 5f };

        private static float lastPlayed = -100f;

        [HarmonyPatch(typeof(MultiplayerPing), "Start")]
        private static class StartPatch
        {
            [HarmonyPrefix]
            private static void Before(MultiplayerPing __instance)
            {
                int mode = ModSettings.PingSoundMode;
                if (mode <= 0 || __instance.createSoundEvent == null) return;

                if (mode >= MuteMode)
                {
                    __instance.createSoundEvent = null;
                    return;
                }

                float now = Time.unscaledTime;
                if (now - lastPlayed < Cooldowns[mode])
                {
                    __instance.createSoundEvent = null;
                    return;
                }

                lastPlayed = now;
            }
        }
    }
}
