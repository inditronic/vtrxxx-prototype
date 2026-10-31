using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// VRSL-Inspired Stage Laser Projector Array:
    /// - Can be switched ON and OFF completely independently of the Moving Heads
    /// - Casts 11 aerial scanning laser beams + 2 real-time Laser Scatter Lights that illuminate walls & posters
    /// - Pure Cyber Blue and Crimson Red beams (ZERO white light)
    /// </summary>
    public class GhostclubLaserScanner : MonoBehaviour
    {
        [Header("Laser Configuration")]
        public bool lasersActive = true;
        [Range(0f, 2f)] public float intensityMultiplier = 1.0f;
        public int beamCount = 11;
        public float fanSpreadAngle = 62f;
        public float beamLength = 24f;
        public Color laserColor = new Color(0.0f, 0.55f, 1.0f);
        public float sweepSpeed = 1.4f;
        public Material sharedLaserMaterial;

        [Header("Scanning Mode")]
        public LaserScanMode scanMode = LaserScanMode.LiquidSky;

        public enum LaserScanMode
        {
            LiquidSky,  // Horizontal flat planar sheet scanning vertically
            FanSweep,   // Aerial fan oscillating horizontally
            ConeTunnel  // Rotating circular tunnel around the dancefloor
        }

        private LineRenderer[] _beams;
        private Light _laserScatterLeft;
        private Light _laserScatterRight;
        private Material _laserMaterial;
        private float _timeAccumulator = 0f;

        void Start()
        {
            BuildLaserBeams();
        }

        void BuildLaserBeams()
        {
            _beams = new LineRenderer[beamCount];

            if (sharedLaserMaterial != null)
            {
                _laserMaterial = new Material(sharedLaserMaterial);
            }
            else
            {
                Shader laserShader = Shader.Find("Ghostclub/VolumetricBeam");
                if (laserShader == null) laserShader = Shader.Find("Sprites/Default");
                if (laserShader == null) laserShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (laserShader != null)
                {
                    _laserMaterial = new Material(laserShader);
                }
            }

            if (_laserMaterial != null)
            {
                if (_laserMaterial.HasProperty("_Surface")) _laserMaterial.SetFloat("_Surface", 1.0f);
                if (_laserMaterial.HasProperty("_Blend")) _laserMaterial.SetFloat("_Blend", 1.0f);
                if (_laserMaterial.HasProperty("_ZWrite")) _laserMaterial.SetInt("_ZWrite", 0);
                if (_laserMaterial.HasProperty("_BaseColor")) _laserMaterial.SetColor("_BaseColor", Color.white);
                if (_laserMaterial.HasProperty("_Color")) _laserMaterial.SetColor("_Color", Color.white);
                _laserMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _laserMaterial.EnableKeyword("_BLENDMODE_ADD");
            }

            for (int i = 0; i < beamCount; i++)
            {
                GameObject beamGo = new GameObject($"Laser_Beam_{i}");
                beamGo.transform.SetParent(transform, false);
                beamGo.transform.localPosition = Vector3.zero;

                LineRenderer lr = beamGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 2;
                lr.startWidth = 0.024f;
                lr.endWidth = 0.050f;
                if (_laserMaterial != null) lr.material = _laserMaterial;
                lr.startColor = laserColor;
                lr.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, 0.06f);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                _beams[i] = lr;
            }

            // Create 2 real-time Laser Scatter Spotlights so the Laser Scanner casts real light onto the walls & posters when ON
            _laserScatterLeft = CreateScatterLight("Laser_Scatter_Left", new Vector3(-1.5f, -0.2f, 2.5f), Quaternion.Euler(12f, -28f, 0f));
            _laserScatterRight = CreateScatterLight("Laser_Scatter_Right", new Vector3(1.5f, -0.2f, 2.5f), Quaternion.Euler(12f, 28f, 0f));
        }

        Light CreateScatterLight(string name, Vector3 localPos, Quaternion localRot)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = 95f;
            l.innerSpotAngle = 50f;
            l.range = 18f;
            l.intensity = 3.5f;
            l.color = laserColor;
            l.shadows = LightShadows.None;
            return l;
        }

        void Update()
        {
            if (_beams == null || _beams.Length == 0) return;

            bool visible = lasersActive && intensityMultiplier > 0.005f;
            if (!visible)
            {
                for (int i = 0; i < _beams.Length; i++)
                {
                    if (_beams[i] != null && _beams[i].enabled) _beams[i].enabled = false;
                }
                if (_laserScatterLeft != null) _laserScatterLeft.intensity = 0f;
                if (_laserScatterRight != null) _laserScatterRight.intensity = 0f;
                return;
            }

            // AudioLink speed & treble modulation
            float trebleBoost = 1.0f;
            float speedMod = 1.0f;
            if (GhostclubAudioLink.Instance != null)
            {
                trebleBoost = 1.0f + Mathf.Clamp(GhostclubAudioLink.Instance.treble * 0.55f, 0f, 0.7f);
                speedMod = 0.6f + Mathf.Clamp(GhostclubAudioLink.Instance.totalEnergy * 1.4f, 0f, 2.0f);
            }

            _timeAccumulator += Time.deltaTime * sweepSpeed * speedMod;

            for (int i = 0; i < beamCount; i++)
            {
                if (_beams[i] == null) continue;
                if (!_beams[i].enabled) _beams[i].enabled = true;

                float t = (float)i / Mathf.Max(1, beamCount - 1);
                float angleOffset = (t - 0.5f) * fanSpreadAngle;

                Vector3 targetDirection = Vector3.forward;

                if (scanMode == LaserScanMode.LiquidSky)
                {
                    float pitch = Mathf.Sin(_timeAccumulator * 2.2f + (i * 0.15f)) * 10f - 4f;
                    Quaternion rot = Quaternion.Euler(pitch, angleOffset, 0f);
                    targetDirection = rot * Vector3.forward;
                }
                else if (scanMode == LaserScanMode.FanSweep)
                {
                    float sweep = Mathf.Sin(_timeAccumulator * 2.5f) * 24f;
                    Quaternion rot = Quaternion.Euler(-8f, angleOffset + sweep, 0f);
                    targetDirection = rot * Vector3.forward;
                }
                else if (scanMode == LaserScanMode.ConeTunnel)
                {
                    float circleAngle = (float)i / beamCount * Mathf.PI * 2f + _timeAccumulator * 2.2f;
                    float tx = Mathf.Cos(circleAngle) * 0.28f;
                    float ty = Mathf.Sin(circleAngle) * 0.28f;
                    targetDirection = new Vector3(tx, ty, 1f).normalized;
                }

                _beams[i].SetPosition(0, Vector3.zero);
                _beams[i].SetPosition(1, targetDirection * beamLength);

                Color beamCol = laserColor * trebleBoost * Mathf.Clamp(intensityMultiplier, 0f, 1.5f);
                beamCol.a = Mathf.Clamp01(0.92f * intensityMultiplier);
                _beams[i].startColor = beamCol;
                _beams[i].endColor = new Color(beamCol.r, beamCol.g, beamCol.b, 0.08f * Mathf.Clamp01(intensityMultiplier));
            }

            float scatterInt = 4.2f * intensityMultiplier * trebleBoost;
            if (_laserScatterLeft != null)
            {
                _laserScatterLeft.color = laserColor;
                _laserScatterLeft.intensity = scatterInt;
            }
            if (_laserScatterRight != null)
            {
                _laserScatterRight.color = laserColor;
                _laserScatterRight.intensity = scatterInt;
            }
        }

        void OnDisable()
        {
            if (_laserScatterLeft != null) _laserScatterLeft.intensity = 0f;
            if (_laserScatterRight != null) _laserScatterRight.intensity = 0f;
        }

        void OnDestroy()
        {
            if (_laserMaterial != null) Destroy(_laserMaterial);
        }
    }
}
