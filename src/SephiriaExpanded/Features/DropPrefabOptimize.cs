using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaExpanded
{
    internal static class DropPrefabOptimize
    {
        private static readonly Dictionary<GameObject, Carrier> Carriers = new Dictionary<GameObject, Carrier>();
        private static readonly Dictionary<GameObject, Carrier> CarriersByPrefab = new Dictionary<GameObject, Carrier>();
        private static readonly MethodInfo CreateMethod = AccessTools.Method(typeof(DropPrefabOptimize), nameof(Create));
        private static readonly MethodInfo SpawnOwnedMethod = AccessTools.Method(typeof(DropPrefabOptimize), nameof(SpawnOwned));
        private static readonly MethodInfo SpawnConnectionMethod = AccessTools.Method(typeof(DropPrefabOptimize), nameof(SpawnConnection));
        private static readonly MethodInfo ExpForceMethod = AccessTools.Method(typeof(DropPrefabOptimize), nameof(ExpForce));
        private static readonly MethodInfo MoneyForceMethod = AccessTools.Method(typeof(DropPrefabOptimize), nameof(MoneyForce));

        private static Batch currentBatch;
        private static GameObject root;

        private static bool Enabled => NetworkServer.active && ModSettings.InstantLeafExpEnabled;

        private static void Begin(out Batch __state)
        {
            __state = null;
            if (!Enabled) return;

            __state = new Batch { Parent = currentBatch };
            currentBatch = __state;
        }

        private static void End(Batch state)
        {
            if (state == null || state.Closed) return;

            state.Closed = true;
            currentBatch = state.Parent;

            foreach (Request request in state.Requests)
            {
                if (request.IsExperience)
                    GrantExperience(request.Target, request.Amount, request.IgnoreExperienceAdjustment);
                else
                    GrantMoney(request.Amount);
            }
        }

        private static Exception FinishAfterException(Exception __exception, Batch __state)
        {
            End(__state);
            return __exception;
        }

        private static GameObject Create(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (currentBatch == null || prefab == null)
                return UnityEngine.Object.Instantiate(prefab, position, rotation);

            Exp prefabExp = prefab.GetComponent<Exp>();
            Money prefabMoney = prefab.GetComponent<Money>();
            if (prefabExp == null && prefabMoney == null)
                return UnityEngine.Object.Instantiate(prefab, position, rotation);

            Carrier carrier;
            if (!CarriersByPrefab.TryGetValue(prefab, out carrier) || carrier.Object == null)
            {
                if (root == null)
                {
                    root = new GameObject("SephiriaExpanded_DropPrefabOptimize");
                    root.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(root);
                }

                GameObject carrierObject = UnityEngine.Object.Instantiate(prefab, root.transform);
                carrierObject.SetActive(false);
                carrier = new Carrier
                {
                    Object = carrierObject,
                    Exp = carrierObject.GetComponent<Exp>(),
                    Money = carrierObject.GetComponent<Money>()
                };
                CarriersByPrefab[prefab] = carrier;
                Carriers[carrierObject] = carrier;
            }

            carrier.Object.transform.SetPositionAndRotation(position, rotation);
            carrier.Last = null;
            if (carrier.Exp != null)
            {
                carrier.Exp.target = null;
                carrier.Exp.amount = prefabExp.amount;
                carrier.Exp.ignoreAdjustment = prefabExp.ignoreAdjustment;
            }
            if (carrier.Money != null)
                carrier.Money.containedMoney = prefabMoney.containedMoney;

            return carrier.Object;
        }

        private static void SpawnOwned(GameObject obj, GameObject owner)
        {
            if (!Capture(obj))
                NetworkServer.Spawn(obj, owner);
        }

        private static void SpawnConnection(GameObject obj, NetworkConnectionToClient connection)
        {
            if (!Capture(obj))
                NetworkServer.Spawn(obj, connection);
        }

        private static void ExpForce(Exp exp, Vector2 move, float jump)
        {
            Carrier carrier;
            if (exp != null && Carriers.TryGetValue(exp.gameObject, out carrier) && carrier.Last != null)
                return;

            exp.AddPhysicalForce(move, jump);
        }

        private static void MoneyForce(Money money, Vector2 move, float jump)
        {
            Carrier carrier;
            if (money != null && Carriers.TryGetValue(money.gameObject, out carrier) && carrier.Last != null)
                return;

            money.AddPhysicalForce(move, jump);
        }

        private static bool Capture(GameObject obj)
        {
            Carrier carrier;
            if (currentBatch == null || obj == null || !Carriers.TryGetValue(obj, out carrier))
                return false;

            Request request = new Request
            {
                IsExperience = carrier.Exp != null,
                Target = carrier.Exp != null ? carrier.Exp.target : null,
                Amount = carrier.Exp != null ? carrier.Exp.amount : carrier.Money.containedMoney,
                IgnoreExperienceAdjustment = carrier.Exp != null && carrier.Exp.ignoreAdjustment
            };
            carrier.Last = request;
            currentBatch.Requests.Add(request);
            return true;
        }

        private static void GrantExperience(UnitAvatar target, int amount, bool ignoreAdjustment)
        {
            if (target == null || amount <= 0) return;

            LevelController level = target.GetComponent<LevelController>();
            if (level != null)
                level.AddExp(GetExperienceAmount(amount, ignoreAdjustment));
        }

        private static void GrantMoney(int amount)
        {
            if (amount <= 0 || PlayerSpawner.MultiplayerList == null) return;

            int playerCount = PlayerSpawner.MultiplayerList.Count(spawner => spawner != null);

            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar player = spawner != null ? spawner.PlayerAvatar : null;
                if (player == null) continue;

                player.AddMoney(GetMoneyAmount(amount, playerCount, player.GetCustomStat(ECustomStat.MoneyDrop)));
            }
        }

        private static int GetExperienceAmount(int amount, bool ignoreAdjustment)
        {
            if (ignoreAdjustment) return amount;

            int connections = NetworkServer.connections != null ? NetworkServer.connections.Count : 1;
            int divisor = Mathf.Clamp(connections, 1, 3);
            return Mathf.Max(0, (int)Math.Round((double)amount / divisor, MidpointRounding.AwayFromZero));
        }

        private static int GetMoneyAmount(int amount, int playerCount, int bonusPercent)
        {
            int multiplier;
            switch (playerCount)
            {
                case 1: multiplier = 5; break;
                case 2: multiplier = 7; break;
                case 3: multiplier = 8; break;
                default: multiplier = 10; break;
            }

            long result = (long)amount * multiplier;
            if (bonusPercent != 0)
                result += (long)((float)result * bonusPercent / 100f);

            return (int)Mathf.Clamp(result, 0L, int.MaxValue);
        }

        private static bool IsInstantiate(MethodInfo method)
        {
            if (method == null || method.DeclaringType != typeof(UnityEngine.Object) || method.Name != "Instantiate")
                return false;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 3 || parameters[1].ParameterType != typeof(Vector3) ||
                parameters[2].ParameterType != typeof(Quaternion))
                return false;

            if (method.ReturnType == typeof(GameObject) && parameters[0].ParameterType == typeof(GameObject))
                return true;

            return method.IsGenericMethod && method.GetGenericArguments().Length == 1 &&
                   method.GetGenericArguments()[0] == typeof(GameObject);
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo method = instruction.operand as MethodInfo;
                if (IsInstantiate(method))
                    instruction.operand = CreateMethod;
                else if (method != null && method.DeclaringType == typeof(NetworkServer) && method.Name == "Spawn")
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 2 && parameters[0].ParameterType == typeof(GameObject))
                    {
                        if (parameters[1].ParameterType == typeof(GameObject))
                            instruction.operand = SpawnOwnedMethod;
                        else if (parameters[1].ParameterType == typeof(NetworkConnectionToClient))
                            instruction.operand = SpawnConnectionMethod;
                    }
                }
                else if (method != null && method.Name == "AddPhysicalForce")
                {
                    if (method.DeclaringType == typeof(Exp))
                        instruction.operand = ExpForceMethod;
                    else if (method.DeclaringType == typeof(Money))
                        instruction.operand = MoneyForceMethod;
                }

                yield return instruction;
            }
        }

        [HarmonyPatch]
        private static class DropMethods
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                string[] types =
                {
                    "DropItemOnDie", "BreakableProp", "C5CrackProp", "CreateGoldOnBreak",
                    "ExpShop", "EXPOrb", "EXPChest", "MolePrisonerAsProp", "MoneyChest", "MagicFountain"
                };
                string[] names =
                {
                    "DropItems", "DropEXP", "ServerDropEXP", "HandleBreak", "ServerCreateExp",
                    "LocalCreateExp", "LocalOpenChest", "LocalCreateMoney", "OnBreak"
                };

                foreach (string typeName in types)
                {
                    Type type = AccessTools.TypeByName(typeName);
                    if (type == null) continue;

                    foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
                        if (names.Contains(method.Name))
                            yield return method;
                }
            }

            [HarmonyPrefix]
            private static void Before(out Batch __state)
            {
                Begin(out __state);
            }

            [HarmonyPostfix]
            private static void After(Batch __state)
            {
                End(__state);
            }

            [HarmonyFinalizer]
            private static Exception AfterException(Exception __exception, Batch __state)
            {
                return FinishAfterException(__exception, __state);
            }

            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> ReplaceDropPrefabs(IEnumerable<CodeInstruction> instructions)
            {
                return Transpile(instructions);
            }
        }

        private sealed class Carrier
        {
            internal GameObject Object;
            internal Exp Exp;
            internal Money Money;
            internal Request Last;
        }

        private sealed class Request
        {
            internal bool IsExperience;
            internal UnitAvatar Target;
            internal int Amount;
            internal bool IgnoreExperienceAdjustment;
        }

        private sealed class Batch
        {
            internal Batch Parent;
            internal bool Closed;
            internal readonly List<Request> Requests = new List<Request>();
        }
    }
}
