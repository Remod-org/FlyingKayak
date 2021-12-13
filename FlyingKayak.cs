#region License (GPL v3)
/*
    DESCRIPTION
    Copyright (c) RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License Information (GPL v3)
using Oxide.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rust;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using System.Globalization;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("FlyingKayak", "RFC1920", "1.0.1")]
    [Description("Flying kayak, because sometimes these things are necessary.")]
    internal class FlyingKayak : RustPlugin
    {
        #region vars
        private ConfigData configData;
        private static LayerMask layerMask;
        private static LayerMask buildingMask;

        private static Dictionary<ulong, PlayerKayakData> loadplayer = new Dictionary<ulong, PlayerKayakData>();
        private static List<ulong> pilotslist = new List<ulong>();

        public List<string> monNames = new List<string>();
        public SortedDictionary<string, Vector3> monPos  = new SortedDictionary<string, Vector3>();
        public SortedDictionary<string, Vector3> monSize = new SortedDictionary<string, Vector3>();

        public static FlyingKayak Instance;

        [PluginReference]
        private readonly Plugin SignArtist, GridAPI;

        public class PlayerKayakData
        {
            public BasePlayer player;
            public int kayakcount;
        }

        private void Init()
        {
            Instance = this;

            LoadConfigVariables();
            layerMask = (1 << 29);
            layerMask |= (1 << 18);
            layerMask = ~layerMask;
            //buildingMask = LayerMask.GetMask("Construction", "Tree", "Rock", "Deployed", "World");
            buildingMask = LayerMask.GetMask("Construction", "Prevent Building", "Deployed", "World", "Terrain", "Tree", "Invisible", "Default");

            AddCovalenceCommand("fk", "cmdKayakBuild");
            AddCovalenceCommand("fkc", "cmdKayakCount");
            AddCovalenceCommand("fkd", "cmdKayakDestroy");
            AddCovalenceCommand("fkg", "cmdKayakGiveChat");
            AddCovalenceCommand("fkhelp", "cmdKayakHelp");

            permission.RegisterPermission("flyingkayak.use", this);
            permission.RegisterPermission("flyingkayak.vip", this);
            permission.RegisterPermission("flyingkayak.admin", this);
            permission.RegisterPermission("flyingkayak.unlimited", this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["helptext1"] = "Flying Kayak instructions:",
                ["helptext2"] = "  type /fk to spawn a Flying Kayak",
                ["helptext3"] = "  type /fkd to destroy your flyingkayak.",
                ["helptext4"] = "  type /fkc to show a count of your kayaks",
                ["notunnel"] = "Access to spawn in tunnels has been blocked !!",
                ["notauthorized"] = "You don't have permission to do that !!",
                ["notfound"] = "Could not locate a kayak.  You must be within {0} meters for this!!",
                ["notflyingkayak"] = "You are not piloting a flying kayak !!",
                ["maxkayaks"] = "You have reached the maximum allowed kayaks",
                ["landingkayak"] = "Kayak landing sequence started !!",
                ["risingkayak"] = "Kayak takeoff sequence started !!",
                ["kayakdestroyed"] = "Flying Kayak destroyed !!",
                ["nostartseat"] = "You cannot light this lantern until seated !!",
                ["noplayer"] = "Unable to find player {0}!",
                ["gaveplayer"] = "Gave kayak to player {0}!",
                ["flyingkayak"] = "Flying Kayak",
                ["menu"] = "Press RELOAD for menu",
                ["close"] = "Close",
                ["cancel"] = "Cancel",
                ["heading"] = "Headed to {0}",
                ["arrived"] = "Arrived at {0}",
                ["nokayaks"] = "You have no Kayaks",
                ["currkayaks"] = "Current Kayaks : {0}",
                ["giveusage"] = "You need to supply a valid SteamId."
            }, this);
        }

        private bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        private bool HasPermission(ConsoleSystem.Arg arg, string permname)
        {
            BasePlayer pl = arg.Connection.player as BasePlayer;
            if (pl == null)
            {
                return true;
            }
            return permission.UserHasPermission(pl.UserIDString, permname);
        }

        private static HashSet<BasePlayer> FindPlayers(string nameOrIdOrIp)
        {
            HashSet<BasePlayer> players = new HashSet<BasePlayer>();
            if (string.IsNullOrEmpty(nameOrIdOrIp)) return players;
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString.Equals(nameOrIdOrIp))
                {
                    players.Add(activePlayer);
                }
                else if (!string.IsNullOrEmpty(activePlayer.displayName) && activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.IgnoreCase))
                {
                    players.Add(activePlayer);
                }
                else if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Equals(nameOrIdOrIp))
                {
                    players.Add(activePlayer);
                }
            }
            foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString.Equals(nameOrIdOrIp))
                {
                    players.Add(sleepingPlayer);
                }
                else if (!string.IsNullOrEmpty(sleepingPlayer.displayName) && sleepingPlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.IgnoreCase))
                {
                    players.Add(sleepingPlayer);
                }
            }
            return players;
        }
        #endregion

        #region Configuration
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new ConfigData
            {
                UseMaxKayakChecks = true,
                debug = false,
                MaxKayaks = 1,
                VIPMaxKayaks = 2,
                MinDistance = 10,
                MinAltitude = 5,
                CruiseAltitude = 35,
                NormalSpeed = 12,
                SprintSpeed = 25,
                Version = Version
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            configData.Version = Version;
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        private class ConfigData
        {
            //public bool AllowDamage;
            public bool BlockInTunnel;
            public bool UseMaxKayakChecks;
            public bool debug;
            public bool debugMovement;

            public int MaxKayaks;
            public int VIPMaxKayaks;
            //public float InitialHealth;
            public float MinDistance;
            public float MinAltitude;
            public float CruiseAltitude;
            public float NormalSpeed;
            public float SprintSpeed;

            public VersionNumber Version;
        }
        #endregion

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        #region Chat Commands
        [Command("fk"), Permission("flyingkayak.use")]
        private void cmdKayakBuild(IPlayer iplayer, string command, string[] args)
        {
            bool vip = false;
            BasePlayer player = iplayer.Object as BasePlayer;
            if (!iplayer.HasPermission("flyingkayak.use")) { Message(iplayer, "notauthorized"); return; }

            if (iplayer.HasPermission("flyingkayak.vip"))
            {
                vip = true;
            }
            if (KayakLimitReached(player, vip)) { Message(iplayer, "maxkayaks"); return; }
            if (configData.BlockInTunnel && player.transform.position.y < -70)
            {
                Message(iplayer, "notunnel");
                return;
            }
            AddKayak(player, player.transform.position);
        }

        [Command("fkg"), Permission("flyingkayak.admin")]
        private void cmdKayakGiveChat(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (args.Length == 0)
            {
                Message(iplayer, "giveusage");
                return;
            }
            bool vip = false;
            string pname = args[0];

            if (!iplayer.HasPermission("flyingkayak.admin")) { Message(iplayer, "notauthorized"); return; }
            if (pname == null) { Message(iplayer, "noplayer", "NAME_OR_ID"); return; }

            BasePlayer Bplayer = BasePlayer.Find(pname);
            if (Bplayer == null)
            {
                Message(iplayer, "noplayer", pname);
                return;
            }

            IPlayer Iplayer = Bplayer.IPlayer;
            if (Iplayer.HasPermission("flyingkayak.vip"))
            {
                vip = true;
            }
            if (KayakLimitReached(Bplayer, vip)) { Message(iplayer, "maxkayaks"); return; }
            AddKayak(Bplayer, Bplayer.transform.position);
            Message(iplayer, "gaveplayer", pname);
        }

        [ConsoleCommand("fkgive")]
        private void cmdKayakGive(ConsoleSystem.Arg arg)
        {
            if (arg.IsRcon)
            {
                if (arg.Args == null)
                {
                    Puts("You need to supply a valid SteamId.");
                    return;
                }
            }
            else if (!HasPermission(arg, "flyingkayak.admin"))
            {
                SendReply(arg, Lang("notauthorized", null, arg.Connection.player as BasePlayer));
                return;
            }
            else if (arg.Args == null)
            {
                SendReply(arg, Lang("giveusage", null, arg.Connection.player as BasePlayer));
                return;
            }

            bool vip = false;
            string pname = arg.GetString(0);

            if (pname.Length < 1) { Puts("Player name or id cannot be null"); return; }

            BasePlayer Bplayer = BasePlayer.Find(pname);
            if (Bplayer == null) { Puts($"Unable to find player '{pname}'"); return; }

            IPlayer Iplayer = Bplayer.IPlayer;
            if (Iplayer.HasPermission("flyingkayak.vip")) { vip = true; }
            if (KayakLimitReached(Bplayer, vip))
            {
                Puts($"Player '{pname}' has reached maxkayaks"); return;
            }
            AddKayak(Bplayer, Bplayer.transform.position);
            Puts($"Gave kayak to '{Bplayer.displayName}'");
        }

        [Command("fkc"), Permission("flyingkayak.use")]
        private void cmdKayakCount(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (!iplayer.HasPermission("flyingkayak.use")) { Message(iplayer, "notauthorized"); return; }
            if (!loadplayer.ContainsKey(player.userID))
            {
                Message(iplayer, "nokayaks");
                return;
            }
            string ccount = loadplayer[player.userID].kayakcount.ToString();
            DoLog("KayakCount: " + ccount);
            Message(iplayer, "currkayaks", ccount);
        }

        [Command("fkd"), Permission("flyingkayak.use")]
        private void cmdKayakDestroy(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (!iplayer.HasPermission("flyingkayak.use")) { Message(iplayer, "notauthorized"); return; }

            string target = null;
            if (args.Length > 0)
            {
                target = args[0];
            }
            if (iplayer.HasPermission("flyingkayak.admin") && target != null)
            {
                if (target == "all")
                {
                    DestroyAllKayaks(player);
                    return;
                }
                HashSet<BasePlayer> players = FindPlayers(target);
                if (players.Count == 0)
                {
                    Message(iplayer, "PlayerNotFound", target);
                    return;
                }
                if (players.Count > 1)
                {
                    Message(iplayer, "MultiplePlayers", target, string.Join(", ", players.Select(p => p.displayName).ToArray()));
                    return;
                }
                BasePlayer targetPlayer = players.First();
                RemoveKayak(targetPlayer);
                DestroyRemoteKayak(targetPlayer);
            }
            else
            {
                RemoveKayak(player);
                DestroyLocalKayak(player);
            }
        }

        [Command("fkhelp"), Permission("flyingkayak.use")]
        private void cmdKayakHelp(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (!iplayer.HasPermission("flyingkayak.use")) { Message(iplayer, "notauthorized"); return; }
            Message(iplayer, "helptext1");
            Message(iplayer, "helptext2");
            Message(iplayer, "helptext3");
            Message(iplayer, "helptext4");
        }
        #endregion

        #region Primary
        private void DoLog(string message, bool ismovement = false)
        {
            if (ismovement && !configData.debugMovement) return;
            if (configData.debugMovement || configData.debug) Interface.Oxide.LogInfo(message);
        }

        private void AddKayak(BasePlayer player, Vector3 location)
        {
            if (player == null && location == default(Vector3)) return;
            Vector3 spawnpos = player.transform.position + (player.transform.forward * 4f) + new Vector3(0, 1f, 0);

            BaseEntity newKayak = GameManager.server.CreateEntity("assets/content/vehicles/boats/kayak/kayak.prefab", spawnpos, new Quaternion(), true);
            newKayak.name = "FlyingKayak";
            BaseMountable chairmount = newKayak.GetComponent<BaseMountable>();
            chairmount.isMobile = true;
            newKayak.enableSaving = false;
            newKayak.OwnerID = player.userID;
            newKayak.Spawn();
            KayakEntity kayak = newKayak.gameObject.AddComponent<KayakEntity>();

            AddPlayerID(player.userID);
        }

        public bool PilotListContainsPlayer(BasePlayer player)
        {
            return pilotslist.Contains(player.userID);
        }

        private void AddPlayerToPilotsList(BasePlayer player)
        {
            if (PilotListContainsPlayer(player)) return;
            pilotslist.Add(player.userID);
        }

        public void RemovePlayerFromPilotsList(BasePlayer player)
        {
            if (PilotListContainsPlayer(player))
            {
                pilotslist.Remove(player.userID);
            }
        }

        private void DestroyLocalKayak(BasePlayer player)
        {
            if (player == null) return;
            List<BaseEntity> kayaklist = new List<BaseEntity>();
            Vis.Entities(player.transform.position, configData.MinDistance, kayaklist);
            bool foundkayak = false;

            foreach (BaseEntity p in kayaklist)
            {
                KayakEntity foundent = p.GetComponentInParent<KayakEntity>();
                if (foundent != null)
                {
                    foundkayak = true;
                    if (foundent.ownerid != player.userID) return;
                    foundent.entity.Kill(BaseNetworkable.DestroyMode.Gib);
                    Message(player.IPlayer, "kayakdestroyed");
                }
            }
            if (!foundkayak)
            {
                Message(player.IPlayer, "notfound", configData.MinDistance.ToString());
            }
        }

        private void DestroyAllKayaks(BasePlayer player)
        {
            List<BaseEntity> kayaklist = new List<BaseEntity>();
            Vis.Entities(Vector3.zero, 3500f, kayaklist);
            bool foundkayak = false;

            foreach (BaseEntity p in kayaklist)
            {
                KayakEntity foundent = p.GetComponentInParent<KayakEntity>();
                if (foundent != null)
                {
                    foundkayak = true;
                    foundent.entity.Kill(BaseNetworkable.DestroyMode.Gib);
                    Message(player.IPlayer, "kayakdestroyed");
                }
            }
            if (!foundkayak)
            {
                Message(player.IPlayer, "notfound", configData.MinDistance.ToString());
            }
        }

        private void DestroyRemoteKayak(BasePlayer player)
        {
            if (player == null) return;
            List<BaseEntity> kayaklist = new List<BaseEntity>();
            Vis.Entities(Vector3.zero, 3500f, kayaklist);
            bool foundkayak = false;

            foreach (BaseEntity p in kayaklist)
            {
                KayakEntity foundent = p.GetComponentInParent<KayakEntity>();
                if (foundent != null)
                {
                    foundkayak = true;
                    if (foundent.ownerid != player.userID) return;
                    foundent.entity.Kill(BaseNetworkable.DestroyMode.Gib);
                    Message(player.IPlayer, "kayakdestroyed");
                }
            }
            if (!foundkayak)
            {
                Message(player.IPlayer, "notfound", configData.MinDistance.ToString());
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            if (!player.isMounted) return;

            KayakEntity activekayak = player.GetMountedVehicle()?.GetComponentInParent<KayakEntity>();
            if (activekayak == null) return;
            if (player.GetMountedVehicle() != activekayak.bv) return;
            if (input != null)
            {
                activekayak.KayakInput(input, player);
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;
            KayakEntity iskayak = entity.GetComponentInParent<KayakEntity>();
            if (iskayak != null) hitInfo.damageTypes.ScaleAll(0);
        }

        private object OnEntityGroundMissing(BaseEntity entity)
        {
            KayakEntity iskayak = entity.GetComponentInParent<KayakEntity>();
            if (iskayak != null) return false;
            return null;
        }

        private bool KayakLimitReached(BasePlayer player, bool vip=false)
        {
            if (configData.UseMaxKayakChecks)
            {
                if (loadplayer.ContainsKey(player.userID))
                {
                    int currentcount = loadplayer[player.userID].kayakcount;
                    int maxallowed = configData.MaxKayaks;
                    if (vip)
                    {
                        maxallowed = configData.VIPMaxKayaks;
                    }
                    if (currentcount >= maxallowed) return true;
                }
            }
            return false;
        }

        private object CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            if (player == null) return null;

            KayakEntity activekayak = player.GetMountedVehicle()?.GetComponentInParent<KayakEntity>();
            if (activekayak == null) return null;
            if (player.GetMountedVehicle() != activekayak.bv) return null;

            if (activekayak.entitypos.y - TerrainMeta.HeightMap.GetHeight(activekayak.entitypos) > configData.MinAltitude + 3)
            //if (activekayak.entitypos.y > configData.MinAltitude + 2f)
            {
                DoLog($"Above MinAltitude.  Too high {activekayak.entitypos.y.ToString()} to exit vehicle...");
                return true;
            }

            //if (PilotListContainsPlayer(player))
            //{
            //    return true;
            //}
            return null;
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            KayakEntity activekayak = mountable.GetComponentInParent<KayakEntity>();
            if (activekayak != null)
            {
                DoLog("OnEntityMounted: player mounted kayak!");
                if (activekayak.bv.GetDriver() == player)
                {
                    DoLog($"OnEntityMounted: {player.displayName} mounted as driver.  Setting takeoff true");
                    activekayak.takeoff = true;
                }
            }
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            KayakEntity activekayak = mountable.GetComponentInParent<KayakEntity>();
            if (activekayak != null)
            {
                DoLog("OnEntityMounted: player dismounted kayak!");
                if (mountable.GetComponent<BaseEntity>() != activekayak.entity) return;
            }
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container == null || player == null) return null;
            KayakEntity iskayak = container.GetComponentInParent<KayakEntity>();

            return null;
        }

        private object CanPickupEntity(BasePlayer player, BaseCombatEntity entity)
        {
            if (entity == null || player == null) return null;

            BaseEntity myent = entity as BaseEntity;
            string myparent = null;
            try
            {
                myparent = myent.GetParentEntity().name;
            }
            catch {}

            if (myparent == "FlyingKayak" || myent.name == "FlyingKayak")
            {
                if (configData.debug)
                {
                    if (myent.name == "FlyingKayak")
                    {
                        Puts("CanPickupEntity: player trying to pickup the kayak!");
                    }
                    else if (myparent == "FlyingKayak")
                    {
                        string entity_name = myent.LookupPrefab().name;
                        Puts($"CanPickupEntity: player trying to remove {entity_name} from a kayak!");
                    }
                }
                Message(player.IPlayer, "notauthorized");
                return false;
            }
            return null;
        }

        private void AddPlayerID(ulong ownerid)
        {
            if (!loadplayer.ContainsKey(ownerid))
            {
                loadplayer.Add(ownerid, new PlayerKayakData
                {
                    kayakcount = 1,
                });
                return;
            }
            loadplayer[ownerid].kayakcount++;
        }

        private void RemovePlayerID(ulong ownerid)
        {
            if (loadplayer.ContainsKey(ownerid)) loadplayer[ownerid].kayakcount--;
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            RemovePlayerFromPilotsList(player);
            return null;
        }

        private void RemoveKayak(BasePlayer player)
        {
            RemovePlayerFromPilotsList(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RemoveKayak(player);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            RemoveKayak(player);
        }

        private void DestroyAll<T>()
        {
            UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsOfType(typeof(T));
            if (objects != null)
            {
                foreach (UnityEngine.Object gameObj in objects)
                {
                    UnityEngine.Object.Destroy(gameObj);
                }
            }
        }

        private void Unload()
        {
            DestroyAll<KayakEntity>();
        }
        #endregion

        #region Kayak Antihack check
        private static List<BasePlayer> kayakantihack = new List<BasePlayer>();

        private object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (player == null) return null;
            if (kayakantihack.Contains(player)) return false;
            return null;
        }
        #endregion

        #region Kayak Entity
        private class KayakEntity : MonoBehaviour
        {
            public BaseEntity entity;
            public BaseVehicle bv;
            public Rigidbody rb;
            public BasePlayer player;
            public string entname = "FlyingKayak";

            public Quaternion entityrot;
            public Vector3 entitypos;

            public bool moveforward;
            public bool movebackward;
            public bool moveup;
            public bool movedown;
            public bool rotright;
            public bool rotleft;
            public bool sprinting;
            public bool islanding;
            public bool takeoff;
            public bool takeoffdone;
            public bool mounted;

            public bool engineon;

            public ulong skinid = 1;
            public ulong ownerid;
            private float minaltitude;
            private float cruisealtitude;
            private FlyingKayak instance;
            public bool throttleup;
            public bool showmenu;
            private bool zmTrigger;
            private float sprintspeed;
            private float normalspeed;
            private SphereCollider sphereCollider;

            private void Awake()
            {
                entity = GetComponentInParent<BaseEntity>();
                bv = entity as BaseVehicle;
                rb = entity.GetComponent<Rigidbody>();
                entityrot = Quaternion.identity;
                minaltitude = Instance.configData.MinAltitude;
                cruisealtitude = Instance.configData.CruiseAltitude;
                instance = new FlyingKayak();
                ownerid = entity.OwnerID;
                gameObject.name = "FlyingKayak";

                engineon = false;
                moveforward = false;
                movebackward = false;
                moveup = false;
                movedown = false;
                rotright = false;
                rotleft = false;
                sprinting = false;
                islanding = false;
                mounted = false;
                throttleup = false;

                sprintspeed = Instance.configData.SprintSpeed;
                normalspeed = Instance.configData.NormalSpeed;

                sphereCollider = entity.gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = (int)Layer.Reserved1;
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 6f;
            }

            private void OnTriggerEnter(Collider col)
            {
                if (col.gameObject.name == "ZoneManager")
                {
                    Instance.DoLog($"Trigger Enter: {col.gameObject.name}");
                    zmTrigger = true;
                }
                else if (col.GetComponentInParent<BasePlayer>() != null)
                {
                    kayakantihack.Add(col.GetComponentInParent<BasePlayer>());
                }
            }

            private void OnTriggerExit(Collider col)
            {
                if (col.gameObject.name == "ZoneManager")
                {
                    Instance.DoLog($"Trigger Exit: {col.gameObject.name}");
                    zmTrigger = false;
                }
                else if (col.GetComponentInParent<BasePlayer>() != null)
                {
                    kayakantihack.Remove(col.GetComponentInParent<BasePlayer>());
                }
            }

            private BasePlayer GetPilot()
            {
                player = bv.GetDriver();
                return player;
            }

            public void KayakInput(InputState input, BasePlayer player)
            {
                if (input == null) return;
                if (player == null)
                {
                    player = this.player;
                }

                if (input.WasJustPressed(BUTTON.FORWARD)) moveforward = true;
                if (input.WasJustReleased(BUTTON.FORWARD)) moveforward = false;
                if (input.WasJustPressed(BUTTON.BACKWARD)) movebackward = true;
                if (input.WasJustReleased(BUTTON.BACKWARD)) movebackward = false;
                if (input.WasJustPressed(BUTTON.RIGHT)) rotright = true;
                if (input.WasJustReleased(BUTTON.RIGHT)) rotright = false;
                if (input.WasJustPressed(BUTTON.LEFT)) rotleft = true;
                if (input.WasJustReleased(BUTTON.LEFT)) rotleft = false;
                if (input.IsDown(BUTTON.SPRINT)) throttleup = true;
                if (input.WasJustReleased(BUTTON.SPRINT)) throttleup = false;
                if (input.WasJustPressed(BUTTON.JUMP)) moveup = true;
                if (input.WasJustReleased(BUTTON.JUMP)) moveup = false;
                if (input.WasJustPressed(BUTTON.DUCK)) movedown = true;
                if (input.WasJustReleased(BUTTON.DUCK)) movedown = false;
                if (input.WasJustPressed(BUTTON.RELOAD)) showmenu = true;
                if (input.WasJustReleased(BUTTON.RELOAD)) showmenu = false;
            }

            private void CancelTakeOff()
            {
                takeoff = false;
            }

            private void Update()
            {
                entitypos = entity.transform.position;
                if (GetPilot() != null)
                {
                    rb.isKinematic = true;
                    if (takeoff) Invoke("CancelTakeOff", 2);
                }
                else
                {
                    rb.isKinematic = false;
                    islanding = true;
                }

                float currentspeed = normalspeed;
                if (throttleup) { currentspeed = sprintspeed; }
                RaycastHit hit;

                if (rb.isKinematic)
                {
                    // Keep above terrain
                    if (entitypos.y < TerrainMeta.HeightMap.GetHeight(entitypos))
                    {
                        entity.transform.localPosition += transform.up * TerrainMeta.HeightMap.GetHeight(entitypos) * Time.deltaTime * 4;
                        ServerMgr.Instance.StartCoroutine(RefreshTrain());
                        return;
                    }

                    if (Physics.Raycast(entitypos, entity.transform.TransformDirection(Vector3.down), out hit, minaltitude + 7, layerMask) && takeoff)// && !zmTrigger)
                    {
                        // takeoff
                        entity.transform.localPosition += transform.up * (minaltitude + 7) * Time.deltaTime * 2;
                        ServerMgr.Instance.StartCoroutine(RefreshTrain());
                    }
                    else if (Physics.Raycast(entitypos, entity.transform.TransformDirection(Vector3.down), out hit, minaltitude, layerMask) && !zmTrigger)
                    {
                        // Maintain minimum height
                        entity.transform.localPosition += transform.up * minaltitude * Time.deltaTime * 2;
                        ServerMgr.Instance.StartCoroutine(RefreshTrain());
                        return;
                    }
                    // Disallow flying forward into buildings, etc.
                    if (Physics.Raycast(entitypos, entity.transform.TransformDirection(Vector3.forward), out hit, 10f, buildingMask))
                    {
                        if (!zmTrigger)
                        {
                            entity.transform.localPosition += transform.forward * -5f * Time.deltaTime;
                            moveforward = false;

                            string d = Math.Round(hit.distance, 2).ToString();
                            Instance.DoLog($"FRONTAL CRASH (distance {d}m)!", true);
                        }
                    }
                    // Disallow flying backward into buildings, etc.
                    else if (Physics.Raycast(new Ray(entitypos, Vector3.forward * -1f), out hit, 10f, buildingMask))
                    {
                        if (!zmTrigger)
                        {
                            entity.transform.localPosition += transform.forward * 5f * Time.deltaTime;
                            movebackward = false;
                        }
                    }

                    float rotspeed = 0.1f;
                    if (throttleup) rotspeed += 0.25f;
                    if (rotright) entity.transform.eulerAngles += new Vector3(0, rotspeed, 0);
                    else if (rotleft) entity.transform.eulerAngles += new Vector3(0, -rotspeed, 0);

                    if (moveforward) entity.transform.localPosition += transform.forward * currentspeed * Time.deltaTime;
                    else if (movebackward) entity.transform.localPosition -= transform.forward * currentspeed * Time.deltaTime;

                    if (moveup) entity.transform.localPosition += transform.up * currentspeed * Time.deltaTime;
                    else if (movedown) entity.transform.localPosition += transform.up * -currentspeed * Time.deltaTime;

                    ServerMgr.Instance.StartCoroutine(RefreshTrain());
                }
            }

            private IEnumerator RefreshTrain()
            {
                // Fix rotation along z (?)
                Quaternion q = Quaternion.FromToRotation(entity.transform.up, Vector3.up) * entity.transform.rotation;
                entity.transform.rotation = Quaternion.Slerp(entity.transform.rotation, q, Time.deltaTime * 3.5f);

                entity.transform.hasChanged = true;
                for (int i = 0; i < entity.children.Count; i++)
                {
                    entity.children[i].transform.hasChanged = true;
                    entity.children[i].SendNetworkUpdateImmediate();
                    entity.children[i].UpdateNetworkGroup();
                }
                entity.SendNetworkUpdateImmediate();
                entity.UpdateNetworkGroup();
                yield return new WaitForEndOfFrame();
            }

            private void ResetMovement()
            {
                moveforward = false;
                movebackward = false;
                moveup = false;
                movedown = false;
                rotright = false;
                rotleft = false;
                throttleup = false;
            }

            public void OnDestroy()
            {
                if (loadplayer.ContainsKey(ownerid)) loadplayer[ownerid].kayakcount--;
                entity?.Invoke("KillMessage", 0.1f);
            }
        }
        #endregion
    }
}
