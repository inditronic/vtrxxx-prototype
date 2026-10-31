using UnityEngine;

namespace Ghostclub
{
    /// <summary>
    /// Procedurally synthesizes an underground warehouse rave techno beat (48 Hz sub-bass + 132 BPM 4/4 kick + offbeat hi-hats)
    /// when enabled and no external YouTube/Twitch stream is playing.
    /// When disabled (or when a stream is stopped in 'Silence' mode), outputs 0.0 so AudioLink detects
    /// zero music and turns Moving Heads completely off ('keine Musik = ganz aus').
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class GhostclubAudioDrone : MonoBehaviour
    {
        public static GhostclubAudioDrone Instance { get; private set; }

        [Range(80f, 160f)]
        public float bpm = 132f;
        [Range(0f, 1f)]
        public float masterVolume = 0.55f;
        public bool droneEnabled = true;
        public bool streamIsPlaying = false;
        public bool enableControllerRumble = true;

        // Exposed real-time synth envelopes so AudioLink works even when Unity Editor Game View is muted
        [HideInInspector] public float synthBass = 0f;
        [HideInInspector] public float synthLowMid = 0f;
        [HideInInspector] public float synthHighMid = 0f;
        [HideInInspector] public float synthTreble = 0f;

        private double _sampleRate;
        private double _phaseSub;
        private double _phaseKick;
        private double _beatInterval;
        private double _nextBeatTime;
        private float _kickDecay = 0f;
        private System.Random _rand = new System.Random();

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            _sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            _beatInterval = 60.0 / bpm;
            _nextBeatTime = AudioSettings.dspTime + 0.1;

            AudioSource source = GetComponent<AudioSource>();
            if (source.clip == null)
            {
                AudioClip dummyClip = AudioClip.Create("Ghostclub_DroneCarrier", 48000, 1, 48000, false);
                float[] ones = new float[48000];
                for (int i = 0; i < ones.Length; i++) ones[i] = 1f;
                dummyClip.SetData(ones, 0);
                source.clip = dummyClip;
            }
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;
            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        void Update()
        {
            _beatInterval = 60.0 / Mathf.Max(60f, bpm);

            if (!droneEnabled || streamIsPlaying || masterVolume <= 0.001f)
            {
                synthBass = 0f;
                synthLowMid = 0f;
                synthHighMid = 0f;
                synthTreble = 0f;
                return;
            }

            // Compute rhythmic 4/4 techno envelopes synchronized to Time.time & bpm
            float beatsElapsed = Time.time * (bpm / 60f);
            float beatFrac = beatsElapsed - Mathf.Floor(beatsElapsed);

            // Punchy 4/4 kick envelope
            float kickEnv = Mathf.Pow(Mathf.Clamp01(1.0f - beatFrac * 2.4f), 2.2f);
            // Offbeat bassline & hi-hat envelope
            float offbeatEnv = Mathf.Pow(Mathf.Clamp01(1.0f - Mathf.Abs(beatFrac - 0.5f) * 4.0f), 1.8f);
            // 16th-note acid synth modulation
            float acidEnv = (Mathf.Sin(beatsElapsed * Mathf.PI * 4f) * 0.5f + 0.5f) * 0.65f;

            synthBass = Mathf.Clamp01((kickEnv * 0.95f + offbeatEnv * 0.35f) * masterVolume * 1.6f);
            synthLowMid = Mathf.Clamp01((offbeatEnv * 0.75f + kickEnv * 0.40f) * masterVolume * 1.5f);
            synthHighMid = Mathf.Clamp01((acidEnv * 0.80f + offbeatEnv * 0.30f) * masterVolume * 1.4f);
            synthTreble = Mathf.Clamp01((offbeatEnv * 0.85f) * masterVolume * 1.4f);
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!droneEnabled || streamIsPlaying || masterVolume <= 0.001f)
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
                return;
            }

            double dspTime = AudioSettings.dspTime;
            double timeStep = 1.0 / _sampleRate;

            for (int i = 0; i < data.Length; i += channels)
            {
                dspTime += timeStep;

                if (dspTime >= _nextBeatTime)
                {
                    _kickDecay = 1.0f;
                    _phaseKick = 0.0;
                    _nextBeatTime += _beatInterval;
                }

                _phaseSub += 2.0 * System.Math.PI * 48.0 / _sampleRate;
                if (_phaseSub > 2.0 * System.Math.PI) _phaseSub -= 2.0 * System.Math.PI;
                float subSignal = (float)System.Math.Sin(_phaseSub) * (0.12f + _kickDecay * 0.25f);

                float kickSignal = 0f;
                if (_kickDecay > 0.001f)
                {
                    double kickFreq = 42.0 + (_kickDecay * _kickDecay * 95.0);
                    _phaseKick += 2.0 * System.Math.PI * kickFreq / _sampleRate;
                    kickSignal = (float)System.Math.Sin(_phaseKick) * _kickDecay * 0.80f;
                    _kickDecay *= 0.9995f;
                }

                float hatSignal = 0f;
                double beatPhase = ((dspTime - (_nextBeatTime - _beatInterval)) / _beatInterval);
                if (beatPhase > 0.48 && beatPhase < 0.62)
                {
                    hatSignal = ((float)_rand.NextDouble() * 2f - 1f) * 0.14f;
                }

                float finalSample = (subSignal + kickSignal + hatSignal) * masterVolume;

                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = finalSample;
                }
            }
        }
    }
}
