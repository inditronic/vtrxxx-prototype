using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// Professional VRChat VRSL-inspired Moving Head Fixture (Mover).
    /// Simulates dual-axis mechanical gimbals (Yoke Pan + Head Tilt) and reacts to AudioLink
    /// in both movement speed and light intensity (0.0 = completely dark & stationary when no music plays!).
    /// </summary>
    public class GhostclubMovingHead : MonoBehaviour
    {
        [Header("Physical Articulation")]
        public Transform yokeTransform;
        public Transform headTransform;
        public Renderer lensRenderer;
        public Light spotLight;
        public Light rimLight;
        public GhostclubVolumetricBeam volumetricBeam;

        [Header("DMX State")]
        public bool fixtureEnabled = true;
        public float pan = 0f;      // -270 to +270 deg
        public float tilt = 45f;    // -135 to +135 deg
        [Range(0f, 1f)] public float dimmer = 1f;
        public Color beamColor = Color.white;
        public float panSpeed = 6f;
        public float tiltSpeed = 6f;

        private float _currentPan = 0f;
        private float _currentTilt = 0f;
        private MaterialPropertyBlock _lensPropBlock;
        private float _baseIntensity = 4.2f;

        void Awake()
        {
            _lensPropBlock = new MaterialPropertyBlock();
        }

        void Start()
        {
            if (spotLight == null) spotLight = GetComponentInChildren<Light>();
            if (volumetricBeam == null) volumetricBeam = GetComponentInChildren<GhostclubVolumetricBeam>();
            if (spotLight != null && spotLight.intensity > 0.1f) _baseIntensity = spotLight.intensity;

            if (_lensPropBlock == null) _lensPropBlock = new MaterialPropertyBlock();
            _currentPan = pan;
            _currentTilt = tilt;
        }

        void Update()
        {
            float effectiveDimmer = fixtureEnabled ? Mathf.Clamp01(dimmer) : 0f;

            // Physical gimbal movement (freezes when panSpeed/tiltSpeed == 0 during silence)
            if (fixtureEnabled && panSpeed > 0.01f)
            {
                _currentPan = Mathf.Lerp(_currentPan, pan, Time.deltaTime * panSpeed);
                _currentTilt = Mathf.Lerp(_currentTilt, tilt, Time.deltaTime * tiltSpeed);
            }

            if (yokeTransform != null)
            {
                yokeTransform.localRotation = Quaternion.Euler(0f, _currentPan, 0f);
            }
            if (headTransform != null)
            {
                headTransform.localRotation = Quaternion.Euler(_currentTilt, 0f, 0f);
            }

            // Update Spotlight (0.0 when no music or switched off)
            if (spotLight != null)
            {
                spotLight.enabled = (effectiveDimmer > 0.005f);
                spotLight.color = beamColor;
                spotLight.intensity = _baseIntensity * effectiveDimmer;
            }

            // Update Fixture Rim Light (Subtle local illumination, no bloom glare)
            if (rimLight != null)
            {
                rimLight.enabled = (effectiveDimmer > 0.005f);
                rimLight.color = beamColor;
                rimLight.intensity = 0.4f * effectiveDimmer;
            }

            // Update Volumetric Beam Cone
            if (volumetricBeam != null)
            {
                bool showBeam = (effectiveDimmer > 0.005f);
                if (volumetricBeam.gameObject.activeSelf != showBeam)
                {
                    volumetricBeam.gameObject.SetActive(showBeam);
                }
            }

            // Update Lens Emissive Flare (Kept very subtle without bloom blowout so the beam cone is the hero!)
            if (lensRenderer != null)
            {
                Color emissiveColor = beamColor * (effectiveDimmer * 0.85f);
                Color baseCol = Color.Lerp(new Color(0.04f, 0.04f, 0.05f, 1f), beamColor * 0.5f, effectiveDimmer);
                _lensPropBlock.SetColor("_EmissionColor", emissiveColor);
                _lensPropBlock.SetColor("_BaseColor", baseCol);
                _lensPropBlock.SetColor("_Color", baseCol);
                lensRenderer.SetPropertyBlock(_lensPropBlock);
            }
        }
    }
}
