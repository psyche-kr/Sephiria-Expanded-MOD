using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal sealed class DamageMeterCollector
    {
        private const float PollInterval = 0.1f;
        private const string CompanionId = "Companion_Unknown";
        private const string LogTag = "[Sephiria Expanded][DamageMeter] ";
        internal const string UnknownId = "Unknown";
        private const char ElementSeparator = '\u001F';

        private enum Mode
        {
            None,
            Host,
            Poll
        }

        private sealed class Binding
        {
            internal Action<UnitAvatar, DamageInstance> Attack;
            internal Action<UnitAvatar, UnitAvatar, DamageInstance> Leader;
        }

        private readonly DamageMeterModel model;
        private readonly Dictionary<PlayerAvatar, Binding> bindings = new Dictionary<PlayerAvatar, Binding>();
        private readonly Dictionary<uint, Dictionary<string, float>> lastSources = new Dictionary<uint, Dictionary<string, float>>();
        private readonly Dictionary<string, float> scratch = new Dictionary<string, float>();
        private readonly List<PlayerAvatar> stale = new List<PlayerAvatar>();
        private float nextPoll;
        private Mode mode;

        internal DamageMeterCollector(DamageMeterModel model)
        {
            this.model = model;
        }

        internal bool InBattle { get; private set; }

        internal static void CollectPlayers(List<PlayerAvatar> buffer)
        {
            buffer.Clear();
            if (PlayerSpawner.MultiplayerList == null) return;

            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar avatar = spawner == null ? null : spawner.PlayerAvatar;
                if (avatar != null && avatar.netId != 0) buffer.Add(avatar);
            }
        }

        internal static string NameOf(PlayerAvatar player)
        {
            return string.IsNullOrEmpty(player.playerNameSource) ? "Player " + player.netId : player.playerNameSource;
        }

        internal static float RunTotal(PlayerAvatar player)
        {
            float total = 0f;
            if (player.dealsStatistics == null) return total;

            foreach (KeyValuePair<DamageKey, float> pair in player.dealsStatistics)
                total += pair.Value;

            return total;
        }

        internal void Tick(float now, List<PlayerAvatar> players)
        {
            InBattle = false;
            foreach (PlayerAvatar player in players)
                if (player.IsInBattle) InBattle = true;

            if (NetworkServer.active)
            {
                SetMode(Mode.Host);
                Bind(players);
                return;
            }

            SetMode(Mode.Poll);
            Unbind();
            Poll(now, players);
        }

        internal void Release()
        {
            Unbind();
            lastSources.Clear();
            mode = Mode.None;
        }

        private void SetMode(Mode next)
        {
            if (mode == next) return;

            mode = next;
            Debug.Log(LogTag + "집계 방식: " + (next == Mode.Host ? "호스트 피해 이벤트" : "바닐라 통계 폴링(허수아비 제외)"));
        }

        private void Unbind()
        {
            foreach (KeyValuePair<PlayerAvatar, Binding> pair in bindings)
            {
                if (pair.Key == null) continue;

                pair.Key.OnAttackUnit -= pair.Value.Attack;
                pair.Key.OnAttackUnitAsLeader -= pair.Value.Leader;
            }

            bindings.Clear();
        }

        private void Bind(List<PlayerAvatar> players)
        {
            stale.Clear();
            foreach (PlayerAvatar bound in bindings.Keys)
                if (bound == null) stale.Add(bound);

            foreach (PlayerAvatar gone in stale)
                bindings.Remove(gone);

            foreach (PlayerAvatar player in players)
            {
                if (bindings.ContainsKey(player)) continue;

                PlayerAvatar owner = player;
                Binding binding = new Binding
                {
                    Attack = (victim, damage) => Record(owner, victim, damage == null ? null : damage.id, damage),
                    Leader = (victim, follower, damage) => Record(owner, victim, follower != null && !string.IsNullOrWhiteSpace(follower.followerDamageId) ? follower.followerDamageId : CompanionId, damage)
                };

                owner.OnAttackUnit += binding.Attack;
                owner.OnAttackUnitAsLeader += binding.Leader;
                bindings[owner] = binding;
            }
        }

        internal static string SourceKey(string id, EDamageElementalType element)
        {
            return element == EDamageElementalType.Physical || element == EDamageElementalType.Normal ? id : id + ElementSeparator + (int)element;
        }

        internal static void SplitSource(string key, out string id, out EDamageElementalType element)
        {
            int index = key == null ? -1 : key.LastIndexOf(ElementSeparator);
            int value;
            if (index > 0 && int.TryParse(key.Substring(index + 1), out value))
            {
                id = key.Substring(0, index);
                element = (EDamageElementalType)value;
                return;
            }

            id = key;
            element = EDamageElementalType.Physical;
        }

        private void Record(PlayerAvatar owner, UnitAvatar victim, string source, DamageInstance damage)
        {
            if (owner == null || damage == null) return;

            model.AddDamage(Time.time, owner.netId, SourceKey(string.IsNullOrWhiteSpace(source) ? UnknownId : source, damage.elementalType), damage.damageResult, InBattle);
            if (victim != null) model.NoteVictim(victim.monsterType == EMonsterType.Dummy, victim.monsterType == EMonsterType.Boss || victim.monsterType == EMonsterType.Miniboss);
        }

        internal static void CollectRunSources(PlayerAvatar player, Dictionary<string, float> result)
        {
            result.Clear();
            if (player.dealsStatistics == null) return;

            foreach (KeyValuePair<DamageKey, float> pair in player.dealsStatistics)
            {
                string key = SourceKey(pair.Key.Id, pair.Key.ElementalType);
                float current;
                result.TryGetValue(key, out current);
                result[key] = current + pair.Value;
            }
        }

        private void Poll(float now, List<PlayerAvatar> players)
        {
            if (now < nextPoll) return;

            nextPoll = now + PollInterval;
            foreach (PlayerAvatar player in players)
            {
                CollectRunSources(player, scratch);

                Dictionary<string, float> last;
                if (!lastSources.TryGetValue(player.netId, out last))
                {
                    lastSources[player.netId] = new Dictionary<string, float>(scratch);
                    continue;
                }

                foreach (KeyValuePair<string, float> pair in scratch)
                {
                    float before;
                    last.TryGetValue(pair.Key, out before);
                    float gained = pair.Value >= before ? pair.Value - before : pair.Value;
                    if (gained > 0f) model.AddDamage(now, player.netId, pair.Key, gained, InBattle);
                }

                last.Clear();
                foreach (KeyValuePair<string, float> pair in scratch)
                    last[pair.Key] = pair.Value;
            }
        }
    }
}
