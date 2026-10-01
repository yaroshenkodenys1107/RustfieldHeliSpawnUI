using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

using Newtonsoft.Json;

using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RustfieldHeliSpawnUI", "Denys Yaroshenko", "1.1.0")]
    [Description("Helicopter buttons over the clothing slots: spawn, fetch and remove through SpawnHeli, its cooldowns drawn as draining bars")]
    public class RustfieldHeliSpawnUI : RustPlugin
    {
        // ------------------------------------------------------------------------------------------
        // Three blocks over the clothing slots, right of the backpack slot, one per helicopter:
        // Minicopter, Attack Helicopter, Scrap Transport. A block is a header - the machine's icon,
        // its name and a status dot - over two buttons standing exactly under two clothing columns.
        //
        //  * Not in the world: the left button spawns it. On the spawn cooldown it is a red bar that
        //    drains right to left, with the time left on it. The right button is off.
        //  * In the world: the left button fetches it to the player, with the fetch cooldown drawn
        //    the same way; the right button removes it.
        //  * The dot: lime - in the world, orange - fetch cooling down, red - spawn cooling down,
        //    green - ready.
        //
        // SpawnHeli does all the work and is never edited. The buttons run its chat commands as the
        // player, so its permissions, checks and messages stay what they are. Whether a machine is
        // in the world comes from its API; the cooldowns from its own data and config, read
        // through reflection, since it has no API for them.
        //
        // Everything hangs off the "Inventory" layer, so the client shows it exactly while the
        // inventory is open. Built once per player; after that only the parts that changed go out,
        // with Update set, so nothing blinks.
        //
        // Read it in this order: Constants, Configuration, Localisation, SpawnHeli, Looks,
        // Drawing, Lifecycle, Commands.
        // ------------------------------------------------------------------------------------------

        #region Constants

        private const string Command = "rfheli";
        private const string RootName = "RustfieldHeliSpawnUI";
        private const string Layer = "Inventory";

        // The menu's languages, as lang folders; there is no "ua".
        private static readonly string[] Codes = { "en", "ru", "uk" };

        // The blocks, in canvas units from the bottom middle of the screen, where the clothing slots
        // hang. On a 1280x720 screen from the top left: block i at x = 107.5 + 108 i, 104 wide, two
        // clothing columns; the header 499.5-523.5, the buttons 525.5-549.5, 50 each and 4 apart, each
        // under one clothing column.
        private const float BlocksLeft = -532.5f;
        private const float BlockPitch = 108f;
        private const float BlockWidth = 104f;
        private const float ButtonsBottom = 170.5f;
        private const float ButtonsTop = 194.5f;
        private const float HeaderBottom = 196.5f;
        private const float HeaderTop = 220.5f;
        private const float ButtonWidth = 50f;
        private const float RightLeft = 54f;

        // Inside the header, from the block's left and bottom: the icon 22 square in the middle of the
        // height, the name from 30, the dot 5 square, 4 in from the right.
        private const float IconLeft = 4f;
        private const float IconWidth = 22f;
        private const float IconHeight = 22f;
        private const float NameLeft = 30f;
        private const float DotSize = 5f;
        private const float DotRight = 4f;

        private const string Bold = "robotocondensed-bold.ttf";
        private const int NameSize = 10;
        private const int ButtonSize = 10;

        // The plates, and the colours of what a button does: green spawns and fetches, red removes
        // and drains, lime, orange and green say where the machine is.
        // The plates are RustfieldButtons' bars: the game's warm tint at a very low alpha over the
        // frosted blur, what an empty belt slot is made of. Green and red lie on them as faces.
        private const string Plate = "0.969 0.922 0.882 0.035";
        private const string Blur = "assets/content/ui/uibackgroundblur.mat";
        private const string Green = "0.439 0.537 0.263 1";
        private const string Red = "0.643 0.263 0.227 1";
        private const string Lime = "0.667 0.933 0.196 1";
        private const string Orange = "0.804 0.529 0.357 1";
        // The words, by the config's style. A - pure white, the inactive at 42% of it, as 1.0.x drew
        // them. B - the warm white #E8DCD3 that RustfieldButtons' labels use, the inactive at 42% of
        // that.
        private const string StyleA = "A";
        private const string StyleB = "B";
        private const string InkA = "1 1 1 1";
        private const string InkOffA = "1 1 1 0.42";
        private const string InkB = "0.91 0.863 0.827 1";
        private const string InkOffB = "0.91 0.863 0.827 0.42";

        private string Ink => config.Style == StyleA ? InkA : InkB;
        private string InkOff => config.Style == StyleA ? InkOffA : InkOffB;
        // The icons are the game's own pictures of the machines, in colour, so they are not tinted.
        private const string IconTint = "1 1 1 1";
        private const string Clear = "0 0 0 0";

        // A button is clear at rest and lays this wash over its plate on hover.
        private const string Wash = "1 1 1 0.1";
        private const string Lit = "1 1 1 1";

        private const string IconRoot = "https://raw.githubusercontent.com/yaroshenkodenys1107/RustfieldHeliSpawnUI/main/images/";

        // A second click within this long is ignored: the command is already on its way.
        private const float ClickGap = 0.5f;

        // After a click the block is looked at again this late, and again, by when SpawnHeli has
        // answered.
        private static readonly float[] Rechecks = { 0.3f, 1.2f };

        #endregion

        #region Configuration

        private PluginConfig config;

        private sealed class MachineConfig
        {
            [JsonProperty("Show")] public bool Enabled = true;
            [JsonProperty("Spawn command")] public string Spawn;
            [JsonProperty("Fetch command")] public string Fetch;
            [JsonProperty("Remove command")] public string Remove;
            [JsonProperty("Icon (web PNG)")] public string Icon;
        }

        private sealed class PluginConfig
        {
            [JsonProperty("Minicopter")]
            public MachineConfig Mini = new MachineConfig { Spawn = "mymini", Fetch = "fmini", Remove = "nomini", Icon = IconRoot + "minicopter.png" };

            [JsonProperty("Attack Helicopter")]
            public MachineConfig Attack = new MachineConfig { Spawn = "myattack", Fetch = "fattack", Remove = "noattack", Icon = IconRoot + "attackhelicopter.png" };

            [JsonProperty("Scrap Transport Helicopter")]
            public MachineConfig Scrap = new MachineConfig { Spawn = "myheli", Fetch = "fheli", Remove = "noheli", Icon = IconRoot + "scraptransport.png" };

            [JsonProperty("Style (A or B)")]
            public string Style = StyleB;

            [JsonProperty("Seconds between updates")]
            public float Tick = 1f;

            // The red bars drain on a timer of their own, as RustfieldGrade's do: often, and only
            // once the bar's edge has moved this far.
            [JsonProperty("Seconds between bar updates")]
            public float DrainTick = 0.05f;

            [JsonProperty("Bar step (canvas units)")]
            public float DrainStep = 0.1f;
        }

        protected override void LoadDefaultConfig() => config = new PluginConfig();

        protected override void SaveConfig() => Config.WriteObject(config, true);

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<PluginConfig>();
            }
            catch (Exception error)
            {
                PrintError($"Configuration could not be read ({error.Message}), using the defaults.");
                config = null;
            }

            if (config == null) config = new PluginConfig();
            if (config.Mini == null) config.Mini = new PluginConfig().Mini;
            if (config.Attack == null) config.Attack = new PluginConfig().Attack;
            if (config.Scrap == null) config.Scrap = new PluginConfig().Scrap;
            config.Style = string.Equals(config.Style, StyleA, StringComparison.OrdinalIgnoreCase) ? StyleA : StyleB;
            if (config.Tick < 0.2f) config.Tick = 0.2f;
            if (config.DrainTick < 0.02f) config.DrainTick = 0.02f;
            if (config.DrainStep < 0f) config.DrainStep = 0f;

            // Written back every load, so a key the file predates reaches it.
            SaveConfig();
        }

        #endregion

        #region Localisation

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            ["Name.mini"] = "Minicopter",
            ["Name.attack"] = "Helicopter",
            ["Name.scrap"] = "Transport",
            ["Button.Spawn"] = "Spawn",
            ["Button.Fetch"] = "Fetch",
            ["Button.Remove"] = "Remove"
        };

        private static readonly Dictionary<string, string> Russian = new Dictionary<string, string>
        {
            ["Name.mini"] = "Миникоптер",
            ["Name.attack"] = "Стрекоза",
            ["Name.scrap"] = "Корова",
            ["Button.Spawn"] = "Вызвать",
            ["Button.Fetch"] = "К себе",
            ["Button.Remove"] = "Убрать"
        };

        private static readonly Dictionary<string, string> Ukrainian = new Dictionary<string, string>
        {
            ["Name.mini"] = "Мінікоптер",
            ["Name.attack"] = "Бабка",
            ["Name.scrap"] = "Корова",
            ["Button.Spawn"] = "Виклик",
            ["Button.Fetch"] = "До себе",
            ["Button.Remove"] = "Забрати"
        };

        // Every language's texts as the lang files hold them, by code.
        private readonly Dictionary<string, Dictionary<string, string>> words = new Dictionary<string, Dictionary<string, string>>();

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(English, this, "en");
            lang.RegisterMessages(Russian, this, "ru");
            lang.RegisterMessages(Ukrainian, this, "uk");
        }

        private void ReadWords()
        {
            words.Clear();
            foreach (string code in Codes)
            {
                Dictionary<string, string> texts = new Dictionary<string, string>(English);
                Dictionary<string, string> file = lang.GetMessages(code, this);
                if (file != null)
                {
                    foreach (KeyValuePair<string, string> pair in file) texts[pair.Key] = pair.Value;
                }
                words[code] = texts;
            }
        }

        private string T(string code, string key)
        {
            Dictionary<string, string> texts;
            string text;
            if (!words.TryGetValue(code, out texts) || !texts.TryGetValue(key, out text)) English.TryGetValue(key, out text);
            return string.IsNullOrEmpty(text) ? string.Empty : text.ToUpperInvariant();
        }

        [PluginReference] private Plugin RustfieldPanel;

        // The language a player reads the menu in: the panel's to say, or the game's language when
        // the panel is not there.
        private string LanguageOf(BasePlayer player)
        {
            if (RustfieldPanel == null || !RustfieldPanel.IsLoaded) RustfieldPanel = plugins.Find("RustfieldPanel");

            string code = RustfieldPanel != null && RustfieldPanel.IsLoaded ? RustfieldPanel.Call("API_GetLanguage", player) as string : null;
            if (Array.IndexOf(Codes, code) >= 0) return code;

            string own = lang.GetLanguage(player.UserIDString) ?? "en";
            foreach (string known in Codes)
            {
                if (own.StartsWith(known, StringComparison.OrdinalIgnoreCase)) return known;
            }

            return "en";
        }

        #endregion

        #region SpawnHeli

        [PluginReference] private Plugin SpawnHeli;

        // One helicopter as SpawnHeli knows it: the API that finds it, the fields its data and its
        // config keep it under, and the name its permissions are built from.
        private sealed class Machine
        {
            public string Key;
            public string Getter;
            public string Field;
            public string Permission;
            public MachineConfig Config;
        }

        private readonly List<Machine> machines = new List<Machine>();

        private void BuildMachines()
        {
            machines.Clear();
            Add("mini", "API_GetMinicopter", "Minicopter", "minicopter", config.Mini);
            Add("attack", "API_GetAttackHelicopter", "AttackHelicopter", "attackhelicopter", config.Attack);
            Add("scrap", "API_GetScrapTransportHelicopter", "ScrapTransportHelicopter", "scraptransport", config.Scrap);
        }

        private void Add(string key, string getter, string field, string permissionName, MachineConfig machineConfig)
        {
            if (!machineConfig.Enabled) return;
            machines.Add(new Machine { Key = key, Getter = getter, Field = field, Permission = permissionName, Config = machineConfig });
        }

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // SpawnHeli's own members, found once per load of it: its data and config, and the method
        // that says how long a cooldown is for a player, permission profiles and all.
        private FieldInfo dataField;
        private FieldInfo configField;
        private MethodInfo cooldownMethod;
        private bool bound;
        private bool warned;

        private bool Bind()
        {
            if (SpawnHeli == null || !SpawnHeli.IsLoaded)
            {
                bound = false;
                return false;
            }

            if (bound) return true;

            Type type = SpawnHeli.GetType();
            dataField = type.GetField("_data", Any);
            configField = type.GetField("_config", Any);
            cooldownMethod = type.GetMethod("GetPlayerCooldownSeconds", Any);
            bound = dataField != null && configField != null && cooldownMethod != null;

            if (!bound && !warned)
            {
                warned = true;
                PrintWarning("SpawnHeli has changed inside: its cooldowns cannot be read, the buttons show none.");
            }

            return bound;
        }

        private static object Member(object owner, string name)
        {
            if (owner == null) return null;
            FieldInfo field = owner.GetType().GetField(name, Any);
            return field == null ? null : field.GetValue(owner);
        }

        private BaseEntity VehicleOf(BasePlayer player, Machine machine)
        {
            if (SpawnHeli == null || !SpawnHeli.IsLoaded) return null;
            BaseEntity vehicle = SpawnHeli.Call(machine.Getter, player) as BaseEntity;
            return vehicle == null || vehicle.IsDestroyed ? null : vehicle;
        }

        // Seconds left on a spawn or fetch cooldown, and how long it is in all; 0 when there is none.
        private double CooldownLeft(BasePlayer player, Machine machine, bool fetch, out double total)
        {
            total = 0;
            if (!Bind()) return 0;

            try
            {
                if (permission.UserHasPermission(player.UserIDString, "spawnheli." + machine.Permission + ".nocooldown")) return 0;

                string kind = fetch ? "FetchCooldowns" : "SpawnCooldowns";

                Dictionary<string, DateTime> starts = Member(Member(dataField.GetValue(SpawnHeli), machine.Field), kind) as Dictionary<string, DateTime>;
                DateTime start;
                if (starts == null || !starts.TryGetValue(player.UserIDString, out start)) return 0;

                object cooldown = Member(Member(configField.GetValue(SpawnHeli), machine.Field), kind);
                if (cooldown == null) return 0;

                total = Convert.ToDouble(cooldownMethod.Invoke(SpawnHeli, new object[] { cooldown, player }), CultureInfo.InvariantCulture);
                double left = (start.AddSeconds(total) - DateTime.Now).TotalSeconds;
                return left > 0 ? left : 0;
            }
            catch (Exception error)
            {
                if (!warned)
                {
                    warned = true;
                    PrintWarning($"SpawnHeli's cooldowns cannot be read ({error.Message}), the buttons show none.");
                }
                return 0;
            }
        }

        #endregion

        #region Looks

        private const int StateReady = 0;
        private const int StateCooling = 1;
        private const int StateWorld = 2;
        private const int StateFetching = 3;

        // What one block shows; two equal looks draw the same pixels.
        private struct Look : IEquatable<Look>
        {
            public int State;

            // The left button: what it runs (null when it waits on a cooldown), its word, the share
            // of it the red bar still covers, and the time left written on it.
            public string Action;
            public string Word;
            public float Fill;
            public string Time;

            // The right button removes, and only while the machine is in the world.
            public bool CanRemove;

            // When the cooldown ends, on the realtime clock, and how long it is; not part of the look.
            public float Ends;
            public float Total;

            public bool Equals(Look other)
            {
                return State == other.State && Action == other.Action && Word == other.Word && Mathf.Approximately(Fill, other.Fill) &&
                       Time == other.Time && CanRemove == other.CanRemove;
            }
        }

        private Look LookOf(BasePlayer player, Machine machine, string code)
        {
            Look look = new Look();
            bool inWorld = VehicleOf(player, machine) != null;

            double total;
            double left = CooldownLeft(player, machine, inWorld, out total);

            look.State = inWorld ? (left > 0 ? StateFetching : StateWorld) : left > 0 ? StateCooling : StateReady;
            look.CanRemove = inWorld;

            if (left > 0 && total > 0)
            {
                look.Total = (float)total;
                look.Ends = Time.realtimeSinceStartup + (float)left;
                look.Fill = Drained(left / total);
                look.Time = Clock(left);
            }
            else
            {
                look.Action = inWorld ? "fetch" : "spawn";
                look.Word = T(code, inWorld ? "Button.Fetch" : "Button.Spawn");
            }

            return look;
        }

        // The share of the button still red, moved in whole bar steps so a send is never wasted on
        // an edge that has not visibly moved; exactly full and exactly empty stay so.
        private float Drained(double fraction)
        {
            float f = Mathf.Clamp01((float)fraction);
            if (config.DrainStep <= 0f || f <= 0f || f >= 1f) return f;
            float step = config.DrainStep / ButtonWidth;
            return Mathf.Clamp01(Mathf.Ceil(f / step) * step);
        }

        // 47:12, or 1:00:00 from an hour up; a started second counts as a whole one.
        private static string Clock(double seconds)
        {
            int whole = (int)Math.Ceiling(seconds);
            int hours = whole / 3600, minutes = whole / 60 % 60, rest = whole % 60;
            return hours > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, rest)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", whole / 60, rest);
        }

        // Lime - in the world; orange - in the world, fetch cooling down; red - spawn cooling down;
        // green - ready.
        private static string DotColor(int state)
        {
            switch (state)
            {
                case StateWorld: return Lime;
                case StateFetching: return Orange;
                case StateCooling: return Red;
                default: return Green;
            }
        }

        #endregion

        #region Drawing

        // What a player has on screen: the language it was drawn in and each block's look.
        private sealed class Screen
        {
            public string Code;
            public readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>();
            public float LastClick;
        }

        private readonly Dictionary<string, Screen> screens = new Dictionary<string, Screen>();

        private static string BlockName(Machine machine) => RootName + "." + machine.Key;

        // The whole strip, built from nothing; the client drops the old one in the same message.
        private void Draw(BasePlayer player)
        {
            if (player == null || !player.IsConnected || machines.Count == 0) return;

            Screen screen = new Screen { Code = LanguageOf(player) };
            screens[player.UserIDString] = screen;

            CuiElementContainer elements = new CuiElementContainer();
            elements.Add(new CuiElement
            {
                Name = RootName,
                Parent = Layer,
                DestroyUi = RootName,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = Num(BlocksLeft) + " " + Num(ButtonsBottom),
                        OffsetMax = Num(BlocksLeft + 2 * BlockPitch + BlockWidth) + " " + Num(HeaderTop)
                    }
                }
            });

            for (int i = 0; i < machines.Count; i++)
            {
                Machine machine = machines[i];
                Look look = LookOf(player, machine, screen.Code);
                screen.Looks[machine.Key] = look;
                Block(elements, machine, i * BlockPitch, look, screen.Code);
            }

            CuiHelper.AddUi(player, elements);
        }

        // One block: the header with its icon, name and dot, and the two buttons. Every part that
        // can change has a name of its own, for the updates.
        private void Block(CuiElementContainer elements, Machine machine, float x, Look look, string code)
        {
            string block = BlockName(machine);
            const float height = HeaderTop - ButtonsBottom;
            const float headBottom = HeaderBottom - ButtonsBottom;
            const float buttonsHeight = ButtonsTop - ButtonsBottom;

            Holder(elements, RootName, block, x, 0f, x + BlockWidth, height);

            Image(elements, block, block + ".Head", 0f, headBottom, BlockWidth, height, Plate);
            float iconBottom = headBottom + (height - headBottom - IconHeight) / 2f;
            elements.Add(new CuiElement
            {
                Name = block + ".Icon",
                Parent = block,
                Components =
                {
                    new CuiRawImageComponent { Url = machine.Config.Icon, Color = IconTint, BlocksRaycast = false },
                    At(IconLeft, iconBottom, IconLeft + IconWidth, iconBottom + IconHeight)
                }
            });
            Text(elements, block, block + ".Name", NameLeft, headBottom, BlockWidth - DotRight - DotSize, height, T(code, "Name." + machine.Key), NameSize, Ink,
                 TextAnchor.MiddleLeft);
            float dotBottom = headBottom + (height - headBottom - DotSize) / 2f;
            Image(elements, block, block + ".Dot", BlockWidth - DotRight - DotSize, dotBottom, BlockWidth - DotRight, dotBottom + DotSize, DotColor(look.State));

            // The left button: the face and the red bar lie on its plate, under the button; its word
            // is the button's child.
            string left = block + ".Left";
            string leftButton = Button(elements, block, left, 0f, 0f, ButtonWidth, buttonsHeight, LeftCommand(machine, look), plate =>
            {
                elements.Add(new CuiElement { Name = plate + ".Face", Parent = plate, Components = { new CuiImageComponent { Color = LeftFace(look) }, Stretch() } });
                elements.Add(new CuiElement { Name = plate + ".Fill", Parent = plate, Components = { new CuiImageComponent { Color = Red }, FillRect(look.Fill) } });
            });
            elements.Add(new CuiElement { Name = leftButton + ".Text", Parent = leftButton, Components = { TextOf(LeftText(look), ButtonSize, Ink, TextAnchor.MiddleCenter), Stretch() } });

            // The right button removes.
            string right = block + ".Right";
            string rightButton = Button(elements, block, right, RightLeft, 0f, RightLeft + ButtonWidth, buttonsHeight, RightCommand(machine, look), plate =>
                elements.Add(new CuiElement { Name = plate + ".Face", Parent = plate, Components = { new CuiImageComponent { Color = RightFace(look) }, Stretch() } }));
            elements.Add(new CuiElement
            {
                Name = rightButton + ".Text",
                Parent = rightButton,
                Components = { TextOf(T(code, "Button.Remove"), ButtonSize, look.CanRemove ? Ink : InkOff, TextAnchor.MiddleCenter), Stretch() }
            });
        }

        // Every button, as RustfieldSorter's Sketch.Button draws one: a plate of its own colour, what
        // lies on the plate (faces, bars), then a clear button over all of it, washed on hover and lit
        // when pressed. Words and icons go on the button, which is returned, as its children.
        private static string Button(CuiElementContainer elements, string parent, string name, float x0, float y0, float x1, float y1, string command,
                                     Action<string> under = null)
        {
            Image(elements, parent, name, x0, y0, x1, y1, Plate);
            under?.Invoke(name);

            string button = name + ".Button";
            elements.Add(new CuiElement { Name = button, Parent = name, Components = { ButtonOf(command), Stretch() } });
            return button;
        }

        // Only what differs from what is on screen, with Update set.
        private void Refresh(BasePlayer player)
        {
            Screen screen;
            if (player == null || !player.IsConnected || !screens.TryGetValue(player.UserIDString, out screen)) return;

            string code = LanguageOf(player);
            if (code != screen.Code)
            {
                Draw(player);
                return;
            }

            CuiElementContainer elements = null;

            foreach (Machine machine in machines)
            {
                Look look = LookOf(player, machine, code);
                Look shown;
                if (screen.Looks.TryGetValue(machine.Key, out shown) && shown.Equals(look))
                {
                    screen.Looks[machine.Key] = look;
                    continue;
                }

                screen.Looks[machine.Key] = look;
                if (elements == null) elements = new CuiElementContainer();

                string block = BlockName(machine), left = block + ".Left", right = block + ".Right";

                if (shown.State != look.State) Update(elements, block + ".Dot", new CuiImageComponent { Color = DotColor(look.State) });
                if (!Mathf.Approximately(shown.Fill, look.Fill)) Update(elements, left + ".Fill", FillRect(look.Fill));
                if (shown.Action != look.Action)
                {
                    Update(elements, left + ".Face", new CuiImageComponent { Color = LeftFace(look) });
                    Update(elements, left + ".Button", ButtonOf(LeftCommand(machine, look)));
                }
                if (shown.Time != look.Time || shown.Word != look.Word) Update(elements, left + ".Button.Text", TextOf(LeftText(look), ButtonSize, Ink, TextAnchor.MiddleCenter));

                if (shown.CanRemove != look.CanRemove)
                {
                    Update(elements, right + ".Face", new CuiImageComponent { Color = RightFace(look) });
                    Update(elements, right + ".Button", ButtonOf(RightCommand(machine, look)));
                    Update(elements, right + ".Button.Text", TextOf(T(code, "Button.Remove"), ButtonSize, look.CanRemove ? Ink : InkOff, TextAnchor.MiddleCenter));
                }
            }

            if (elements != null) CuiHelper.AddUi(player, elements);
        }

        // Green when it runs something; under a cooldown's bar only the plate shows.
        private static string LeftFace(Look look) => look.Action == null ? Clear : Green;

        private static string RightFace(Look look) => look.CanRemove ? Red : Clear;

        private string LeftCommand(Machine machine, Look look) => look.Action == null ? null : Command + " " + look.Action + " " + machine.Key;

        private static string LeftText(Look look) => look.Action == null ? look.Time : look.Word;

        private string RightCommand(Machine machine, Look look) => look.CanRemove ? Command + " remove " + machine.Key : null;

        // Clear at rest, washed lighter on hover, lit when pressed.
        private static CuiButtonComponent ButtonOf(string command)
        {
            return new CuiButtonComponent
            {
                Command = command ?? string.Empty,
                Color = Wash,
                NormalColor = Clear,
                HighlightedColor = Lit,
                PressedColor = Lit,
                SelectedColor = Clear,
                DisabledColor = Clear,
                FadeDuration = 0.08f
            };
        }

        private static void Update(CuiElementContainer elements, string name, ICuiComponent component)
        {
            elements.Add(new CuiElement { Name = name, Update = true, Components = { component } });
        }

        // The bar drains right to left: anchored to the button's own rect, so its fixed edges land on
        // the button's pixels whatever the canvas rounds them to.
        private static CuiRectTransformComponent FillRect(float fraction)
        {
            return new CuiRectTransformComponent
            {
                AnchorMin = "0 0",
                AnchorMax = fraction.ToString("0.#####", CultureInfo.InvariantCulture) + " 1",
                OffsetMin = "0 0",
                OffsetMax = "0 0"
            };
        }

        private static void Holder(CuiElementContainer elements, string parent, string name, float x0, float y0, float x1, float y1)
        {
            elements.Add(new CuiElement { Name = name, Parent = parent, Components = { At(x0, y0, x1, y1) } });
        }

        private static void Image(CuiElementContainer elements, string parent, string name, float x0, float y0, float x1, float y1, string color)
        {
            elements.Add(new CuiElement { Name = name, Parent = parent, Components = { new CuiImageComponent { Color = color, Material = color == Plate ? Blur : null }, At(x0, y0, x1, y1) } });
        }

        // One line of text; its rect is 40 wider on the side away from its alignment, so a text a
        // hair too long never wraps onto a cut second line.
        private static void Text(CuiElementContainer elements, string parent, string name, float x0, float y0, float x1, float y1, string text, int size,
                                 string color, TextAnchor align)
        {
            if (align == TextAnchor.MiddleLeft) x1 += 40f;
            elements.Add(new CuiElement { Name = name, Parent = parent, Components = { TextOf(text, size, color, align), At(x0, y0, x1, y1) } });
        }

        private static CuiTextComponent TextOf(string text, int size, string color, TextAnchor align)
        {
            // Clicks go through words: a label stretched over a button would otherwise take them all.
            return new CuiTextComponent { Text = text ?? string.Empty, FontSize = size, Font = Bold, Color = color, Align = align, BlocksRaycast = false };
        }

        private static CuiRectTransformComponent At(float x0, float y0, float x1, float y1)
        {
            return new CuiRectTransformComponent
            {
                AnchorMin = "0 0",
                AnchorMax = "0 0",
                OffsetMin = Num(x0) + " " + Num(y0),
                OffsetMax = Num(x1) + " " + Num(y1)
            };
        }

        private static CuiRectTransformComponent Stretch()
        {
            return new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" };
        }

        private static string Num(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        #endregion

        #region Lifecycle

        private Timer ticker;
        private Timer drainer;

        private void OnServerInitialized()
        {
            ReadWords();
            BuildMachines();

            foreach (BasePlayer player in BasePlayer.activePlayerList) Draw(player);

            ticker = timer.Every(config.Tick, Tick);
            drainer = timer.Every(config.DrainTick, Drain);
        }

        private void Unload()
        {
            ticker?.Destroy();
            drainer?.Destroy();
            foreach (BasePlayer player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, RootName);
            screens.Clear();
        }

        // Between the full looks, only the bars and their times, worked out from when each cooldown
        // ends - no SpawnHeli calls. A cooldown that has run out gets the full look at once.
        private void Drain()
        {
            float now = Time.realtimeSinceStartup;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                Screen screen;
                if (!screens.TryGetValue(player.UserIDString, out screen)) continue;

                CuiElementContainer elements = null;
                bool ended = false;

                foreach (Machine machine in machines)
                {
                    Look look;
                    if (!screen.Looks.TryGetValue(machine.Key, out look) || look.Action != null || look.Total <= 0f) continue;

                    float left = look.Ends - now;
                    if (left <= 0f)
                    {
                        ended = true;
                        continue;
                    }

                    float fill = Drained(left / look.Total);
                    string time = Clock(left);
                    if (Mathf.Approximately(fill, look.Fill) && time == look.Time) continue;

                    if (elements == null) elements = new CuiElementContainer();
                    string name = BlockName(machine) + ".Left";
                    if (!Mathf.Approximately(fill, look.Fill)) Update(elements, name + ".Fill", FillRect(fill));
                    if (time != look.Time) Update(elements, name + ".Button.Text", TextOf(time, ButtonSize, Ink, TextAnchor.MiddleCenter));

                    look.Fill = fill;
                    look.Time = time;
                    screen.Looks[machine.Key] = look;
                }

                if (elements != null) CuiHelper.AddUi(player, elements);
                if (ended) Refresh(player);
            }
        }

        private void Tick()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (screens.ContainsKey(player.UserIDString)) Refresh(player);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player) => Draw(player);

        private void OnPlayerRespawned(BasePlayer player) => Draw(player);

        private void OnPlayerDisconnected(BasePlayer player) => screens.Remove(player.UserIDString);

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin != null && plugin.Name == "SpawnHeli") bound = false;
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin != null && plugin.Name == "SpawnHeli") bound = false;
        }

        #endregion

        #region Commands

        // rfheli spawn|fetch|remove mini|attack|scrap - runs SpawnHeli's chat command as the player
        // when the block on screen offers that action, then looks at the block again.
        [ConsoleCommand(Command)]
        private void OnCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !arg.HasArgs(2)) return;

            Screen screen;
            if (!screens.TryGetValue(player.UserIDString, out screen)) return;
            if (Time.realtimeSinceStartup - screen.LastClick < ClickGap) return;

            string action = arg.GetString(0), key = arg.GetString(1);
            Machine machine = machines.Find(m => m.Key == key);
            if (machine == null) return;

            Look look = LookOf(player, machine, screen.Code);
            string chat;
            switch (action)
            {
                case "spawn":
                case "fetch":
                    if (look.Action != action) return;
                    chat = action == "spawn" ? machine.Config.Spawn : machine.Config.Fetch;
                    break;

                case "remove":
                    if (!look.CanRemove) return;
                    chat = machine.Config.Remove;
                    break;

                default:
                    return;
            }

            if (string.IsNullOrEmpty(chat)) return;

            screen.LastClick = Time.realtimeSinceStartup;
            player.SendConsoleCommand("chat.say /" + chat);

            foreach (float delay in Rechecks) timer.Once(delay, () => Refresh(player));
        }

        #endregion
    }
}
