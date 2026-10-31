using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// Controls atmospheric club fog, haze, and stage smoke machine effects.
    /// Supports:
    /// - Real-time RenderSettings fog density control
    /// - Fog Toggle (ON / OFF)
    /// - Stage Smoke Blast (CO2 cannon surge effect) with smooth decay
    /// - Deep subterranean dark club atmosphere (dark fog tint matching blue/red lighting)
    /// </summary>
    public class GhostclubFogSystem : MonoBehaviour
    {
        public static GhostclubFogSystem Instance { get; private set; }

        [Header("Fog Settings")]
        public bool fogEnabled = true;
        [Range(0.000f, 0.035f)]
        public float baseDensity = 0.009f;

        [Header("Smoke Machine / Blast")]
        public ParticleSystem stageSmokeParticleSystem;
        private float _blastBoost = 0f;
        private float _blastDecaySpeed = 0.45f;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            ApplyFogSettings();
        }

        void Update()
        {
            if (_blastBoost > 0.0001f)
            {
                _blastBoost = Mathf.MoveTowards(_blastBoost, 0f, Time.deltaTime * _blastDecaySpeed * 0.025f);
            }

            ApplyFogSettings();
        }

        public void ApplyFogSettings()
        {
            RenderSettings.fog = fogEnabled;
            if (fogEnabled)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.001f, 0.002f, 0.005f, 1f); // Inky dark atmospheric tint
                RenderSettings.fogDensity = Mathf.Clamp(baseDensity + _blastBoost, 0f, 0.045f);
            }
        }

        public void ToggleFog()
        {
            fogEnabled = !fogEnabled;
            ApplyFogSettings();
        }

        public void SetDensity(float val)
        {
            baseDensity = Mathf.Clamp(val, 0.000f, 0.035f);
            ApplyFogSettings();
        }

        /// <summary>
        /// Fires a heavy CO2 / Smoke blast from the stage smoke machine.
        /// </summary>
        public void TriggerSmokeBlast()
        {
            _blastBoost = 0.022f; // Dense atmospheric haze burst
            if (stageSmokeParticleSystem != null)
            {
                stageSmokeParticleSystem.Emit(60);
            }
            Debug.Log("[GhostclubFogSystem] Triggered Stage Smoke / CO2 Blast!");
        }
    }
}
