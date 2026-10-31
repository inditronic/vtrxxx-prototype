using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// Generates dynamic real-time brutalist VJ visuals (audio spectrum analyzer, cyber grid, scanlines, glitches)
    /// on the 16:9 Widescreen Video Wall ("Leinwand") directly behind the DJ Booth.
    /// Automatically yields to GhostclubStreamPlayer when a YouTube/Twitch video is playing,
    /// and immediately restores live VJ visuals when stopped or idle so the screen is NEVER black!
    /// Strictly uses Cyber Blue (#0055FF) and Blood Crimson Red (#FF0518) duotones (NO white light!).
    /// </summary>
    public class GhostclubVJScreen : MonoBehaviour
    {
        private static GhostclubVJScreen _instance;
        public static GhostclubVJScreen Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<GhostclubVJScreen>();
                return _instance;
            }
            private set => _instance = value;
        }

        void Awake()
        {
            _instance = this;
        }

        public Renderer targetRenderer;
        public bool screenEnabled = true;
        [Range(0f, 2f)] public float screenBrightness = 1.0f;
        public int textureWidth = 512;
        public int textureHeight = 256;
        public float glitchSpeed = 10f;

        private Texture2D _vjTexture;
        private Color32[] _pixelBuffer;
        private float _timeTracker = 0f;
        private Material _screenMaterial;

        public Texture2D VJTexture => _vjTexture;

        void Start()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            _vjTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            _vjTexture.filterMode = FilterMode.Point;
            _vjTexture.wrapMode = TextureWrapMode.Clamp;
            _pixelBuffer = new Color32[textureWidth * textureHeight];

            if (targetRenderer != null)
            {
                _screenMaterial = targetRenderer.material;
            }

            RenderGlitchFrame();
            _vjTexture.SetPixels32(_pixelBuffer);
            _vjTexture.Apply(false);
            ApplyVJTextureToMaterial();
        }

        public void ApplyVJTextureToMaterial()
        {
            if (_screenMaterial == null && targetRenderer != null)
            {
                _screenMaterial = targetRenderer.material;
            }
            if (_screenMaterial == null || _vjTexture == null) return;

            if (!screenEnabled || screenBrightness <= 0.005f)
            {
                if (_screenMaterial.HasProperty("_BaseColor")) _screenMaterial.SetColor("_BaseColor", Color.black);
                if (_screenMaterial.HasProperty("_Color")) _screenMaterial.SetColor("_Color", Color.black);
                if (_screenMaterial.HasProperty("_EmissionColor")) _screenMaterial.SetColor("_EmissionColor", Color.black);
                return;
            }

            _screenMaterial.mainTexture = _vjTexture;
            if (_screenMaterial.HasProperty("_BaseMap"))
            {
                _screenMaterial.SetTexture("_BaseMap", _vjTexture);
            }
            if (_screenMaterial.HasProperty("_BaseColor"))
            {
                _screenMaterial.SetColor("_BaseColor", Color.white * Mathf.Clamp01(screenBrightness));
            }
            if (_screenMaterial.HasProperty("_Color"))
            {
                _screenMaterial.SetColor("_Color", Color.white * Mathf.Clamp01(screenBrightness));
            }
            if (_screenMaterial.HasProperty("_EmissionMap"))
            {
                _screenMaterial.SetTexture("_EmissionMap", _vjTexture);
                _screenMaterial.SetColor("_EmissionColor", Color.white * (2.2f * screenBrightness));
                _screenMaterial.EnableKeyword("_EMISSION");
            }
        }

        void Update()
        {
            if (_vjTexture == null || _pixelBuffer == null) return;

            if (!screenEnabled || screenBrightness <= 0.005f)
            {
                ApplyVJTextureToMaterial();
                return;
            }

            // If StreamPlayer is actively playing a video stream, let it drive the texture
            if (GhostclubStreamPlayer.Instance != null && GhostclubStreamPlayer.Instance.isPlaying)
            {
                if (_screenMaterial != null)
                {
                    if (_screenMaterial.HasProperty("_BaseColor")) _screenMaterial.SetColor("_BaseColor", Color.white * Mathf.Clamp01(screenBrightness));
                    if (_screenMaterial.HasProperty("_EmissionColor")) _screenMaterial.SetColor("_EmissionColor", Color.white * (2.2f * screenBrightness));
                }
                return;
            }

            _timeTracker += Time.deltaTime * glitchSpeed;
            RenderGlitchFrame();
            _vjTexture.SetPixels32(_pixelBuffer);
            _vjTexture.Apply(false);

            // Ensure our VJ texture is bound whenever no stream is playing
            ApplyVJTextureToMaterial();
        }

        void RenderGlitchFrame()
        {
            int h = textureHeight;
            int w = textureWidth;
            float t = _timeTracker;

            float bassKick = 0.25f;
            float midLevel = 0.2f;
            float trebleLevel = 0.2f;
            if (GhostclubAudioLink.Instance != null)
            {
                bassKick = Mathf.Max(0.20f, GhostclubAudioLink.Instance.bass);
                midLevel = Mathf.Max(0.15f, GhostclubAudioLink.Instance.lowMid);
                trebleLevel = Mathf.Max(0.15f, GhostclubAudioLink.Instance.treble);
            }

            // Precompute 32 vertical spectrum columns across width
            int numCols = 32;
            float[] colHeights = new float[numCols];
            for (int c = 0; c < numCols; c++)
            {
                float normC = (float)c / numCols;
                float n = Mathf.PerlinNoise(normC * 5.5f, t * 0.35f);
                float centerWeight = 1.0f - Mathf.Abs(normC - 0.5f) * 1.4f;
                colHeights[c] = Mathf.Clamp01(0.18f + n * 0.45f + bassKick * 0.38f * Mathf.Max(0.2f, centerWeight));
            }

            float pulseRing = Mathf.Repeat(t * 0.12f, 1.0f);

            for (int y = 0; y < h; y++)
            {
                bool isScanline = (y % 4 == 0);
                float yn = (float)y / h;
                float dy = yn - 0.5f;

                // Border frame check (top/bottom neon frame on the Leinwand)
                bool isHorizBorder = (y < 6 || y >= h - 6);

                // Moving horizontal glitch band
                float band = Mathf.Sin(yn * 16f + t * 0.6f);
                bool isGlitchBand = (band > 0.86f);

                for (int x = 0; x < w; x++)
                {
                    float xn = (float)x / w;
                    float dx = (xn - 0.5f) * 2.0f; // Aspect corrected
                    int idx = y * w + x;

                    // Rich dark cyber-navy base so the screen surface is always distinct from the wall
                    byte r = 8;
                    byte g = 14;
                    byte b = 36;

                    // Subtle background cyber grid lines
                    if (x % 32 == 0 || y % 32 == 0)
                    {
                        r = 15;
                        g = 35;
                        b = 85;
                    }

                    // Bright Neon Outer Border of the Video Wall
                    bool isVertBorder = (x < 6 || x >= w - 6);
                    if (isHorizBorder || isVertBorder)
                    {
                        if (x < w / 2)
                        {
                            r = 0; g = 135; b = 255; // Cyber Blue left frame
                        }
                        else
                        {
                            r = 255; g = 15; b = 45; // Crimson Red right frame
                        }
                        _pixelBuffer[idx] = new Color32(r, g, b, 255);
                        continue;
                    }

                    // 32-Band Symmetric AudioLink Spectrum Analyzer Bars
                    int colIdx = Mathf.Clamp((int)(xn * numCols), 0, numCols - 1);
                    int colLocalX = x % (w / numCols);
                    bool inBarGap = (colLocalX < 2 || colLocalX >= (w / numCols) - 2);
                    float barH = colHeights[colIdx];

                    if (!inBarGap && yn < barH)
                    {
                        float barIntensity = 0.45f + 0.55f * (yn / Mathf.Max(0.01f, barH));
                        // Left half Cyber Blue, Right half Blood Red, alternating on peaks
                        bool redBar = (colIdx >= numCols / 2) ^ (yn > 0.72f);
                        if (redBar)
                        {
                            r = (byte)Mathf.Clamp(255f * barIntensity, 0, 255);
                            g = (byte)Mathf.Clamp(12f * barIntensity, 0, 255);
                            b = (byte)Mathf.Clamp(38f * barIntensity, 0, 255);
                        }
                        else
                        {
                            r = (byte)Mathf.Clamp(10f * barIntensity, 0, 255);
                            g = (byte)Mathf.Clamp(125f * barIntensity, 0, 255);
                            b = (byte)Mathf.Clamp(255f * barIntensity, 0, 255);
                        }
                    }

                    // Center AudioLink Geometric Diamond / Pulse Ring
                    float dist = Mathf.Abs(dx) + Mathf.Abs(dy * 2.0f);
                    float targetRadius = 0.25f + bassKick * 0.35f;
                    if (Mathf.Abs(dist - targetRadius) < 0.035f || Mathf.Abs(dist - pulseRing) < 0.02f)
                    {
                        r = 255;
                        g = 15;
                        b = 45;
                    }
                    else if (Mathf.Abs(dist - (targetRadius * 0.6f)) < 0.03f)
                    {
                        r = 0;
                        g = 150;
                        b = 255;
                    }

                    // High-contrast cyber glitch blocks
                    if (isGlitchBand && ((x + (int)(t * 28)) % 32 < 12))
                    {
                        if ((x / 32) % 2 == 0)
                        {
                            r = 0; g = 140; b = 255;
                        }
                        else
                        {
                            r = 255; g = 10; b = 35;
                        }
                    }

                    // CRT Scanline texture
                    if (isScanline)
                    {
                        r = (byte)(r * 0.65f);
                        g = (byte)(g * 0.65f);
                        b = (byte)(b * 0.65f);
                    }

                    _pixelBuffer[idx] = new Color32(r, g, b, 255);
                }
            }
        }

        void OnDestroy()
        {
            if (_vjTexture != null)
            {
                Destroy(_vjTexture);
            }
        }
    }
}
