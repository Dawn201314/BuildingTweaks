using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace InstantBuild
{
    /// <summary>
    /// InstantBuild is mod for Green Hell that allows the player to instantly build blueprints and finish the ones that are already placed.
    /// Usage: Simply press the shortcut to open settings window (by default it is NumPad8).
    /// Author: OSubMarin
    /// </summary>
    public class InstantBuild : MonoBehaviour
    {
        #region Enums

        public enum MessageType
        {
            Info,
            Warning,
            Error
        }

        #endregion

        #region Constructors/Destructor

        public InstantBuild()
        {
            Instance = this;
        }

        private static InstantBuild Instance;

        public static InstantBuild Get() => InstantBuild.Instance;

        #endregion

        #region Statics

        /// <summary>The name of this mod.</summary>
        private static readonly string ModName = nameof(InstantBuild);

        /// <summary>Default shortcut to show mod settings.</summary>
        private static readonly KeyCode DefaultModKeybindingId_Settings = KeyCode.Keypad8;

        /// <summary>Default shortcut to finish existing blueprints.</summary>
        private static readonly KeyCode DefaultModKeybindingId_Finish = KeyCode.Keypad9;

        private static KeyCode ModKeybindingId_Settings { get; set; } = DefaultModKeybindingId_Settings;

        public static KeyCode ModKeybindingId_Finish { get; set; } = DefaultModKeybindingId_Finish;

        /// <summary>Path to ModAPI runtime configuration file (contains game shortcuts).</summary>
        private static readonly string RuntimeConfigurationFile = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "RuntimeConfiguration.xml");

        /// <summary>Path to InstantBuild mod configuration file (if it does not already exist it will be automatically created on first run).</summary>
        private static readonly string InstantBuildConfigurationFile = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "InstantBuild.txt");

        private static HUDManager LocalHUDManager = null;
        private static Player LocalPlayer = null;

        private static readonly float ModScreenTotalWidth = 550f;
        private static readonly float ModScreenTotalHeight = 100f;
        private static readonly float ModScreenMinWidth = 550f;
        private static readonly float ModScreenMaxWidth = 600f;
        private static readonly float ModScreenMinHeight = 50f;
        private static readonly float ModScreenMaxHeight = 300f;

        public static Rect InstantBuildScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);

        private static float ModScreenStartPositionX { get; set; } = Screen.width / 7f;
        private static float ModScreenStartPositionY { get; set; } = Screen.height / 7f;
        private static bool IsMinimized { get; set; } = false;

        private Color DefaultGuiColor = GUI.color;
        private bool ShowUI = false;

        public static bool InstantBuildEnabled { get; set; } = false;
        private static bool InstantBuildEnabledOrig { get; set; } = false;

        public static bool FinishBlueprintsEnabled { get; set; } = false;
        private static bool FinishBlueprintsEnabledOrig { get; set; } = false;

        public static float FinishBlueprintRadius = 20.0f;
        public static string BlueprintRadiusFinishField = "20";
        public static string BlueprintRadiusFinishFieldOrig = "20";

        public static bool IsWithinRadius(Vector3 objA, Vector3 objB)
            => !(objA.x < (objB.x - InstantBuild.FinishBlueprintRadius) ||
            objA.x > (objB.x + InstantBuild.FinishBlueprintRadius) ||
            objA.y < (objB.y - InstantBuild.FinishBlueprintRadius) ||
            objA.y > (objB.y + InstantBuild.FinishBlueprintRadius) ||
            objA.z < (objB.z - InstantBuild.FinishBlueprintRadius) ||
            objA.z > (objB.z + InstantBuild.FinishBlueprintRadius));

        public static string HUDBigInfoMessage(string message, MessageType messageType, Color? headcolor = null) => $"<color=#{ (headcolor != null ? ColorUtility.ToHtmlStringRGBA(headcolor.Value) : ColorUtility.ToHtmlStringRGBA(Color.red))  }>{messageType}</color>\n{message}";

        private static void ShowHUDBigInfo(string text, float duration = 2f)
        {
            string header = ModName + " Info";
            string textureName = HUDInfoLogTextureType.Reputation.ToString();
            HUDBigInfo obj = (HUDBigInfo)LocalHUDManager.GetHUD(typeof(HUDBigInfo));
            HUDBigInfoData.s_Duration = duration;
            HUDBigInfoData data = new HUDBigInfoData
            {
                m_Header = header,
                m_Text = text,
                m_TextureName = textureName,
                m_ShowTime = Time.time
            };
            obj.AddInfo(data);
            obj.Show(show: true);
        }

        private static void SaveSettings()
        {
            try
            {
                string radiusStr = Convert.ToString((int)FinishBlueprintRadius, CultureInfo.InvariantCulture);
                File.WriteAllText(InstantBuildConfigurationFile, $"InstantBuildFeatureEnabled={(InstantBuildEnabled ? "true" : "false")}\r\nFinishBlueprintsShortcutEnabled={(FinishBlueprintsEnabled ? "true" : "false")}\r\nFinishBlueprintsRadius={radiusStr}\r\n", Encoding.UTF8);
                ModAPI.Log.Write($"[{ModName}:SaveSettings] Configuration saved (Instant build feature: {(InstantBuildEnabled ? "enabled" : "disabled")}. Finish blueprints shortcut: {(FinishBlueprintsEnabled ? "enabled" : "disabled")}. Finish blueprints radius: {radiusStr} meters).");
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write($"[{ModName}:SaveSettings] Exception caught while saving configuration: [{ex.ToString()}].");
            }
        }

        private static void LoadSettings()
        {
            if (!File.Exists(InstantBuildConfigurationFile))
            {
                ModAPI.Log.Write($"[{ModName}:LoadSettings] Configuration file was not found, creating it.");
                SaveSettings();
            }
            else
            {
                ModAPI.Log.Write($"[{ModName}:LoadSettings] Parsing configuration file...");
                string[] lines = null;
                try
                {
                    lines = File.ReadAllLines(InstantBuildConfigurationFile, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    ModAPI.Log.Write($"[{ModName}:LoadSettings] Exception caught while reading configuration file: [{ex.ToString()}].");
                }

                if (lines != null && lines.Length > 0)
                {
                    bool instantBuildFound = false;
                    bool finishBlueprintsFound = false;
                    bool radiusFound = false;

                    foreach (string line in lines)
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            if (line.StartsWith("InstantBuildFeatureEnabled="))
                            {
                                instantBuildFound = true;
                                if (line.Contains("true", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    InstantBuildEnabled = true;
                                    InstantBuildEnabledOrig = true;
                                    if (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster())
                                        Cheats.m_InstantBuild = true;
                                }
                                else
                                {
                                    InstantBuildEnabled = false;
                                    InstantBuildEnabledOrig = false;
                                    if (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster())
                                        Cheats.m_InstantBuild = false;
                                }
                            }
                            else if (line.StartsWith("FinishBlueprintsShortcutEnabled="))
                            {
                                finishBlueprintsFound = true;
                                if (line.Contains("true", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    FinishBlueprintsEnabled = true;
                                    FinishBlueprintsEnabledOrig = true;
                                }
                                else
                                {
                                    FinishBlueprintsEnabled = false;
                                    FinishBlueprintsEnabledOrig = false;
                                }
                            }
                            else if (line.StartsWith("FinishBlueprintsRadius=") && line.Length > "FinishBlueprintsRadius=".Length)
                            {
                                radiusFound = true;
                                string split = line.Substring("FinishBlueprintsRadius=".Length).Trim();
                                if (!string.IsNullOrWhiteSpace(split) && int.TryParse(split, NumberStyles.Integer, CultureInfo.InvariantCulture, out int radius) && radius > 0 && radius <= 2000000)
                                {
                                    FinishBlueprintRadius = (float)radius;
                                    BlueprintRadiusFinishField = Convert.ToString(radius, CultureInfo.InvariantCulture);
                                    BlueprintRadiusFinishFieldOrig = BlueprintRadiusFinishField;
                                }
                                else
                                    ModAPI.Log.Write($"[{ModName}:LoadSettings] Warning: Finish blueprint radius value was not correct (it must be between 1 and 2000000).");
                            }
                        }

                    if (instantBuildFound && finishBlueprintsFound && radiusFound)
                        ModAPI.Log.Write($"[{ModName}:LoadSettings] Successfully parsed configuration file.");
                    else
                        ModAPI.Log.Write($"[{ModName}:LoadSettings] Warning: Parsed configuration file but some values were missing (Found instant build: {(instantBuildFound ? "true" : "false")}. Found finish blueprints: {(finishBlueprintsFound ? "true" : "false")}. Found radius: {(radiusFound ? "true" : "false")}).");
                }
                else
                    ModAPI.Log.Write($"[{ModName}:LoadSettings] Warning: Configuration file was empty. Using default values.");
                ModAPI.Log.Write($"[{ModName}:LoadSettings] Instant build feature: {(InstantBuildEnabled ? "enabled" : "disabled")}. Finish blueprints shortcut: {(FinishBlueprintsEnabled ? "enabled" : "disabled")}. Finish blueprints radius: {Convert.ToString((int)FinishBlueprintRadius, CultureInfo.InvariantCulture)} meters.");
            }
        }

        private static KeyCode GetConfigurableKey(string buttonId, KeyCode defaultValue)
        {
            if (File.Exists(RuntimeConfigurationFile))
            {
                string[] lines = null;
                try
                {
                    lines = File.ReadAllLines(RuntimeConfigurationFile);
                }
                catch (Exception ex)
                {
                    ModAPI.Log.Write($"[{ModName}:GetConfigurableKey] Exception caught while reading shortcuts configuration: [{ex.ToString()}].");
                }
                if (lines != null && lines.Length > 0)
                {
                    string sttDelim = "<Button ID=\"" + buttonId + "\">";
                    string endDelim = "</Button>";
                    foreach (string line in lines)
                    {
                        if (line.Contains(sttDelim) && line.Contains(endDelim))
                        {
                            int stt = line.IndexOf(sttDelim);
                            if ((stt >= 0) && (line.Length > (stt + sttDelim.Length)))
                            {
                                string split = line.Substring(stt + sttDelim.Length);
                                if (split != null && split.Contains(endDelim))
                                {
                                    int end = split.IndexOf(endDelim);
                                    if ((end > 0) && (split.Length > end))
                                    {
                                        string parsed = split.Substring(0, end);
                                        if (!string.IsNullOrEmpty(parsed))
                                        {
                                            parsed = parsed.Replace("NumPad", "Keypad").Replace("Oem", "");
                                            if (!string.IsNullOrEmpty(parsed) && Enum.TryParse<KeyCode>(parsed, true, out KeyCode parsedKey))
                                            {
                                                ModAPI.Log.Write($"[{ModName}:GetConfigurableKey] Shortcut for \"{buttonId}\" has been parsed ({parsed}).");
                                                return parsedKey;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            ModAPI.Log.Write($"[{ModName}:GetConfigurableKey] Could not parse shortcut for \"{buttonId}\". Using default value ({defaultValue.ToString()}).");
            return defaultValue;
        }

        #endregion


        #region UI methods

        private void InitWindow()
        {
            int wid = GetHashCode();
            InstantBuildScreen = GUILayout.Window(wid,
                InstantBuildScreen,
                InitInstantBuildScreen,
                "Instant Build mod v1.0, by OSubMarin",
                GUI.skin.window,
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(ModScreenMinWidth),
                GUILayout.MaxWidth(ModScreenMaxWidth),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(ModScreenMinHeight),
                GUILayout.MaxHeight(ModScreenMaxHeight));
        }

        private void InitData()
        {
            LocalHUDManager = HUDManager.Get();
            LocalPlayer = Player.Get();
        }

        private void InitInstantBuildScreen(int windowID)
        {
            ModScreenStartPositionX = InstantBuildScreen.x;
            ModScreenStartPositionY = InstantBuildScreen.y;

            using (var modContentScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                ScreenMenuBox();
                if (!IsMinimized)
                    ModOptionsBox();
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void ScreenMenuBox()
        {
            if (GUI.Button(new Rect(InstantBuildScreen.width - 40f, 0f, 20f, 20f), "-", GUI.skin.button))
                CollapseWindow();

            if (GUI.Button(new Rect(InstantBuildScreen.width - 20f, 0f, 20f, 20f), "X", GUI.skin.button))
                CloseWindow();
        }

        private void ModOptionsBox()
        {
            if (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster())
            {
                using (var optionsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUIStyle descriptionStyle = new GUIStyle(GUI.skin.label);
                    descriptionStyle.fontStyle = FontStyle.Italic;
                    descriptionStyle.fontSize = descriptionStyle.fontSize - 2;
                    GUIStyle boldDescriptionStyle = new GUIStyle(descriptionStyle);
                    boldDescriptionStyle.fontStyle = FontStyle.BoldAndItalic;

                    InstantBuildEnabled = GUILayout.Toggle(InstantBuildEnabled, "Enable \"instant build\" feature?", GUI.skin.toggle);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("This will instantly build any blueprint ", descriptionStyle);
                    GUILayout.Label("when you place it", boldDescriptionStyle);
                    GUILayout.Label(".", descriptionStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(15.0f);
                    FinishBlueprintsEnabled = GUILayout.Toggle(FinishBlueprintsEnabled, "Enable \"finish existing blueprints\" shortcut?", GUI.skin.toggle);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Shortcut \"{ModKeybindingId_Finish.ToString()}\" will instantly build ", descriptionStyle);
                    GUILayout.Label("already placed blueprints", boldDescriptionStyle);
                    GUILayout.Label(".", descriptionStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    if (InstantBuildEnabled != InstantBuildEnabledOrig)
                    {
                        InstantBuildEnabledOrig = InstantBuildEnabled;
                        if (InstantBuildEnabled)
                        {
                            if (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster())
                                Cheats.m_InstantBuild = true;
                            ShowHUDBigInfo(HUDBigInfoMessage("Instant build feature enabled.", MessageType.Info, Color.green));
                            ModAPI.Log.Write($"[{ModName}:ModOptionsBox] Instant build feature has been enabled.");
                        }
                        else
                        {
                            if (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster())
                                Cheats.m_InstantBuild = false;
                            ShowHUDBigInfo(HUDBigInfoMessage("Instant build feature disabled.", MessageType.Info, Color.red));
                            ModAPI.Log.Write($"[{ModName}:ModOptionsBox] Instant build feature has been disabled.");
                        }
                        SaveSettings();
                    }
                    if (FinishBlueprintsEnabled != FinishBlueprintsEnabledOrig)
                    {
                        FinishBlueprintsEnabledOrig = FinishBlueprintsEnabled;
                        if (FinishBlueprintsEnabled)
                        {
                            ShowHUDBigInfo(HUDBigInfoMessage("Finish blueprints shortcut enabled.", MessageType.Info, Color.green));
                            ModAPI.Log.Write($"[{ModName}:ModOptionsBox] Finish blueprints shortcut \"{DefaultModKeybindingId_Finish.ToString()}\" has been enabled.");
                        }
                        else
                        {
                            ShowHUDBigInfo(HUDBigInfoMessage("Finish blueprints shortcut disabled.", MessageType.Info, Color.red));
                            ModAPI.Log.Write($"[{ModName}:ModOptionsBox] Finish blueprints shortcut \"{DefaultModKeybindingId_Finish.ToString()}\" has been disabled.");
                        }
                        SaveSettings();
                        InitWindow();
                    }
                    if (FinishBlueprintsEnabled)
                    {
                        GUILayout.Space(15.0f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Finish blueprints radius: ", GUI.skin.label);
                        BlueprintRadiusFinishField = GUILayout.TextField(BlueprintRadiusFinishField, 10, GUI.skin.textField, GUILayout.MinWidth(150.0f));
                        GUILayout.Label(" meters", GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("All blueprints within this radius will be built instantly.", descriptionStyle);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        if (!string.IsNullOrWhiteSpace(BlueprintRadiusFinishField))
                            if (BlueprintRadiusFinishField != BlueprintRadiusFinishFieldOrig)
                            {
                                BlueprintRadiusFinishFieldOrig = BlueprintRadiusFinishField;
                                if (int.TryParse(BlueprintRadiusFinishField, NumberStyles.Integer, CultureInfo.InvariantCulture, out int radius) && radius > 0 && radius <= 2000000)
                                {
                                    FinishBlueprintRadius = (float)radius;
                                    ShowHUDBigInfo(HUDBigInfoMessage($"Finish blueprints radius updated ({Convert.ToString(radius, CultureInfo.InvariantCulture)} meters).", MessageType.Info, Color.green));
                                    ModAPI.Log.Write($"[{ModName}:ModOptionsBox] Finish blueprints radius has been updated to {Convert.ToString(radius, CultureInfo.InvariantCulture)} meters.");
                                    SaveSettings();
                                }
                            }
                    }
                }
            }
            else
            {
                using (var optionsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUI.color = Color.yellow;
                    GUILayout.Label($"{ModName} mod only works if you are the host or in singleplayer mode.", GUI.skin.label);
                    GUI.color = DefaultGuiColor;
                }
            }
        }

        private void CollapseWindow()
        {
            if (!IsMinimized)
            {
                InstantBuildScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenMinHeight);
                IsMinimized = true;
            }
            else
            {
                InstantBuildScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
                IsMinimized = false;
            }
            InitWindow();
        }

        private void CloseWindow()
        {
            ShowUI = false;
            EnableCursor(false);
        }

        private void EnableCursor(bool blockPlayer = false)
        {
            CursorManager.Get().ShowCursor(blockPlayer, false);

            if (blockPlayer)
            {
                LocalPlayer.BlockMoves();
                LocalPlayer.BlockRotation();
                LocalPlayer.BlockInspection();
            }
            else
            {
                LocalPlayer.UnblockMoves();
                LocalPlayer.UnblockRotation();
                LocalPlayer.UnblockInspection();
            }
        }

        #endregion

        #region Unity methods

        private void Start()
        {
            ModAPI.Log.Write($"[{ModName}:Start] Initializing {ModName}...");
            InitData();
            ModKeybindingId_Settings = GetConfigurableKey("ShowSettings", DefaultModKeybindingId_Settings);
            ModKeybindingId_Finish = GetConfigurableKey("FinishBlueprints", DefaultModKeybindingId_Finish);
            LoadSettings();
            ModAPI.Log.Write($"[{ModName}:Start] {ModName} initialized.");
        }

        private void OnGUI()
        {
            if (ShowUI)
            {
                InitData();
                GUI.skin = ModAPI.Interface.Skin;
                InitWindow();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(ModKeybindingId_Settings))
            {
                if (!ShowUI)
                {
                    InitData();
                    EnableCursor(true);
                }
                ShowUI = !ShowUI;
                if (!ShowUI)
                    EnableCursor(false);
            }
        }

        #endregion
    }
}
