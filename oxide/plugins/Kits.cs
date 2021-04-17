using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Globalization;
using Oxide.Core;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Kits", "bitchy", "0.0.4")]
    class Kits : RustPlugin
    {
        #region Variables
        [PluginReference] private Plugin ImageLibrary;
        private List<Kit> _kits;
        private Dictionary<ulong, Dictionary<string, KitData>> _kitsData;
        private Dictionary<BasePlayer, List<string>> _kitsGUI = new Dictionary<BasePlayer, List<string>>();
        private static List<string> CustomAutoKits = new List<string>();
        public Kits plugin;

        private readonly string Layer = "ui.kits";
        #endregion

        #region Config
        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Enable GUI Notifications")]
            public bool notificationSwitch;

            [JsonProperty(PropertyName = "Notification Settings")]
            public notificationSettings notificationSettings;

            [JsonProperty(PropertyName = "Kits on Respawn")]
            public List<string> CustomAutoKits;
        }
        private class notificationSettings
        {
            [JsonProperty(PropertyName = "Notification Display Time")]
            public float notificationTime;

            [JsonProperty(PropertyName = "Background Color of Positive Notifications")]
            public string notificationGreenColor;

            [JsonProperty(PropertyName = "Background Color of Negative Notifications")]
            public string notificationRedColor;
        }
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                notificationSwitch = true,
                notificationSettings = new notificationSettings
                {
                    notificationTime = 4f,
                    notificationGreenColor = "#00db6aFF",
                    notificationRedColor = "#F95E69FF"
                },
                CustomAutoKits = new List<string>
                {
                    "autokit1",
                    "autokit2",
                }
            };
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("The configuration file is corrupted (or does not exist), I created a new one!");
            config = GetDefaultConfig();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Classes

        public class Kit
        {
            [JsonProperty("Title")]
            public string Name;
            [JsonProperty("Name Format")]
            public string DisplayName;
            [JsonProperty("Color of the kit (default - #46C4FFFF)")]
            public string KitColor;
            [JsonProperty("Description")]
            public string Description;
            [JsonProperty("Color of the kit description (default - #CBCBCBFF)")]
            public string DescriptionColor;
            [JsonProperty("Maximum usages")]
            public int Amount;
            [JsonProperty("Cooldown")]
            public double Cooldown;
            [JsonProperty("Visible or Hidden")]
            public bool Hide;
            [JsonProperty("Privilege")]
            public string Permission;
            [JsonProperty("Items")]
            public List<KitItem> Items;
        }

        public class KitItem
        {
            [JsonProperty("Item Name")]
            public string ShortName;
            [JsonProperty("Amount")]
            public int Amount;
            [JsonProperty("Blueprint")]
            public int Blueprint;
            [JsonProperty("Container")]
            public string Container;
            [JsonProperty("Condition")]
            public float Condition;
            [JsonProperty("SkinID")]
            public ulong SkinID;
            [JsonProperty("Weapon")]
            public Weapon Weapon;
            public List<ItemContent> Content;

        }
        public class Weapon
        {
            [JsonProperty("Ammo Type")]
            public string ammoType;
            [JsonProperty("Amount of Ammo")]
            public int ammoAmount;
        }
        public class ItemContent
        {
            [JsonProperty("Item Name")]
            public string ShortName;
            [JsonProperty("Condition")]
            public float Condition;
            [JsonProperty("Amount")]
            public int Amount;
        }

        public class KitData
        {
            [JsonProperty("Amount")]
            public int Amount;
            [JsonProperty("Cooldown")]
            public double Cooldown;
        }

        #endregion

        #region Oxide

        void OnPlayerRespawned(BasePlayer player)
        {
            foreach (var kits in CustomAutoKits)
            {
                if (_kits.Exists(x => x.Name == kits))
                {
                    var kit1 = _kits.First(x => x.Name == kits);
                    if (permission.UserHasPermission(player.UserIDString, kit1.Permission))
                    {
                        player.inventory.Strip();
                        GiveItems(player, kit1);
                        return;
                    }
                }
            }
            if (_kits.Exists(x => x.Name == "autokit"))
            {
                player.inventory.Strip();
                var kit = _kits.First(x => x.Name == "autokit");
                GiveItems(player, kit);
            }

        }
        private void SaveKits()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Kits", _kits);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Kits_Data", _kitsData);
        }

        void OnServerSave()
        {
            SaveData();
            SaveKits();
        }

        private void Loaded()
        {
            _kits = Interface.Oxide.DataFileSystem.ReadObject<List<Kit>>("Kits");
            _kitsData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, KitData>>>("Kits_Data");
        }

        private void Unload()
        {
            SaveData();

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        private void OnServerInitialized()
        {
            foreach (var kit in _kits)
            {
                if (!permission.PermissionExists(kit.Permission))
                    permission.RegisterPermission(kit.Permission, this);
            }

            if (!plugins.Find("ImageLibrary"))
            {
                PrintError("Please setup ImageLibrary plugin!");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }

            ImageLibrary.Call("AddImage", "https://i.imgur.com/Yhnckps.png", "kits_background");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/HY0OKRm.png", "kits_item_bg");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/6jMyTCL.png", "kits_item_btn_open");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/Qjof5eS.png", "kits_item_image_open");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/GvhRhey.png", "kits_item_opened");

            timer.Every(1, RefreshCooldownKitsUI);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            _kitsGUI.Remove(player);
        }

        #endregion

        #region Commands
        [ConsoleCommand("kit")]
        private void CommandConsoleKit(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;

            var player = arg.Player();

            if (!arg.HasArgs())
                return;

            var value = arg.Args[0].ToLower();

            if (value == "ui")
            {
                TriggerUI(player, 0);
                return;
            }

            if (value == "page")
            {
                int page = 0;
                if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;
                InitilizeUI(player, page);
                return;
            }

            if (!_kitsGUI.ContainsKey(player))
                return;

            if (!_kitsGUI[player].Contains(value))
                return;

            GiveKit(player, value);

            var container = new CuiElementContainer();
            var kit = _kits.First(x => x.Name == value);
            var playerData = GetPlayerData(player.userID, value);

            if (kit.Cooldown > 0)
            {
                var currentTime = GetCurrentTime();
                if (playerData.Cooldown > currentTime)
                {
                    CuiHelper.DestroyUi(player, Layer + $".BlockPanel.{value}.Btn");
                    InitilizeCooldownLabelUI(ref container, value, TimeSpan.FromSeconds(playerData.Cooldown - currentTime));
                }

                CuiHelper.DestroyUi(player, Layer + $".BlockPanel.{value}.Image");
                container.Add(new CuiElement
                {
                    Name = Layer + $".BlockPanel.{kit.Name}.Image",
                    Parent = Layer + $".BlockPanel.{kit.Name}",
                    Components = {
                         new CuiRawImageComponent { FadeIn = 1.0f, Color = "1 1 1 1", Png = playerData.Cooldown > currentTime ? (string) ImageLibrary.Call("GetImage", "kits_item_opened") : (string) ImageLibrary.Call("GetImage", "kits_item_image_open")},
                         new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = playerData.Cooldown > currentTime ? "-53.5 -123.5" : "-42.5 -128", OffsetMax = playerData.Cooldown > currentTime ? "53.5 -29.5" : "42.5 -25" }
                    }
                });
            }
            CuiHelper.AddUi(player, container);

            return;
        }

        [ChatCommand("kit")]
        private void CommandChatKit(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return;

            if (args.Length == 0)
            {
                TriggerUI(player, 0);
                return;
            }

            if (!player.IsAdmin)
            {
                GiveKit(player, args[0].ToLower());
                return;
            }

            switch (args[0].ToLower())
            {
                case "help":
                    SendReply(player, "Teams:\n/kit new [name]: create a kit\n/kit clone [name]: clone a kit\n/kit remove [name]: delete a kit\n/kit list: list of kits\n/kit reset: reset all kits");
                    return;
                case "new":
                    if (args.Length < 2)
                        SendReply(player, "/kit new [name]: create a kit");
                    else
                        KitCommandAdd(player, args[1].ToLower());
                    return;
                case "clone":
                    if (args.Length < 2)
                        SendReply(player, "/kit clone [name]: clone a kit");
                    else
                        KitCommandClone(player, args[1].ToLower());
                    return;
                case "remove":
                    if (args.Length < 2)
                        SendReply(player, "/kit remove [name]: delete a kit");
                    else
                        KitCommandRemove(player, args[1].ToLower());
                    return;
                case "list":
                    KitCommandList(player);
                    return;
                case "reset":
                    KitCommandReset(player);
                    return;
                case "give":
                    if (args.Length < 3)
                    {
                        SendReply(player, "/kit give [name] nickname/SteamID");
                    }
                    else
                    {
                        var foundPlayer = FindPlayer(player, args[1].ToLower());
                        if (foundPlayer == null)
                            return;
                        Message(player, "KITS.PLAYERGIVED", foundPlayer.displayName, args[2]);
                        KitCommandGive(player, foundPlayer, args[2].ToLower());
                    }
                    return;
                default:
                    GiveKit(player, args[0].ToLower());
                    return;
            }
        }

        #endregion

        #region Kit

        private bool GiveKit(BasePlayer player, string kitname)
        {
            if (string.IsNullOrEmpty(kitname))
                return false;

            if (Interface.Oxide.CallHook("canRedeemKit", player) != null)
            {
                return false;
            }
            if (!_kits.Exists(x => x.Name == kitname))
            {
                Message(player, "ERROR.INVALID");
                return false;
            }

            var kit = _kits.First(x => x.Name == kitname);

            if (!string.IsNullOrEmpty(kit.Permission) && !permission.UserHasPermission(player.UserIDString, kit.Permission))
            {
                Message(player, "ERROR.NOACCESS");
                return false;
            }

            var playerData = GetPlayerData(player.userID, kitname);

            if (kit.Amount > 0 && playerData.Amount >= kit.Amount)
            {
                Message(player, "ERROR.LIMIT");
                return false;
            }

            if (kit.Cooldown > 0)
            {
                var currentTime = GetCurrentTime();
                if (playerData.Cooldown > currentTime)
                {
                    Message(player, "ERROR.COOLDOWN", TimeExtensions.FormatTime(TimeSpan.FromSeconds(playerData.Cooldown - currentTime)));
                    return false;
                }
            }

            int beltcount = kit.Items.Where(i => i.Container == "belt").Count();
            int wearcount = kit.Items.Where(i => i.Container == "wear").Count();
            int maincount = kit.Items.Where(i => i.Container == "main").Count();
            int totalcount = beltcount + wearcount + maincount;
            if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < beltcount || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < wearcount || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < maincount)
                if (totalcount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                {
                    Message(player, "ERROR.INVENTORYFULL");
                    return false;
                }
            GiveItems(player, kit);

            if (kit.Amount > 0)
                playerData.Amount += 1;

            if (kit.Cooldown > 0)
                playerData.Cooldown = GetCurrentTime() + kit.Cooldown;

            Message(player, "KITS.GIVED", kit.DisplayName);
            return true;
        }

        private void KitCommandAdd(BasePlayer player, string kitname)
        {
            if (_kits.Exists(x => x.Name == kitname))
            {
                Message(player, "ERROR.DUBLICATE");
                return;
            }

            _kits.Add(new Kit
            {
                Name = kitname,
                DisplayName = kitname,
                KitColor = "#46C4FFFF",
                Description = "Description of the kit. You can change it on the configuration.",
                DescriptionColor = "#CBCBCBFF",
                Cooldown = 600,
                Hide = true,
                Permission = "kits.default",
                Amount = 0,
                Items = GetPlayerItems(player)
            });
            permission.RegisterPermission($"kits.default", this);
            Message(player, "KITS.ADDED", kitname);

            SaveKits();
            SaveData();
        }

        private void KitCommandClone(BasePlayer player, string kitname)
        {
            if (!_kits.Exists(x => x.Name == kitname))
            {
                Message(player, "ERROR.INVALID");
                return;
            }

            _kits.First(x => x.Name == kitname).Items = GetPlayerItems(player);

            Message(player, "KITS.ITEMS", kitname);

            SaveKits();
        }

        private void KitCommandRemove(BasePlayer player, string kitname)
        {
            if (_kits.RemoveAll(x => x.Name == kitname) <= 0)
            {
                Message(player, "ERROR.INVALID");
                return;
            }

            Message(player, "ERROR.DELETED", kitname);

            SaveKits();
        }

        private void KitCommandList(BasePlayer player)
        {
            foreach (var kit in _kits)
                SendReply(player, $"{kit.Name} - {kit.DisplayName}");
        }

        private void KitCommandReset(BasePlayer player)
        {
            _kitsData.Clear();

            Message(player, "KITS.RESET");
        }

        private void KitCommandGive(BasePlayer player, BasePlayer foundPlayer, string kitname)
        {
            var reply = 1;
            if (reply == 0) { }
            if (!_kits.Exists(x => x.Name == reply.ToString())) { }

            if (!_kits.Exists(x => x.Name == kitname))
            {
                 Message(player, "ERROR.INVALID");
                return;
            }

            GiveItems(foundPlayer, _kits.First(x => x.Name == kitname));
        }
        private void GiveItems(BasePlayer player, Kit kit)
        {
            foreach (var kitem in kit.Items)
            {
                GiveItem(player,
                    BuildItem(kitem.ShortName, kitem.Amount, kitem.SkinID, kitem.Condition, kitem.Blueprint,
                        kitem.Weapon, kitem.Content),
                    kitem.Container == "belt" ? player.inventory.containerBelt :
                    kitem.Container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
            }
        }
        private void GiveItem(BasePlayer player, Item item, ItemContainer cont = null)
        {
            if (item == null) return;
            var inv = player.inventory;

            var moved = item.MoveToContainer(cont) || item.MoveToContainer(inv.containerMain);
            if (!moved)
            {
                if (cont == inv.containerBelt)
                    moved = item.MoveToContainer(inv.containerWear);
                if (cont == inv.containerWear)
                    moved = item.MoveToContainer(inv.containerBelt);
            }

            if (!moved)
                item.Drop(player.GetCenter(), player.GetDropVelocity());
        }
        private Item BuildItem(string ShortName, int Amount, ulong SkinID, float Condition, int blueprintTarget, Weapon weapon, List<ItemContent> Content)
        {
            Item item = ItemManager.CreateByName(ShortName, Amount > 1 ? Amount : 1, SkinID);
            item.condition = Condition;

            if (blueprintTarget != 0)
                item.blueprintTarget = blueprintTarget;

            if (weapon != null)
            {
                (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = weapon.ammoAmount;
                (item.GetHeldEntity() as BaseProjectile).primaryMagazine.ammoType = ItemManager.FindItemDefinition(weapon.ammoType);
            }
            if (Content != null)
            {
                foreach (var cont in Content)
                {
                    Item new_cont = ItemManager.CreateByName(cont.ShortName, cont.Amount);
                    new_cont.condition = cont.Condition;
                    new_cont.MoveToContainer(item.contents);
                }
            }
            return item;
        }
        #endregion

        #region Interface
        private void TriggerUI(BasePlayer player, int page)
        {
            if (_kitsGUI.ContainsKey(player))
            {
                DestroyUI(player);
            }
            else
            {
                InitilizeUI(player, page);
            }
        }

        private void InitilizeUI(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, Layer);
            _kitsGUI[player] = new List<string>();
            var currentTime = GetCurrentTime();
            var kits = GetKitsForPlayer(player).ToList();
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat" },
            }, "Overlay", Layer);

            container.Add(new CuiElement
            {
                Name = Layer + ".BgImage",
                Parent = Layer,
                Components =
                                {
                                   new CuiRawImageComponent
                                   {
                                       FadeIn = 1.0f,
                                       Color = "1 1 1 0.7",
                                       Png = (string) ImageLibrary.Call("GetImage", "kits_background")
                                   },
                                   new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-420 -250", OffsetMax = "420 250" }
                                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-55 -50", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = "kit ui" },
                Text = { FadeIn = 3f, Color = "1 1 1 1", Text = "X", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 40 }
            }, Layer + ".BgImage");

            for (int i = 0; i < kits.Count && i < (page + 1) * 4; i++)
            {
                var kit = kits[i];
                _kitsGUI[player].Add(kit.Name);
                var playerData = GetPlayerData(player.userID, kit.Name);

                var currentPosition = i % 4;
                var xSwitch = 0 - 375 + currentPosition * 180 + (currentPosition) * 10;

                container.Add(new CuiElement
                {
                    Name = Layer + $".BlockPanel.{kit.Name}",
                    Parent = Layer + ".BgImage",
                    Components =
                    {
                         new CuiRawImageComponent
                         {
                             FadeIn = 1.0f,
                             Color = "1 1 1 1",
                             Png = (string) ImageLibrary.Call("GetImage", "kits_item_bg")
                         },
                         new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{xSwitch} -210", OffsetMax = $"{xSwitch + 180} 210" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = Layer + $".BlockPanel.{kit.Name}.Image",
                    Parent = Layer + $".BlockPanel.{kit.Name}",
                    Components = {
                         new CuiRawImageComponent { FadeIn = 1.0f, Color = "1 1 1 1", Png = playerData.Cooldown > currentTime ? (string) ImageLibrary.Call("GetImage", "kits_item_opened") : (string) ImageLibrary.Call("GetImage", "kits_item_image_open")},
                         new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = playerData.Cooldown > currentTime ? "-53.5 -123.5" : "-42.5 -128", OffsetMax = playerData.Cooldown > currentTime ? "53.5 -29.5" : "42.5 -25" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = Layer + $".BlockPanel.{kit.Name}.Name",
                    Parent = Layer + $".BlockPanel.{kit.Name}",
                    Components = {
                         new CuiTextComponent { Text = $"{kit.DisplayName}", FadeIn = 1.0f, Color = $"{HexToCuiColor(kit.KitColor)}", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.UpperCenter },
                         new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-90 -195", OffsetMax = "90 -145" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = Layer + $".BlockPanel.{kit.Name}.Description",
                    Parent = Layer + $".BlockPanel.{kit.Name}",
                    Components = {
                         new CuiTextComponent { Text = $"{kit.Description}", FadeIn = 1.0f, Color = $"{HexToCuiColor(kit.DescriptionColor)}", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.UpperCenter },
                         new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-70 -340", OffsetMax = "70 -215" }
                    }
                });

                if (kit.Cooldown > 0 && (playerData.Cooldown > currentTime))
                {
                    InitilizeCooldownLabelUI(ref container, kit.Name, TimeSpan.FromSeconds(playerData.Cooldown - currentTime));
                }
                else
                {
                    InitilizeButtonUI(ref container, kit.Name);
                }
            }

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = page > 0 ? $"kit page {page - 1}" : "" },
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "-25 -40", OffsetMax = "60 40" },
                Text = { Text = "«", FontSize = 50, Font = "robotocondensed-bold.ttf", Color = page > 0 ? "0.3568628 0.7254902 0.9803922 1" : "0 0 0 0", Align = TextAnchor.MiddleCenter }
            }, Layer + ".BgImage", Layer + ".BgImage.PageBack");

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = (page + 1) * 4 < kits.Count ? $"kit page {page + 1}" : "" },
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-60 -40", OffsetMax = "25 40" },
                Text = { Text = "»", FontSize = 50, Font = "robotocondensed-bold.ttf", Color = (page + 1) * 4 < kits.Count ? "0.3568628 0.7254902 0.9803922 1" : "0 0 0 0", Align = TextAnchor.MiddleCenter }
            }, Layer + ".BgImage", Layer + ".BgImage.PageNext");

            CuiHelper.AddUi(player, container);
        }

        private void InitilizeButtonUI(ref CuiElementContainer container, string kitname)
        {
            container.Add(new CuiElement
            {
                Name = Layer + $".BlockPanel.{kitname}.Btn",
                Parent = Layer + $".BlockPanel.{kitname}",
                Components = {
                         new CuiRawImageComponent { Color = "0.27 0.77 1 1", Png = (string) ImageLibrary.Call("GetImage", "kits_item_btn_open")},
                         new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 25", OffsetMax = "70 60" }
                    }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"kit {kitname}" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "GET", FontSize = 18, Align = TextAnchor.MiddleCenter }
            }, Layer + $".BlockPanel.{kitname}.Btn", Layer + $".BlockPanel.{kitname}.BtnCMD");
        }

        private void InitilizeCooldownLabelUI(ref CuiElementContainer container, string kitname, TimeSpan time)
        {
            container.Add(new CuiElement
            {
                Name = Layer + $".BlockPanel.{kitname}.Btn.Time",
                Parent = Layer + $".BlockPanel.{kitname}",
                Components = {
                         new CuiRawImageComponent { Color = "0.98 0.36 0.36 1", Png = (string) ImageLibrary.Call("GetImage", "kits_item_btn_open")},
                         new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 25", OffsetMax = "70 60" }
                    }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"kit {kitname}" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = TimeExtensions.FormatShortTime(time), FontSize = 18, Align = TextAnchor.MiddleCenter }
            }, Layer + $".BlockPanel.{kitname}.Btn.Time", Layer + $".BlockPanel.{kitname}.BtnCMD.Time");
        }

        private void RefreshCooldownKitsUI()
        {
            var currentTime = GetCurrentTime();
            foreach (var playerGUIData in _kitsGUI)
            {
                var container = new CuiElementContainer();
                if (!_kitsData.ContainsKey(playerGUIData.Key.userID)) continue;
                var playerKitsData = _kitsData[playerGUIData.Key.userID];
                foreach (var kitname in playerGUIData.Value)
                {
                    var playerKitData = playerKitsData[kitname];
                    if (playerKitData.Cooldown > 0)
                    {
                        CuiHelper.DestroyUi(playerGUIData.Key, Layer + $".BlockPanel.{kitname}.Btn.Time");
                        if (playerKitData.Cooldown < currentTime)
                        {
                            CuiHelper.DestroyUi(playerGUIData.Key, Layer + $".BlockPanel.{kitname}.Btn");
                            InitilizeButtonUI(ref container, kitname);
                        }
                        else
                        {
                            InitilizeCooldownLabelUI(ref container, kitname, TimeSpan.FromSeconds(playerKitData.Cooldown - currentTime));
                        }
                    }
                }
                CuiHelper.AddUi(playerGUIData.Key, container);
            }
        }

        private void NotificationUI(BasePlayer player, string message, string color)
        {
            var container = new CuiElementContainer
            {
                {
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-175 -90", OffsetMax = "175 -10" },
                        Button = { Color = HexToCuiColor(color) },
                        Text = { FadeIn = 3f, Color = "1 1 1 1", Text = message, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18 }
                    },
                    "Overlay",
                    Layer + ".Notification"
                }
            };

            CuiHelper.DestroyUi(player, Layer + ".Notification");
            CuiHelper.AddUi(player, container);

            timer.Once(config.notificationSettings.notificationTime, () =>
            {
                if (player != null)
                {
                    CuiHelper.DestroyUi(player, Layer + ".Notification");
                }
            });
        }
        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"ERROR.INVALID", "This kit does not exist"},
                {"ERROR.NOACCESS", "You are not able to use this kit"},
                {"ERROR.LIMIT", "You have already used this kit a large number of times"},
                {"ERROR.COOLDOWN", "You will be able to use this kit through {0}"},
                {"ERROR.INVENTORYFULL", "There is not enough space in the inventory"},
                {"KITS.GIVED", "You got the kit {0}"},
                {"ERROR.DUBLICATE", "This kit already exists"},
                {"KITS.ADDED", "You have created a new kit{0}"},
                {"KITS.ITEMS", "Items were copied from inventory to kit {0}"},
                {"ERROR.DELETED", "Kit {0} was deleted"},
                {"KITS.RESET", "You zeroed all data on the kits of players"},
                {"ERROR.NOPLAYER", "Player not found"},
                {"ERROR.MOREPLAYERS", "Found a few players"},
                {"KITS.PLAYERGIVED", "You have successfully issued the player {0} set {1}"}
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"ERROR.INVALID", "This kit does not exist"},
                {"ERROR.NOACCESS", "You are not able to use this kit"},
                {"ERROR.LIMIT", "You have already used this kit a large number of times"},
                {"ERROR.COOLDOWN", "You will be able to use this kit through {0}"},
                {"ERROR.INVENTORYFULL", "There is not enough space in the inventory"},
                {"KITS.GIVED", "You got the kit {0}"},
                {"ERROR.DUBLICATE", "This kit already exists"},
                {"KITS.ADDED", "You have created a new kit{0}"},
                {"KITS.ITEMS", "Items were copied from inventory to kit {0}"},
                {"ERROR.DELETED", "Kit {0} was deleted"},
                {"KITS.RESET", "You zeroed all data on the kits of players"},
                {"ERROR.NOPLAYER", "Player not found"},
                {"ERROR.MOREPLAYERS", "Found a few players"},
                {"KITS.PLAYERGIVED", "You have successfully issued the player {0} set {1}"}
            }, this, "ru");
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null)
            {
                return;
            }

            var message = GetMessage(messageKey, player.UserIDString, args);
            player.SendConsoleCommand("chat.add", (object)0, (object)message);
            if (config.notificationSwitch)
            {
                if (messageKey.StartsWith("KITS."))
                { NotificationUI(player, message, config.notificationSettings.notificationGreenColor); }
                else if (messageKey.StartsWith("ERROR."))
                { NotificationUI(player, message, config.notificationSettings.notificationRedColor); }
                else { PrintError("Error in the Kits Plugin"); }
            }
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        #endregion

        #region Helper

        private static string HexToCuiColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        private KitData GetPlayerData(ulong userID, string name)
        {
            if (!_kitsData.ContainsKey(userID))
                _kitsData[userID] = new Dictionary<string, KitData>();

            if (!_kitsData[userID].ContainsKey(name))
                _kitsData[userID][name] = new KitData();

            return _kitsData[userID][name];
        }

        private List<KitItem> GetPlayerItems(BasePlayer player)
        {
            List<KitItem> kititems = new List<KitItem>();
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemToKit(item, "wear");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemToKit(item, "main");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemToKit(item, "belt");
                    kititems.Add(iteminfo);
                }
            }
            return kititems;
        }

        string GetMsg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player == null ? null : player.UserIDString);

        private KitItem ItemToKit(Item item, string container)
        {
            KitItem kitem = new KitItem();
            kitem.Amount = item.amount;
            kitem.Container = container;
            kitem.SkinID = item.skin;
            kitem.Blueprint = item.blueprintTarget;
            kitem.ShortName = item.info.shortname;
            kitem.Condition = item.condition;
            kitem.Weapon = null;
            kitem.Content = null;
            if (item.info.category == ItemCategory.Weapon)
            {
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    kitem.Weapon = new Weapon();
                    kitem.Weapon.ammoType = weapon.primaryMagazine.ammoType.shortname;
                    kitem.Weapon.ammoAmount = weapon.primaryMagazine.contents;
                }
            }
            if (item.contents != null)
            {
                kitem.Content = new List<ItemContent>();
                foreach (var cont in item.contents.itemList)
                {
                    kitem.Content.Add(new ItemContent()
                    {
                        Amount = cont.amount,
                        Condition = cont.condition,
                        ShortName = cont.info.shortname
                    });
                }
            }
            return kitem;
        }

        private List<Kit> GetKitsForPlayer(BasePlayer player)
        {
            return _kits.Where(kit => kit.Hide == false && (string.IsNullOrEmpty(kit.Permission) || permission.UserHasPermission(player.UserIDString, kit.Permission)) && (kit.Amount == 0 || (kit.Amount > 0 && GetPlayerData(player.userID, kit.Name).Amount < kit.Amount))).ToList();
        }

        private BasePlayer FindPlayer(BasePlayer player, string nameOrID)
        {
            ulong id;
            if (ulong.TryParse(nameOrID, out id) && nameOrID.StartsWith("") && nameOrID.Length == 17)
            {
                var findedPlayer = BasePlayer.FindByID(id);
                if (findedPlayer == null || !findedPlayer.IsConnected)
                {
                    Message(player, "ERROR.NOPLAYER");
                    return null;
                }

                return findedPlayer;
            }

            var foundPlayers = BasePlayer.activePlayerList.Where(x => x.displayName.ToLower().Contains(nameOrID.ToLower()));

            if (foundPlayers.Count() == 0)
            {
                Message(player, "ERROR.NOPLAYER");
                return null;
            }

            if (foundPlayers.Count() > 1)
            {
                Message(player, "ERROR.MOREPLAYERS");
                return null;
            }

            return foundPlayers.First();
        }

        private double GetCurrentTime() => new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;

        private static class TimeExtensions
        {
            public static string FormatShortTime(TimeSpan time)
            {
                string result = string.Empty;
                if (time.Days > 0)
                    result = $"{Format(time.Days, "days", "days", "days")} {Format(time.Hours, "hours", "hours", "hours")}";

                if (time.Days == 0)
                    result = $"{Format(time.Hours, "hours", "hours", "hours")} {time.Minutes} minutes";

                if (time.Hours == 0)
                    result = $"{time.Minutes} minutes {time.Seconds} seconds";

                if (time.Minutes == 0)
                    result = $"{time.Seconds} seconds";

                return result;
            }

            public static string FormatTime(TimeSpan time)
            {
                string result = string.Empty;
                if (time.Days != 0)
                    result += $"{Format(time.Days, "days", "days", "days")} ";

                if (time.Hours != 0)
                    result += $"{Format(time.Hours, "hours", "hours", "hours")} ";

                if (time.Minutes != 0)
                    result += $"{Format(time.Minutes, "minutes", "minuntes", "minutes")} ";

                if (time.Seconds != 0)
                    result += $"{Format(time.Seconds, "seconds", "seconds", "seconds")} ";

                return result;
            }

            private static string Format(int units, string form1, string form2, string form3)
            {
                var tmp = units % 10;

                if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                    return $"{units} {form1}";

                if (tmp >= 2 && tmp <= 4)
                    return $"{units} {form2}";

                return $"{units} {form3}";
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            if (!_kitsGUI.ContainsKey(player))
                return;

            CuiHelper.DestroyUi(player, Layer);

            _kitsGUI.Remove(player);
        }

        #endregion
    }
}
