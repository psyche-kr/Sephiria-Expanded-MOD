using System.Collections;
using System;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class BossChallengeNet
    {
        internal const float RingStep = 1.75f;
        internal const float RingDelay = 0.08f;

        private const int PerBossLevels = 1024;
        private static int clientLevel;
        private static int sentLevel = -1;

        private struct CircleMessage : NetworkMessage
        {
            public Vector2 Position;
            public float Radius;
            public float Time;
        }

        private struct RingMessage : NetworkMessage
        {
            public uint BossId;
            public Vector2 Center;
            public int Rings;
            public int Count;
            public float Offset;
        }

        private struct LineMessage : NetworkMessage
        {
            public Vector2 From;
            public Vector2 To;
            public float Time;
        }

        private struct LevelMessage : NetworkMessage
        {
            public int Level;
        }

        internal static int GetDisplayLevel(Type unitType)
        {
            BossChallengeRules rules = BossChallenge.For(unitType);
            if (rules == null) return 0;
            if (NetworkServer.active) return rules.Level;
            return NetworkClient.active ? DecodeLevel(clientLevel, unitType) : 0;
        }

        internal static int GetDisplayLevel(UnitAI_BossBasic ai)
        {
            if (ai is UnitAI_MoleChieftain) return GetDisplayLevel(typeof(Unit_MoleChieftain));
            if (ai is UnitAI_MoleBigBomb) return GetDisplayLevel(typeof(Unit_MoleBigBomb));
            if (ai is UnitAI_RootDemon) return GetDisplayLevel(typeof(Unit_RootDemon));
            if (ai is UnitAI_OinkKing) return GetDisplayLevel(typeof(Unit_OinkKing));
            if (ai is UnitAI_PantherRogueKing) return GetDisplayLevel(typeof(Unit_PantherRogueKing));
            if (ai is UnitAI_MadArmadillo) return GetDisplayLevel(typeof(Unit_MadArmadillo));
            if (ai is UnitAI_LibraryGuard) return GetDisplayLevel(typeof(Unit_LibraryGuard));
            if (ai is UnitAI_Askard_Chapter3) return GetDisplayLevel(typeof(Unit_Askard_Chapter3));
            if (ai is UnitAI_BirdDemon) return GetDisplayLevel(typeof(Unit_BirdDemon));
            if (ai is UnitAI_LizardDemon) return GetDisplayLevel(typeof(Unit_LizardDemon));
            return 0;
        }

        private static int EncodeLevels()
        {
            return PerBossLevels
                | BossChallenge.MoleChieftain.Level
                | BossChallenge.MoleBigBomb.Level << 2
                | BossChallenge.RootDemon.Level << 4
                | BossChallenge.OinkKing.Level << 6
                | BossChallenge.PantherRogueKing.Level << 8
                | BossChallenge.MadArmadillo.Level << 11
                | BossChallenge.LibraryGuard.Level << 13
                | BossChallenge.Askard.Level << 15
                | BossChallenge.BirdDemon.Level << 17
                | BossChallenge.LizardDemon.Level << 19;
        }

        private static int DecodeLevel(int levels, Type unitType)
        {
            if ((levels & PerBossLevels) == 0) return Mathf.Clamp(levels, 0, 2);
            int shift = typeof(Unit_MoleChieftain).IsAssignableFrom(unitType) ? 0
                : typeof(Unit_MoleBigBomb).IsAssignableFrom(unitType) ? 2
                : typeof(Unit_RootDemon).IsAssignableFrom(unitType) ? 4
                : typeof(Unit_OinkKing).IsAssignableFrom(unitType) ? 6
                : typeof(Unit_MadArmadillo).IsAssignableFrom(unitType) ? 11
                : typeof(Unit_LibraryGuard).IsAssignableFrom(unitType) ? 13
                : typeof(Unit_Askard).IsAssignableFrom(unitType) || typeof(Unit_Askard_Chapter2).IsAssignableFrom(unitType) || typeof(Unit_Askard_Chapter3).IsAssignableFrom(unitType) ? 15
                : typeof(Unit_BirdDemon).IsAssignableFrom(unitType) ? 17
                : typeof(Unit_LizardDemon).IsAssignableFrom(unitType) ? 19
                : 8;
            return Mathf.Clamp(levels >> shift & 3, 0, 2);
        }

        internal static void RegisterSerializers()
        {
            Writer<CircleMessage>.write = (writer, message) =>
            {
                writer.WriteVector2(message.Position);
                writer.WriteFloat(message.Radius);
                writer.WriteFloat(message.Time);
            };
            Reader<CircleMessage>.read = reader => new CircleMessage
            {
                Position = reader.ReadVector2(),
                Radius = reader.ReadFloat(),
                Time = reader.ReadFloat()
            };
            Writer<RingMessage>.write = (writer, message) =>
            {
                writer.WriteUInt(message.BossId);
                writer.WriteVector2(message.Center);
                writer.WriteInt(message.Rings);
                writer.WriteInt(message.Count);
                writer.WriteFloat(message.Offset);
            };
            Reader<RingMessage>.read = reader => new RingMessage
            {
                BossId = reader.ReadUInt(),
                Center = reader.ReadVector2(),
                Rings = reader.ReadInt(),
                Count = reader.ReadInt(),
                Offset = reader.ReadFloat()
            };
            Writer<LineMessage>.write = (writer, message) =>
            {
                writer.WriteVector2(message.From);
                writer.WriteVector2(message.To);
                writer.WriteFloat(message.Time);
            };
            Reader<LineMessage>.read = reader => new LineMessage
            {
                From = reader.ReadVector2(),
                To = reader.ReadVector2(),
                Time = reader.ReadFloat()
            };
            Writer<LevelMessage>.write = (writer, message) => writer.WriteInt(message.Level);
            Reader<LevelMessage>.read = reader => new LevelMessage { Level = reader.ReadInt() };
        }

        internal static void RegisterClientHandlers()
        {
            NetworkClient.ReplaceHandler<CircleMessage>(message => ShowCircle(message.Position, message.Radius, message.Time), false);
            NetworkClient.ReplaceHandler<RingMessage>(ReceiveRing, false);
            NetworkClient.ReplaceHandler<LineMessage>(message => ShowLine(message.From, message.To, message.Time), false);
            NetworkClient.ReplaceHandler<LevelMessage>(message => clientLevel = message.Level, false);
        }

        internal static void Tick()
        {
            if (!NetworkServer.active)
            {
                sentLevel = -1;
                return;
            }

            int level = EncodeLevels();
            if (level == sentLevel) return;

            sentLevel = level;
            MoreMapCompatibility.SendToModClients(new LevelMessage { Level = level });
        }

        internal static void SendLevelTo(NetworkConnectionToClient connection)
        {
            if (connection != null) connection.Send(new LevelMessage { Level = EncodeLevels() }, Channels.Reliable);
        }

        internal static void Circle(Vector2 position, float radius, float time)
        {
            ShowCircle(position, radius, time);
            MoreMapCompatibility.SendToModClients(new CircleMessage { Position = position, Radius = radius, Time = time });
        }

        internal static void Ring(Unit_MoleBigBomb boss, Vector2 center, int rings, int count, float offset)
        {
            if (boss == null) return;

            ShowRing(boss, center, rings, count, offset);
            MoreMapCompatibility.SendToModClients(new RingMessage { BossId = boss.netId, Center = center, Rings = rings, Count = count, Offset = offset });
        }

        internal static void Line(Vector2 from, Vector2 to, float time)
        {
            ShowLine(from, to, time);
            MoreMapCompatibility.SendToModClients(new LineMessage { From = from, To = to, Time = time });
        }

        private static void ShowLine(Vector2 from, Vector2 to, float time)
        {
            if (NetworkClient.active && UIManager.Pool != null) AOEWarningFactory.CreateAoe_RangeAttackLine(from, to, AOEWarningFactory.HostileWarningColor, time);
        }

        private static void ShowCircle(Vector2 position, float radius, float time)
        {
            if (NetworkClient.active && UIManager.Pool != null) AOEWarningFactory.CreateAoe_Ellipse(position, position, AOEWarningFactory.HostileWarningColor, radius, time);
        }

        private static void ReceiveRing(RingMessage message)
        {
            NetworkIdentity identity;
            if (!NetworkClient.spawned.TryGetValue(message.BossId, out identity) || identity == null) return;

            Unit_MoleBigBomb boss = identity.GetComponent<Unit_MoleBigBomb>();
            if (boss != null) ShowRing(boss, message.Center, message.Rings, message.Count, message.Offset);
        }

        private static void ShowRing(Unit_MoleBigBomb boss, Vector2 center, int rings, int count, float offset)
        {
            if (NetworkClient.active && boss.missileWarningFxPrefab != null) boss.StartCoroutine(RingRoutine(boss, center, rings, count, offset));
        }

        private static IEnumerator RingRoutine(Unit_MoleBigBomb boss, Vector2 center, int rings, int count, float offset)
        {
            for (int time = 0; time < rings; time++)
            {
                for (int i = 0; i < count && (time != 0 || i < 1); i++)
                {
                    Vector2 point = center + time * RingStep * HorayUtility.GetVector2FromAngle((float)i / count * 360f + offset);
                    if (!Physics2D.OverlapPoint(point, CombatManager.TileLayerMask)) SpriteFx.Pool.Spawn(boss.missileWarningFxPrefab, point);
                }

                yield return new WaitForSeconds(RingDelay);
            }
        }
    }
}
