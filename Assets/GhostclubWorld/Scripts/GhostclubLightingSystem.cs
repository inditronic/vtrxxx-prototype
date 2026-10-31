using UnityEngine;
using System.Collections.Generic;

namespace Ghostclub
{
    /// <summary>
    /// Master DMX Lighting Director for Ghostclub 5.0:
    /// - Independent toggles for 10 VRSL Moving Heads ('movingHeadsEnabled') and Stage Laser Scanner ('lasersEnabled')
    /// - AudioLink-reactive in both movement speed/beat-snaps AND light intensity ('keine Musik = ganz aus')
    /// - Controls DJ Booth Key Lights so when lights are off, the room is 100% pitch black
    /// </summary>
    public class GhostclubLightingSystem : MonoBehaviour
    {
        [Header("Independent Fixture Switches")]
        public bool movingHeadsEnabled = true;
        public bool lasersEnabled = true;
        public bool blackoutAll = false;
        public bool audioReactive = true;
        [Range(0f, 1.5f)] public float masterDimmer = 1.0f;

        [Header("BPM & Timing")]
        [Range(80f, 160f)]
        public float bpm = 132f;

        [System.Serializable]
        public struct PaletteDuo
        {
            public Color primary;
            public Color secondary;

            public PaletteDuo(Color p, Color s)
            {
                primary = p;
                secondary = s;
            }
        }

        [Header("Strict Blue & Red Color Palettes (No White Light)")]
        public PaletteDuo[] palettes = new PaletteDuo[]
        {
            new PaletteDuo(new Color(0.0f, 0.45f, 1.0f), new Color(1.0f, 0.02f, 0.10f)),
            new PaletteDuo(new Color(0.02f, 0.15f, 0.90f), new Color(0.92f, 0.0f, 0.08f)),
            new PaletteDuo(new Color(0.20f, 0.0f, 0.95f), new Color(1.0f, 0.0f, 0.04f)),
            new PaletteDuo(new Color(0.0f, 0.75f, 1.0f), new Color(0.85f, 0.02f, 0.15f))
        };

        [Header("Fixtures")]
        public List<GhostclubMovingHead> movingHeads = new List<GhostclubMovingHead>();
        public List<Light> strobeLights = new List<Light>();
        public List<Light> djBoothLights = new List<Light>();
        public GhostclubLaserScanner stageLaserScanner;

        public enum ShowRoutine
        {
            CrossSweep,
            FanOut,
            StageFocus,
            SkywardSpiral
        }

        [Header("Active Routine")]
        public ShowRoutine currentRoutine = ShowRoutine.CrossSweep;

        private float _beatTimer = 0f;
        private int _beatCount = 0;
        private int _paletteIndex = 0;
        private float _timeAccumulator = 0f;
        private float _beatKickAngle = 0f;

        void Start()
        {
            if (movingHeads.Count == 0)
            {
                movingHeads.AddRange(FindObjectsByType<GhostclubMovingHead>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            }
        }

        public void SetPaletteIndex(int idx)
        {
            if (palettes != null && palettes.Length > 0)
            {
                _paletteIndex = Mathf.Clamp(idx, 0, palettes.Length - 1);
            }
        }

        void Update()
        {
            // Read live AudioLink energy
            float energy = 1f;
            float bass = 0.6f;
            float mid = 0.5f;
            float high = 0.4f;
            bool beatHit = false;

            if (audioReactive && GhostclubAudioLink.Instance != null)
            {
                energy = GhostclubAudioLink.Instance.totalEnergy;
                bass = GhostclubAudioLink.Instance.bass;
                mid = GhostclubAudioLink.Instance.lowMid;
                high = GhostclubAudioLink.Instance.highMid;
                beatHit = GhostclubAudioLink.Instance.isBeatHit;
            }

            // "Keine Musik = ganz aus": when audioReactive is active and no music is playing, turn fixtures completely off & stop movement!
            bool hasMusic = (!audioReactive) || (energy > 0.012f);
            bool globalActive = (!blackoutAll) && (masterDimmer > 0.005f) && hasMusic;

            if (globalActive && (movingHeadsEnabled || lasersEnabled))
            {
                float musicDrive = audioReactive ? Mathf.Clamp(0.20f + energy * 1.35f + bass * 1.45f, 0.15f, 3.0f) : 1.0f;
                _timeAccumulator += Time.deltaTime * musicDrive;

                if (beatHit)
                {
                    _beatKickAngle = 22f * Mathf.Clamp01(bass * 1.4f);
                }
                else
                {
                    _beatKickAngle = Mathf.MoveTowards(_beatKickAngle, 0f, Time.deltaTime * 55f);
                }

                float secondsPerBeat = 60f / Mathf.Max(60f, bpm);
                _beatTimer += Time.deltaTime * musicDrive;
                if (_beatTimer >= secondsPerBeat)
                {
                    _beatTimer -= secondsPerBeat;
                    _beatCount++;
                    OnBeat(_beatCount, bass);
                }
            }
            else
            {
                _beatKickAngle = Mathf.MoveTowards(_beatKickAngle, 0f, Time.deltaTime * 40f);
            }

            // 1. INDEPENDENT LASER SCANNER CONTROL ('lasersEnabled')
            if (stageLaserScanner != null)
            {
                bool shouldRunLasers = globalActive && lasersEnabled;
                if (shouldRunLasers)
                {
                    if (!stageLaserScanner.gameObject.activeSelf)
                    {
                        stageLaserScanner.gameObject.SetActive(true);
                    }
                    stageLaserScanner.lasersActive = true;
                    float laserAudioScale = audioReactive ? Mathf.Clamp(energy * 1.5f + bass * 0.5f, 0.35f, 1.4f) : 1.0f;
                    stageLaserScanner.intensityMultiplier = masterDimmer * laserAudioScale;
                }
                else
                {
                    stageLaserScanner.lasersActive = false;
                    stageLaserScanner.intensityMultiplier = 0f;
                    if (stageLaserScanner.gameObject.activeSelf)
                    {
                        stageLaserScanner.gameObject.SetActive(false);
                    }
                }
            }

            // 2. INDEPENDENT MOVING HEADS CONTROL ('movingHeadsEnabled')
            if (globalActive && movingHeadsEnabled)
            {
                AnimateMovingHeadsRoutine(energy, bass, mid, high);
                DecayStrobes();
            }
            else
            {
                foreach (var mh in movingHeads)
                {
                    if (mh != null)
                    {
                        if (!movingHeadsEnabled || blackoutAll || masterDimmer <= 0.005f)
                        {
                            mh.fixtureEnabled = false;
                            mh.dimmer = 0f;
                        }
                        else
                        {
                            // Music paused/silent: keep fixture enabled but fade dimmer to 0 and freeze movement
                            mh.fixtureEnabled = true;
                            mh.dimmer = Mathf.MoveTowards(mh.dimmer, 0f, Time.deltaTime * 8f);
                        }
                        mh.panSpeed = 0f;
                        mh.tiltSpeed = 0f;
                    }
                }
                foreach (Light s in strobeLights)
                {
                    if (s != null) s.intensity = 0f;
                }
            }

            // 3. DJ Booth Overhead Key Lights (Only active when Moving Heads or Lasers are ON — 0 when lights are off!)
            bool anyStageLightOn = globalActive && (movingHeadsEnabled || lasersEnabled);
            float boothTargetInt = anyStageLightOn ? (3.8f * masterDimmer * (audioReactive ? Mathf.Clamp(0.4f + energy * 0.9f, 0.3f, 1.2f) : 1.0f)) : 0f;
            for (int k = 0; k < djBoothLights.Count; k++)
            {
                if (djBoothLights[k] != null)
                {
                    djBoothLights[k].intensity = boothTargetInt;
                }
            }
        }

        void OnBeat(int beat, float bassLevel)
        {
            if (beat % 32 == 0)
            {
                _paletteIndex = (_paletteIndex + 1) % palettes.Length;
            }

            PaletteDuo activePalette = palettes[_paletteIndex];

            if (stageLaserScanner != null)
            {
                stageLaserScanner.laserColor = (currentRoutine == ShowRoutine.CrossSweep || currentRoutine == ShowRoutine.StageFocus)
                    ? activePalette.primary
                    : activePalette.secondary;
            }

            if (movingHeadsEnabled)
            {
                bool shouldStrobe = ((beat % 4 == 0) || ((beat % 16) >= 12)) && (!audioReactive || bassLevel > 0.35f);
                if (shouldStrobe)
                {
                    Color strobeColor = (beat % 2 == 0) ? activePalette.secondary : activePalette.primary;
                    float strobeInt = 14f * masterDimmer * (audioReactive ? Mathf.Clamp01(bassLevel * 1.3f) : 1f);
                    foreach (Light s in strobeLights)
                    {
                        if (s != null)
                        {
                            s.color = strobeColor;
                            s.intensity = strobeInt;
                        }
                    }
                }
            }
        }

        void AnimateMovingHeadsRoutine(float energy, float bass, float mid, float high)
        {
            float baseSpeed = (bpm / 60f) * 0.75f;
            PaletteDuo activePalette = palettes[_paletteIndex];
            int count = movingHeads.Count;

            for (int i = 0; i < count; i++)
            {
                var mh = movingHeads[i];
                if (mh == null) continue;

                mh.fixtureEnabled = true;
                mh.beamColor = (i % 2 == 0) ? activePalette.primary : activePalette.secondary;

                // Movement interpolation speed scales with music intensity & bass kicks
                float fixtureSpeed = audioReactive ? (2.2f + energy * 5.5f + bass * 8.5f) : 6.0f;
                mh.panSpeed = fixtureSpeed;
                mh.tiltSpeed = fixtureSpeed;

                // Light intensity (dimmer) reacts directly to the music band energy!
                if (audioReactive)
                {
                    float bandEnergy = (i % 2 == 0)
                        ? (bass * 0.75f + mid * 0.45f)
                        : (mid * 0.65f + high * 0.55f + bass * 0.35f);
                    float targetDimmer = Mathf.Clamp01(bandEnergy * 1.35f) * Mathf.Clamp01(masterDimmer);
                    mh.dimmer = Mathf.Lerp(mh.dimmer, targetDimmer, Time.deltaTime * 18f);
                }
                else
                {
                    mh.dimmer = Mathf.Clamp01(masterDimmer);
                }

                float offset = (float)i / Mathf.Max(1, count) * Mathf.PI * 2f;
                bool isLeftSide = (i % 2 == 0);
                float kickSign = (i % 2 == 0) ? 1f : -1f;

                switch (currentRoutine)
                {
                    case ShowRoutine.CrossSweep:
                        // Wide sweep across dancefloor AND side walls so Moving Head beams illuminate the wall posters!
                        float sweep = Mathf.Sin(_timeAccumulator * baseSpeed + (i * 0.5f));
                        mh.pan = (isLeftSide ? 24f : -24f) + sweep * 46f + (_beatKickAngle * 0.5f * kickSign);
                        mh.tilt = 52f + Mathf.Cos(_timeAccumulator * baseSpeed * 0.8f + offset) * 24f + _beatKickAngle;
                        break;

                    case ShowRoutine.FanOut:
                        float fanPan = Mathf.Lerp(-62f, 62f, (float)i / Mathf.Max(1, count - 1));
                        mh.pan = fanPan + Mathf.Sin(_timeAccumulator * baseSpeed * 0.6f) * 22f;
                        mh.tilt = 48f + Mathf.Sin(_timeAccumulator * baseSpeed + offset) * 20f + _beatKickAngle;
                        break;

                    case ShowRoutine.StageFocus:
                        mh.pan = (isLeftSide ? 35f : -35f) + (_beatKickAngle * 0.4f * kickSign);
                        mh.tilt = 38f + Mathf.PingPong(_timeAccumulator * 2.2f, 20f) + _beatKickAngle * 0.8f;
                        break;

                    case ShowRoutine.SkywardSpiral:
                        mh.pan = Mathf.Sin(_timeAccumulator * baseSpeed * 1.2f + offset) * 65f;
                        mh.tilt = 58f + Mathf.Cos(_timeAccumulator * baseSpeed * 1.2f + offset) * 24f + _beatKickAngle;
                        break;
                }
            }
        }

        void DecayStrobes()
        {
            foreach (Light s in strobeLights)
            {
                if (s != null && s.intensity > 0f)
                {
                    s.intensity = Mathf.MoveTowards(s.intensity, 0f, Time.deltaTime * 85f);
                }
            }
        }
    }
}
