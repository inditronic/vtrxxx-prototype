using System.Collections.Generic;
using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// Samples the live Video Wall texture (YouTube/Twitch video stream or VJ visuals) in real-time
    /// and drives multi-zone club wash lights so the Video Wall generates the dynamic light mood
    /// ('Lichtstimmung') of the room.
    /// When 'videoMoodEnabled' is false or all lights are turned off, shuts off wash lights, ambient light,
    /// and DJ gear emission so the club room is 100% pitch black!
    /// </summary>
    public class GhostclubVideoMoodLighting : MonoBehaviour
    {
        public Renderer videoWallRenderer;
        public GhostclubVJScreen vjScreen;
        public GhostclubLightingSystem lightingSystem;
        public GhostclubWallNeonSystem wallNeonSystem;
        public ReflectionProbe clubReflectionProbe;

        [Header("Video-Driven Club Wash Lights")]
        public Light leftStageWash;
        public Light centerRoomWash;
        public Light rightStageWash;
        public Light frontFloorBounce;
        public Light rearFloorBounce;

        [Header("Intensity & State")]
        public bool videoMoodEnabled = true;
        [Range(0f, 3f)] public float moodIntensityMultiplier = 1.35f;
        public float colorSmoothSpeed = 9f;

        private RenderTexture _sampleRT;
        private Texture2D _readTex;
        private Color _curLeft = new Color(0f, 0.35f, 0.9f);
        private Color _curCenter = new Color(0.2f, 0.25f, 0.8f);
        private Color _curRight = new Color(0.9f, 0.05f, 0.15f);
        private Color _curBottom = new Color(0.15f, 0.2f, 0.6f);
        private int _frameCounter = 0;

        private class DjEmissiveEntry
        {
            public Material mat;
            public Color origEmission;
            public Color origBase;
        }
        private List<DjEmissiveEntry> _djEmissives = new List<DjEmissiveEntry>();

        void Start()
        {
            _sampleRT = new RenderTexture(16, 9, 0, RenderTextureFormat.ARGB32);
            _sampleRT.Create();
            _readTex = new Texture2D(16, 9, TextureFormat.RGBA32, false);

            if (vjScreen == null) vjScreen = FindFirstObjectByType<GhostclubVJScreen>();
            if (lightingSystem == null) lightingSystem = FindFirstObjectByType<GhostclubLightingSystem>();
            if (wallNeonSystem == null) wallNeonSystem = FindFirstObjectByType<GhostclubWallNeonSystem>();
            if (clubReflectionProbe == null) clubReflectionProbe = FindFirstObjectByType<ReflectionProbe>();

            DiscoverDjGearEmissives();
        }

        void DiscoverDjGearEmissives()
        {
            // DJ equipment is strictly non-emissive and illuminated only by the lighting system
            _djEmissives.Clear();
        }

        void Update()
        {
            bool globalBlackout = (lightingSystem != null && (lightingSystem.blackoutAll || lightingSystem.masterDimmer <= 0.005f));
            float masterDim = (lightingSystem != null) ? lightingSystem.masterDimmer : 1.0f;

            bool audioReactive = (lightingSystem != null) && lightingSystem.audioReactive;
            float energy = (GhostclubAudioLink.Instance != null) ? GhostclubAudioLink.Instance.totalEnergy : 1f;
            bool streamPlaying = (GhostclubStreamPlayer.Instance != null) && GhostclubStreamPlayer.Instance.isPlaying;
            bool hasAudioOrStream = (!audioReactive) || (energy > 0.012f) || streamPlaying;

            bool moversOn = (lightingSystem != null && lightingSystem.movingHeadsEnabled && !globalBlackout && hasAudioOrStream);
            bool lasersOn = (lightingSystem != null && lightingSystem.lasersEnabled && !globalBlackout && hasAudioOrStream);
            bool neonsOn = (wallNeonSystem != null && wallNeonSystem.neonEnabled && wallNeonSystem.brightness > 0.005f && !globalBlackout && hasAudioOrStream);
            bool videoActive = videoMoodEnabled && (moodIntensityMultiplier > 0.005f) && !globalBlackout && hasAudioOrStream;

            bool anyClubLightOn = moversOn || lasersOn || neonsOn || videoActive;

            // Keep ambient light strictly 0 (black) so posters & walls never glow on their own, while keeping subtle dark-room reflections!
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.reflectionIntensity = anyClubLightOn ? Mathf.Clamp01(masterDim) : 0.22f;

            if (clubReflectionProbe != null)
            {
                clubReflectionProbe.intensity = anyClubLightOn ? (1.35f * Mathf.Clamp01(masterDim)) : 0.25f;
            }



            if (vjScreen != null)
            {
                vjScreen.screenEnabled = videoActive;
                vjScreen.screenBrightness = videoActive ? Mathf.Clamp(masterDim, 0f, 1.5f) : 0f;
            }

            if (!videoActive)
            {
                SetWashLightsIntensity(0f);
                return;
            }

            _frameCounter++;
            if (_frameCounter % 2 == 0)
            {
                SampleVideoWallColors();
            }

            // Boost video light mood when Moving Heads are switched off so the Video Wall carries the room atmosphere!
            float activeMult = moodIntensityMultiplier * masterDim * (!moversOn ? 1.55f : 1.0f);

            if (leftStageWash != null)
            {
                leftStageWash.color = _curLeft;
                leftStageWash.intensity = ComputePerceivedBrightness(_curLeft) * 6.5f * activeMult;
            }
            if (centerRoomWash != null)
            {
                centerRoomWash.color = _curCenter;
                centerRoomWash.intensity = ComputePerceivedBrightness(_curCenter) * 8.5f * activeMult;
            }
            if (rightStageWash != null)
            {
                rightStageWash.color = _curRight;
                rightStageWash.intensity = ComputePerceivedBrightness(_curRight) * 6.5f * activeMult;
            }
            if (frontFloorBounce != null)
            {
                frontFloorBounce.color = _curBottom;
                frontFloorBounce.intensity = ComputePerceivedBrightness(_curBottom) * 5.2f * activeMult;
            }
            if (rearFloorBounce != null)
            {
                Color rearCol = Color.Lerp(_curLeft, _curRight, 0.5f);
                rearFloorBounce.color = rearCol;
                rearFloorBounce.intensity = ComputePerceivedBrightness(rearCol) * 4.5f * activeMult;
            }
        }

        void SampleVideoWallColors()
        {
            if (videoWallRenderer == null || videoWallRenderer.sharedMaterial == null) return;
            if (_sampleRT == null || _readTex == null) return;

            Material mat = videoWallRenderer.sharedMaterial;
            Texture srcTex = null;
            if (mat.HasProperty("_BaseMap")) srcTex = mat.GetTexture("_BaseMap");
            if (srcTex == null) srcTex = mat.mainTexture;
            if (srcTex == null) return;

            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(srcTex, _sampleRT);
            RenderTexture.active = _sampleRT;
            _readTex.ReadPixels(new Rect(0, 0, 16, 9), 0, 0, false);
            _readTex.Apply(false);
            RenderTexture.active = prev;

            Color[] pixels = _readTex.GetPixels();
            Color sumL = Color.black, sumC = Color.black, sumR = Color.black, sumB = Color.black;
            int cntL = 0, cntC = 0, cntR = 0, cntB = 0;

            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    Color c = pixels[y * 16 + x];
                    if (x < 5) { sumL += c; cntL++; }
                    else if (x < 11) { sumC += c; cntC++; }
                    else { sumR += c; cntR++; }

                    if (y < 4) { sumB += c; cntB++; }
                }
            }

            Color targetL = BoostClubSaturation(cntL > 0 ? sumL / cntL : Color.black);
            Color targetC = BoostClubSaturation(cntC > 0 ? sumC / cntC : Color.black);
            Color targetR = BoostClubSaturation(cntR > 0 ? sumR / cntR : Color.black);
            Color targetB = BoostClubSaturation(cntB > 0 ? sumB / cntB : Color.black);

            float dt = Time.deltaTime * colorSmoothSpeed * 2f;
            _curLeft = Color.Lerp(_curLeft, targetL, dt);
            _curCenter = Color.Lerp(_curCenter, targetC, dt);
            _curRight = Color.Lerp(_curRight, targetR, dt);
            _curBottom = Color.Lerp(_curBottom, targetB, dt);
        }

        Color BoostClubSaturation(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s = Mathf.Clamp01(Mathf.Max(s * 1.35f, 0.35f));
            v = Mathf.Clamp01(v * 1.15f);
            return Color.HSVToRGB(h, s, v);
        }

        float ComputePerceivedBrightness(Color c)
        {
            return Mathf.Clamp01(c.r * 0.35f + c.g * 0.45f + c.b * 0.55f);
        }

        void SetWashLightsIntensity(float val)
        {
            if (leftStageWash != null) leftStageWash.intensity = val;
            if (centerRoomWash != null) centerRoomWash.intensity = val;
            if (rightStageWash != null) rightStageWash.intensity = val;
            if (frontFloorBounce != null) frontFloorBounce.intensity = val;
            if (rearFloorBounce != null) rearFloorBounce.intensity = val;
        }

        void OnDestroy()
        {
            if (_sampleRT != null) _sampleRT.Release();
        }
    }
}
