using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// Generates and manages volumetric light beam shafts for club spotlights and Moving Heads.
    /// Synchronizes color, intensity, and direction in real-time with the parent Light component.
    /// Uses a persistent serialized Material asset reference so shaders are never stripped in Standalone builds.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class GhostclubVolumetricBeam : MonoBehaviour
    {
        [Range(0.05f, 2.5f)]
        public float beamAlphaMultiplier = 0.85f;
        public int coneSegments = 28;
        public Material sharedBeamMaterial;

        private Light _light;
        private GameObject _beamObject;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _beamMaterial;
        private MaterialPropertyBlock _propBlock;

        void Start()
        {
            _light = GetComponent<Light>();
            _propBlock = new MaterialPropertyBlock();

            BuildBeamCone();
        }

        void BuildBeamCone()
        {
            if (_beamObject != null) Destroy(_beamObject);

            _beamObject = new GameObject("Volumetric_Beam_Mesh");
            _beamObject.transform.SetParent(transform, false);
            _beamObject.transform.localPosition = Vector3.zero;
            _beamObject.transform.localRotation = Quaternion.identity;

            _meshFilter = _beamObject.AddComponent<MeshFilter>();
            _meshRenderer = _beamObject.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            if (sharedBeamMaterial != null)
            {
                _beamMaterial = new Material(sharedBeamMaterial);
            }
            else
            {
                Shader beamShader = Shader.Find("Ghostclub/VolumetricBeam");
                if (beamShader == null) beamShader = Shader.Find("Sprites/Default");
                if (beamShader == null) beamShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (beamShader != null)
                {
                    _beamMaterial = new Material(beamShader);
                }
            }

            if (_beamMaterial != null)
            {
                _meshRenderer.material = _beamMaterial;
            }

            Mesh mesh = GenerateConeMesh(_light.spotAngle, _light.range * 0.92f, coneSegments);
            _meshFilter.sharedMesh = mesh;
        }

        Mesh GenerateConeMesh(float spotAngleDeg, float length, int segments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "BeamCone";

            float radius = Mathf.Tan(spotAngleDeg * 0.5f * Mathf.Deg2Rad) * length;
            int vertCount = (segments + 1) * 2;
            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            Color[] colors = new Color[vertCount];

            // Top apex ring (near Moving Head lens)
            float topRadius = 0.14f;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Top vertex
                vertices[i] = new Vector3(cos * topRadius, sin * topRadius, 0.05f);
                uvs[i] = new Vector2((float)i / segments, 0f);
                colors[i] = new Color(1f, 1f, 1f, 1f);

                // Bottom vertex
                int baseIdx = i + (segments + 1);
                vertices[baseIdx] = new Vector3(cos * radius, sin * radius, length);
                uvs[baseIdx] = new Vector2((float)i / segments, 1f);
                colors[baseIdx] = new Color(1f, 1f, 1f, 0.0f);
            }

            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int next = i + 1;
                int triIdx = i * 6;

                int tl = i;
                int tr = next;
                int bl = i + (segments + 1);
                int br = next + (segments + 1);

                triangles[triIdx + 0] = tl;
                triangles[triIdx + 1] = tr;
                triangles[triIdx + 2] = bl;

                triangles[triIdx + 3] = tr;
                triangles[triIdx + 4] = br;
                triangles[triIdx + 5] = bl;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        void LateUpdate()
        {
            if (_light == null || _meshRenderer == null) return;

            // Sync beam visibility and color with spotlight
            bool active = _light.enabled && _light.intensity > 0.05f;
            if (_meshRenderer.enabled != active)
            {
                _meshRenderer.enabled = active;
            }
            if (!active) return;

            Color c = _light.color;
            float alpha = Mathf.Clamp01((_light.intensity / 4.2f) * beamAlphaMultiplier);
            c.a = alpha;

            if (_beamMaterial != null)
            {
                if (_beamMaterial.HasProperty("_Color")) _beamMaterial.SetColor("_Color", c);
                if (_beamMaterial.HasProperty("_BaseColor")) _beamMaterial.SetColor("_BaseColor", c);
            }

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            _propBlock.SetColor("_Color", c);
            _propBlock.SetColor("_BaseColor", c);
            _meshRenderer.SetPropertyBlock(_propBlock);
        }

        void OnDestroy()
        {
            if (_beamMaterial != null) Destroy(_beamMaterial);
        }
    }
}
