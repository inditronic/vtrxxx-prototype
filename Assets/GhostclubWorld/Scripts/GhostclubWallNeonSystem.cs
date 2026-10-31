using System.Collections.Generic;
using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// Controls the perimeter edge neon light strips on the club's walls:
    /// - Normal Mode: Static vertical light tubes mounted on pillar monoliths with real point lights illuminating posters.
    /// - Special Effect ("Floating Neon Segments"):
    ///   Each of the 6 wall light strips splits into 4 independent glowing neon segments (24 segments total)
    ///   which detach from the pillars and float, drift and tumble smoothly through the 3D club volume along
    ///   randomized organic wander paths, pulsing to the music beat and casting colored point lights as they float.
    ///   The effect stays active indefinitely until explicitly toggled off (via UI or [X]).
    ///   When deactivated, the segments smoothly fly back to their wall anchor slots, seamlessly restoring the static wall tubes.
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

        [Header("Special Effect: Floating Neon Segments")]
        public bool floatingSegmentsActive = false;
        [Range(0.2f, 3.0f)]
        public float floatSpeed = 1.0f;
        [Range(0.5f, 2.5f)]
        public float segmentGlowBoost = 1.4f;

        [Header("Colors (Strictly Cyber Blue & Crimson Red)")]
        public Color leftWallBaseColor = new Color(0.0f, 0.45f, 1.0f);
        public Color rightWallBaseColor = new Color(1.0f, 0.02f, 0.10f);

        private class SegmentItem
        {
            public GameObject gameObject;
            public Transform transform;
            public Renderer renderer;
            public Material material;
            public Light pointLight;
            public Vector3 homeWorldPos;
            public Quaternion homeWorldRot;
            public Vector3 currentPos;
            public Vector3 targetPos;
            public float wanderTimer;
            public float wanderDuration;
            public Vector3 rotAxis;
            public float rotSpeed;
            public float phaseOffset;
            public bool isLeftWall;
        }

        private class NeonStripItem
        {
            public Renderer originalRenderer;
            public Material originalMaterial;
            public Light originalPointLight;
            public bool isLeftWall;
            public float zPosition;
            public List<SegmentItem> segments = new List<SegmentItem>();
        }

        private List<NeonStripItem> _neonStrips = new List<NeonStripItem>();
        private List<SegmentItem> _allSegments = new List<SegmentItem>();
        private GameObject _floatingRoot;
        private bool _prevFloatingActive = false;

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
            _allSegments.Clear();

            // 1. Prepare clean unscaled root for floating segments
            _floatingRoot = GameObject.Find("Floating_Neon_Segments_Root");
            if (_floatingRoot == null)
            {
                _floatingRoot = new GameObject("Floating_Neon_Segments_Root");
                _floatingRoot.transform.position = Vector3.zero;
                _floatingRoot.transform.rotation = Quaternion.identity;
                _floatingRoot.transform.localScale = Vector3.one;
            }
            else
            {
                // Clean any prior children
                for (int c = _floatingRoot.transform.childCount - 1; c >= 0; c--)
                {
                    var child = _floatingRoot.transform.GetChild(c);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            // 2. Discover all wall strips and attach static wall point lights
            var allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var r in allRenderers)
            {
                string rName = r.gameObject.name.ToLower();
                string mName = (r.sharedMaterial != null) ? r.sharedMaterial.name.ToLower() : "";

                if (rName.Contains("lightstrip_wall") || rName.Contains("dj_front_led") || mName.Contains("neon_blue") || mName.Contains("neon_red"))
                {
                    bool isLeft = rName.Contains("left") || rName.Contains("dj_front_led") || mName.Contains("neon_blue");
                    Light wallLight = null;

                    // Clean any old nested segment cubes on the original transform
                    for (int c = r.transform.childCount - 1; c >= 0; c--)
                    {
                        var ch = r.transform.GetChild(c);
                        if (ch.name.ToLower().Contains("neon_segment"))
                        {
                            if (Application.isPlaying) Destroy(ch.gameObject);
                            else DestroyImmediate(ch.gameObject);
                        }
                    }

                    // Attach static point light for wall illumination
                    if (rName.Contains("lightstrip_wall"))
                    {
                        Transform existingLight = r.transform.Find("Neon_Wall_PointLight");
                        GameObject lightGo = existingLight != null ? existingLight.gameObject : new GameObject("Neon_Wall_PointLight");
                        lightGo.transform.SetParent(r.transform, false);
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
                        originalRenderer = r,
                        originalMaterial = r.material,
                        originalPointLight = wallLight,
                        isLeftWall = isLeft,
                        zPosition = r.transform.position.z
                    };
                    _neonStrips.Add(item);
                }
            }

            // 3. Build 4 floating neon segments for each of the 6 wall strips (24 segments total)
            BuildSegmentsForStrips();
            Debug.Log($"[GhostclubWallNeonSystem] Configured {_neonStrips.Count} neon strips and {_allSegments.Count} floating segments.");
        }

        private void BuildSegmentsForStrips()
        {
            int segmentGlobalIndex = 0;

            for (int s = 0; s < _neonStrips.Count; s++)
            {
                var strip = _neonStrips[s];
                if (strip.originalRenderer == null) continue;

                // Only wall light strips split into 4 segments (leave DJ booth front LED bar static)
                if (!strip.originalRenderer.gameObject.name.ToLower().Contains("lightstrip_wall"))
                    continue;

                strip.segments.Clear();
                Bounds b = strip.originalRenderer.bounds;
                float totalH = Mathf.Max(b.size.y, 3.8f);
                float step = totalH / 4.0f;
                float segHeight = step * 0.86f; // ~0.82m sleek neon bar segment
                Color baseCol = strip.isLeftWall ? leftWallBaseColor : rightWallBaseColor;

                for (int i = 0; i < 4; i++)
                {
                    float segCenterY = (b.size.y > 0.5f) ? (b.min.y + step * (i + 0.5f)) : (1.1f + i * 0.95f);
                    Vector3 homeWorld = new Vector3(strip.originalRenderer.transform.position.x, segCenterY, strip.originalRenderer.transform.position.z);
                    Quaternion homeRot = strip.originalRenderer.transform.rotation;

                    // Create capsule primitive under clean unscaled root
                    GameObject segGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    segGo.name = $"Floating_Neon_Segment_{strip.originalRenderer.gameObject.name}_{i:00}";
                    segGo.transform.SetParent(_floatingRoot.transform, false);
                    segGo.transform.position = homeWorld;
                    segGo.transform.rotation = homeRot;
                    // In Unity, Capsule has diameter 1, height 2.
                    // Scale X=0.14, Z=0.14, Y=(segHeight/2f) gives radius 0.07m, height ~0.82m!
                    segGo.transform.localScale = new Vector3(0.14f, segHeight * 0.5f, 0.14f);

                    // Remove collider so it never obstructs player movement or camera raycasts
                    Collider col = segGo.GetComponent<Collider>();
                    if (col != null)
                    {
                        if (Application.isPlaying) Destroy(col);
                        else DestroyImmediate(col);
                    }

                    // Create glowing emissive material
                    Renderer segRen = segGo.GetComponent<Renderer>();
                    Material segMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (segMat.shader == null || segMat.shader.name == "Hidden/InternalErrorShader")
                    {
                        segMat = new Material(strip.originalMaterial);
                    }
                    segMat.name = $"Mat_NeonSegment_{segmentGlobalIndex:00}";
                    if (segMat.HasProperty("_BaseColor")) segMat.SetColor("_BaseColor", baseCol);
                    if (segMat.HasProperty("_Smoothness")) segMat.SetFloat("_Smoothness", 0.95f);
                    if (segMat.HasProperty("_Metallic")) segMat.SetFloat("_Metallic", 0.2f);
                    segMat.SetColor("_EmissionColor", baseCol * 15f);
                    segMat.EnableKeyword("_EMISSION");
                    segRen.material = segMat;

                    // Attach dedicated real-time point light for illuminating surroundings while floating
                    GameObject lightGo = new GameObject("Segment_PointLight");
                    lightGo.transform.SetParent(segGo.transform, false);
                    lightGo.transform.localPosition = Vector3.zero;

                    Light segLight = lightGo.AddComponent<Light>();
                    segLight.type = LightType.Point;
                    segLight.range = 6.5f;
                    segLight.intensity = 3.8f;
                    segLight.color = baseCol;
                    segLight.shadows = LightShadows.None;

                    // Initially inactive until effect is turned on
                    segGo.SetActive(false);

                    SegmentItem segItem = new SegmentItem
                    {
                        gameObject = segGo,
                        transform = segGo.transform,
                        renderer = segRen,
                        material = segMat,
                        pointLight = segLight,
                        homeWorldPos = homeWorld,
                        homeWorldRot = homeRot,
                        currentPos = homeWorld,
                        targetPos = PickRandomRoomPosition(),
                        wanderTimer = Random.Range(0f, 2.5f),
                        wanderDuration = Random.Range(3.5f, 7.0f),
                        rotAxis = Random.onUnitSphere,
                        rotSpeed = Random.Range(25f, 65f),
                        phaseOffset = Random.Range(0f, Mathf.PI * 2f),
                        isLeftWall = strip.isLeftWall
                    };

                    strip.segments.Add(segItem);
                    _allSegments.Add(segItem);
                    segmentGlobalIndex++;
                }
            }
        }

        private Vector3 PickRandomRoomPosition()
        {
            // Room volume bounds:
            // X: [-4.8 to +4.8] (inside dancefloor and club aisles)
            // Y: [1.3 to 6.2]   (from chest height up towards the 7.6m elevated ceiling)
            // Z: [-5.8 to +5.8] (from front of DJ stage to rear entrance)
            float rx = Random.Range(-4.8f, 4.8f);
            float ry = Random.Range(1.3f, 6.2f);
            float rz = Random.Range(-5.8f, 5.8f);
            return new Vector3(rx, ry, rz);
        }

        void Update()
        {
            if (_neonStrips.Count == 0) return;

            // Detect state change from Inspector or script
            if (floatingSegmentsActive != _prevFloatingActive)
            {
                _prevFloatingActive = floatingSegmentsActive;
                if (floatingSegmentsActive)
                {
                    OnFloatingActivated();
                }
            }

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

            bool isLightActive = neonEnabled && !globalBlackout && brightness > 0.005f;

            if (floatingSegmentsActive)
            {
                // =========================================================================
                // SPECIAL EFFECT ACTIVE: 24 NEON SEGMENTS FLOATING & DRIFTING IN 3D ROOM
                // =========================================================================
                // 1. Hide original static wall tubes and wall point lights
                for (int s = 0; s < _neonStrips.Count; s++)
                {
                    var strip = _neonStrips[s];
                    if (strip.originalRenderer != null && strip.originalRenderer.gameObject.name.ToLower().Contains("lightstrip_wall"))
                    {
                        if (strip.originalRenderer.enabled) strip.originalRenderer.enabled = false;
                        if (strip.originalPointLight != null) strip.originalPointLight.intensity = 0f;
                    }
                }

                // 2. Animate all 24 floating segments
                for (int i = 0; i < _allSegments.Count; i++)
                {
                    var seg = _allSegments[i];
                    if (!seg.gameObject.activeSelf) seg.gameObject.SetActive(true);

                    // Update wander target timer
                    seg.wanderTimer += Time.deltaTime * floatSpeed;
                    if (seg.wanderTimer >= seg.wanderDuration)
                    {
                        seg.wanderTimer = 0f;
                        seg.wanderDuration = Random.Range(3.5f, 7.5f) / Mathf.Max(0.2f, floatSpeed);
                        seg.targetPos = PickRandomRoomPosition();
                    }

                    // Smooth drifting translation toward target with multi-harmonic undulating float wave
                    float bobY = Mathf.Sin(Time.time * (1.6f * floatSpeed) + seg.phaseOffset) * 0.40f;
                    float bobX = Mathf.Cos(Time.time * (1.1f * floatSpeed) + seg.phaseOffset * 1.3f) * 0.35f;
                    float bobZ = Mathf.Sin(Time.time * (1.3f * floatSpeed) + seg.phaseOffset * 2.1f) * 0.35f;

                    seg.currentPos = Vector3.Lerp(seg.currentPos, seg.targetPos, Time.deltaTime * (0.85f * floatSpeed));
                    Vector3 floatOffset = new Vector3(bobX, bobY, bobZ);
                    seg.transform.position = seg.currentPos + floatOffset;

                    // Continuous smooth tumbling rotation in 3D
                    seg.transform.Rotate(seg.rotAxis, seg.rotSpeed * (Time.deltaTime * floatSpeed), Space.World);

                    // Dynamic emission and point light intensity
                    Color baseCol = seg.isLeftWall ? leftWallBaseColor : rightWallBaseColor;
                    if (!isLightActive)
                    {
                        seg.material.SetColor("_EmissionColor", Color.black);
                        if (seg.material.HasProperty("_BaseColor")) seg.material.SetColor("_BaseColor", Color.black);
                        seg.material.DisableKeyword("_EMISSION");
                        if (seg.pointLight != null) seg.pointLight.intensity = 0f;
                    }
                    else
                    {
                        float currentIntensity = brightness * masterDim * 16.0f * segmentGlowBoost;
                        float ptLightInt = brightness * masterDim * 3.8f * segmentGlowBoost;

                        if (audioReactive)
                        {
                            if (energy <= 0.012f)
                            {
                                currentIntensity = 0f;
                                ptLightInt = 0f;
                            }
                            else
                            {
                                float audioBoost = (bass * 2.2f) + (lowMid * 1.1f) + (energy * 0.65f);
                                currentIntensity *= audioBoost;
                                ptLightInt *= Mathf.Clamp(audioBoost, 0.2f, 2.2f);
                            }
                        }

                        Color finalEmissive = baseCol * currentIntensity;
                        if (seg.material.HasProperty("_BaseColor")) seg.material.SetColor("_BaseColor", baseCol);
                        seg.material.SetColor("_EmissionColor", finalEmissive);
                        seg.material.EnableKeyword("_EMISSION");

                        if (seg.pointLight != null)
                        {
                            seg.pointLight.color = baseCol;
                            seg.pointLight.intensity = ptLightInt;
                        }
                    }
                }
            }
            else
            {
                // =========================================================================
                // NORMAL MODE OR RETURNING TO WALL
                // =========================================================================
                bool allReturned = true;

                for (int i = 0; i < _allSegments.Count; i++)
                {
                    var seg = _allSegments[i];
                    if (seg.gameObject.activeSelf)
                    {
                        // Smoothly fly back to wall anchor slot
                        seg.currentPos = Vector3.MoveTowards(seg.currentPos, seg.homeWorldPos, Time.deltaTime * 5.0f * floatSpeed);
                        seg.transform.position = seg.currentPos;
                        seg.transform.rotation = Quaternion.Slerp(seg.transform.rotation, seg.homeWorldRot, Time.deltaTime * 4.5f * floatSpeed);

                        if (Vector3.Distance(seg.transform.position, seg.homeWorldPos) < 0.06f)
                        {
                            seg.transform.position = seg.homeWorldPos;
                            seg.transform.rotation = seg.homeWorldRot;
                            seg.gameObject.SetActive(false);
                        }
                        else
                        {
                            allReturned = false;
                        }
                    }
                }

                // Once segments have docked or are hidden, restore static tubes
                for (int s = 0; s < _neonStrips.Count; s++)
                {
                    var strip = _neonStrips[s];
                    if (strip.originalRenderer == null) continue;

                    if (allReturned && !strip.originalRenderer.enabled)
                    {
                        strip.originalRenderer.enabled = true;
                    }

                    Color baseCol = strip.isLeftWall ? leftWallBaseColor : rightWallBaseColor;
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
                            float wave = Mathf.Sin(Time.time * 5.0f + strip.zPosition * 0.35f) * 0.22f + 0.78f;
                            currentIntensity *= audioBoost * wave;
                            pointLightInt *= Mathf.Clamp(audioBoost * wave, 0.15f, 2.2f);
                        }
                    }

                    Color finalEmissive = baseCol * currentIntensity;
                    Color finalBase = (currentIntensity > 0.01f) ? baseCol : Color.black;

                    if (!isLightActive)
                    {
                        strip.originalMaterial.SetColor("_EmissionColor", Color.black);
                        if (strip.originalMaterial.HasProperty("_BaseColor")) strip.originalMaterial.SetColor("_BaseColor", Color.black);
                        if (strip.originalPointLight != null) strip.originalPointLight.intensity = 0f;
                    }
                    else
                    {
                        if (strip.originalMaterial.HasProperty("_BaseColor")) strip.originalMaterial.SetColor("_BaseColor", finalBase);
                        strip.originalMaterial.SetColor("_EmissionColor", finalEmissive);
                        strip.originalMaterial.EnableKeyword("_EMISSION");

                        if (strip.originalPointLight != null)
                        {
                            strip.originalPointLight.color = baseCol;
                            strip.originalPointLight.intensity = pointLightInt;
                        }
                    }
                }
            }
        }

        private void OnFloatingActivated()
        {
            // When effect starts, activate all segments from their home wall positions
            for (int i = 0; i < _allSegments.Count; i++)
            {
                var seg = _allSegments[i];
                seg.gameObject.SetActive(true);
                seg.currentPos = seg.homeWorldPos;
                seg.transform.position = seg.homeWorldPos;
                seg.transform.rotation = seg.homeWorldRot;
                seg.targetPos = PickRandomRoomPosition();
                seg.wanderTimer = 0f;
            }

            // Hide original static wall tubes
            for (int s = 0; s < _neonStrips.Count; s++)
            {
                var strip = _neonStrips[s];
                if (strip.originalRenderer != null && strip.originalRenderer.gameObject.name.ToLower().Contains("lightstrip_wall"))
                {
                    strip.originalRenderer.enabled = false;
                    if (strip.originalPointLight != null) strip.originalPointLight.intensity = 0f;
                }
            }
        }

        public void ToggleFloatingSegments()
        {
            floatingSegmentsActive = !floatingSegmentsActive;
            _prevFloatingActive = floatingSegmentsActive;
            if (floatingSegmentsActive)
            {
                OnFloatingActivated();
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
