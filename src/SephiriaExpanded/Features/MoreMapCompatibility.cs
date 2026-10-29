using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class MoreMapCompatibility
    {
        private const int ProtocolVersion = 1;
        private const float CheckInterval = 0.25f;
        private static readonly HashSet<int> CompatibleConnections = new HashSet<int>();
        private static readonly HashSet<int> IncompatibleConnections = new HashSet<int>();
        private static readonly HashSet<int> ModClients = new HashSet<int>();
        private static bool initialized;
        private static bool hostDisabled;
        private static bool hostAllowsMoreMap;
        private static bool hostSessionLocked;
        private static bool sessionLocked;
        private static bool helloSent;
        private static bool wasServerActive;
        private static bool wasClientActive;
        private static float nextCheck;
        private static string pendingNotice;
        private static string lastStatus;

        private struct HelloMessage : NetworkMessage
        {
            public int Protocol;
            public string Version;
            public bool Enabled;
        }

        private struct StatusMessage : NetworkMessage
        {
            public int Protocol;
            public string Version;
            public bool Enabled;
            public bool Locked;
        }

        internal static void Initialize()
        {
            if (initialized) return;

            initialized = true;
            hostAllowsMoreMap = false;
            hostSessionLocked = false;
            RegisterSerializers();
            RegisterServerHandlers();
            RegisterClientHandlers();
        }

        private static void RegisterSerializers()
        {
            BossChallengeNet.RegisterSerializers();
            BossBreak.RegisterSerializers();
            BossChallengeBadge.RegisterSerializers();
            BossDialogue.RegisterSerializers();
            BossChallengeNotice.RegisterSerializers();
            KillFeed.RegisterSerializers();
            WeaponShine.RegisterSerializers();
            Writer<HelloMessage>.write = (writer, message) =>
            {
                writer.WriteInt(message.Protocol);
                writer.WriteString(message.Version);
                writer.WriteBool(message.Enabled);
            };
            Reader<HelloMessage>.read = reader => new HelloMessage
            {
                Protocol = reader.ReadInt(),
                Version = reader.ReadString(),
                Enabled = reader.ReadBool()
            };
            Writer<StatusMessage>.write = (writer, message) =>
            {
                writer.WriteInt(message.Protocol);
                writer.WriteString(message.Version);
                writer.WriteBool(message.Enabled);
                writer.WriteBool(message.Locked);
            };
            Reader<StatusMessage>.read = reader => new StatusMessage
            {
                Protocol = reader.ReadInt(),
                Version = reader.ReadString(),
                Enabled = reader.ReadBool(),
                Locked = reader.ReadBool()
            };
        }

        private static void RegisterServerHandlers()
        {
            NetworkServer.ReplaceHandler<HelloMessage>(ReceiveHello, false);
            WeaponShine.RegisterServerHandlers();
        }

        private static void RegisterClientHandlers()
        {
            NetworkClient.ReplaceHandler<StatusMessage>(ReceiveStatus, false);
            BossChallengeNet.RegisterClientHandlers();
            BossBreak.RegisterClientHandlers();
            BossChallengeBadge.RegisterClientHandlers();
            BossDialogue.RegisterClientHandlers();
            BossChallengeNotice.RegisterClientHandlers();
            KillFeed.RegisterClientHandlers();
            WeaponShine.RegisterClientHandlers();
        }

        internal static void Cleanup()
        {
            CompatibleConnections.Clear();
            IncompatibleConnections.Clear();
            ModClients.Clear();
            hostDisabled = false;
            hostAllowsMoreMap = false;
            sessionLocked = false;
            helloSent = false;
            wasServerActive = false;
            wasClientActive = false;
            nextCheck = 0f;
            pendingNotice = null;
            lastStatus = null;
        }

        internal static void Tick()
        {
            if (!initialized) return;

            if (NetworkServer.active && !wasServerActive)
            {
                RegisterServerHandlers();
                ResetServerSession();
            }
            else if (!NetworkServer.active && wasServerActive)
                ResetServerSession();
            wasServerActive = NetworkServer.active;

            if (NetworkClient.active && !wasClientActive)
            {
                RegisterClientHandlers();
                hostAllowsMoreMap = false;
                hostSessionLocked = false;
                    helloSent = false;
            }
            else if (!NetworkClient.active && wasClientActive)
            {
                hostAllowsMoreMap = false;
                hostSessionLocked = false;
                    helloSent = false;
            }
            wasClientActive = NetworkClient.active;

            if (NetworkClient.active && !NetworkServer.active && !helloSent)
                SendHello();

            if (NetworkServer.active && Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + CheckInterval;
                RemoveDisconnectedConnections();
                SendStatusToCompatibleClients();
            }

            ShowPendingNotice();
        }

        internal static bool IsExpansionAllowed()
        {
            if (NetworkServer.active)
                return ModSettings.MoreMapEnabled && sessionLocked && !hostDisabled;

            if (NetworkClient.active)
                return hostAllowsMoreMap;

            return ModSettings.MoreMapEnabled;
        }

        internal static bool IsOptionLocked()
        {
            if (NetworkServer.active)
                return hostDisabled;

            return NetworkClient.active && hostSessionLocked;
        }

        internal static bool TryLockBeforeExpansion()
        {
            if (!ModSettings.MoreMapEnabled)
                return false;

            if (!NetworkServer.active)
                return IsExpansionAllowed();

            if (hostDisabled)
                return false;

            if (sessionLocked)
                return true;

            if (HasUnsupportedConnection())
            {
                DisableForSession();
                return false;
            }

            sessionLocked = true;
            SendStatusToCompatibleClients();
            return true;
        }

        internal static void EndRunSession()
        {
            hostDisabled = false;
            hostSessionLocked = false;
            sessionLocked = false;
            pendingNotice = null;
            lastStatus = null;
            ExpandedOptionRows.RefreshValues();
            SendStatusToCompatibleClients();
        }

        private static void SendHello()
        {
            if (!NetworkClient.active || NetworkServer.active || helloSent)
                return;

            NetworkClient.Send(new HelloMessage
            {
                Protocol = ProtocolVersion,
                Version = ModInfo.Version,
                Enabled = ModSettings.MoreMapEnabled
            }, Channels.Reliable);
            helloSent = true;
        }

        private static void ReceiveHello(NetworkConnectionToClient connection, HelloMessage message)
        {
            if (connection == null) return;

            Debug.Log("[Sephiria Expanded][MoreMap] Hello 수신: conn=" + connection.connectionId + ", protocol=" + message.Protocol + ", version=" + message.Version + ", guestEnabled=" + message.Enabled + ", hostEnabled=" + ModSettings.MoreMapEnabled);

            CompatibleConnections.Remove(connection.connectionId);
            IncompatibleConnections.Remove(connection.connectionId);

            bool compatible = message.Protocol == ProtocolVersion &&
                               string.Equals(message.Version, ModInfo.Version, StringComparison.Ordinal);

            if (compatible)
                CompatibleConnections.Add(connection.connectionId);
            else
                IncompatibleConnections.Add(connection.connectionId);

            ModClients.Remove(connection.connectionId);
            if (message.Protocol == ProtocolVersion && string.Equals(message.Version, ModInfo.Version, StringComparison.Ordinal))
            {
                ModClients.Add(connection.connectionId);
                BossChallengeNet.SendLevelTo(connection);
                CutsceneSkip.SendStatusTo(connection);
            }

            if (!compatible && sessionLocked && !hostDisabled)
                DisableForSession();

            SendStatus(connection);
        }

        internal static bool AllConnectionsHaveMod()
        {
            if (!NetworkServer.active || NetworkServer.connections == null)
                return true;

            foreach (KeyValuePair<int, NetworkConnectionToClient> pair in NetworkServer.connections)
            {
                NetworkConnectionToClient connection = pair.Value;
                if (connection == null || !connection.isAuthenticated || connection == NetworkServer.localConnection)
                    continue;
                if (!ModClients.Contains(connection.connectionId))
                    return false;
            }

            return true;
        }

        internal static void SendToModClients<T>(T message) where T : struct, NetworkMessage
        {
            if (!NetworkServer.active || NetworkServer.connections == null) return;

            foreach (KeyValuePair<int, NetworkConnectionToClient> pair in NetworkServer.connections)
            {
                NetworkConnectionToClient connection = pair.Value;
                if (connection != null && connection != NetworkServer.localConnection && ModClients.Contains(connection.connectionId))
                    connection.Send(message, Channels.Reliable);
            }
        }

        private static void ReceiveStatus(StatusMessage message)
        {
            bool wasEnabled = ModSettings.MoreMapEnabled;
            Debug.Log("[Sephiria Expanded][MoreMap] Status 수신: protocol=" + message.Protocol + ", version=" + message.Version + ", enabled=" + message.Enabled + ", locked=" + message.Locked);
            hostAllowsMoreMap = message.Protocol == ProtocolVersion &&
                                message.Enabled &&
                                string.Equals(message.Version, ModInfo.Version, StringComparison.Ordinal);
            hostSessionLocked = message.Locked;

            if (!hostAllowsMoreMap && wasEnabled)
            {
                ModSettings.MoreMapEnabled = false;
                ExpandedOptionRows.RefreshValues();
                pendingNotice = "호스트가 맵 확장을 사용할 수 없어 자동으로 꺼졌습니다.";
            }
        }

        private static bool HasUnsupportedConnection()
        {
            if (NetworkServer.connections == null)
                return false;

            foreach (KeyValuePair<int, NetworkConnectionToClient> pair in NetworkServer.connections)
            {
                NetworkConnectionToClient connection = pair.Value;
                if (connection == null || !connection.isAuthenticated)
                    continue;
                if (connection == NetworkServer.localConnection)
                    continue;

                if (!CompatibleConnections.Contains(connection.connectionId))
                    return true;
            }

            return false;
        }

        private static void RemoveDisconnectedConnections()
        {
            if (NetworkServer.connections == null) return;

            CompatibleConnections.RemoveWhere(id => !NetworkServer.connections.ContainsKey(id));
            IncompatibleConnections.RemoveWhere(id => !NetworkServer.connections.ContainsKey(id));
            ModClients.RemoveWhere(id => !NetworkServer.connections.ContainsKey(id));
        }

        private static void ResetServerSession()
        {
            CompatibleConnections.Clear();
            IncompatibleConnections.Clear();
            ModClients.Clear();
            hostDisabled = false;
            hostSessionLocked = false;
            sessionLocked = false;
            lastStatus = null;
        }

        private static void DisableForSession()
        {
            if (hostDisabled) return;

            hostDisabled = true;
            Debug.Log("[Sephiria Expanded][MoreMap] 호환되지 않는 연결이 있어 비활성화합니다. 호환=" + CompatibleConnections.Count + ", 비호환=" + IncompatibleConnections.Count);
            ModSettings.MoreMapEnabled = false;
            ModSettings.Save();
            ExpandedOptionRows.RefreshValues();
            pendingNotice = "맵 확장이 호환되지 않는 게스트로 인해 자동으로 꺼졌습니다.";
            SendStatusToCompatibleClients();
        }

        private static void SendStatusToCompatibleClients()
        {
            if (NetworkServer.connections == null) return;

            string status = ModSettings.MoreMapEnabled ? "enabled" : "disabled";
            if (status == lastStatus && !hostDisabled) return;
            lastStatus = status;

            foreach (KeyValuePair<int, NetworkConnectionToClient> pair in NetworkServer.connections)
            {
                NetworkConnectionToClient connection = pair.Value;
                if (connection != null && CompatibleConnections.Contains(connection.connectionId))
                    SendStatus(connection);
            }
        }

        private static void SendStatus(NetworkConnectionToClient connection)
        {
            connection.Send(new StatusMessage
            {
                Protocol = ProtocolVersion,
                Version = ModInfo.Version,
                Enabled = ModSettings.MoreMapEnabled &&
                          !hostDisabled &&
                          CompatibleConnections.Contains(connection.connectionId),
                Locked = hostDisabled
            }, Channels.Reliable);
        }

        private static void ShowPendingNotice()
        {
            if (string.IsNullOrEmpty(pendingNotice)) return;

            foreach (UI_HUDLogViewer viewer in UnityEngine.Object.FindObjectsByType<UI_HUDLogViewer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (viewer == null) continue;
                viewer.SpawnLog(pendingNotice, Color.yellow);
                pendingNotice = null;
                return;
            }
        }

        [HarmonyPatch(typeof(NetworkClient), "OnTransportConnected")]
        private static class ClientConnectedPatch
        {
            [HarmonyPostfix]
            private static void After()
            {
                helloSent = false;
                RegisterClientHandlers();
                SendHello();
            }
        }

        [HarmonyPatch(typeof(HorayNetworkManager), "NewGame")]
        private static class NewGamePatch
        {
            [HarmonyPrefix]
            private static void Before()
            {
                EndRunSession();
            }
        }
    }
}
