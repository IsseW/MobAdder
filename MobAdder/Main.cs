using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MobAdder
{
    [BepInPlugin(Guid, Name, Version)]
    internal class Mod : BaseUnityPlugin
    {
        public const string
            Name = "MobAdder",
            Author = "Isse",
            Guid = Author + "." + Name,
            Version = "1.0.0.0";

        public static Mod instance;
        public Harmony harmony;
        public ManualLogSource log;

        public ConfigEntry<int> numShrines;
        public ConfigEntry<int> numLastBosses;

        public GameObject defaultHitFx;
        public GameObject defaultDestroyFx;
        public GameObject defaultNumberFx;
        public GameObject defaultFootstepFx;
        void Awake()
        {
            if (!instance)
                instance = this;
            else
                Destroy(this);

            log = Logger;
            harmony = new Harmony(Guid);
            harmony.PatchAll();

            numShrines = Config.Bind("General", "numShrines", 112, "How many shrines that should be generated");
            numLastBosses = Config.Bind("General", "numLastBosses", 1, "How many bosses that should spawn when the boat leaves the island");


            var bundle = GetAssetBundle("bossmod");

            var assets = bundle.GetAllAssetNames();

            log.LogWarning("Assets: ");
            foreach (string name in assets)
            {
                log.LogMessage(name);
            }

            var joePrefab = bundle.LoadAsset<GameObject>("Joe.prefab");
            var joe = bundle.LoadAsset<MobType>("Vampire.asset");
            var joeShrine = bundle.LoadAsset<GameObject>("BossShrineJoe.prefab");

            defaultNumberFx = bundle.LoadAsset<GameObject>("DefaultNumberFx.prefab");
            defaultDestroyFx = bundle.LoadAsset<GameObject>("DefaultDestroyFx.prefab");
            defaultHitFx = bundle.LoadAsset<GameObject>("DefaultHitFx.prefab");
            defaultFootstepFx = bundle.LoadAsset<GameObject>("DefaultFootstepFx.prefab");

            ItemAdder.LoadAssetBundle(bundle, Guid);

            MobLoader.LoadMob(joe, Guid, MobOption.ShrineBoss | MobOption.NightBoss | MobOption.OverrideMobComponent, mobType: typeof(JoeMob), bossShrineOptions: new MobLoader.BossShrine(joeShrine, 100));
        }

        static readonly OSPlatform[] supportedPlatforms = new[] { OSPlatform.Windows, OSPlatform.Linux, OSPlatform.OSX };

        static AssetBundle GetAssetBundle(string name)
        {
            foreach (var platform in supportedPlatforms)
            {
                if (RuntimeInformation.IsOSPlatform(platform))
                {
                    name = $"{name}-{platform.ToString().ToLower()}";
                    goto load;
                }
            }

            throw new PlatformNotSupportedException("Unsupported platform, cannot load AssetBundles");

        load:
            var execAssembly = Assembly.GetExecutingAssembly();

            instance.log.LogMessage(name);

            instance.log.LogWarning("RESOURCES: ");
            foreach (var v in execAssembly.GetManifestResourceNames())
            {
                instance.log.LogMessage(v);
            }

            var resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(name));

            using (var stream = execAssembly.GetManifestResourceStream(resourceName))
            {
                return AssetBundle.LoadFromStream(stream);
            }
        }
    }

    internal static class Helper
    {
        public static void ExtendArray<T>(ref T[] arr, List<T> list)
        {
            var old = arr;
            arr = new T[old.Length + list.Count];
            int i = 0;
            for (; i < old.Length; i++)
            {
                arr[i] = old[i];
            }
            for (int j = 0; j < list.Count; j++)
            {
                arr[i + j] = list[j];
            }
            list.Clear();
        }
        public static void SetProperty<T, V>(string name, V obj, T value)
        {
            var prop = typeof(V).GetProperty(name);
            prop = prop.DeclaringType.GetProperty(name);
            prop.SetValue(obj, value, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public, null, null, null);
        }
    }

    public static class MobAdder
    {
        static LootDrop empty;
        public static void AssignAndAddDefault(GameObject mobPrefab, MobType mobType, int hp = 100, LootDrop drops = null)
        {
            if (!drops)
            {
                if (!empty)
                {
                    empty = ScriptableObject.CreateInstance<LootDrop>();
                    empty.loot = new LootDrop.LootItems[0];
                }
                drops = empty;
            }
            if (mobPrefab)
            {
                {
                    var hitable = mobPrefab.GetComponent<HitableMob>();
                    if (!hitable)
                    {
                        hitable = mobPrefab.AddComponent<HitableMob>();
                    }
                    if (hitable.entityName.IsNullOrWhiteSpace())
                    {
                        hitable.entityName = mobType.name;
                    }
                    if (!hitable.dropTable)
                    {
                        hitable.dropTable = drops;
                    }
                    if (hitable.maxHp == 0)
                    {
                        hitable.maxHp = hp;
                        hitable.hp = hp;
                    }
                    if (!hitable.hitFx)
                    {
                        hitable.hitFx = Mod.instance.defaultHitFx;
                    }
                    if (!hitable.destroyFx)
                    {
                        hitable.hitFx = Mod.instance.defaultDestroyFx;
                    }
                    if (!hitable.numberFx)
                    {
                        hitable.hitFx = Mod.instance.defaultNumberFx;
                    }
                }
                {
                    var mob = mobPrefab.GetComponent<Mob>();
                    if (!mob)
                    {
                        mobPrefab.AddComponent<Mob>();
                    }
                    if (!mob.mobType)
                    {
                        mob.mobType = mobType;
                    }
                    if (mob.attackAnimations == null)
                    {
                        mob.attackAnimations = new AnimationClip[0];
                    }
                    if (!mob.footstepFx)
                    {
                        mob.footstepFx = Mod.instance.defaultFootstepFx;
                    }
                    if (mob.footstepFrequency == 0f)
                    {
                        mob.footstepFrequency = 1.0f;
                    }
                    if (mob.attackCooldown == 0)
                    {
                        mob.attackCooldown = 1;
                    }
                }
                {
                    var agent = mobPrefab.GetComponent<NavMeshAgent>();
                    if (!agent)
                    {
                        agent = mobPrefab.AddComponent<NavMeshAgent>();
                    }
                }
                {
                    var animator = mobPrefab.GetComponent<Animator>();
                    if (!animator)
                    {
                        animator = mobPrefab.AddComponent<Animator>();
                    }
                }
            }
        }

        public static Mob ReplaceMobComponent(GameObject prefab, Type t)
        {
            if (t == null || !typeof(Mob).IsAssignableFrom(t))
            {
                Mod.instance.log.LogError("Tried to add mob type that is null or doesn't inherit from mob");
                return null;
            }
            Mob m = prefab.GetComponent<Mob>();
            if (!m)
            {
                Mod.instance.log.LogError("Prefab doesn't have mob component");
                return null;
            }
            Object.Destroy(m);
            var nm = (Mob)prefab.AddComponent(t);
            nm.attackAnimations = m.attackAnimations;
            nm.attackCooldown = m.attackCooldown;
            nm.bossType = m.bossType;
            nm.countedKill = m.countedKill;
            nm.footstepFrequency = m.footstepFrequency;
            nm.footstepFx = m.footstepFx;
            nm.mobType = m.mobType;
            nm.nRangedAttacks = m.nRangedAttacks;
            nm.stopOnAttack = m.stopOnAttack;
            return nm;
        }

        public static void DefaultCreationFunction(GameObject gameObject, MobZone zone)
        {
            gameObject.AddComponent<DontAttackUntilPlayerSpotted>().mobZoneId = zone.id;
        }

        /// <summary>
        /// Registers a mob.
        /// </summary>
        /// <param name="mob"></param>
        /// <param name="creationFunc">Function called upon creation on the server, mostly for adding behaviour to it and setting it's spawn zone.</param>
        /// <param name="is_enemy">Should this mob be counted as an enemy?</param>
        /// <returns></returns>
        public static void AddMob(MobType mob, string GUID, System.Action<GameObject, MobZone> creationFunc = null, bool is_enemy = true)
        {
            if (!mob)
            {
                Mod.instance.log.LogWarning("Tried to add null mob");
                return;
            }

            mob.id = toAdd.Count;
            toAdd.Add(mob);
            modAccess.Add(mob.id, GUID);
            if (!is_enemy)
            {
                notEnemy.Add(mob.name);
            }
            if (creationFunc != null)
            {
                creationFunction.Add(mob.name, creationFunc);
            }
        }

        public static string GetMobMod(int id)
        {
            if (modAccess.TryGetValue(id, out string mod))
            {
                return mod;
            }
            return "Unknown";
        }

        public static string GetMod(this MobType mob)
        {
            return GetMobMod(mob.id);
        }

        public static void AddMobSpawn(MobType mob, int dayStart, int dayPeak, float maxWeight)
        {
            if (!mob)
            {
                Mod.instance.log.LogWarning("Tried to add null mob spawn");
                return;
            }

            mobSpawns.Add(new GameLoop.MobSpawn() { mob = mob, dayStart = dayStart, dayPeak = dayPeak, maxWeight = maxWeight });
        }

        static Dictionary<int, string> modAccess = new Dictionary<int, string>();

        static List<GameLoop.MobSpawn> mobSpawns = new List<GameLoop.MobSpawn>();

        static List<MobType> toAdd = new List<MobType>();

        static int nextMobBehavoiurID = 0;
        static List<Type> behaviours = new List<Type>();

        static List<bool> enemyTags = new List<bool>();

        static HashSet<string> notEnemy = new HashSet<string>();

        static Dictionary<System.Type, MobType.MobBehaviour> get = new Dictionary<System.Type, MobType.MobBehaviour>();

        static Dictionary<string, System.Action<GameObject, MobZone>> creationFunction = new Dictionary<string, System.Action<GameObject, MobZone>>();
        static Dictionary<System.Type, System.Action<GameObject, MobZone>> bCreationFunction = new Dictionary<System.Type, System.Action<GameObject, MobZone>>();

        static MobAdder()
        {
            AddDefaultBehaviours();
            notEnemy.Add("Woodman");
            creationFunction.Add("Woodman", (gameObject, zone) => gameObject.GetComponent<WoodmanBehaviour>().mobZoneId = zone.id);
        }


        static int loaded = 0;
        public static event EventHandler FinishedLoading = new EventHandler(Loaded);
        static void Loaded(object sender, EventArgs args)
        {

        }

        static void LoadedSomething()
        {
            loaded++;
            if (loaded == 2)
            {
                FinishedLoading?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void FillMobArray(ref MobType[] mobs)
        {
            Dictionary<int, string> newModAccess = new Dictionary<int, string>();
            int nextMobIndex = MobSpawner.Instance.allMobs.Length;
            for (int i = 0; i < nextMobIndex; i++)
            {
                newModAccess.Add(i, "vanilla");
            }
            for (int i = 0; i < toAdd.Count; i++)
            {
                newModAccess.Add(nextMobIndex, modAccess[i]);
                toAdd[i].id = nextMobIndex++;
            }
            modAccess = newModAccess;
            

            Helper.ExtendArray(ref mobs, toAdd);
            toAdd.Clear(); 
            LoadedSomething();
        }

        public static void FillSpawnArray(ref GameLoop.MobSpawn[] spawns)
        {
            Helper.ExtendArray(ref spawns, mobSpawns);
            mobSpawns.Clear();
            LoadedSomething();
        }

        static void AddDefaultBehaviours()
        {
            CreateMobBehaviour<MobServerNeutral>(null, false);
            CreateMobBehaviour<MobServerEnemy>();
            CreateMobBehaviour<MobServerEnemyMeleeAndRanged>();
            CreateMobBehaviour<MobServerDragon>();
        }

        public static bool TryGetCreationFunction(MobType mob, out System.Action<GameObject, MobZone> func)
        {
            return creationFunction.TryGetValue(mob.name, out func) || bCreationFunction.TryGetValue(behaviours[(int)mob.behaviour], out func);
        }

        /// <summary>
        /// Create a mob behaviour, and get it's "id"
        /// </summary>
        /// <returns>id</returns>
        public static MobType.MobBehaviour CreateMobBehaviour<T>(bool is_enemy = true) where T : MobServer
        {
            return CreateMobBehaviour<T>(DefaultCreationFunction, is_enemy);
        }

        /// <summary>
        /// Create a mob behaviour, and get it's "id"
        /// </summary>
        /// <returns>id</returns>
        public static MobType.MobBehaviour CreateMobBehaviour<T>(System.Action<GameObject, MobZone> creationFunc, bool is_enemy = true) where T : MobServer
        {
            if (get.TryGetValue(typeof(T), out var v))
            {
                return v;
            }
            behaviours.Add(typeof(T));
            enemyTags.Add(is_enemy);
            if (creationFunc != null)
            {
                bCreationFunction.Add(typeof(T), creationFunc);
            }

            get.Add(typeof(T), (MobType.MobBehaviour)nextMobBehavoiurID);
            return (MobType.MobBehaviour)nextMobBehavoiurID++;
        }


        public static MobType.MobBehaviour IDOf<T>() where T : MobServer
        {
            if (get.TryGetValue(typeof(T), out var v))
            {
                return v;
            }
            return (MobType.MobBehaviour)(-1);
        }

        public static System.Type GetBehaviour(MobType.MobBehaviour behaviour)
        {
            return behaviours[(int)behaviour];
        }

        public static bool IsEnemy(Mob mob)
        {
            return !notEnemy.Contains(mob.mobType.name) && enemyTags[(int)mob.mobType.behaviour];
        }
    }

    public static class BossAdder
    {
        public static void AddBossShrine(GameObject prefab, float weight)
        {
            if (!prefab)
            {
                Mod.instance.log.LogWarning("Tried to add null boss shrine");
                return;
            }
            shrines.Add(new StructureSpawner.WeightedSpawn() { prefab = prefab, weight = weight });
        }

        public static void AddBossToNightRotation(MobType mob)
        {
            if (!mob)
            {
                Mod.instance.log.LogWarning("Tried to add null mob to boss rotation");
                return;
            }
            nightRotation.Add(mob);
        }

        public class LastBoss
        {
            public MobType mob;
            public Vector3 spawnPos;
            public float rarity;

            public LastBoss(MobType mob, Vector3 spawnPos, float rarity)
            {
                this.mob = mob;
                this.spawnPos = spawnPos;
                this.rarity = rarity;
            }
        }
        static List<LastBoss> lastBosses = new List<LastBoss>();
        static float max_rarity = 0;

        static List<StructureSpawner.WeightedSpawn> shrines = new List<StructureSpawner.WeightedSpawn>();
        public static List<MobType> nightRotation = new List<MobType>();


        static int loaded = 0;
        public static event EventHandler FinishedLoading = new EventHandler(Loaded);
        static void Loaded(object sender, EventArgs args)
        {
             
        }
        static void LoadedSomething()
        {
            loaded++;

            if (loaded == 3)
            {
                FinishedLoading?.Invoke(null, EventArgs.Empty);
            }
        }

        static bool lastBossesLoaded = false;
        public static void OnBoatLoaded()
        {
            if (lastBossesLoaded) return;
            lastBossesLoaded = true;
            AddLastBoss(Boat.Instance.dragonBoss, new Vector3(100, 100, 100), 1f);

            float acc = 0;
            for (int i = 0; i < lastBosses.Count; i++)
            {
                LastBoss tmp = lastBosses[i];
                tmp.rarity = acc;
                acc += lastBosses[i].rarity;
                lastBosses[i] = tmp;
            }
            LoadedSomething();
        }

        public static void FillShrineArray(ref StructureSpawner.WeightedSpawn[] spawns)
        {
            Helper.ExtendArray(ref spawns, shrines);
            shrines.Clear();
            LoadedSomething();
        }

        public static void FillNightRotationArray(ref MobType[] mobs)
        {
            Helper.ExtendArray(ref mobs, nightRotation);
            nightRotation.Clear();
            LoadedSomething();
        }


        static LastBoss BinarySearchBoss(float value, int start, int end)
        {
            if (start >= end) return lastBosses[start];
            int center = (start + end) / 2;
            if (lastBosses[center].rarity < value)
            {
                return BinarySearchBoss(value, start, center - 1);
            }
            if (lastBosses[center].rarity > value)
            {
                return BinarySearchBoss(value, center + 1, end);
            }
            return lastBosses[center];
        }

        public static LastBoss GetLastBoss(ConsistentRandom random)
        {
            return BinarySearchBoss((float)(random.NextDouble() * max_rarity), 0, lastBosses.Count - 1);
        }

        public static void AddLastBoss(MobType mob, Vector3 spawnPos, float weight)
        {
            if (!mob)
            {
                Mod.instance.log.LogWarning("Tried to add null mob as last boss");
                return;
            }
            lastBosses.Add(new LastBoss(mob, spawnPos, weight));
            max_rarity += weight;
        }
    }

    public static class ItemAdder
    {
        static List<InventoryItem> items = new List<InventoryItem>();
        static Dictionary<int, string> itemModLookup = new Dictionary<int, string>();

        static List<LootDrop> lootDrops = new List<LootDrop>();

        public static void AddItem(InventoryItem item, string Guid)
        {
            itemModLookup.Add(items.Count, Guid);
            item.id = items.Count;
            items.Add(item);
        }

        public static void AddLootDrop(LootDrop drop)
        {
            drop.id = lootDrops.Count;
            lootDrops.Add(drop);
        }

        static int loaded = 0;
        public static event EventHandler FinishedLoading = new EventHandler(Loaded);
        static void Loaded(object sender, EventArgs args)
        {

        }
        static void LoadedSomething()
        {
            loaded++;

            if (loaded == 2)
            {
                FinishedLoading?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void FillItemList()
        {
            Dictionary<int, string> itemModLookup = new Dictionary<int, string>();
            for (int i = 0; i < ItemManager.Instance.allItems.Count; i++)
            {
                itemModLookup.Add(i, "vanilla");
            }
            int nextId = ItemManager.Instance.allItems.Count;
            for (int i = 0; i < items.Count; i++)
            {
                while (ItemManager.Instance.allItems.ContainsKey(nextId))
                {
                    nextId++;
                }
                itemModLookup.Add(nextId, ItemAdder.itemModLookup[i]);
                items[i].id = nextId;
                ItemManager.Instance.allItems.Add(nextId, items[i]);
                nextId++;
            }
            ItemAdder.itemModLookup = itemModLookup;
            LoadedSomething();
            items.Clear();
        }

        public static void FillLootDropList()
        {
            int nextId = ItemManager.Instance.allDropTables.Count;
            foreach (var drop in lootDrops)
            {
                while (ItemManager.Instance.allDropTables.ContainsKey(nextId)) nextId++;

                ItemManager.Instance.allDropTables.Add(nextId, drop);
                drop.id = nextId;
                nextId++;
            }
            LoadedSomething();
        }

        public static string GetItemMod(int id)
        {
            if (itemModLookup.TryGetValue(id, out string mod))
            {
                return mod;
            }
            return "Unknown";
        }
        public static string GetMod(this InventoryItem item)
        {
            return GetItemMod(item.id);
        }
        
        public static void LoadAssetBundle(AssetBundle bundle, string Guid, bool ignoreSet = true)
        {
            foreach (var item in bundle.LoadAllAssets<InventoryItem>())
            {
                if (item)
                {
                    AddItem(item, Guid);
                }
            }
            foreach (var loot in bundle.LoadAllAssets<LootDrop>())
            {
                if (loot)
                {
                    AddLootDrop(loot);
                }
            }
        }
    }

    [Flags]
    public enum MobOption
    {
        None = 0, 
        AssignAddDefault = 1, 
        OverrideMobComponent = 2,
        MobSpawn = 4,
        ShrineBoss = 8, 
        NightBoss = 16, 
        LastBoss = 32,

    }

    public static class MobLoader
    {
        public struct MobSpawn
        {
            public int dayStart;
            public int dayPeak;
            public float weight;

            public MobSpawn(int dayStart, int dayPeak, float weight)
            {
                this.dayStart = dayStart;
                this.dayPeak = dayPeak;
                this.weight = weight;
            }
        }

        public struct BossShrine
        {
            public GameObject prefab;
            public float weight;

            public BossShrine(GameObject prefab, float weight)
            {
                this.prefab = prefab;
                this.weight = weight;
            }
        }
        public struct LastBoss
        {
            public Vector3 spawnPos;
            public float weight;

            public LastBoss(Vector3 spawnPos, float weight)
            {
                this.spawnPos = spawnPos;
                this.weight = weight;
            }
        }

        public static void LoadMob(MobType type, string GUID, MobOption options = MobOption.None, bool is_enemy = true, 
                                   Action<GameObject, MobZone> creationFunc = null, Type mobType = null, MobSpawn spawnOptions = default(MobSpawn),
                                   BossShrine bossShrineOptions = default(BossShrine), LastBoss lastBossOptions = default(LastBoss))
        {
            if ((options & MobOption.AssignAddDefault) != 0)
            {
                MobAdder.AssignAndAddDefault(type.mobPrefab, type);
            }
            if ((options & MobOption.OverrideMobComponent) != 0)
            {
                MobAdder.ReplaceMobComponent(type.mobPrefab, mobType);
            }
            MobAdder.AddMob(type, GUID, creationFunc, is_enemy);
            if ((options & MobOption.MobSpawn) != 0)
            {
                MobAdder.AddMobSpawn(type, spawnOptions.dayStart, spawnOptions.dayPeak, spawnOptions.weight);
            }
            if ((options & MobOption.ShrineBoss) != 0)
            {
                BossAdder.AddBossShrine(bossShrineOptions.prefab, bossShrineOptions.weight);
            }
            if ((options & MobOption.NightBoss) != 0)
            {
                BossAdder.AddBossToNightRotation(type);
            }
            if ((options & MobOption.LastBoss) != 0)
            {
                BossAdder.AddLastBoss(type, lastBossOptions.spawnPos, lastBossOptions.weight);
            }
        }
    }


    public interface IBoss
    {
        void BossUpdate(int state);

        int GetID();

        Mob GetMob();
    }


    internal class BobMobWrapper : IBoss
    {
        BobMob mob;

        public BobMobWrapper(BobMob mob)
        {
            this.mob = mob;
        }

        BobMob.DragonState lastState;

        public void BossUpdate(int state)
        {
            if (mob)
            {
                mob.DragonUpdate((BobMob.DragonState)state);
            }
        }

        public Mob GetMob()
        {
            return mob;
        }

        public int GetID()
        {
            return mob.GetId();
        }
    }


    public class Bosses : MonoBehaviour
    {
        public static Bosses Instance { get; private set; }

        public enum LandingPlace
        {
            Port,
            Starbord,
            Behind,
            Mast,
        }

        public static int numLandingPlaces = 4;
        public static Vector3[] LandingPlaces = new Vector3[] { new Vector3(-15, 14, 0), new Vector3(15, 14, 0) };

        int[] landingPlaces = new int[numLandingPlaces];

        public int GetBossAtLandingPlace(LandingPlace place)
        {
            return landingPlaces[(int)place];
        }

        public bool IsLandingPlaceFree(LandingPlace place)
        {
            return landingPlaces[(int)place] == 0;
        }

        public void UseLandingPlace(int id, LandingPlace place)
        {
            landingPlaces[(int)place] = id;
        }
        public void UseLandingPlace(IBoss boss, LandingPlace place)
        {
            UseLandingPlace(boss.GetID(), place);
        }

        public void LeaveLandingPlace(IBoss boss)
        {
            LeaveLandingPlace(boss.GetID());
        }
        public void LeaveLandingPlace(int boss)
        {
            for (int i = 0; i < landingPlaces.Length; i++)
            {
                if (landingPlaces[i] == boss)
                {
                    landingPlaces[i] = 0;
                }
            }
        }
        
        public void BossDie(int id)
        {
            LeaveLandingPlace(id);
            bosses.Remove(id);
            bossesAlive--;
            if (bossesAlive == 0)
            {
                Debug.LogError("Game is over lol");
                if (LocalClient.serverOwner)
                {
                    GameManager.instance.GameOver(-3, 8f);
                    ServerSend.GameOver(-3);
                }
            }
        }

        public IBoss GetBoss(int id)
        {
            if (bosses.TryGetValue(id, out var b)) return b;
            return null;
        }

        int bossesAlive;

        Dictionary<int, IBoss> bosses;

        int bossesRecieved;

        int currentBoss;

        void Awake()
        {
            MusicController.Instance.FinalBoss();
        }

        static void Create(int data)
        {
            GameObject g = new GameObject("BossObject");
            Instance = g.AddComponent<Bosses>();

            Instance.bosses = new Dictionary<int, IBoss>();
            Instance.bossesAlive = data;

            Instance.bossesRecieved = 0;
        }

        static void RecieveBoss(int id)
        {
            Mob m = MobManager.Instance.mobs[id];
            if (m as BobMob != null)
            {
                Instance.bosses.Add(id, new BobMobWrapper((BobMob)m));
            }
            else
            {
                Instance.bosses.Add(id, (IBoss)m);
            }
            Instance.bossesRecieved++;
        }

        public static void SetBossesID(params int[] ids)
        {
            if (LocalClient.serverOwner && Instance == null)
            {
                Create(ids.Length);
                ServerSend.DragonUpdate(ids.Length);
                for (int i = 0; i < ids.Length; i++)
                {
                    RecieveBoss(ids[i]);
                    ServerSend.DragonUpdate(ids[i]);
                }
            }
        }

        public static void RecieveUpdate(int data)
        {
            if (Instance == null)
            {
                Create(data);
            }
            else if (Instance.bossesRecieved < Instance.bossesAlive)
            {
                RecieveBoss(data);
            }
            else if (Instance.currentBoss == -1 || !Instance.bosses.ContainsKey(Instance.currentBoss) || 
                     Instance.bosses[Instance.currentBoss] == null || !Instance.bosses[Instance.currentBoss].GetMob().isActiveAndEnabled)
            {
                Instance.currentBoss = data;
            }
            else
            {
                Instance.bosses[Instance.currentBoss].BossUpdate(data);
                Instance.currentBoss = -1;
            }
        }

        public static void SendStateChange(int id, int newState)
        {
            ServerSend.DragonUpdate(id);
            ServerSend.DragonUpdate(newState);
        }


        void OnDestroy()
        {
            Instance = null;
        }
    }

    public class JoeMob : GronkMob
    {

    }
}
