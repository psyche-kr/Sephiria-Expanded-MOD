namespace SephiriaExpanded
{
    internal static class LocalPlayer
    {
        internal static PlayerAvatar Find()
        {
            if (PlayerSpawner.MultiplayerList == null) return null;

            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar avatar = spawner == null ? null : spawner.PlayerAvatar;
                if (avatar != null && avatar.isLocalPlayer) return avatar;
            }

            return null;
        }
    }
}
