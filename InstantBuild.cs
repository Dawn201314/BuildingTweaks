using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace InstantBuild
{
    /// <summary>
    /// InstantBuild is a mod for Green Hell that allows the player to instantly build blueprints and finish the ones that are already placed.
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

        #region Attributes

        /// <summary>The name of this mod.</summary>
        public static readonly string ModName = nameof(InstantBuild);

        /// <summary>Path to ModAPI runtime configuration file (contains game shortcuts).</summary>
        private static readonly string RuntimeConfigurationFile = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "RuntimeConfiguration.xml");

        /// <summary>Path to InstantBuild mod configuration file (if it does not already exist it will be automatically created on first run).</summary>
        private static readonly string InstantBuildConfigurationFile = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "InstantBuild.txt");

        /// <summary>Default shortcut to show mod settings.</summary>
        private static readonly KeyCode DefaultModKeybindingId_Settings = KeyCode.Keypad8;
        private static KeyCode ModKeybindingId_Settings { get; set; } = DefaultModKeybindingId_Settings;

        /// <summary>Default shortcut to finish existing blueprints.</summary>
        private static readonly KeyCode DefaultModKeybindingId_Finish = KeyCode.Keypad9;
        public static KeyCode ModKeybindingId_Finish { get; set; } = DefaultModKeybindingId_Finish;

        // Game handles.

        private static HUDManager LocalHUDManager = null;
        private static Player LocalPlayer = null;

        // UI attributes.

        private Color DefaultGuiColor = GUI.color;

        private static readonly float ModScreenTotalWidth = 550f;
        private static readonly float ModScreenTotalHeight = 100f;
        private static readonly float ModScreenMinWidth = 550f;
        private static readonly float ModScreenMaxWidth = 600f;
        private static readonly float ModScreenMinHeight = 50f;
        private static readonly float ModScreenMaxHeight = 300f;

        private static float ModScreenStartPositionX { get; set; } = Screen.width / 7f;
        private static float ModScreenStartPositionY { get; set; } = Screen.height / 7f;

        public static Rect InstantBuildScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);

        private bool IsMinimized = false;
        private bool ShowUI = false;

        // Mod option attributes.

        public static bool InstantBuildOrigState = false;

        public static bool InstantBuildEnabled { get; set; } = false; // Binded to GUILayout.Toggle
        private bool InstantBuildEnabledOrig = false;

        public static bool FinishBlueprintsEnabled { get; set; } = false; // Binded to GUILayout.Toggle
        private bool FinishBlueprintsEnabledOrig = false;

        public string BlueprintRadiusFinishField { get; set; } = "20"; // Binded to GUILayout.TextField
        public string BlueprintRadiusFinishFieldOrig = "20";
        public static float FinishBlueprintRadius = 20f;

        // Permission attributes.

        public static readonly string PermissionRequestBegin = "Can I use \"";
        public static readonly string PermissionRequestEnd = "\" mod? (Host can reply \"Allowed\" to give permission)";
        public static readonly string PermissionRequestFinal = PermissionRequestBegin + "Instant Build" + PermissionRequestEnd;

        public static bool DoRequestPermission = false;
        public static bool PermissionGranted = false;
        public static bool PermissionDenied = false;
        public static int NbPermissionRequests = 0;

        public static bool WaitingPermission = false;
        public static long PermissionAskTime = -1L;
        public static long WaitAMinBeforeFirstRequest = -1L;

        public static bool OtherWaitingPermission = false;
        public static long OtherPermissionAskTime = -1L;

        #endregion

        #region Static functions

        public static bool IsWithinRadius(Vector3 objA, Vector3 objB)
            => !(objA.x < (objB.x - InstantBuild.FinishBlueprintRadius) ||
            objA.x > (objB.x + InstantBuild.FinishBlueprintRadius) ||
            objA.y < (objB.y - InstantBuild.FinishBlueprintRadius) ||
            objA.y > (objB.y + InstantBuild.FinishBlueprintRadius) ||
            objA.z < (objB.z - InstantBuild.FinishBlueprintRadius) ||
            objA.z > (objB.z + InstantBuild.FinishBlueprintRadius));

        private static void ShowHUDBigInfo(string text, float duration)
        {
            string header = "Instant Build Info";
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

        public static void ShowHUDInfo(string msg, float duration = 6f, Color? color = null) => ShowHUDBigInfo($"<color=#{ColorUtility.ToHtmlStringRGBA(color != null && color.HasValue ? color.Value : Color.green)}>Info</color>\n{msg}", duration);
        public static void ShowHUDError(string msg, float duration = 6f, Color? color = null) => ShowHUDBigInfo($"<color=#{ColorUtility.ToHtmlStringRGBA(color != null && color.HasValue ? color.Value : Color.red)}>Error</color>\n{msg}", duration);

        public static string ReadNetMessage(P2PNetworkReader reader)
        {
            uint readerPrePos = reader.Position;
            if (readerPrePos >= int.MaxValue) // Failsafe for uint cast to int.
                return null;
            string message = reader.ReadString();
            reader.Seek(-1 * ((int)reader.Position - (int)readerPrePos));
            return message;
        }

        public static void SetInstantBuildInitialState()
        {
            if (InstantBuildEnabled)
            {
                bool isSingleplayerOrMaster = (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster());
                bool hasPermission = (InstantBuild.PermissionGranted && !InstantBuild.PermissionDenied);
                Cheats.m_InstantBuild = (isSingleplayerOrMaster || hasPermission);
#if VERBOSE
                if (Cheats.m_InstantBuild )
                    ModAPI.Log.Write($"[{ModName}:SetInstantBuildInitialState] Instant build feature has been enabled.");
                else
                    ModAPI.Log.Write($"[{ModName}:SetInstantBuildInitialState] Could not enable instant build feature ({(!isSingleplayerOrMaster ? "no permission" : "only available if you are the host or in singleplayer mode")}).");
#endif
            }
            else
            {
                Cheats.m_InstantBuild = false;
#if VERBOSE
                ModAPI.Log.Write($"[{ModName}:SetInstantBuildInitialState] Instant build feature has been disabled.");
#endif
            }
        }

        public static void RestorePermissionStateToOrig()
        {
            InstantBuild.OtherWaitingPermission = false;
            InstantBuild.OtherPermissionAskTime = -1L;
            InstantBuild.WaitingPermission = false;
            InstantBuild.PermissionAskTime = -1L;
            InstantBuild.WaitAMinBeforeFirstRequest = -1L;
            InstantBuild.PermissionDenied = false;
            InstantBuild.PermissionGranted = false;
            InstantBuild.DoRequestPermission = false;
            InstantBuild.NbPermissionRequests = 0;
            Cheats.m_InstantBuild = InstantBuild.InstantBuildOrigState;
#if VERBOSE
            ModAPI.Log.Write($"[{InstantBuild.ModName}:RestorePermissionStateToOrig] Restored initial instant build state to {(InstantBuild.InstantBuildOrigState ? "true" : "false")}.");
#endif
        }

        public static void TextChatRecv(P2PNetworkMessage net_msg)
        {
            try
            {
                if (!InstantBuild.PermissionDenied && !InstantBuild.PermissionGranted && net_msg.m_MsgType == 10 && net_msg.m_ChannelId == 1)
                {
                    bool peerIsMaster = net_msg.m_Connection.m_Peer.IsMaster();
                    if (!peerIsMaster)
                    {
                        string message = ReadNetMessage(net_msg.m_Reader);
                        if (!string.IsNullOrWhiteSpace(message) && message.StartsWith(InstantBuild.PermissionRequestBegin, StringComparison.InvariantCulture) && message.IndexOf(InstantBuild.PermissionRequestEnd, StringComparison.InvariantCultureIgnoreCase) > 0)
                        {
                            InstantBuild.OtherPermissionAskTime = DateTime.Now.Ticks / 10000000L;
                            InstantBuild.OtherWaitingPermission = true;
                        }
                    }
                    if (!InstantBuild.OtherWaitingPermission && InstantBuild.WaitingPermission && peerIsMaster)
                    {
                        string message = ReadNetMessage(net_msg.m_Reader);
                        if (!string.IsNullOrWhiteSpace(message) && string.Compare(message, "Allowed", true, CultureInfo.InvariantCulture) == 0)
                        {
                            InstantBuild.WaitingPermission = false;
                            InstantBuild.PermissionAskTime = -1L;
                            InstantBuild.PermissionGranted = true;
                            ShowHUDInfo("Host gave you permission to use \"Instant Build\" mod.");
#if VERBOSE
                            ModAPI.Log.Write($"[{ModName}:TextChatRecv] Setting initial instant build state from TextChatRecv.");
#endif
                            InstantBuild.SetInstantBuildInitialState();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write($"[{ModName}:TextChatRecv] Exception caught while receiving text chat: [{ex.ToString()}]");
            }
        }

        #endregion

        #region Methods

        private KeyCode GetConfigurableKey(string buttonId, KeyCode defaultValue)
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
                    string sttDelim = $"<Button ID=\"{buttonId}\">";
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

        private void SaveSettings()
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

        private void LoadSettings()
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
                                }
                                else
                                {
                                    InstantBuildEnabled = false;
                                    InstantBuildEnabledOrig = false;
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

                    if (instantBuildFound)
                    {
#if VERBOSE
                        ModAPI.Log.Write($"[{ModName}:LoadSettings] Setting initial instant build state from LoadSettings.");
#endif
                        InstantBuild.SetInstantBuildInitialState();
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

        #endregion

        #region UI methods

        private void InitWindow()
        {
            int wid = GetHashCode();
            InstantBuildScreen = GUILayout.Window(wid,
                InstantBuildScreen,
                InitInstantBuildScreen,
                "Instant Build mod v1.0.0.2, by OSubMarin",
                GUI.skin.window,
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(ModScreenMinWidth),
                GUILayout.MaxWidth(ModScreenMaxWidth),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(ModScreenMinHeight),
                GUILayout.MaxHeight(ModScreenMaxHeight));
        }

        private void GetGameHandles()
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
            bool isSingleplayerOrMaster = (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster());
            bool hasPermission = (InstantBuild.PermissionGranted && !InstantBuild.PermissionDenied);
            if (isSingleplayerOrMaster || hasPermission)
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

                    GUILayout.Space(15f);
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
                            Cheats.m_InstantBuild = true;
                            ShowHUDInfo("Instant build feature enabled.", 4f);
#if VERBOSE
                            ModAPI.Log.Write($"[{ModName}:ModOptionsBox] Instant build feature has been enabled.");
#endif
                        }
                        else
                        {
                            Cheats.m_InstantBuild = false;
                            ShowHUDInfo("Instant build feature disabled.", 4f, Color.yellow);
#if VERBOSE
                            ModAPI.Log.Write($"[{ModName}:ModOptionsBox] Instant build feature has been disabled.");
#endif
                        }
                        SaveSettings();
                    }
                    if (FinishBlueprintsEnabled != FinishBlueprintsEnabledOrig)
                    {
                        FinishBlueprintsEnabledOrig = FinishBlueprintsEnabled;
                        if (FinishBlueprintsEnabled)
                        {
                            ShowHUDInfo("Finish blueprints shortcut enabled.", 4f);
#if VERBOSE
                            ModAPI.Log.Write($"[{ModName}:ModOptionsBox] Finish blueprints shortcut \"{DefaultModKeybindingId_Finish.ToString()}\" has been enabled.");
#endif
                        }
                        else
                        {
                            ShowHUDInfo("Finish blueprints shortcut disabled.", 4f, Color.yellow);
#if VERBOSE
                            ModAPI.Log.Write($"[{ModName}:ModOptionsBox] Finish blueprints shortcut \"{DefaultModKeybindingId_Finish.ToString()}\" has been disabled.");
#endif
                        }
                        SaveSettings();
                        InitWindow();
                    }
                    if (FinishBlueprintsEnabled)
                    {
                        GUILayout.Space(5f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Finish blueprints radius: ", GUI.skin.label);
                        BlueprintRadiusFinishField = GUILayout.TextField(BlueprintRadiusFinishField, 10, GUI.skin.textField, GUILayout.MinWidth(100f));
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
                                    ShowHUDInfo($"Finish blueprints radius updated ({Convert.ToString(radius, CultureInfo.InvariantCulture)} meters).", 2f);
#if VERBOSE
                                    ModAPI.Log.Write($"[{ModName}:ModOptionsBox] Finish blueprints radius has been updated to {Convert.ToString(radius, CultureInfo.InvariantCulture)} meters.");
#endif
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
                    if (!hasPermission && InstantBuild.NbPermissionRequests >= 3)
                    {
                        GUI.color = Color.yellow;
                        GUILayout.Label("Host did not reply to your permission requests or has denied permission to use Instant Build mod.", GUI.skin.label);
                        GUI.color = DefaultGuiColor;
                    }
                    else
                    {
                        GUILayout.Label("It seems that you are not the host. You can ask permission to use Instant Build mod with the button below:", GUI.skin.label);
                        if (GUILayout.Button("Ask permission", GUI.skin.button, GUILayout.MinWidth(120f)))
                            InstantBuild.DoRequestPermission = true;
                    }
                }
            }
        }

        private void CollapseWindow()
        {
            IsMinimized = !IsMinimized;
            if (IsMinimized)
                InstantBuildScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenMinHeight);
            else
                InstantBuildScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
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
            GetGameHandles();
            ModKeybindingId_Settings = GetConfigurableKey("ShowSettings", DefaultModKeybindingId_Settings);
            ModKeybindingId_Finish = GetConfigurableKey("FinishBlueprints", DefaultModKeybindingId_Finish);
            InstantBuild.InstantBuildOrigState = Cheats.m_InstantBuild;
#if VERBOSE
            ModAPI.Log.Write($"[{ModName}:PlayerExtended.Start] Saved initial instant build state ({(InstantBuild.InstantBuildOrigState ? "true" : "false")}).");
#endif
            LoadSettings();
            ModAPI.Log.Write($"[{ModName}:Start] {ModName} initialized.");
        }

        private void OnDestroy()
        {
            InstantBuild.RestorePermissionStateToOrig();
        }

        private void OnGUI()
        {
            if (ShowUI)
            {
                GetGameHandles();
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
                    GetGameHandles();
                    EnableCursor(true);
                }
                ShowUI = !ShowUI;
                if (!ShowUI)
                    EnableCursor(false);
            }
            if (!(P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster() || InstantBuild.PermissionDenied || InstantBuild.PermissionGranted))
            {
                long currTime = DateTime.Now.Ticks / 10000000L;
                if (InstantBuild.WaitingPermission)
                {
                    if ((currTime - InstantBuild.PermissionAskTime) > 56L)
                    {
                        if (InstantBuild.NbPermissionRequests >= 3)
                            InstantBuild.PermissionDenied = true;
                        InstantBuild.WaitingPermission = false;
                        InstantBuild.PermissionAskTime = -1L;
                        ShowHUDInfo($"Host did not reply to your permission request{(InstantBuild.PermissionDenied ? "" : ", please try again")}.", 6f, Color.yellow);
                    }
                }
                if (InstantBuild.OtherWaitingPermission)
                {
                    if ((currTime - InstantBuild.OtherPermissionAskTime) > 59L)
                    {
                        InstantBuild.OtherWaitingPermission = false;
                        InstantBuild.OtherPermissionAskTime = -1L;
                    }
                }
            }
        }

        #endregion
    }
}
