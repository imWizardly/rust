using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    // Credit to Orange for advising to use Reserved8 flag
    [Info( "Powerless Turrets", "August", "3.2.7" )]
    public class PowerlessTurrets : RustPlugin
    {
        #region Fields
        
        private const string PermUse = "powerlessturrets.use";
        private const string PermUseRadius = "powerlessturrets.radius";
        private const string PermUseSamRadius = "powerlessturrets.samradius";
        
        private static PowerlessTurrets _instance;
        private TurretManager _turretManager;
        private PluginConfig _config;
        private PluginData _stored;

        #endregion

        #region PluginConfig

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }
                
                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning($"PluginConfig file {Name}.json updated.");
                    
                    SaveConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();

                PrintError("Config file contains an error and has been replaced with the default file.");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private class PluginConfig
        {
            [JsonProperty( PropertyName = "Range at which turrets can be toggled", Order = 0 )]
            public float Range { get; set; }
            
            [JsonProperty( PropertyName = "Command for toggling individual turrets", Order = 1 )]
            public string ToggleCommand { get; set; }
            
            [JsonProperty( PropertyName = "Command for toggling turrets in TC zone", Order = 2 )]
            public string ToggleTcCommand { get; set; }
            
            [JsonProperty( PropertyName = "Command for toggling sams in TC zone", Order = 3 )]
            public string ToggleSamTcCommand { get; set; }
            
            public string ToJson() => JsonConvert.SerializeObject(this);
            
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }
        
        private PluginConfig GetDefaultConfig()
        {
            
            return new PluginConfig
            {
                Range = 10f,
                ToggleCommand = "turret",
                ToggleTcCommand = "turret.tc",
                ToggleSamTcCommand = "sam.tc"
            };
        }
        
        #endregion

        #region PluginData

        private class PluginData
        {
            public List<uint> AutoTurrets = new List<uint>();
            public List<uint> SamSites    = new List<uint>();
        }

        private void SaveData()
        {
            _stored.AutoTurrets = _turretManager.OnlineTurrets;
            _stored.SamSites    = _turretManager.OnlineSams;
            
            Interface.Oxide.DataFileSystem.WriteObject(Name, _stored);
        }

        #endregion

        #region Localization
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages( new Dictionary<string, string>
            {
                { "TurretsToggled", "You have toggled {0} turrets." },
                { "NoPermission", "You do not have permission to use this command." },
                { "InvalidEntity", "This is not a valid entity." },
                { "NoTurretsFound", "There are no valid turrets in your area." },
                { "NoPermThisTurret", "You do not have permission to toggle this turret." },
                { "BuildingBlocked", "You do not have building privilege." },
                { "Syntax", "Invalid syntax." }
            }, this );
        }

        private string Lang( string key, string id = null, params object[] args ) => string.Format( lang.GetMessage( key, this, id ), args );
        
        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            foreach (var ent in BaseNetworkable.serverEntities)
            {
                if (ent is AutoTurret)
                {
                    if (_stored.AutoTurrets.Contains(ent.net.ID))
                    {
                        _turretManager.PowerTurretOn(ent as AutoTurret);
                    }
                }
                
                if (ent is SamSite)
                {
                    if (_stored.SamSites.Contains(ent.net.ID))
                    {
                        _turretManager.PowerSamsiteOn(ent as SamSite);
                    }
                }
            }
        }
        
        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermUseRadius, this);
            permission.RegisterPermission(PermUseSamRadius, this);
            
            cmd.AddChatCommand(_config.ToggleCommand, this, nameof(TurretCommand));
            cmd.AddChatCommand(_config.ToggleTcCommand, this, nameof(ToggleTurretsInTc));
            cmd.AddChatCommand(_config.ToggleSamTcCommand, this, nameof(ToggleSamsInTc));
            
            _instance = this;
            _stored = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            _turretManager = new TurretManager();
        }

        private void Unload()
        {
            _instance = null;
        }
        
        private void OnServerSave() => SaveData();
        
        #endregion

        #region Commands
        
        private void TurretCommand(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }

            RaycastHit raycastHit;
            if (!Physics.Raycast(player.eyes.position, player.eyes.rotation * Vector3.forward, out raycastHit, _config.Range))
            {
                return;
            }

            BaseEntity entity = raycastHit.GetEntity();
            if (entity is AutoTurret)
            {
                _turretManager.ToggleTurret(entity as AutoTurret, player);
                return;
            }
            
            if (entity is SamSite)
            {
                _turretManager.ToggleSamsite(entity as SamSite, player);
                return;
            }
            
            player.ChatMessage(Lang("InvalidEntity", player.UserIDString));
        }

        private void ToggleTurretsInTc(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUseRadius))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }
            
            if (args.Length != 1)
            {
                player.ChatMessage(Lang("Syntax", player.UserIDString));
                return;
            }
            
            string arg0 = args[0].ToLower();
            
            if (!(arg0 == "on" || arg0.ToLower() == "off"))
            {
                player.ChatMessage(Lang( "Syntax", player.UserIDString));
                return;
            }
            
            _turretManager.ToggleTurretsInTcRange(player, arg0);
        }
        
        private void ToggleSamsInTc(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUseRadius))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length != 1)
            {
                player.ChatMessage(Lang("Syntax", player.UserIDString));
                return;
            }
            
            string arg0 = args[0].ToLower();
            
            if (!( arg0 == "on" || arg0.ToLower() == "off"))
            {
                player.ChatMessage(Lang("Syntax", player.UserIDString));
                return;
            }
            
            _turretManager.ToggleSamsInTcRange(player, arg0);
        }
        
        #endregion

        #region Turret Manager
        
        private class TurretManager
        {
            #region Auto Turrets
            
            private readonly List<AutoTurret> _onlineTurrets = new List<AutoTurret>();
            
            public List<uint> OnlineTurrets 
            { 
                get 
                {
                    List<uint> ids = new List<uint>();
                    
                    foreach (AutoTurret turret in _onlineTurrets)
                    {
                        if (turret.net != null)
                        {
                            ids.Add(turret.net.ID);
                        }
                    }
                    
                    return ids;
                } 
                
                private set { }
            }

            public void ToggleTurret(AutoTurret turret, BasePlayer player = null)
            {
                if (turret == null)
                {
                    return;
                }
                
                if (player != null && (!turret.IsAuthed(player) || turret.GetBuildingPrivilege()?.IsAuthed(player) == false))
                {
                    player.ChatMessage(_instance.Lang("NoPermThisTurret", player.UserIDString));
                    return;
                }

                if (turret.IsOnline())
                {
                    PowerTurretOff(turret);
                }
                else
                {
                    PowerTurretOn(turret);
                }
            }
            
            public void PowerTurretOn(AutoTurret turret)
            {
                turret.SetFlag(BaseEntity.Flags.Reserved8, true);
                turret.InitiateStartup();
                turret.UpdateFromInput(11, 0);
                
                if (!_onlineTurrets.Contains(turret))
                {
                    _onlineTurrets.Add(turret);
                }
            }
            
            private void PowerTurretOff(AutoTurret turret)
            {
                turret.SetFlag(BaseEntity.Flags.Reserved8, false);
                turret.InitiateShutdown();
                turret.UpdateFromInput(0, 0);
                
                if (_onlineTurrets.Contains(turret))
                {
                    _onlineTurrets.Remove(turret);
                }
            }
            
            public void ToggleTurretsInTcRange(BasePlayer player, string arg)
            {
                List<AutoTurret> turretList = Pool.GetList<AutoTurret>();
                
                foreach (AutoTurret turret in BaseNetworkable.serverEntities.OfType<AutoTurret>())
                {
                    if (turret.GetBuildingPrivilege()?.IsAuthed(player) == true && turret.GetBuildingPrivilege() == player.GetBuildingPrivilege() == true)
                    {
                        turretList.Add(turret);
                    }
                }
                
                if (turretList.Count < 1)
                {
                    return;
                }
                
                foreach (AutoTurret turret in turretList)
                {
                    if (arg == "on")
                    {
                        PowerTurretOn(turret);
                    }
                    else
                    {
                        PowerTurretOff(turret);
                    }
                }
                
                player.ChatMessage(_instance.Lang("TurretsToggled", player.UserIDString, turretList.Count));
                
                Pool.FreeList(ref turretList);
            }
            
            #endregion

            #region Sam Turrets
            
            private readonly List<SamSite> _onlineSams = new List<SamSite>();
            
            public List<uint> OnlineSams
            {
                get
                {
                    List<uint> ids = new List<uint>();
                    
                    foreach (SamSite sam in _onlineSams)
                    {
                        if (sam.net != null)
                        {
                            ids.Add(sam.net.ID);
                        }
                    }
                    
                    return ids;
                }
                
                private set { }
            }

            public void ToggleSamsite(SamSite samSite, BasePlayer player = null)
            {
                if (samSite == null)
                {
                    return;
                }
                
                if (player != null && samSite.GetBuildingPrivilege()?.IsAuthed(player) == false)
                {
                    player.ChatMessage(_instance.Lang("NoPermThisTurret", player.UserIDString));
                    return;
                }

                if (samSite.IsPowered())
                {
                    PowerSamsiteOff(samSite);
                }
                else
                {
                    PowerSamsiteOn(samSite);
                }
                
                samSite.SendNetworkUpdate();
            }

            public void PowerSamsiteOn(SamSite sam)
            {
                sam.UpdateHasPower(25, 0);
                sam.SetFlag(BaseEntity.Flags.Reserved8, true);
                if (!_onlineSams.Contains(sam))
                {
                    _onlineSams.Add(sam);
                }
            }
            
            private void PowerSamsiteOff(SamSite sam)
            {
                sam.UpdateHasPower(0, 0);
                sam.SetFlag(BaseEntity.Flags.Reserved8, false);
                if (_onlineSams.Contains(sam))
                {
                    _onlineSams.Remove(sam);
                }
            }
            
            public void ToggleSamsInTcRange(BasePlayer player, string arg)
            {
                List<SamSite> turretList = Pool.GetList<SamSite>();
                
                foreach (SamSite turret in BaseNetworkable.serverEntities.OfType<SamSite>())
                {
                    if (turret.GetBuildingPrivilege()?.IsAuthed(player) == true && turret.GetBuildingPrivilege() == player.GetBuildingPrivilege())
                    {
                        turretList.Add(turret);
                    }
                }
                
                if (turretList.Count < 1)
                {
                    return;
                }
                
                foreach (SamSite sam in turretList)
                {
                    if (arg == "on")
                    {
                        PowerSamsiteOn(sam);
                    }
                    else
                    {
                        PowerSamsiteOff(sam);
                    }
                }
                
                player.ChatMessage(_instance.Lang("TurretsToggled", player.UserIDString, turretList.Count));
                
                Pool.FreeList(ref turretList);
            }
            
            #endregion
        } 
        
        #endregion
    }
}
