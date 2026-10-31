using System.Collections.Generic;
using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// Special Effect: Floating Video Wall Shards (Fragmentierte Schwebende Video Wall)
    /// - Divides the 16:9 stage Video Wall into 24 irregularly shaped polygonal shards.
    /// - Shards detach from the wall and float/tumble through the 3D volume of the club.
    /// - Live video stream (or VJ screen visuals) continues seamlessly playing across all shards.
    /// - Fully docked state forms the seamless, gapless 16:9 wall.
    /// - Responds to GhostclubAudioLink (bass bounce, audio-reactive emission pulse).
    /// </summary>
    public class GhostclubFloatingVideoWall : MonoBehaviour
    {
        public static GhostclubFloatingVideoWall Instance { get; private set; }

        [Header("Target Videowall")]
        public Renderer targetVideoWallRenderer;

        [Header("Effect Control")]
        public bool floatingActive = false;
        [Range(0.2f, 3.0f)] public float floatSpeed = 1.0f;
        [Range(0.2f, 3.0f)] public float spreadMultiplier = 1.0f;
        [Range(0.0f, 2.0f)] public float rotationMultiplier = 1.0f;
        [Range(0.0f, 2.0f)] public float audioReactivity = 1.0f;
        [Range(0.5f, 3.0f)] public float glowBoost = 1.0f;

        [Header("Materials")]
        public Material shardVideoMaterial;
        public Material shardChassisMaterial;

        // Container
        private GameObject _shardsRoot;
        private List<VideoShard> _shards = new List<VideoShard>();
        private bool _isInitialized = false;
        private bool _prevFloatingActive = false;

        private class VideoShard
        {
            public GameObject gameObject;
            public Transform transform;
            public MeshRenderer renderer;

            // Home / Docking transform
            public Vector3 homeLocalPos;
            public Quaternion homeLocalRot;
            public Vector3 homeWorldPos;
            public Quaternion homeWorldRot;

            // Floating simulation state
            public Vector3 currentPos;
            public Vector3 targetPos;
            public float wanderTimer;
            public float wanderDuration;
            public float phaseOffset;
            public Vector3 rotAxis;
            public float rotSpeed;
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            InitializeFloatingVideoWall();
        }

        public void InitializeFloatingVideoWall()
        {
            if (_isInitialized) return;

            if (targetVideoWallRenderer == null)
            {
                var wallObj = GameObject.Find("VJ_Widescreen_Leinwand");
                if (wallObj != null) targetVideoWallRenderer = wallObj.GetComponent<Renderer>();
            }

            if (targetVideoWallRenderer == null)
            {
                Debug.LogError("[FloatingVideoWall] targetVideoWallRenderer not found!");
                return;
            }

            SetupMaterials();
            BuildShards();
            _isInitialized = true;
        }

        void SetupMaterials()
        {
            // Front face material: URP Lit with Emission for the live video
            if (shardVideoMaterial == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Standard");
                shardVideoMaterial = new Material(litShader);
                shardVideoMaterial.name = "Mat_FloatingVideoShard_Live";
                shardVideoMaterial.SetColor("_BaseColor", Color.white);
                shardVideoMaterial.SetColor("_Color", Color.white);
                shardVideoMaterial.EnableKeyword("_EMISSION");
                shardVideoMaterial.SetColor("_EmissionColor", Color.white * 2.5f);
            }

            // Chassis material (rim & back): sleek industrial steel
            if (shardChassisMaterial == null)
            {
                var steel = Resources.FindObjectsOfTypeAll<Material>();
                foreach (var m in steel)
                {
                    if (m.name == "M_Steel_Industrial")
                    {
                        shardChassisMaterial = m;
                        break;
                    }
                }

                if (shardChassisMaterial == null)
                {
                    Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (litShader == null) litShader = Shader.Find("Standard");
                    shardChassisMaterial = new Material(litShader);
                    shardChassisMaterial.name = "Mat_VideoShard_Chassis";
                    shardChassisMaterial.SetColor("_BaseColor", new Color(0.12f, 0.13f, 0.15f));
                    shardChassisMaterial.SetFloat("_Smoothness", 0.75f);
                    shardChassisMaterial.SetFloat("_Metallic", 0.85f);
                }
            }
        }

        void BuildShards()
        {
            if (_shardsRoot != null) Destroy(_shardsRoot);

            _shardsRoot = new GameObject("Floating_VideoWall_Shards_Root");
            _shardsRoot.transform.position = Vector3.zero;
            _shardsRoot.transform.rotation = Quaternion.identity;
            _shardsRoot.transform.localScale = Vector3.one;

            Transform wallTransform = targetVideoWallRenderer.transform;
            Vector3 wallScale = wallTransform.lossyScale; // (8.20, 4.60, 1.00)

            // We create an irregular grid partition of the local [-0.5, 0.5] x [-0.5, 0.5] quad:
            // 5 columns (0..4) and 4 rows (0..3) = 20 cells.
            // 4 cells are split diagonally into 2 triangles = 24 irregular polygonal shards total!
            int cols = 5;
            int rows = 4;
            Vector2[,] gridNodes = new Vector2[cols + 1, rows + 1];

            // Deterministic pseudo-random offsets for interior nodes
            for (int i = 0; i <= cols; i++)
            {
                for (int j = 0; j <= rows; j++)
                {
                    float u = (float)i / cols;
                    float v = (float)j / rows;

                    float baseX = -0.5f + u;
                    float baseY = -0.5f + v;

                    // Boundary nodes stay strictly on perimeter
                    if (i == 0 || i == cols || j == 0 || j == rows)
                    {
                        gridNodes[i, j] = new Vector2(baseX, baseY);
                    }
                    else
                    {
                        // Interior nodes receive organic angular jitter
                        float jitterX = Mathf.Sin(i * 3.71f + j * 7.13f) * 0.055f;
                        float jitterY = Mathf.Cos(i * 5.27f + j * 2.89f) * 0.065f;
                        gridNodes[i, j] = new Vector2(baseX + jitterX, baseY + jitterY);
                    }
                }
            }

            int shardIndex = 0;

            for (int i = 0; i < cols; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    Vector2 bl = gridNodes[i, j];
                    Vector2 br = gridNodes[i + 1, j];
                    Vector2 tr = gridNodes[i + 1, j + 1];
                    Vector2 tl = gridNodes[i, j + 1];

                    // Select 4 specific cells to split diagonally into triangles (giving 24 total shards)
                    bool splitDiagonal = (i == 0 && j == 1) || (i == 2 && j == 2) || (i == 4 && j == 1) || (i == 3 && j == 3);

                    if (splitDiagonal)
                    {
                        // Triangle 1: bl -> br -> tl
                        CreateSingleShard(new Vector2[] { bl, br, tl }, $"VideoShard_{shardIndex++:D2}_TriA", wallTransform, wallScale);
                        // Triangle 2: br -> tr -> tl
                        CreateSingleShard(new Vector2[] { br, tr, tl }, $"VideoShard_{shardIndex++:D2}_TriB", wallTransform, wallScale);
                    }
                    else
                    {
                        // Irregular quadrilateral: bl -> br -> tr -> tl
                        CreateSingleShard(new Vector2[] { bl, br, tr, tl }, $"VideoShard_{shardIndex++:D2}_Quad", wallTransform, wallScale);
                    }
                }
            }

            Debug.Log($"[FloatingVideoWall] Built {_shards.Count} irregular 3D shards.");
        }

        void CreateSingleShard(Vector2[] quadLocalVerts, string shardName, Transform wallTransform, Vector3 wallScale)
        {
            // 1. Calculate centroid in local Quad space [-0.5, 0.5]
            Vector2 centroid = Vector2.zero;
            for (int k = 0; k < quadLocalVerts.Length; k++) centroid += quadLocalVerts[k];
            centroid /= quadLocalVerts.Length;

            // 2. Build 3D extruded prism mesh (front face + back face + side rims)
            float localThickness = 0.045f; // ~4.5cm panel thickness
            Mesh mesh = GenerateExtrudedShardMesh(quadLocalVerts, centroid, localThickness, wallScale);

            // 3. Create GameObject
            GameObject shardGo = new GameObject(shardName);
            shardGo.transform.SetParent(_shardsRoot.transform, false);

            MeshFilter mf = shardGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = shardGo.AddComponent<MeshRenderer>();
            mr.materials = new Material[] { shardVideoMaterial, shardChassisMaterial };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // 4. Calculate home world position
            // Local Quad position on wall: (centroid.x, centroid.y, 0)
            Vector3 homeLocalPos = new Vector3(centroid.x, centroid.y, 0f);
            Vector3 homeWorldPos = wallTransform.TransformPoint(homeLocalPos);
            Quaternion homeWorldRot = wallTransform.rotation;

            shardGo.transform.position = homeWorldPos;
            shardGo.transform.rotation = homeWorldRot;

            // 5. Build simulation record
            VideoShard shard = new VideoShard
            {
                gameObject = shardGo,
                transform = shardGo.transform,
                renderer = mr,
                homeLocalPos = homeLocalPos,
                homeLocalRot = Quaternion.identity,
                homeWorldPos = homeWorldPos,
                homeWorldRot = homeWorldRot,
                currentPos = homeWorldPos,
                targetPos = homeWorldPos,
                wanderTimer = Random.Range(0f, 3f),
                wanderDuration = Random.Range(4f, 8f),
                phaseOffset = Random.Range(0f, Mathf.PI * 2f),
                rotAxis = Random.onUnitSphere,
                rotSpeed = Random.Range(18f, 42f)
            };

            _shards.Add(shard);
        }

        Mesh GenerateExtrudedShardMesh(Vector2[] polyVerts, Vector2 centroid, float thickness, Vector3 wallScale)
        {
            Mesh mesh = new Mesh();
            mesh.name = "ExtrudedShardMesh";

            int n = polyVerts.Length;
            Vector3[] frontLocalVerts = new Vector3[n];
            Vector2[] uvs = new Vector2[n];

            for (int i = 0; i < n; i++)
            {
                // Scaled relative to centroid so shard center of mass is (0,0,0) in local space
                float relX = (polyVerts[i].x - centroid.x) * wallScale.x;
                float relY = (polyVerts[i].y - centroid.y) * wallScale.y;
                frontLocalVerts[i] = new Vector3(relX, relY, 0f);

                // UVs are mapped to the full [0, 1] x [0, 1] video canvas:
                // Local Quad [-0.5, 0.5] maps to [0, 1]
                uvs[i] = new Vector2(polyVerts[i].x + 0.5f, polyVerts[i].y + 0.5f);
            }

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvList = new List<Vector2>();

            // --- Front Face (Submesh 0) ---
            for (int i = 0; i < n; i++)
            {
                verts.Add(frontLocalVerts[i]);
                normals.Add(new Vector3(0, 0, -1)); // Front normal points into room (towards -Z in local space)
                uvList.Add(uvs[i]);
            }

            List<int> frontTris = new List<int>();
            if (n == 3)
            {
                // Clockwise facing -Z: 0 -> 2 -> 1
                frontTris.Add(0); frontTris.Add(2); frontTris.Add(1);
            }
            else if (n == 4)
            {
                // 0 (BL) -> 2 (TR) -> 1 (BR)
                frontTris.Add(0); frontTris.Add(2); frontTris.Add(1);
                // 0 (BL) -> 3 (TL) -> 2 (TR)
                frontTris.Add(0); frontTris.Add(3); frontTris.Add(2);
            }

            // --- Back Face & Rim (Submesh 1) ---
            List<int> chassisTris = new List<int>();
            int backStart = verts.Count;

            // Back face pushed backward into the wall (+Z in local space)
            for (int i = 0; i < n; i++)
            {
                verts.Add(new Vector3(frontLocalVerts[i].x, frontLocalVerts[i].y, thickness));
                normals.Add(new Vector3(0, 0, 1));
                uvList.Add(Vector2.zero);
            }

            if (n == 3)
            {
                // Reversed winding for back face facing +Z
                chassisTris.Add(backStart + 0); chassisTris.Add(backStart + 1); chassisTris.Add(backStart + 2);
            }
            else if (n == 4)
            {
                chassisTris.Add(backStart + 0); chassisTris.Add(backStart + 1); chassisTris.Add(backStart + 2);
                chassisTris.Add(backStart + 0); chassisTris.Add(backStart + 2); chassisTris.Add(backStart + 3);
            }

            // Side Rim Quads
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vector3 f0 = frontLocalVerts[i];
                Vector3 f1 = frontLocalVerts[next];
                Vector3 b0 = new Vector3(f0.x, f0.y, thickness);
                Vector3 b1 = new Vector3(f1.x, f1.y, thickness);

                // Edge normal pointing outward
                Vector3 edge = f1 - f0;
                Vector3 sideNorm = Vector3.Cross(edge, Vector3.forward).normalized;

                int s = verts.Count;
                verts.Add(f0); normals.Add(sideNorm); uvList.Add(Vector2.zero);
                verts.Add(f1); normals.Add(sideNorm); uvList.Add(Vector2.zero);
                verts.Add(b1); normals.Add(sideNorm); uvList.Add(Vector2.zero);
                verts.Add(b0); normals.Add(sideNorm); uvList.Add(Vector2.zero);

                chassisTris.Add(s + 0); chassisTris.Add(s + 2); chassisTris.Add(s + 1);
                chassisTris.Add(s + 0); chassisTris.Add(s + 3); chassisTris.Add(s + 2);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvList);

            mesh.subMeshCount = 2;
            mesh.SetTriangles(frontTris, 0);
            mesh.SetTriangles(chassisTris, 1);

            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            if (!_isInitialized || _shards.Count == 0) return;

            // Detect state change
            if (floatingActive != _prevFloatingActive)
            {
                _prevFloatingActive = floatingActive;
                if (floatingActive)
                {
                    OnFloatingActivated();
                }
            }

            // Synchronize Live Video / VJ Texture across all shards
            SyncVideoTexture();

            // AudioLink reactivity
            float bass = 0f;
            float totalEnergy = 0f;
            if (GhostclubAudioLink.Instance != null)
            {
                bass = GhostclubAudioLink.Instance.bass;
                totalEnergy = GhostclubAudioLink.Instance.totalEnergy;
            }

            float speed = Mathf.Max(0.1f, floatSpeed);
            float spread = Mathf.Max(0.2f, spreadMultiplier);

            if (floatingActive)
            {
                // =====================================================================
                // SPECIAL EFFECT ACTIVE: 24 VIDEO SHARDS FLOATING & TUMBLING IN 3D ROOM
                // =====================================================================
                if (targetVideoWallRenderer != null && targetVideoWallRenderer.enabled)
                {
                    targetVideoWallRenderer.enabled = false;
                }

                for (int i = 0; i < _shards.Count; i++)
                {
                    var shard = _shards[i];
                    if (!shard.renderer.enabled) shard.renderer.enabled = true;

                    // Periodic room target update
                    shard.wanderTimer += Time.deltaTime * speed;
                    if (shard.wanderTimer >= shard.wanderDuration)
                    {
                        shard.wanderTimer = 0f;
                        shard.wanderDuration = Random.Range(4f, 8f) / speed;
                        shard.targetPos = PickRandomRoomPosition();
                    }

                    // Multi-harmonic smooth floating translation
                    float bobY = Mathf.Sin(Time.time * (1.3f * speed) + shard.phaseOffset) * (0.45f * spread);
                    float bobX = Mathf.Cos(Time.time * (0.9f * speed) + shard.phaseOffset * 1.4f) * (0.40f * spread);
                    float bobZ = Mathf.Sin(Time.time * (1.1f * speed) + shard.phaseOffset * 2.1f) * (0.35f * spread);

                    // Bass Kick audio bounce
                    float bassKick = bass * 0.45f * audioReactivity;

                    shard.currentPos = Vector3.Lerp(shard.currentPos, shard.targetPos, Time.deltaTime * (0.75f * speed));
                    Vector3 floatOffset = new Vector3(bobX, bobY + (bassKick * 0.5f), bobZ + bassKick);
                    shard.transform.position = shard.currentPos + floatOffset;

                    // 3D Tumbling rotation
                    float rotDeg = shard.rotSpeed * rotationMultiplier * (Time.deltaTime * speed);
                    shard.transform.Rotate(shard.rotAxis, rotDeg, Space.World);
                }
            }
            else
            {
                // =====================================================================
                // NORMAL DOCKED MODE: SHARDS RETURN & SEAMLESSLY FORM 16:9 VIDEO WALL
                // =====================================================================
                bool allReturned = true;

                for (int i = 0; i < _shards.Count; i++)
                {
                    var shard = _shards[i];

                    float dist = Vector3.Distance(shard.transform.position, shard.homeWorldPos);
                    float angle = Quaternion.Angle(shard.transform.rotation, shard.homeWorldRot);

                    if (dist > 0.008f || angle > 0.5f)
                    {
                        allReturned = false;
                        shard.transform.position = Vector3.MoveTowards(shard.transform.position, shard.homeWorldPos, Time.deltaTime * (5.5f * speed));
                        shard.transform.rotation = Quaternion.Slerp(shard.transform.rotation, shard.homeWorldRot, Time.deltaTime * (6.0f * speed));
                        shard.currentPos = shard.transform.position;
                    }
                    else
                    {
                        shard.transform.position = shard.homeWorldPos;
                        shard.transform.rotation = shard.homeWorldRot;
                        shard.currentPos = shard.homeWorldPos;
                    }
                }

                // When fully docked, restore main wall renderer
                if (allReturned && targetVideoWallRenderer != null && !targetVideoWallRenderer.enabled)
                {
                    targetVideoWallRenderer.enabled = true;
                    for (int i = 0; i < _shards.Count; i++)
                    {
                        _shards[i].renderer.enabled = false;
                    }
                }
            }
        }

        void OnFloatingActivated()
        {
            for (int i = 0; i < _shards.Count; i++)
            {
                var shard = _shards[i];
                shard.renderer.enabled = true;
                shard.wanderTimer = 0f;
                shard.wanderDuration = Random.Range(3f, 6f);
                shard.targetPos = PickRandomRoomPosition();
            }

            if (targetVideoWallRenderer != null)
            {
                targetVideoWallRenderer.enabled = false;
            }
        }

        Vector3 PickRandomRoomPosition()
        {
            // Room boundaries of Ghostclub 5.0 (over DJ booth and dancefloor)
            // X: [-4.2m, +4.2m]
            // Y: [1.8m, 5.4m]
            // Z: [-7.8m, +2.5m]
            float x = Random.Range(-4.2f, 4.2f);
            float y = Random.Range(1.8f, 5.4f);
            float z = Random.Range(-7.8f, 2.5f);
            return new Vector3(x, y, z);
        }

        void SyncVideoTexture()
        {
            if (shardVideoMaterial == null) return;

            Texture targetTex = null;

            if (GhostclubStreamPlayer.Instance != null && GhostclubStreamPlayer.Instance.isPlaying && GhostclubStreamPlayer.Instance.videoRenderTexture != null)
            {
                targetTex = GhostclubStreamPlayer.Instance.videoRenderTexture;
            }
            else if (GhostclubVJScreen.Instance != null && GhostclubVJScreen.Instance.VJTexture != null)
            {
                targetTex = GhostclubVJScreen.Instance.VJTexture;
            }

            if (targetTex != null && shardVideoMaterial.mainTexture != targetTex)
            {
                shardVideoMaterial.mainTexture = targetTex;
                shardVideoMaterial.SetTexture("_BaseMap", targetTex);
                shardVideoMaterial.SetTexture("_EmissionMap", targetTex);
            }

            // AudioLink boost modulation for emission
            float audioBoost = 1.0f;
            if (GhostclubAudioLink.Instance != null)
            {
                audioBoost += (GhostclubAudioLink.Instance.bass * 0.8f * audioReactivity);
            }

            Color emissionCol = Color.white * (2.5f * glowBoost * audioBoost);
            shardVideoMaterial.SetColor("_EmissionColor", emissionCol);
        }

        public void ToggleFloating()
        {
            SetFloating(!floatingActive);
        }

        public void SetFloating(bool active)
        {
            floatingActive = active;
        }

        void OnDestroy()
        {
            if (_shardsRoot != null) Destroy(_shardsRoot);
            if (shardVideoMaterial != null) Destroy(shardVideoMaterial);
        }
    }
}
