#region License (GPL v3)
/*
    Lighthouse - Adds searchlights and other features
    Copyright (c)2021 RFC1920 <desolationoutpostpve@gmail.com>

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
#endregion
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Lighthouse", "RFC1920", "1.0.2")]
    [Description("Some fixups to the lighthouse to make it more useful.")]
    class Lighthouse : RustPlugin
    {
        #region vars
        private Dictionary<string, LHInfo> lh = new Dictionary<string, LHInfo>();
        private List<uint> lhnets = new List<uint>();
        ConfigData configData;
        public static Lighthouse Instance = null;

        class LHInfo
        {
            public Vector3 location;
            public Quaternion rotation;
            public Vector3 recycler;
            public float rot;
        }
        #endregion

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        private void LMessage(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        #region main
        void OnServerInitialized()
        {
            LoadConfigVariables();
            Instance = this;
            FindLH();

            var lhobjects = UnityEngine.Object.FindObjectsOfType(typeof(LighthouseSearch));
            foreach(var lo in lhobjects)
            {
                UnityEngine.Object.Destroy(lo);
            }

            BuildSearchLights();
        }

        void Unload()
        {
            var lhobjects = UnityEngine.Object.FindObjectsOfType(typeof(LighthouseSearch));
            foreach(var lo in lhobjects)
            {
                UnityEngine.Object.Destroy(lo);
            }
            foreach(var l in lhnets)
            {
                var entity = BaseNetworkable.serverEntities.Find(l);
                if (entity != null)
                {
                    entity.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }
        }

        private void BuildSearchLights()
        {
            foreach(KeyValuePair<string, LHInfo> l in lh)
            {
                Vector3 searchloc1 = new Vector3(l.Value.location.x, l.Value.location.y + 56.8f, l.Value.location.z);
                Vector3 searchloc2 = new Vector3(l.Value.location.x, l.Value.location.y + 59.4f, l.Value.location.z);

                List<BaseEntity> sl = new List<BaseEntity>();
                Vis.Entities(searchloc1, 1f, sl);
                foreach(var be in sl)
                {
                    if (!be.IsDestroyed) be.Kill();
                }
                Vis.Entities(searchloc2, 1f, sl);
                foreach(var be in sl)
                {
                    if (!be.IsDestroyed) be.Kill();
                }

                var search1 = GameManager.server.CreateEntity("assets/prefabs/deployable/search light/searchlight.deployed.prefab", searchloc1, new Quaternion(), true);
                search1.SetFlag(BaseEntity.Flags.Reserved8, true);
                search1?.Spawn();
                search1.gameObject.AddComponent<LighthouseSearch>();
                lhnets.Add(search1.net.ID);

                var search2 = GameManager.server.CreateEntity("assets/prefabs/deployable/search light/searchlight.deployed.prefab", searchloc2, new Quaternion(0, 0, 1, 0), true);
                search2.SetFlag(BaseEntity.Flags.Reserved8, true);
                search2?.Spawn();
                var s2 = search2.gameObject.AddComponent<LighthouseSearch>();
                s2.SetTwo();
                lhnets.Add(search2.net.ID);
            }
        }

        private void FindLH()
        {
            bool ishapis =  ConVar.Server.level.Contains("Hapis");
            foreach (MonumentInfo mons in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                string name = null;
                if (!mons.name.Contains("ighthouse")) continue;
                if (ishapis)
                {
                    var elem = Regex.Matches(mons.name, @"\w{4,}|\d{1,}");
                    foreach (Match e in elem)
                    {
                        if (e.Value.Equals("MONUMENT")) continue;
                        if (e.Value.Contains("Label")) continue;
                        name += e.Value + " ";
                    }
                    name = name.Trim();
                }
                else
                {
                    name = Regex.Match(mons.name, @"\w{6}\/(.+\/)(.+)\.(.+)").Groups[2].Value.Replace("_", " ").Replace(" 1", "").Titleize();
                }

                Vector3 recycler = new Vector3();
                List<BaseEntity> ents = new List<BaseEntity>();
                Vis.Entities(mons.transform.position, 35, ents);
                foreach (BaseEntity entity in ents)
                {
                    if (entity.PrefabName.Contains("ecycler"))
                    {
                        Puts($"Found recycler at {entity.transform.position}");
                        recycler = entity.transform.position;
                    }
                }
                if(lh.ContainsKey(name))
                {
                    name += " 2";
                }
                lh.Add(name, new LHInfo()
                {
                    location = mons.transform.position,
                    rotation = mons.transform.rotation,
                    recycler = recycler,
                    rot = mons.gameObject.transform.transform.eulerAngles.y
                });
            }
        }

        class LighthouseSearch : MonoBehaviour
        {
            public SearchLight searchlight;
            public GameObject target;
            public BasePlayer player;
            public float degrees;

            void Awake()
            {
                searchlight = GetComponent<SearchLight>();
                target = new GameObject();
                degrees = Instance.configData.dps1;

                PlayerControllerSpawn();
                searchlight.PlayerEnter(player);
            }

            void PlayerControllerSpawn()
            {
                if (player == null)
                {
                    Vector3 spawnloc = new Vector3(searchlight.transform.position.x, searchlight.transform.position.y - 1.8f, searchlight.transform.position.z);
                    player = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", spawnloc, new Quaternion()).ToPlayer();
                    player.Spawn();
                    player.displayName = "SearchLight Driver";
                    AntiHack.ShouldIgnore(player);
                    player._limitedNetworking = true;
                    player.EnablePlayerCollider();
                    var connections = Net.sv.connections.Where(con => con.connected && con.isAuthenticated && con.player is BasePlayer && con.player != player).ToList();
                    player.OnNetworkSubscribersLeave(connections);
                }

                searchlight.PlayerEnter(player);
            }

            public void SetTwo()
            {
                degrees = Instance.configData.dps2;
            }

            void OnDestroy()
            {
                Destroy(target);
                player.Kill();
            }

            void Update()
            {
                target.transform.RotateAround(searchlight.transform.position, Vector3.up, degrees * Time.deltaTime);
                searchlight.SetTargetAimpoint(target.transform.position);
            }
        }
        #endregion

        #region config
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData
            {
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
        public class ConfigData
        {
            [JsonProperty(PropertyName = "Lower SearchLight Degrees per Second")]
            public float dps1 = 10f;

            [JsonProperty(PropertyName = "Upper SearchLight Degrees per Second")]
            public float dps2 = 30f;

            public VersionNumber Version { get; internal set; }
        }
        #endregion
    }
}
