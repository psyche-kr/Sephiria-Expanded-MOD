using System;
using Mirror;

namespace SephiriaExpanded
{
    internal static class MoreSapphire
    {
        internal static bool ApplyDelta(int delta)
        {
            if (delta == 0) return true;
            if (SaveManager.Current == null) return false;

            int current = SaveManager.Current.GetInt("Sapphire", 0);
            int next = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)current + delta));

            SaveManager.Current.SetInt("Sapphire", next);
            SaveManager.Save(true, false);

            var identity = NetworkClient.localPlayer;

            if (identity != null)
            {
                var storage = identity.GetComponent<PlayerLocalDataStorage>();
                if (storage != null) storage.Networksapphire = next;
            }

            return true;
        }
    }
}