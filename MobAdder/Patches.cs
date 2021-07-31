
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.AI;

namespace MobAdder
{
	[HarmonyPatch(typeof(MobServerDragon))]
	public class MobServerDragonPatch
    {
		[HarmonyPatch("GroundedToFlight"), HarmonyPrefix]
		static bool GroundedToFlight(MobServerDragon __instance, ref Mob ___mob)
		{
			Bosses.Instance.LeaveLandingPlace(___mob.GetId());
			return true;
		}

		[HarmonyPatch("FlyingToGrounded"), HarmonyPrefix]
		// Token: 0x060002B5 RID: 693 RVA: 0x000104FC File Offset: 0x0000E6FC
		static bool FlyingToGrounded(MobServerDragon __instance, ref Vector3 __result, ref Mob ___mob, ref int ___currentNodes, ref List<Vector3> ___nodes)
		{
			var id = Bosses.Instance.GetBossAtLandingPlace(Bosses.LandingPlace.Port);
			if (id == 0 || id == ___mob.id)
			{
				Bosses.Instance.UseLandingPlace(___mob.id, Bosses.LandingPlace.Port);
				return true;
			}
			___currentNodes = 0;
			__result = ((BobMob)___mob).desiredPos;
			int num = 0;
			while (__result == ((BobMob)___mob).desiredPos)
			{
				__result = ___nodes[Random.Range(0, ___nodes.Count)];
				num++;
				if (num > 100)
				{
					break;
				}
			}
			return false;
		}
	}


	[HarmonyPatch(typeof(BobMob))]
	public class BobMobPatches
	{
		[HarmonyPatch("Awake")]
		static bool BobAwake(BobMob __instance)
		{
			var traverse = new Traverse(__instance);
			__instance.projectileController = __instance.GetComponent<ProjectileAttackNoGravity>();
			__instance.state = BobMob.DragonState.Flying;
			Helper.SetProperty("hitable", __instance, __instance.GetComponent<Hitable>());
			Helper.SetProperty("animator", __instance, __instance.GetComponent<Animator>());

			if (LocalClient.serverOwner)
			{
				__instance.gameObject.AddComponent(MobAdder.GetBehaviour(__instance.mobType.behaviour));
			}
			__instance.attackTimes = new float[__instance.attackAnimations.Length];
			for (int i = 0; i < __instance.attackAnimations.Length; i++)
			{
				__instance.attackTimes[i] = __instance.attackAnimations[i].length;
			}
			return false;
		}
	}

    [HarmonyPatch]
    public class BehaviourPatches
    {
        [HarmonyPatch(typeof(WoodmanBehaviour), nameof(WoodmanBehaviour.MakeAggressive)), HarmonyPrefix]
        static bool MakeAggressive(WoodmanBehaviour __instance, bool first, ref bool ___aggressive, ref Mob ___mob, ref MobServerNeutral ___neutral)
        {
			if (___aggressive)
			{
				return false;
			}
			___aggressive = true;
			___mob.ready = true;
			if (___neutral)
            {
				Object.Destroy(___neutral);
            }

			__instance.gameObject.AddComponent(MobAdder.GetBehaviour(___mob.mobType.behaviour));

			if (first)
			{
				foreach (GameObject gameObject in MobZoneManager.Instance.zones[__instance.mobZoneId].entities)
				{
					gameObject.GetComponent<WoodmanBehaviour>().MakeAggressive(false);
				}
			}
			___mob.agent.speed = ___mob.mobType.speed;
			Object.Destroy(__instance);
			Object.Destroy(__instance.interactObject);
			return false;
        }


		/*
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
			const int codesToAdd = 30;
			int addedCodes = 0;
			int ignoredOpcodes = 0;
			int state = 0;
			int pops = 0;
			foreach (CodeInstruction instruction in instructions)
            {
				switch (state)
                {
					case 0:
						if (instruction.opcode.Name == "brfalse.s")
						{
							state = 1;
						}
						else
						{
							addedCodes++;
							yield return instruction;
						}
						break;
					case 1:
						if (instruction.opcode.Name == "pop")
                        {
							pops++;
							if (pops == 4)
                            {
								yield return new CodeInstruction(OpCodes.Brfalse_S, addedCodes + codesToAdd);
								yield return new CodeInstruction(OpCodes.Ldarg_0);
								yield return new CodeInstruction(OpCodes.Callvirt, typeof(Component).GetMethod("get_gameObject"));
								yield return new CodeInstruction(OpCodes.Ldarg_0);
								yield return new CodeInstruction(OpCodes.Ldfld, typeof(Mob).GetField("mobType"));
								yield return new CodeInstruction(OpCodes.Ldfld, typeof(MobType).GetField("behaviour"));
								yield return new CodeInstruction(OpCodes.Call, typeof(MobAdder).GetMethod("GetBehaviour"));
								yield return new CodeInstruction(OpCodes.Callvirt, typeof(GameObject).GetMethod("AddComponent"));
								yield return new CodeInstruction(OpCodes.Pop);
								state = 2;
                            }
                        }
						ignoredOpcodes++;
						break;
					case 2:
						yield return instruction;
						break;
				}
            }

        }
		*/

		[HarmonyPatch(typeof(Mob), "Awake"), HarmonyPrefix]
		static bool MobAwake(Mob __instance, ref float ___defaulAngularSpeed)
		{
			var traverse = new Traverse(__instance);
			Helper.SetProperty("hitable", __instance, __instance.GetComponent<Hitable>());
			Helper.SetProperty("agent", __instance, __instance.GetComponent<NavMeshAgent>());

			if (!__instance.mobType)
			{
				Mod.instance.log.LogWarning("Mobtype is NULL");
				return false;
			}

			__instance.agent.speed = __instance.mobType.speed;

			___defaulAngularSpeed = __instance.agent.angularSpeed;

			Helper.SetProperty("animator", __instance, __instance.GetComponent<Animator>());

			if (LocalClient.serverOwner)
			{
				__instance.gameObject.AddComponent(MobAdder.GetBehaviour(__instance.mobType.behaviour));
			}
			__instance.attackTimes = new float[__instance.attackAnimations.Length];
			for (int i = 0; i < __instance.attackAnimations.Length; i++)
			{
				__instance.attackTimes[i] = __instance.attackAnimations[i].length;
			}
			return false;
		}

		[HarmonyPatch(typeof(MobManager), nameof(MobManager.GetActiveEnemies)), HarmonyPrefix]
		static bool GetActiveEnemies(MobManager __instance, ref int __result)
		{
			__result = 0;
			foreach (Mob mob in __instance.mobs.Values)
			{
				if (!mob.gameObject.CompareTag("DontCount") && MobAdder.IsEnemy(mob))
				{
					Debug.LogError("Counting enemy: " + mob.gameObject.name);
					__result++;
				}
			}
			return false;
		}

		[HarmonyPatch(typeof(DontAttackUntilPlayerSpotted), "FoundPlayer"), HarmonyPrefix]
		private static bool FoundPlayer(DontAttackUntilPlayerSpotted __instance, ref Mob ___mob, ref MobServerNeutral ___neutral)
		{
			___mob.ready = true;
			Object.Destroy(___neutral);
			__instance.gameObject.AddComponent(MobAdder.GetBehaviour(___mob.mobType.behaviour));
			Object.Destroy(__instance);
			return false;
		}

		[HarmonyPatch(typeof(MobZone), nameof(MobZone.ServerSpawnEntity)), HarmonyPrefix]
		static bool ServerSpawnEntity(MobZone __instance, ref int ___entityBuffer)
		{
			Vector3 vector = __instance.FindRandomPos();
			if (vector == Vector3.zero)
			{
				return false;
			}
			___entityBuffer--;
			int nextId = MobManager.Instance.GetNextId();
			int id = __instance.mobType.id;
			GameObject gameObject = __instance.LocalSpawnEntity(vector, id, nextId, __instance.id);
			MobServerNeutral component = gameObject.GetComponent<MobServerNeutral>();
			if (component)
			{
				component.mobZoneId = __instance.id;
			}
			ServerSend.MobZoneSpawn(vector, id, nextId, __instance.id);

			if (MobAdder.TryGetCreationFunction(__instance.mobType, out var func))
            {
				func(gameObject, __instance);
            }
			return false;
		}
	}

	[HarmonyPatch(typeof(Boat))]
	class BoatPatch
	{
		[HarmonyPatch("Start"), HarmonyPostfix]
		static void Start(Boat __instance)
        {
			BossAdder.OnBoatLoaded();
        }

		[HarmonyPatch("MoveBoat"), HarmonyPrefix]
		static bool Move(Boat __instance, ref Rigidbody ___rb, ref float ___heightUnderWater, ref bool ___sinking, ref ConsistentRandom ___rand)
		{
			float d = 2f;
			Vector3 b = Vector3.up * d * Time.deltaTime;
			World.Instance.water.position += b;
			float y = World.Instance.water.position.y;
			if (___rb.position.y < y - ___heightUnderWater)
			{
				if (!__instance.waterSfx.activeInHierarchy)
				{
					__instance.waterSfx.SetActive(true);
				}
				___rb.MovePosition(new Vector3(__instance.transform.position.x, y - ___heightUnderWater, __instance.transform.position.z));
			}
			if (y > 85f)
			{
				___sinking = false;
				if (LocalClient.serverOwner)
				{
					float bossMultiplier = 0.85f + 0.15f * (float)GameManager.instance.GetPlayersAlive();

					int length = Mod.instance.numLastBosses.Value;
					int[] ids = new int[length];

                    for (int i = 0; i < length; i++)
					{
						int nextId = MobManager.Instance.GetNextId();
						var boss = BossAdder.GetLastBoss(___rand);
						MobSpawner.Instance.ServerSpawnNewMob(nextId, boss.mob.id, __instance.transform.TransformPoint(boss.spawnPos), 1f, bossMultiplier, Mob.BossType.None);

						ids[i] = nextId;
					}

					Bosses.SetBossesID(ids);

					List<Mob> list = new List<Mob>();
					foreach (Mob item in MobManager.Instance.mobs.Values)
					{
						list.Add(item);
					}
					for (int i = 0; i < list.Count; i++)
					{
						list[i].hitable.Hit(list[i].hitable.maxHp, 1f, 2, list[i].transform.position, -1);
					}
				}
			}
			return false;
		}
	}
	


	[HarmonyPatch(typeof(GenerateAllResources))]
	class GeneratePatch
    {

		[HarmonyPatch("Awake"), HarmonyPrefix]
		static bool Awake(GenerateAllResources __instance)
        {
			StructureSpawner shrineGenerator = null;
			for (int i = 0; i < __instance.spawners.Length; i++)
            {
				if (__instance.spawners[i] && __instance.spawners[i].name == "ShrineGenerator")
                {
					shrineGenerator = __instance.spawners[i].GetComponent<StructureSpawner>();
					break;
                }
            }
			shrineGenerator.nShrines = Mod.instance.numShrines.Value;
			BossAdder.FillShrineArray(ref shrineGenerator.structurePrefabs);
			return true;
        }
    }

	[HarmonyPatch(typeof(MobSpawner))]
	public class MobSpawnerPatch
    {
		[HarmonyPatch("FillList"), HarmonyPostfix]
		static void FillList(MobSpawner __instance)
        {
			MobAdder.FillMobArray(ref __instance.allMobs);
        }
    }

	[HarmonyPatch(typeof(GameLoop))]
	public class GameLoopPatch
    {
		[HarmonyPatch("Awake"), HarmonyPrefix]
		static void Awake(GameLoop __instance)
        {
			MobAdder.FillSpawnArray(ref __instance.mobs);
			BossAdder.FillNightRotationArray(ref __instance.bosses);
        }
    }

	[HarmonyPatch(typeof(ItemManager))]
	public class ItemManagerPatches
    {
		[HarmonyPatch("InitAllDropTables"), HarmonyPostfix]
		static void InitAllDropTables(ItemManager __instance)
		{
			ItemAdder.FillLootDropList();
		}

		[HarmonyPatch("InitAllItems"), HarmonyPostfix]
		static void InitAllItems(ItemManager __instance)
		{
			ItemAdder.FillItemList();
		}
	}


	[HarmonyPatch(typeof(Dragon))]
	class DragonPatch
    {
		[HarmonyPatch("Start"), HarmonyPrefix]
		static bool Start(Dragon __instance)
        {
			return false;
        }


		[HarmonyPatch("OnDestroy"), HarmonyPrefix]
		static bool OnDestroy(Dragon __instance)
		{
			Object.Instantiate<GameObject>(__instance.roar, __instance.transform.position, Quaternion.identity);
			Bosses.Instance.BossDie(__instance.GetComponent<BobMob>().GetId());
			return false;
		}
	}

	[HarmonyPatch(typeof(ClientHandle))]
	class ClientHandlePatches
    {
		[HarmonyPatch(nameof(ClientHandle.DragonUpdate)), HarmonyPrefix]
		static bool BossUpdate(Packet packet)
		{
			int state = packet.ReadInt(true);
			if (Bosses.Instance)
            {
				Bosses.RecieveUpdate(state);
            }
			return false;
		}
	}
}
