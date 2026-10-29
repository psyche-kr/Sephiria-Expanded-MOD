using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class WeaponShine
    {
        private const int RequiredPoints = 60;
        private const int FrameCount = 10;
        private const float SweepTime = 0.35f;
        private const float CycleTime = 2.5f;
        private const float BandWidthRate = 0.18f;
        private const float BandAlpha = 0.85f;
        private const byte SolidAlpha = 128;
        private const float ReportInterval = 2f;
        private const float BroadcastInterval = 1f;
        private const string ShineName = "SephiriaExpanded_WeaponShine";
        private const string LogTag = "[Sephiria Expanded][WeaponShine] ";

        private struct ReportMessage : NetworkMessage
        {
            public uint PlayerId;
            public bool Shining;
        }

        private struct StateMessage : NetworkMessage
        {
            public uint PlayerId;
            public bool Shining;
        }

        private sealed class Entry
        {
            internal SpriteRenderer Target;
            internal SpriteRenderer Shine;
        }

        private const float PearlStep = 3f;
        private static readonly Color[] Pearl =
        {
            new Color32(0xFF, 0xC6, 0xE2, 0xFF),
            new Color32(0xE6, 0xC8, 0xFF, 0xFF),
            new Color32(0xC6, 0xE4, 0xFF, 0xFF),
            new Color32(0xC8, 0xFF, 0xF0, 0xFF),
            new Color32(0xFF, 0xF2, 0xC6, 0xFF)
        };

        private static readonly Dictionary<Sprite, Sprite[]> Frames = new Dictionary<Sprite, Sprite[]>();
        private static readonly Dictionary<PlayerAvatar, Entry> Entries = new Dictionary<PlayerAvatar, Entry>();
        private static readonly Dictionary<uint, bool> ServerStates = new Dictionary<uint, bool>();
        private static readonly Dictionary<uint, bool> ClientStates = new Dictionary<uint, bool>();
        private static readonly HashSet<PlayerAvatar> Seen = new HashSet<PlayerAvatar>();
        private static readonly List<PlayerAvatar> Gone = new List<PlayerAvatar>();
        private static Material material;
        private static bool reportedState;
        private static uint reportedId;
        private static float nextReport;
        private static float nextBroadcast;

        internal static void RegisterSerializers()
        {
            Writer<ReportMessage>.write = (writer, message) =>
            {
                writer.WriteUInt(message.PlayerId);
                writer.WriteBool(message.Shining);
            };
            Reader<ReportMessage>.read = reader => new ReportMessage { PlayerId = reader.ReadUInt(), Shining = reader.ReadBool() };
            Writer<StateMessage>.write = (writer, message) =>
            {
                writer.WriteUInt(message.PlayerId);
                writer.WriteBool(message.Shining);
            };
            Reader<StateMessage>.read = reader => new StateMessage { PlayerId = reader.ReadUInt(), Shining = reader.ReadBool() };
        }

        internal static void RegisterServerHandlers()
        {
            NetworkServer.ReplaceHandler<ReportMessage>((connection, message) => SetServerState(message.PlayerId, message.Shining), false);
        }

        internal static void RegisterClientHandlers()
        {
            NetworkClient.ReplaceHandler<StateMessage>(message => ClientStates[message.PlayerId] = message.Shining, false);
        }

        internal static void Tick()
        {
            PlayerAvatar local = CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayer : null;
            bool localShining = local != null && Cleared(local);
            Share(local, localShining);
            Display(local, localShining);
        }

        private static bool Cleared(PlayerAvatar player)
        {
            WeaponSimple weapon = WeaponOf(player);
            return weapon != null && JournalWeaponOutline.ClearedPoints(weapon.entityId) >= RequiredPoints;
        }

        private static WeaponSimple WeaponOf(PlayerAvatar player)
        {
            WeaponControllerSimple controller = player != null ? player.GetComponent<WeaponControllerSimple>() : null;
            return controller != null ? controller.currentWeapon : null;
        }

        private static void Share(PlayerAvatar local, bool localShining)
        {
            if (!NetworkClient.active && !NetworkServer.active)
            {
                ServerStates.Clear();
                ClientStates.Clear();
                return;
            }

            uint localId = local != null ? local.netId : 0;
            if (NetworkServer.active)
            {
                if (localId != 0) SetServerState(localId, localShining);
                if (Time.unscaledTime < nextBroadcast) return;

                nextBroadcast = Time.unscaledTime + BroadcastInterval;
                foreach (KeyValuePair<uint, bool> pair in ServerStates)
                    MoreMapCompatibility.SendToModClients(new StateMessage { PlayerId = pair.Key, Shining = pair.Value });

                return;
            }

            if (localId == 0 || ClientStates.Count == 0 || (localShining == reportedState && localId == reportedId && Time.unscaledTime < nextReport)) return;

            reportedState = localShining;
            reportedId = localId;
            nextReport = Time.unscaledTime + ReportInterval;
            NetworkClient.Send(new ReportMessage { PlayerId = localId, Shining = localShining }, Channels.Reliable);
        }

        private static void SetServerState(uint playerId, bool shining)
        {
            bool previous;
            if (ServerStates.TryGetValue(playerId, out previous) && previous == shining) return;

            ServerStates[playerId] = shining;
            MoreMapCompatibility.SendToModClients(new StateMessage { PlayerId = playerId, Shining = shining });
        }

        private static bool IsShining(PlayerAvatar player, PlayerAvatar local, bool localShining)
        {
            if (player == local) return localShining;

            bool shining;
            Dictionary<uint, bool> states = NetworkServer.active ? ServerStates : ClientStates;
            return states.TryGetValue(player.netId, out shining) && shining;
        }

        private static void Display(PlayerAvatar local, bool localShining)
        {
            Seen.Clear();
            if (ModSettings.WeaponShineEnabled)
            {
                foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
                {
                    PlayerAvatar player = spawner != null ? spawner.PlayerAvatar : null;
                    if (player == null || !IsShining(player, local, localShining)) continue;

                    WeaponSimple weapon = WeaponOf(player);
                    SpriteRenderer renderer = weapon != null && weapon.mainWeaponBody != null ? weapon.mainWeaponBody.weaponSpriteRenderer : null;
                    if (renderer == null) continue;

                    Seen.Add(player);
                    Animate(EntryFor(player, renderer));
                }
            }

            Gone.Clear();
            foreach (KeyValuePair<PlayerAvatar, Entry> pair in Entries)
            {
                if (!Seen.Contains(pair.Key)) Gone.Add(pair.Key);
            }

            foreach (PlayerAvatar player in Gone)
            {
                Entry entry = Entries[player];
                if (entry.Shine != null) Object.Destroy(entry.Shine.gameObject);
                Entries.Remove(player);
            }
        }

        private static Entry EntryFor(PlayerAvatar player, SpriteRenderer renderer)
        {
            Entry entry;
            if (Entries.TryGetValue(player, out entry) && entry.Target == renderer && entry.Shine != null) return entry;

            if (entry != null && entry.Shine != null) Object.Destroy(entry.Shine.gameObject);

            GameObject holder = new GameObject(ShineName);
            holder.transform.SetParent(renderer.transform, false);
            entry = new Entry { Target = renderer, Shine = holder.AddComponent<SpriteRenderer>() };
            entry.Shine.sharedMaterial = ShineMaterial(renderer);
            Entries[player] = entry;
            Debug.Log(LogTag + "적용: " + Path(renderer.transform));
            return entry;
        }

        private static void Animate(Entry entry)
        {
            SpriteRenderer target = entry.Target;
            SpriteRenderer shine = entry.Shine;
            Sprite[] frames = FramesFor(target.sprite);
            float elapsed = Time.time % CycleTime;
            int index = frames == null || elapsed >= SweepTime ? -1 : Mathf.Min(FrameCount - 1, (int)(elapsed / SweepTime * FrameCount));
            shine.sprite = index >= 0 ? frames[index] : null;
            shine.flipX = target.flipX;
            shine.flipY = target.flipY;
            shine.color = new Color(1f, 1f, 1f, target.color.a);
            shine.sortingLayerID = target.sortingLayerID;
            shine.sortingOrder = target.sortingOrder + 1;
            shine.enabled = target.enabled && target.gameObject.activeInHierarchy;
        }

        private static Material ShineMaterial(SpriteRenderer renderer)
        {
            if (material != null) return material;

            Shader shader = Shader.Find("Sprites/Default");
            material = shader != null ? new Material(shader) : renderer.sharedMaterial;
            return material;
        }

        private static string Path(Transform transform)
        {
            string path = transform.name;
            for (Transform current = transform.parent; current != null; current = current.parent)
                path = current.name + "/" + path;

            return path;
        }

        private static Sprite[] FramesFor(Sprite source)
        {
            if (source == null) return null;

            Sprite[] frames;
            if (Frames.TryGetValue(source, out frames)) return frames;

            frames = Build(source);
            Frames[source] = frames;
            return frames;
        }

        private static Sprite[] Build(Sprite source)
        {
            Color32[] pixels = IconColor.ReadPixels(source);
            if (pixels == null) return null;

            int width = Mathf.RoundToInt(source.textureRect.width);
            int height = Mathf.RoundToInt(source.textureRect.height);
            float span = width + height;
            float band = span * BandWidthRate;
            Vector2 pivot = new Vector2(source.pivot.x / width, source.pivot.y / height);
            Sprite[] frames = new Sprite[FrameCount];
            for (int frame = 0; frame < FrameCount; frame++)
            {
                float center = -band + (span + band * 2f) * frame / (FrameCount - 1);
                Color32[] output = new Color32[pixels.Length];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        float distance = Mathf.Abs(x + y - center);
                        if (pixels[i].a < SolidAlpha || distance > band) continue;

                        Color tone = Pearl[(int)Mathf.Repeat((x - y) / PearlStep, Pearl.Length)];
                        tone.a = BandAlpha * (1f - distance / band);
                        output[i] = tone;
                    }
                }

                Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.SetPixels32(output);
                texture.Apply();
                frames[frame] = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, source.pixelsPerUnit);
            }

            return frames;
        }
    }
}
