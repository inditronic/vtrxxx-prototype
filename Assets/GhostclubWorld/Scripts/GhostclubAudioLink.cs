using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// VRChat AudioLink-compatible Audio Analysis Engine:
    /// Analyzes the live audio spectrum into 4 core DMX frequency bands:
    /// - Bass (20 Hz - 150 Hz): Kick drums, sub-bass 808s
    /// - LowMid (150 Hz - 600 Hz): Snare bodies, bassline drive
    /// - HighMid (600 Hz - 2.5 kHz): Vocals, synth leads, claps
    /// - Treble (2.5 kHz - 14 kHz): Hi-hats, risers, white noise
    /// Exposes totalEnergy, hasAudioSignal, and beat transient detection so Moving Heads
    /// react in both movement speed and light intensity (and turn completely OFF when no music plays!).
    /// </summary>
    public class GhostclubAudioLink : MonoBehaviour
    {
        public static GhostclubAudioLink Instance { get; private set; }

        [Header("Band Levels (0.0 - 1.0)")]
        [Range(0f, 1f)] public float bass = 0f;
        [Range(0f, 1f)] public float lowMid = 0f;
        [Range(0f, 1f)] public float highMid = 0f;
        [Range(0f, 1f)] public float treble = 0f;

        [Header("Master Energy & Beat State")]
        [Range(0f, 1f)] public float totalEnergy = 0f;
        public bool hasAudioSignal = false;
        public bool isBeatHit = false;

        [Header("Smoothing")]
        public float attackSpeed = 42f;
        public float decaySpeed = 14f;

        private float[] _spectrum = new float[512];
        private float _rawBass, _rawLowMid, _rawHighMid, _rawTreble;
        private float _beatCooldown = 0f;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        void Update()
        {
            AudioListener.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);

            // Compute energy in frequency bands with noise gate so silence = exact 0.0f
            float bAvg = AverageSpectrum(1, 4);
            float lmAvg = AverageSpectrum(4, 13);
            float hmAvg = AverageSpectrum(13, 54);
            float trAvg = AverageSpectrum(54, 250);

            _rawBass = (bAvg > 0.0003f) ? bAvg * 9.5f : 0f;
            _rawLowMid = (lmAvg > 0.0002f) ? lmAvg * 13.0f : 0f;
            _rawHighMid = (hmAvg > 0.00015f) ? hmAvg * 18.0f : 0f;
            _rawTreble = (trAvg > 0.0001f) ? trAvg * 28.0f : 0f;

            // Combine with GhostclubAudioDrone envelopes when active (ensures 100% reliability even if Editor Game View audio is muted)
            var drone = GhostclubAudioDrone.Instance;
            if (drone != null && drone.droneEnabled && !drone.streamIsPlaying)
            {
                _rawBass = Mathf.Max(_rawBass, drone.synthBass);
                _rawLowMid = Mathf.Max(_rawLowMid, drone.synthLowMid);
                _rawHighMid = Mathf.Max(_rawHighMid, drone.synthHighMid);
                _rawTreble = Mathf.Max(_rawTreble, drone.synthTreble);
            }

            // Beat transient detection on bass kicks
            if (_beatCooldown > 0f) _beatCooldown -= Time.deltaTime;
            if (_rawBass > 0.18f && _rawBass > bass * 1.22f && _beatCooldown <= 0f)
            {
                isBeatHit = true;
                _beatCooldown = 0.18f;
            }
            else
            {
                isBeatHit = false;
            }

            // Smooth with fast attack, smooth decay
            bass = SmoothBand(bass, _rawBass);
            lowMid = SmoothBand(lowMid, _rawLowMid);
            highMid = SmoothBand(highMid, _rawHighMid);
            treble = SmoothBand(treble, _rawTreble);

            if (bass < 0.005f) bass = 0f;
            if (lowMid < 0.005f) lowMid = 0f;
            if (highMid < 0.005f) highMid = 0f;
            if (treble < 0.005f) treble = 0f;

            totalEnergy = Mathf.Clamp01(bass * 0.45f + lowMid * 0.25f + highMid * 0.20f + treble * 0.10f);
            if (totalEnergy < 0.01f) totalEnergy = 0f;
            hasAudioSignal = (totalEnergy > 0.012f);
        }

        float AverageSpectrum(int startBin, int endBin)
        {
            float sum = 0f;
            for (int i = startBin; i <= endBin && i < _spectrum.Length; i++)
            {
                sum += _spectrum[i];
            }
            return sum / (endBin - startBin + 1);
        }

        float SmoothBand(float current, float target)
        {
            target = Mathf.Clamp01(target);
            float speed = (target > current) ? attackSpeed : decaySpeed;
            return Mathf.Lerp(current, target, Time.deltaTime * speed);
        }
    }
}
