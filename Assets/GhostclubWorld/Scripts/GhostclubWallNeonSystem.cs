using System.Collections.Generic;
using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// Controls the perimeter edge neon light strips on the club's left and right walls (plus DJ front LED strip):
    /// - Master Toggle (ON / OFF)
    /// - Real Unity Point Lights along the wall neon tubes so they genuinely illuminate the non-emissive Party Posters & concrete walls
    /// - AudioLink dynamic audio-reactivity (pulses to bass and kicks; 0% when silent)
    /// - When turned OFF, both the neon tubes and their point lights drop to 0 (complete darkness)
    /// </summary>
    public class GhostclubWallNeonSystem : MonoBehaviour
    {
        public static GhostclubWallNeonSystem Instance { get; private set; }

        [Header("State Controls")]
        public bool neonEnabled = true;
        public bool audioReactive = true;
        [Range(0f, 2.5f)]
        public float brightness = 1.0f;
        public GhostclubLightingSystem lightingSystem;

        [Header("Colors (Strictly Blue & Red)")]
        public Color leftWallBaseColor = new Color(0.0f, 0.45f, 1.0f);
        public Color rightWallBaseColor = new Color(1.0f, 0.02f, 0.10f);

        private class NeonStripItem
        {
            public Renderer renderer;
            public Material material;
            public Light pointLight;
            public bool isLeftWall;
            public float zPosition;
        }

        private List<NeonStripItem> _neonStrips = new List<NeonStripItem>();

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (lightingSystem == null) lightingSystem = FindFirstObjectByType<GhostclubLightingSystem>();
            DiscoverWallStrips();
        }

        public void DiscoverWallStrips()
        {
            _neonStrips.Clear();
            var allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var r in allRenderers)
            {
                string rName = r.gameObject.name.ToLower();
                string mName = (r.sharedMaterial != null) ? r.sharedMaterial.name.ToLower() : "";

                if (rName.Contains("lightstrip_wall") || rName.Contains("dj_front_led") || mName.Contains("neon_blue") || mName.Contains("neon_red"))
                {
                    bool isLeft = rName.Contains("left") || rName.Contains("dj_front_led") || mName.Contains("neon_blue");
                    Light wallLight = null;

                    // Attach a real-time Point Light to each wall neon pillar so it illuminates the adjacent non-emissive Party Posters!
                    if (rName.Contains("lightstrip_wall"))
                    {
                        Transform existingLight = r.transform.Find("Neon_Wall_PointLight");
                        GameObject lightGo = existingLight != null ? existingLight.gameObject : new GameObject("Neon_Wall_PointLight");
                        lightGo.transform.SetParent(r.transform, false);
                        // Position slightly inward from the pillar so light washes cleanly across the concrete wall & posters
                        Vector3 worldPos = r.transform.position;
                        float inwardX = (worldPos.x < 0f) ? (worldPos.x + 0.65f) : (worldPos.x - 0.65f);
                        lightGo.transform.position = new Vector3(inwardX, 2.55f, worldPos.z);

                        wallLight = lightGo.GetComponent<Light>();
                        if (wallLight == null) wallLight = lightGo.AddComponent<Light>();
                        wallLight.type = LightType.Point;
                        wallLight.range = 8.8f;
                        wallLight.intensity = 4.5f;
                        wallLight.color = isLeft ? leftWallBaseColor : rightWallBaseColor;
                        wallLight.shadows = LightShadows.None;
                    }

                    NeonStripItem item = new NeonStripItem
                    {
                        renderer = r,
                        material = r.material, // unique instance
                        pointLight = wallLight,
                        isLeftWall = isLeft,
                        zPosition = r.transform.position.z
                    };
                    _neonStrips.Add(item);
                }
            }
            Debug.Log($"[GhostclubWallNeonSystem] Discovered {_neonStrips.Count} neon strips & wall lights.");
        }

        void Update()
        {
            if (_neonStrips.Count == 0) return;

            bool globalBlackout = (lightingSystem != null && (lightingSystem.blackoutAll || lightingSystem.masterDimmer <= 0.005f));
            float masterDim = (lightingSystem != null) ? lightingSystem.masterDimmer : 1.0f;

            float bass = 0f;
            float lowMid = 0f;
            float energy = 1f;
            if (GhostclubAudioLink.Instance != null)
            {
                bass = GhostclubAudioLink.Instance.bass;
                lowMid = GhostclubAudioLink.Instance.lowMid;
                energy = GhostclubAudioLink.Instance.totalEnergy;
            }

            for (int i = 0; i < _neonStrips.Count; i++)
            {
                var item = _neonStrips[i];
                if (item.renderer == null) continue;

                if (!neonEnabled || globalBlackout || brightness <= 0.005f)
                {
                    item.material.SetColor("_EmissionColor", Color.black);
                    if (item.material.HasProperty("_BaseColor")) item.material.SetColor("_BaseColor", Color.black);
                    if (item.pointLight != null) item.pointLight.intensity = 0f;
                    continue;
                }

                Color baseCol = item.isLeftWall ? leftWallBaseColor : rightWallBaseColor;
                float currentIntensity = brightness * masterDim * 14.0f;
                float pointLightInt = brightness * masterDim * 4.8f;

                if (audioReactive)
                {
                    if (energy <= 0.012f)
                    {
                        currentIntensity = 0f;
                        pointLightInt = 0f;
                    }
                    else
                    {
                        float audioBoost = (bass * 2.2f) + (lowMid * 1.1f) + (energy * 0.65f);
                        float wave = Mathf.Sin(Time.time * 5.0f + item.zPosition * 0.35f) * 0.22f + 0.78f;
                        currentIntensity *= audioBoost * wave;
                        pointLightInt *= Mathf.Clamp(audioBoost * wave, 0.15f, 2.2f);
                    }
                }

                Color finalEmissive = baseCol * currentIntensity;
                Color finalBase = (currentIntensity > 0.01f) ? baseCol : Color.black;
                if (item.material.HasProperty("_BaseColor")) item.material.SetColor("_BaseColor", finalBase);
                item.material.SetColor("_EmissionColor", finalEmissive);
                item.material.EnableKeyword("_EMISSION");

                if (item.pointLight != null)
                {
                    item.pointLight.color = baseCol;
                    item.pointLight.intensity = pointLightInt;
                }
            }
        }

        public void ToggleNeon()
        {
            neonEnabled = !neonEnabled;
        }

        public void ToggleAudioReactivity()
        {
            audioReactive = !audioReactive;
        }

        public void SetBrightness(float val)
        {
            brightness = Mathf.Clamp(val, 0f, 2.5f);
        }
    }
}
