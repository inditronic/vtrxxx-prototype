using System;
using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// Interactive VRSL (VRChat Stage Lighting) Control Panel for Ghostclub 5.0 (80-Avatar VRChat Edition).
    /// Full in-game control for:
    /// - 10 Room-Distributed Moving Heads (ON/OFF toggle, AudioLink movement & intensity reactivity, DMX routines)
    /// - Video-Driven Club Light Mood ('Video-Lichtstimmung' when Moving Heads are off or running)
    /// - Wall Neon Light Strips (Toggle On/Off, AudioLink Reactivity, Brightness)
    /// - Club Fog / Smoke Machine (Toggle On/Off, Density Slider, CO2 Smoke Blast)
    /// - Widescreen Video Wall Stream Player (YouTube & Twitch, Play/Pause/Stop, Demo-Beat / Silence toggle)
    /// Toggleable with 'Tab' or 'L' key, or 'Options' on PS5 Controller. 'M' toggles Moving Heads ON/OFF.
    /// </summary>
    public class GhostclubVRSLControlPanel : MonoBehaviour
    {
        public static GhostclubVRSLControlPanel Instance { get; private set; }

        [Header("State")]
        public bool isOpen = false;

        [Header("References")]
        public GhostclubLightingSystem lightingSystem;
        public GhostclubVideoMoodLighting videoMoodLighting;
        public GhostclubWallNeonSystem wallNeonSystem;
        public GhostclubFloatingVideoWall floatingVideoWall;
        public GhostclubFogSystem fogSystem;
        public GhostclubStreamPlayer streamPlayer;
        public GhostclubAudioLink audioLink;

        private string _streamUrlInput = "https://www.youtube.com/watch?v=WlJ0h3nzOxo";
        private float _masterDimmer = 1.0f;
        private float _volume = 0.85f;
        public int selectedTab = 0; // 0: Lighting, 1: Edge Neon, 2: Fog/Smoke, 3: Video Wall

        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _btnActiveStyle;
        private GUIStyle _btnNormalStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _tabActiveStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _statusStyle;
        private bool _stylesInitialized = false;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (lightingSystem == null) lightingSystem = FindFirstObjectByType<GhostclubLightingSystem>();
            if (videoMoodLighting == null) videoMoodLighting = FindFirstObjectByType<GhostclubVideoMoodLighting>();
            if (wallNeonSystem == null) wallNeonSystem = FindFirstObjectByType<GhostclubWallNeonSystem>();
            if (fogSystem == null) fogSystem = FindFirstObjectByType<GhostclubFogSystem>();
            if (streamPlayer == null) streamPlayer = FindFirstObjectByType<GhostclubStreamPlayer>();
            if (audioLink == null) audioLink = GhostclubAudioLink.Instance;
        }

        void Update()
        {
            bool togglePressed = false;
            bool mPressed = false;
            bool kPressed = false;
            bool nPressed = false;
            bool vPressed = false;
            bool bPressed = false;
            bool xPressed = false;
            bool cPressed = false;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.tabKey.wasPressedThisFrame || kb.lKey.wasPressedThisFrame) togglePressed = true;
                if (kb.mKey.wasPressedThisFrame) mPressed = true;
                if (kb.kKey.wasPressedThisFrame) kPressed = true;
                if (kb.nKey.wasPressedThisFrame) nPressed = true;
                if (kb.vKey.wasPressedThisFrame) vPressed = true;
                if (kb.bKey.wasPressedThisFrame) bPressed = true;
                if (kb.xKey.wasPressedThisFrame) xPressed = true;
                if (kb.cKey.wasPressedThisFrame) cPressed = true;
            }
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null && (pad.selectButton.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame))
            {
                togglePressed = true;
            }
#endif
            if (!togglePressed && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.L))) togglePressed = true;
            if (!mPressed && Input.GetKeyDown(KeyCode.M)) mPressed = true;
            if (!kPressed && Input.GetKeyDown(KeyCode.K)) kPressed = true;
            if (!nPressed && Input.GetKeyDown(KeyCode.N)) nPressed = true;
            if (!vPressed && Input.GetKeyDown(KeyCode.V)) vPressed = true;
            if (!bPressed && Input.GetKeyDown(KeyCode.B)) bPressed = true;
            if (!xPressed && Input.GetKeyDown(KeyCode.X)) xPressed = true;
            if (!cPressed && Input.GetKeyDown(KeyCode.C)) cPressed = true;

            if (togglePressed)
            {
                TogglePanel();
            }

            // Special Effects shortcuts (work both when panel is open and when walking around!)
            if (xPressed && wallNeonSystem != null) wallNeonSystem.ToggleFloatingSegments();
            if (cPressed)
            {
                if (floatingVideoWall == null) floatingVideoWall = FindFirstObjectByType<GhostclubFloatingVideoWall>();
                if (floatingVideoWall != null) floatingVideoWall.ToggleFloating();
            }

            if (!isOpen)
            {
                if (mPressed && lightingSystem != null) lightingSystem.movingHeadsEnabled = !lightingSystem.movingHeadsEnabled;
                if (kPressed && lightingSystem != null) lightingSystem.lasersEnabled = !lightingSystem.lasersEnabled;
                if (nPressed && wallNeonSystem != null) wallNeonSystem.neonEnabled = !wallNeonSystem.neonEnabled;
                if (vPressed && videoMoodLighting != null) videoMoodLighting.videoMoodEnabled = !videoMoodLighting.videoMoodEnabled;
                if (bPressed && lightingSystem != null) lightingSystem.blackoutAll = !lightingSystem.blackoutAll;
            }
        }

        public void TogglePanel()
        {
            isOpen = !isOpen;

            if (isOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void InitStyles()
        {
            if (_stylesInitialized) return;

            Texture2D darkBg = MakeTex(2, 2, new Color(0.04f, 0.05f, 0.09f, 0.96f));
            Texture2D btnNormBg = MakeTex(2, 2, new Color(0.09f, 0.12f, 0.20f, 0.95f));
            Texture2D btnActBg = MakeTex(2, 2, new Color(0.0f, 0.45f, 1.0f, 0.95f));
            Texture2D tabActBg = MakeTex(2, 2, new Color(0.85f, 0.02f, 0.12f, 0.95f));

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = darkBg;
            _windowStyle.onNormal.background = darkBg;
            _windowStyle.padding = new RectOffset(16, 16, 16, 16);

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontSize = 15;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.normal.textColor = new Color(0.0f, 0.85f, 1.0f);
            _headerStyle.alignment = TextAnchor.MiddleCenter;

            _btnNormalStyle = new GUIStyle(GUI.skin.button);
            _btnNormalStyle.normal.background = btnNormBg;
            _btnNormalStyle.normal.textColor = Color.white;
            _btnNormalStyle.fontSize = 11;
            _btnNormalStyle.fontStyle = FontStyle.Bold;

            _btnActiveStyle = new GUIStyle(GUI.skin.button);
            _btnActiveStyle.normal.background = btnActBg;
            _btnActiveStyle.normal.textColor = Color.white;
            _btnActiveStyle.fontSize = 11;
            _btnActiveStyle.fontStyle = FontStyle.Bold;

            _tabStyle = new GUIStyle(GUI.skin.button);
            _tabStyle.normal.background = btnNormBg;
            _tabStyle.normal.textColor = Color.gray;
            _tabStyle.fontSize = 11;
            _tabStyle.fontStyle = FontStyle.Bold;

            _tabActiveStyle = new GUIStyle(GUI.skin.button);
            _tabActiveStyle.normal.background = tabActBg;
            _tabActiveStyle.normal.textColor = Color.white;
            _tabActiveStyle.fontSize = 11;
            _tabActiveStyle.fontStyle = FontStyle.Bold;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.normal.textColor = new Color(0.85f, 0.90f, 1.0f);
            _labelStyle.fontSize = 11;

            _statusStyle = new GUIStyle(GUI.skin.label);
            _statusStyle.normal.textColor = new Color(0.2f, 1.0f, 0.5f);
            _statusStyle.fontSize = 11;
            _statusStyle.fontStyle = FontStyle.Bold;

            _stylesInitialized = true;
        }

        void OnGUI()
        {
            if (!isOpen)
            {
                GUI.color = new Color(0.0f, 0.65f, 1.0f, 0.85f);
                string mhState = (lightingSystem != null && lightingSystem.movingHeadsEnabled && !lightingSystem.blackoutAll) ? "AN" : "AUS";
                string lsState = (lightingSystem != null && lightingSystem.lasersEnabled && !lightingSystem.blackoutAll) ? "AN" : "AUS";
                string wnState = (wallNeonSystem != null && wallNeonSystem.neonEnabled && (lightingSystem == null || !lightingSystem.blackoutAll)) ? "AN" : "AUS";
                string fxState = (wallNeonSystem != null && wallNeonSystem.floatingSegmentsActive) ? "AN" : "AUS";
                string vwState = (floatingVideoWall != null && floatingVideoWall.floatingActive) ? "AN" : "AUS";
                GUI.Label(new Rect(20, Screen.height - 35, 1200, 25), $"[TAB]/[L]: PANEL | [M] Movers: {mhState} | [K] Laser: {lsState} | [N] Wand: {wnState} | [X] Neon-Float: {fxState} | [C] Video-Float: {vwState} | [B] Blackout");
                GUI.color = Color.white;
                return;
            }

            InitStyles();

            float width = 720;
            float height = 580;
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;

            GUI.Window(999, new Rect(x, y, width, height), DrawPanelWindow, "VTRXXX PROTOTYPE (80-AVATAR VRCHAT) — VRSL DMX & VIDEO CONTROL", _windowStyle);
        }

        void DrawPanelWindow(int windowID)
        {
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("LICHT, MOVERS & LASER", selectedTab == 0 ? _tabActiveStyle : _tabStyle, GUILayout.Height(30))) selectedTab = 0;
            if (GUILayout.Button("WAND-LICHTER", selectedTab == 1 ? _tabActiveStyle : _tabStyle, GUILayout.Height(30))) selectedTab = 1;
            if (GUILayout.Button("SPECIAL EFFECTS", selectedTab == 4 ? _tabActiveStyle : _tabStyle, GUILayout.Height(30))) selectedTab = 4;
            if (GUILayout.Button("NEBEL & SMOKE", selectedTab == 2 ? _tabActiveStyle : _tabStyle, GUILayout.Height(30))) selectedTab = 2;
            if (GUILayout.Button("VIDEOWALL PLAYER", selectedTab == 3 ? _tabActiveStyle : _tabStyle, GUILayout.Height(30))) selectedTab = 3;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            switch (selectedTab)
            {
                case 0: DrawLightingTab(); break;
                case 1: DrawEdgeNeonTab(); break;
                case 2: DrawFogTab(); break;
                case 3: DrawMediaTab(); break;
                case 4: DrawSpecialEffectsTab(); break;
            }

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.Label("VTRxxx Prototype VRSL • 80-Avatar VRChat • Special Effects Floating Neon Segments", _labelStyle);
            if (GUILayout.Button("SCHLIESSEN [TAB]", _btnNormalStyle, GUILayout.Width(130), GUILayout.Height(24)))
            {
                TogglePanel();
            }
            GUILayout.EndHorizontal();
        }

        void DrawLightingTab()
        {
            bool moversOn = (lightingSystem != null) ? lightingSystem.movingHeadsEnabled : true;
            bool lasersOn = (lightingSystem != null) ? lightingSystem.lasersEnabled : true;
            bool neonsOn = (wallNeonSystem != null) ? wallNeonSystem.neonEnabled : true;
            bool videoOn = (videoMoodLighting != null) ? videoMoodLighting.videoMoodEnabled : true;
            bool blackout = (lightingSystem != null) ? lightingSystem.blackoutAll : false;
            bool audioReact = (lightingSystem != null) ? lightingSystem.audioReactive : true;

            // 1. Separate Moving Heads & Laser Toggles ("Lass mich Laser und Moving Heads getrennt an und ausschalten")
            GUILayout.Label("Einzelsteuerung der Lichtsysteme (Getrennt schaltbar — Poster werden nur angestrahlt):", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(moversOn ? "✔ MOVING HEADS [M]: AN" : "✖ MOVING HEADS [M]: AUS", (moversOn && !blackout) ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(32)))
            {
                if (lightingSystem != null)
                {
                    lightingSystem.blackoutAll = false;
                    lightingSystem.movingHeadsEnabled = !moversOn;
                }
            }
            if (GUILayout.Button(lasersOn ? "✔ LASER-SCANNER [K]: AN" : "✖ LASER-SCANNER [K]: AUS", (lasersOn && !blackout) ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(32)))
            {
                if (lightingSystem != null)
                {
                    lightingSystem.blackoutAll = false;
                    lightingSystem.lasersEnabled = !lasersOn;
                }
            }
            GUILayout.EndHorizontal();

            // 2. Wall Neons, Video-Mood & Complete Darkness (Blackout) Toggles
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(neonsOn ? "✔ WAND-LICHTER [N]: AN" : "✖ WAND-LICHTER [N]: AUS", (neonsOn && !blackout) ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(28)))
            {
                if (lightingSystem != null) lightingSystem.blackoutAll = false;
                if (wallNeonSystem != null) wallNeonSystem.neonEnabled = !neonsOn;
            }
            if (GUILayout.Button(videoOn ? "✔ VIDEO-LICHT & SCREEN [V]: AN" : "✖ VIDEO-LICHT & SCREEN [V]: AUS", (videoOn && !blackout) ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(28)))
            {
                if (lightingSystem != null) lightingSystem.blackoutAll = false;
                if (videoMoodLighting != null) videoMoodLighting.videoMoodEnabled = !videoOn;
            }
            bool allOff = blackout || (!moversOn && !lasersOn && !neonsOn && !videoOn);
            if (GUILayout.Button(allOff ? "💡 ALLE LICHTER EINSCHALTEN [B]" : "🌑 ALLE LICHTER AUS (RAUM DUNKEL) [B]", allOff ? _tabActiveStyle : _btnNormalStyle, GUILayout.Height(28)))
            {
                if (allOff)
                {
                    if (lightingSystem != null)
                    {
                        lightingSystem.blackoutAll = false;
                        lightingSystem.movingHeadsEnabled = true;
                        lightingSystem.lasersEnabled = true;
                    }
                    if (wallNeonSystem != null) wallNeonSystem.neonEnabled = true;
                    if (videoMoodLighting != null) videoMoodLighting.videoMoodEnabled = true;
                }
                else
                {
                    if (lightingSystem != null)
                    {
                        lightingSystem.movingHeadsEnabled = false;
                        lightingSystem.lasersEnabled = false;
                    }
                    if (wallNeonSystem != null) wallNeonSystem.neonEnabled = false;
                    if (videoMoodLighting != null) videoMoodLighting.videoMoodEnabled = false;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 3. Audio-Reactivity & Quick Audio Test Toggle ("Keine Musik = ganz aus")
            var drone = GhostclubAudioDrone.Instance;
            bool hasMusicNow = (audioLink != null && audioLink.hasAudioSignal);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(audioReact ? "🎵 MUSIK-REAKTIV: [AN]" : "— MUSIK-REAKTIV: [STATISCH]", audioReact ? _btnActiveStyle : _btnNormalStyle, GUILayout.Width(200), GUILayout.Height(26)))
            {
                if (lightingSystem != null) lightingSystem.audioReactive = !audioReact;
                if (wallNeonSystem != null) wallNeonSystem.audioReactive = !audioReact;
            }
            GUILayout.Label(hasMusicNow ? "Musik: AKTIV (Beat-Steuerung)" : "Musik: STILLE (Lichter AUS)", _statusStyle);
            if (drone != null)
            {
                if (GUILayout.Button(drone.droneEnabled ? "🔇 MUSIK STOPPEN (STILLE)" : "🔊 DEMO-BEAT STARTEN", drone.droneEnabled ? _btnNormalStyle : _btnActiveStyle, GUILayout.Width(190), GUILayout.Height(26)))
                {
                    if (drone.droneEnabled || (streamPlayer != null && streamPlayer.isPlaying))
                    {
                        if (streamPlayer != null) streamPlayer.Stop();
                        drone.droneEnabled = false;
                    }
                    else
                    {
                        drone.droneEnabled = true;
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 4. Moving Heads & Laser Master Dimmer
            int headCount = (lightingSystem != null) ? lightingSystem.movingHeads.Count : 10;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{headCount} Movers & Laser Dimmer:", _labelStyle, GUILayout.Width(170));
            float newDim = GUILayout.HorizontalSlider(_masterDimmer, 0f, 1.5f);
            if (!Mathf.Approximately(newDim, _masterDimmer))
            {
                _masterDimmer = newDim;
                if (lightingSystem != null) lightingSystem.masterDimmer = _masterDimmer;
            }
            GUILayout.Label($"{Mathf.RoundToInt(_masterDimmer * 100)}%", _labelStyle, GUILayout.Width(50));
            GUILayout.EndHorizontal();

            // 5. Video-Driven Light Mood ("Video-Lichtstimmung") Slider
            if (videoMoodLighting != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Video-Lichtstimmung (Raum):", _labelStyle, GUILayout.Width(170));
                float newMood = GUILayout.HorizontalSlider(videoMoodLighting.moodIntensityMultiplier, 0f, 3.0f);
                videoMoodLighting.moodIntensityMultiplier = newMood;
                GUILayout.Label($"{Mathf.RoundToInt(newMood * 100)}%", _labelStyle, GUILayout.Width(50));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);

            // 6. DMX Show Routines (Moving Heads)
            GUILayout.Label("Moving Heads DMX Routinen (Streifen über Dancefloor & Wand-Poster):", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CROSS-SWEEP", (lightingSystem != null && lightingSystem.currentRoutine == GhostclubLightingSystem.ShowRoutine.CrossSweep) ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(26)))
            {
                if (lightingSystem != null) lightingSystem.currentRoutine = GhostclubLightingSystem.ShowRoutine.CrossSweep;
            }
            if (GUILayout.Button("FAN-OUT", (lightingSystem != null && lightingSystem.currentRoutine == GhostclubLightingSystem.ShowRoutine.FanOut) ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(26)))
            {
                if (lightingSystem != null) lightingSystem.currentRoutine = GhostclubLightingSystem.ShowRoutine.FanOut;
            }
            if (GUILayout.Button("STAGE-FOCUS", (lightingSystem != null && lightingSystem.currentRoutine == GhostclubLightingSystem.ShowRoutine.StageFocus) ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(26)))
            {
                if (lightingSystem != null) lightingSystem.currentRoutine = GhostclubLightingSystem.ShowRoutine.StageFocus;
            }
            if (GUILayout.Button("SKYWARD-SPIRAL", (lightingSystem != null && lightingSystem.currentRoutine == GhostclubLightingSystem.ShowRoutine.SkywardSpiral) ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(26)))
            {
                if (lightingSystem != null) lightingSystem.currentRoutine = GhostclubLightingSystem.ShowRoutine.SkywardSpiral;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 7. Laser Scan Modes & Color Duo Presets
            GUILayout.Label("Laser-Scanner Modus & Farbpalette (Strikte Cyber-Club Farben — Kein Weiß):", _labelStyle);
            GUILayout.BeginHorizontal();
            if (lightingSystem != null && lightingSystem.stageLaserScanner != null)
            {
                var ls = lightingSystem.stageLaserScanner;
                if (GUILayout.Button("LASER: LIQUID SKY", ls.scanMode == GhostclubLaserScanner.LaserScanMode.LiquidSky ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(24)))
                    ls.scanMode = GhostclubLaserScanner.LaserScanMode.LiquidSky;
                if (GUILayout.Button("LASER: FAN SWEEP", ls.scanMode == GhostclubLaserScanner.LaserScanMode.FanSweep ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(24)))
                    ls.scanMode = GhostclubLaserScanner.LaserScanMode.FanSweep;
                if (GUILayout.Button("LASER: TUNNEL", ls.scanMode == GhostclubLaserScanner.LaserScanMode.ConeTunnel ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(24)))
                    ls.scanMode = GhostclubLaserScanner.LaserScanMode.ConeTunnel;
            }
            if (GUILayout.Button("⚡ STROBE BLITZ", _btnActiveStyle, GUILayout.Height(24), GUILayout.Width(135)))
            {
                TriggerStrobeBurst();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Electric Blue / Crimson", _btnNormalStyle, GUILayout.Height(24))) SetPalette(0);
            if (GUILayout.Button("Cobalt / Ruby Laser", _btnNormalStyle, GUILayout.Height(24))) SetPalette(1);
            if (GUILayout.Button("Neon Indigo / Scarlet", _btnNormalStyle, GUILayout.Height(24))) SetPalette(2);
            if (GUILayout.Button("Cyan-Blue / Blood Red", _btnNormalStyle, GUILayout.Height(24))) SetPalette(3);
            GUILayout.EndHorizontal();
        }

        void DrawEdgeNeonTab()
        {
            if (wallNeonSystem == null) wallNeonSystem = FindFirstObjectByType<GhostclubWallNeonSystem>();

            GUILayout.Label("Perimeter Rand-Lichter (Vertikale Neon-Stripes an den Club-Säulen):", _labelStyle);
            GUILayout.Space(8);

            bool neonOn = (wallNeonSystem != null) ? wallNeonSystem.neonEnabled : true;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Wandlichter Status:", _labelStyle, GUILayout.Width(160));
            if (GUILayout.Button(neonOn ? "✔ EINGESCHALTET (AN)" : "✖ AUSGESCHALTET (AUS)", neonOn ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(32)))
            {
                if (wallNeonSystem != null) wallNeonSystem.ToggleNeon();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            bool audioRec = (wallNeonSystem != null) ? wallNeonSystem.audioReactive : true;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Musik-Reaktion (AudioLink):", _labelStyle, GUILayout.Width(160));
            if (GUILayout.Button(audioRec ? "⚡ MUSIK-REAKTIV (PULSE AKTIV)" : "— STATISCHES LICHT", audioRec ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(32)))
            {
                if (wallNeonSystem != null) wallNeonSystem.ToggleAudioReactivity();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            float curBright = (wallNeonSystem != null) ? wallNeonSystem.brightness : 1.0f;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Helligkeit: {Mathf.RoundToInt(curBright * 100)}%", _labelStyle, GUILayout.Width(160));
            float newBright = GUILayout.HorizontalSlider(curBright, 0.0f, 2.5f);
            if (!Mathf.Approximately(newBright, curBright))
            {
                if (wallNeonSystem != null) wallNeonSystem.SetBrightness(newBright);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(14);
            GUILayout.Label("• Linke Wand-Säulen: Tiefe Cyber-Blue (#0055FF) Röhren", _labelStyle);
            GUILayout.Label("• Rechte Wand-Säulen: Tiefe Blood-Crimson (#FF0518) Röhren", _labelStyle);
            GUILayout.Label("• Die Party-Poster hängen frei in den Beton-Wandfeldern zwischen den Säulen!", _statusStyle);
        }

        void DrawSpecialEffectsTab()
        {
            if (wallNeonSystem == null) wallNeonSystem = FindFirstObjectByType<GhostclubWallNeonSystem>();

            GUILayout.Label("SPECIAL EFFECTS — INTERAKTIVE SHOW-EFFEKTE:", _headerStyle);
            GUILayout.Space(12);

            bool fxOn = (wallNeonSystem != null) ? wallNeonSystem.floatingSegmentsActive : false;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("✨ Wandbeleuchtung: 4-Segment Raum-Schwebung", _headerStyle);
            GUILayout.Label("Teilt jeden der 6 vertikalen Wand-Lichtstäbe in 4 leuchtende Segmente auf (24 Segmente insgesamt).\nDiese lösen sich von den Säulen und schweben sanft & randomisiert durch den 3D-Raum des Clubs,\nrotieren frei, reagieren auf die Musik und werfen farbige Lichtkegel.", _labelStyle);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Effekt-Status:", _labelStyle, GUILayout.Width(130));
            if (GUILayout.Button(fxOn ? "✔ EFFEKT AKTIV [SCHWEBEND] — STOPPEN [X]" : "▶ EFFEKT STARTEN [WANDBELEUCHTUNG TEILEN] [X]", fxOn ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(36)))
            {
                if (wallNeonSystem != null) wallNeonSystem.ToggleFloatingSegments();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (wallNeonSystem != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Schwebe-Geschwindigkeit:", _labelStyle, GUILayout.Width(170));
                float newSpeed = GUILayout.HorizontalSlider(wallNeonSystem.floatSpeed, 0.2f, 3.0f);
                wallNeonSystem.floatSpeed = newSpeed;
                GUILayout.Label($"{newSpeed:F1}x", _labelStyle, GUILayout.Width(50));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Segment Glow-Boost:", _labelStyle, GUILayout.Width(170));
                float newGlow = GUILayout.HorizontalSlider(wallNeonSystem.segmentGlowBoost, 0.5f, 2.5f);
                wallNeonSystem.segmentGlowBoost = newGlow;
                GUILayout.Label($"{Mathf.RoundToInt(newGlow * 100)}%", _labelStyle, GUILayout.Width(50));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("Tipp: Du kannst diesen Effekt jederzeit auch mit der Taste [X] umschalten!", _statusStyle);
            GUILayout.EndVertical();

            GUILayout.Space(14);

            if (floatingVideoWall == null) floatingVideoWall = FindFirstObjectByType<GhostclubFloatingVideoWall>();
            bool wallFxOn = (floatingVideoWall != null) ? floatingVideoWall.floatingActive : false;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("🎬 Video Wall: 24-Shard Raum-Schwebung (Fragmentierung)", _headerStyle);
            GUILayout.Label("Teilt die 16:9 Video Wall in 24 unregelmäßig geformte 3D-Polygon-Shards auf.\nDie Shards lösen sich von der Wand und schweben/trudeln dreidimensional durch den Club,\nwährend das YouTube-Video (oder VJ-Visuals) nahtlos und live auf allen Teilen weiterläuft!", _labelStyle);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Effekt-Status:", _labelStyle, GUILayout.Width(130));
            if (GUILayout.Button(wallFxOn ? "✔ VIDEO WALL SCHWEBEND — ZURÜCK DOCKEN [C]" : "▶ VIDEO WALL TEILEN & RAUM-SCHWEBUNG [C]", wallFxOn ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(36)))
            {
                if (floatingVideoWall != null) floatingVideoWall.ToggleFloating();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (floatingVideoWall != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Schwebe-Geschwindigkeit:", _labelStyle, GUILayout.Width(170));
                float newSpeed = GUILayout.HorizontalSlider(floatingVideoWall.floatSpeed, 0.2f, 3.0f);
                floatingVideoWall.floatSpeed = newSpeed;
                GUILayout.Label($"{newSpeed:F1}x", _labelStyle, GUILayout.Width(50));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Raum-Ausbreitung (Spread):", _labelStyle, GUILayout.Width(170));
                float newSpread = GUILayout.HorizontalSlider(floatingVideoWall.spreadMultiplier, 0.2f, 3.0f);
                floatingVideoWall.spreadMultiplier = newSpread;
                GUILayout.Label($"{newSpread:F1}x", _labelStyle, GUILayout.Width(50));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("3D Rotations-Taumeln:", _labelStyle, GUILayout.Width(170));
                float newRot = GUILayout.HorizontalSlider(floatingVideoWall.rotationMultiplier, 0.0f, 2.0f);
                floatingVideoWall.rotationMultiplier = newRot;
                GUILayout.Label($"{newRot:F1}x", _labelStyle, GUILayout.Width(50));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Bass-Kick / Audio-Reaktivität:", _labelStyle, GUILayout.Width(170));
                float newAudio = GUILayout.HorizontalSlider(floatingVideoWall.audioReactivity, 0.0f, 2.0f);
                floatingVideoWall.audioReactivity = newAudio;
                GUILayout.Label($"{Mathf.RoundToInt(newAudio * 100)}%", _labelStyle, GUILayout.Width(50));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("Tipp: Du kannst diesen Effekt jederzeit auch mit der Taste [C] umschalten!", _statusStyle);
            GUILayout.EndVertical();
        }

        void DrawFogTab()
        {
            if (fogSystem == null) fogSystem = FindFirstObjectByType<GhostclubFogSystem>();

            GUILayout.Label("Club Nebel- & Bühnen-Smoke-System (Volumetrischer Club-Haze):", _labelStyle);
            GUILayout.Space(8);

            bool fOn = (fogSystem != null) ? fogSystem.fogEnabled : true;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Nebel Status:", _labelStyle, GUILayout.Width(160));
            if (GUILayout.Button(fOn ? "✔ NEBEL AKTIV (AN)" : "✖ NEBEL DEAKTIVIERT (AUS)", fOn ? _btnActiveStyle : _btnNormalStyle, GUILayout.Height(32)))
            {
                if (fogSystem != null) fogSystem.ToggleFog();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            float curDensity = (fogSystem != null) ? fogSystem.baseDensity : 0.009f;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Dichte: {curDensity:F4}", _labelStyle, GUILayout.Width(160));
            float newDensity = GUILayout.HorizontalSlider(curDensity, 0.000f, 0.035f);
            if (!Mathf.Approximately(newDensity, curDensity))
            {
                if (fogSystem != null) fogSystem.SetDensity(newDensity);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(16);

            if (GUILayout.Button("💨 BÜHNEN SMOKE / CO2 BLAST AUSLÖSEN", _btnActiveStyle, GUILayout.Height(40)))
            {
                if (fogSystem != null) fogSystem.TriggerSmokeBlast();
            }
        }

        void DrawMediaTab()
        {
            GUILayout.Label("YouTube / Twitch Stream URL (Live auf 16:9 Video Wall, Boden-Reflexion & Raum-Lichtstimmung):", _labelStyle);
            _streamUrlInput = GUILayout.TextField(_streamUrlInput, GUILayout.Height(28));

            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ STREAM LADEN & ABSPIELEN", _btnActiveStyle, GUILayout.Height(32)))
            {
                if (streamPlayer != null) streamPlayer.PlayStream(_streamUrlInput);
            }
            if (GUILayout.Button("⏸ PAUSE (STILLE)", _btnNormalStyle, GUILayout.Height(32), GUILayout.Width(120)))
            {
                if (streamPlayer != null) streamPlayer.Pause();
            }
            if (GUILayout.Button("⏹ STOP (STILLE)", _btnNormalStyle, GUILayout.Height(32), GUILayout.Width(120)))
            {
                if (streamPlayer != null) streamPlayer.Stop();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.Label("Schnell-Auswahl Presets (YouTube / Boiler Room / Club Sets):", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("bed Live (George Tavern)", _btnNormalStyle, GUILayout.Height(26)))
            {
                _streamUrlInput = "https://www.youtube.com/watch?v=b10gQQ_pyQo";
                if (streamPlayer != null) streamPlayer.PlayStream(_streamUrlInput);
            }
            if (GUILayout.Button("GHOSTCLUB 5.0 (0b4k3)", _btnNormalStyle, GUILayout.Height(26)))
            {
                _streamUrlInput = "https://www.youtube.com/watch?v=WlJ0h3nzOxo";
                if (streamPlayer != null) streamPlayer.PlayStream(_streamUrlInput);
            }
            if (GUILayout.Button("Sara Landry", _btnNormalStyle, GUILayout.Height(26)))
            {
                _streamUrlInput = "https://www.youtube.com/watch?v=EIQlDpgAY5Y";
                if (streamPlayer != null) streamPlayer.PlayStream(_streamUrlInput);
            }
            if (GUILayout.Button("Boiler Room", _btnNormalStyle, GUILayout.Height(26)))
            {
                _streamUrlInput = "https://www.youtube.com/watch?v=l-VHXfda5J0";
                if (streamPlayer != null) streamPlayer.PlayStream(_streamUrlInput);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Lautstärke: {Mathf.RoundToInt(_volume * 100)}%", _labelStyle, GUILayout.Width(130));
            float newVol = GUILayout.HorizontalSlider(_volume, 0f, 1f);
            if (!Mathf.Approximately(newVol, _volume))
            {
                _volume = newVol;
                if (streamPlayer != null) streamPlayer.SetVolume(_volume);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            string status = (streamPlayer != null) ? streamPlayer.streamStatus : "Bereit";
            GUILayout.Label($"Status: {status}", _statusStyle);

            GUILayout.Space(6);

            GhostclubAudioLink al = audioLink != null ? audioLink : GhostclubAudioLink.Instance;
            if (al != null)
            {
                DrawAudioMeter("BASS (Kick)", al.bass, new Color(1.0f, 0.05f, 0.15f));
                DrawAudioMeter("LOW-MID (Bassline)", al.lowMid, new Color(1.0f, 0.4f, 0.0f));
                DrawAudioMeter("HIGH-MID (Synths)", al.highMid, new Color(0.0f, 0.7f, 1.0f));
                DrawAudioMeter("TREBLE (Hats/Air)", al.treble, new Color(0.0f, 1.0f, 0.5f));
            }
        }

        void DrawAudioMeter(string name, float value, Color barColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _labelStyle, GUILayout.Width(140));
            Rect r = GUILayoutUtility.GetRect(180, 12);
            GUI.Box(r, "");
            float fillW = r.width * Mathf.Clamp01(value);
            Texture2D fillTex = MakeTex(2, 2, barColor);
            GUI.DrawTexture(new Rect(r.x, r.y, fillW, r.height), fillTex);
            GUILayout.EndHorizontal();
        }

        void SetPalette(int p)
        {
            if (lightingSystem != null)
            {
                lightingSystem.SetPaletteIndex(p);
            }
        }

        void TriggerStrobeBurst()
        {
            if (lightingSystem != null)
            {
                foreach (var s in lightingSystem.strobeLights)
                {
                    if (s != null)
                    {
                        s.color = new Color(1.0f, 0.02f, 0.10f);
                        s.intensity = 20f;
                    }
                }
            }
        }

        Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
