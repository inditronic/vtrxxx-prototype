using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

namespace Ghostclub
{
    /// <summary>
    /// High-End Media Stream Player for Ghostclub 5.0:
    /// - Plays YouTube and Twitch video/audio streams on the 16:9 Widescreen Video Wall ("Leinwand")
    /// - Uses bundled yt-dlp with H.264/AAC MP4 format selection (18/22/avc1) + --no-playlist
    /// - Includes automatic local MP4 cache fallback if macOS AVFoundation rejects a remote signed URL
    /// - Seamlessly hands off to GhostclubVJScreen when idle/stopped so the Video Wall is NEVER black
    /// - Routes audio into an AudioSource for 100% synchronized VRChat AudioLink reactivity
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class GhostclubStreamPlayer : MonoBehaviour
    {
        public static GhostclubStreamPlayer Instance { get; private set; }

        [Header("Target Videowall")]
        public Renderer vjScreenRenderer;
        public RenderTexture videoRenderTexture;

        [Header("Audio Output")]
        public AudioSource videoAudioSource;

        [Header("Default Streams")]
        public string defaultUrl = "https://www.youtube.com/watch?v=WlJ0h3nzOxo"; // 0b4k3 - GHOSTCLUB 5.0

        [Header("State")]
        public bool isPlaying = false;
        public string currentTitle = "Live VJ Visuals (Bereit)";
        public string streamStatus = "Bereit — Live VJ Modus aktiv";

        public event Action<string> OnStatusChanged;

        private VideoPlayer _videoPlayer;
        private string _resolvedStreamUrl = null;
        private string _lastOriginalUrl = null;
        private bool _isResolving = false;
        private bool _triedLocalFallback = false;

        void Awake()
        {
            Instance = this;
            _videoPlayer = GetComponent<VideoPlayer>();
        }

        void Start()
        {
            SetupVideoPlayer();
        }

        void SetupVideoPlayer()
        {
            if (videoRenderTexture == null)
            {
                videoRenderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                videoRenderTexture.name = "Ghostclub_VJ_StreamTexture";
                videoRenderTexture.Create();
            }

            _videoPlayer.playOnAwake = false;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = videoRenderTexture;
            _videoPlayer.isLooping = true;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

            if (videoAudioSource == null)
            {
                videoAudioSource = GetComponent<AudioSource>();
                if (videoAudioSource == null) videoAudioSource = gameObject.AddComponent<AudioSource>();
            }

            videoAudioSource.playOnAwake = false;
            videoAudioSource.loop = true;
            videoAudioSource.spatialBlend = 0.35f;
            videoAudioSource.minDistance = 8f;
            videoAudioSource.maxDistance = 65f;

            _videoPlayer.SetTargetAudioSource(0, videoAudioSource);

            // NOTE: Do NOT bind empty black videoRenderTexture at Start()!
            // GhostclubVJScreen drives the Video Wall until a stream actually starts playing.

            _videoPlayer.prepareCompleted += (source) =>
            {
                isPlaying = true;
                BindVideoTextureToWall();
                _videoPlayer.Play();
                videoAudioSource.Play();

                // Mute background drone while stream plays so AudioLink reacts purely to the stream
                if (GhostclubAudioDrone.Instance != null)
                {
                    GhostclubAudioDrone.Instance.streamIsPlaying = true;
                }

                UpdateStatus($"Playing: {currentTitle}");
            };

            _videoPlayer.errorReceived += async (source, message) =>
            {
                UnityEngine.Debug.LogWarning($"[StreamPlayer] VideoPlayer Error: {message}");

                if (!_triedLocalFallback && !string.IsNullOrEmpty(_lastOriginalUrl))
                {
                    _triedLocalFallback = true;
                    UpdateStatus("Direct URL rejected by macOS AVFoundation — caching MP4 via yt-dlp...");
                    string localFile = await Task.Run(() => DownloadLocalMp4WithYtDlp(_lastOriginalUrl));
                    if (!string.IsNullOrEmpty(localFile) && File.Exists(localFile))
                    {
                        PlayDirectUrl("file://" + localFile);
                        return;
                    }
                }

                isPlaying = false;
                RestoreVJVisuals();
                UpdateStatus($"Video Error: {message}");
            };
        }

        void BindVideoTextureToWall()
        {
            if (vjScreenRenderer == null || videoRenderTexture == null) return;
            Material mat = vjScreenRenderer.sharedMaterial != null ? vjScreenRenderer.sharedMaterial : vjScreenRenderer.material;
            if (mat == null) return;

            mat.mainTexture = videoRenderTexture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", videoRenderTexture);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", videoRenderTexture);
                mat.SetColor("_EmissionColor", Color.white * 2.0f);
                mat.EnableKeyword("_EMISSION");
            }
        }

        void RestoreVJVisuals()
        {
            if (GhostclubAudioDrone.Instance != null)
            {
                GhostclubAudioDrone.Instance.streamIsPlaying = false;
            }

            if (vjScreenRenderer != null)
            {
                var vj = vjScreenRenderer.GetComponent<GhostclubVJScreen>();
                if (vj != null)
                {
                    vj.ApplyVJTextureToMaterial();
                }
            }
        }

        public async void PlayStream(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (_isResolving) return;

            url = CleanYouTubeUrl(url.Trim());
            _lastOriginalUrl = url;
            _triedLocalFallback = false;
            currentTitle = url;

            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
            {
                PlayDirectUrl(url);
                return;
            }

            _isResolving = true;
            UpdateStatus("Löse YouTube/Twitch Stream via yt-dlp auf...");

            try
            {
                string resolved = await Task.Run(() => ResolveWithYtDlp(url));
                if (!string.IsNullOrEmpty(resolved))
                {
                    UpdateStatus("Stream aufgelöst — Starte Video Wall...");
                    PlayDirectUrl(resolved);
                }
                else
                {
                    UpdateStatus("Direktlink fehlgeschlagen — Lade MP4 Cache...");
                    _triedLocalFallback = true;
                    string localFile = await Task.Run(() => DownloadLocalMp4WithYtDlp(url));
                    if (!string.IsNullOrEmpty(localFile) && File.Exists(localFile))
                    {
                        PlayDirectUrl("file://" + localFile);
                    }
                    else
                    {
                        isPlaying = false;
                        RestoreVJVisuals();
                        UpdateStatus("Fehler: URL konnte nicht geladen werden.");
                    }
                }
            }
            catch (Exception ex)
            {
                isPlaying = false;
                RestoreVJVisuals();
                UpdateStatus($"Stream Fehler: {ex.Message}");
            }
            finally
            {
                _isResolving = false;
            }
        }

        string CleanYouTubeUrl(string url)
        {
            // Strip playlist parameters (&list=..., &index=..., &start_radio=...) so single video resolves fast
            try
            {
                if (url.Contains("youtube.com/watch") && url.Contains("&list="))
                {
                    int listIdx = url.IndexOf("&list=", StringComparison.OrdinalIgnoreCase);
                    if (listIdx > 0) url = url.Substring(0, listIdx);
                }
                else if (url.Contains("youtu.be/") && url.Contains("?list="))
                {
                    int listIdx = url.IndexOf("?list=", StringComparison.OrdinalIgnoreCase);
                    if (listIdx > 0) url = url.Substring(0, listIdx);
                }
            }
            catch { }
            return url;
        }

        void PlayDirectUrl(string directUrl)
        {
            _resolvedStreamUrl = directUrl;
            _videoPlayer.Stop();
            videoAudioSource.Stop();
            _videoPlayer.url = directUrl;
            _videoPlayer.Prepare();
        }

        public void Pause()
        {
            if (_videoPlayer.isPlaying)
            {
                _videoPlayer.Pause();
                videoAudioSource.Pause();
                isPlaying = false;
                if (GhostclubAudioDrone.Instance != null)
                {
                    GhostclubAudioDrone.Instance.streamIsPlaying = false;
                    GhostclubAudioDrone.Instance.droneEnabled = false;
                }
                UpdateStatus("Pausiert (Keine Musik — Moving Heads aus)");
            }
        }

        public void Resume()
        {
            if (!_videoPlayer.isPlaying && !string.IsNullOrEmpty(_videoPlayer.url))
            {
                isPlaying = true;
                if (GhostclubAudioDrone.Instance != null)
                {
                    GhostclubAudioDrone.Instance.streamIsPlaying = true;
                }
                BindVideoTextureToWall();
                _videoPlayer.Play();
                videoAudioSource.Play();
                UpdateStatus("Playing");
            }
        }

        public void Stop()
        {
            _videoPlayer.Stop();
            videoAudioSource.Stop();
            isPlaying = false;
            if (GhostclubAudioDrone.Instance != null)
            {
                GhostclubAudioDrone.Instance.streamIsPlaying = false;
                GhostclubAudioDrone.Instance.droneEnabled = false;
            }
            RestoreVJVisuals();
            UpdateStatus("Gestoppt (Keine Musik — Moving Heads aus)");
        }

        public void SetVolume(float volume)
        {
            if (videoAudioSource != null)
            {
                videoAudioSource.volume = Mathf.Clamp01(volume);
            }
        }

        string GetYtDlpBinaryPath()
        {
            string ytdlpPath = Path.Combine(Application.streamingAssetsPath, "yt-dlp");
            if (!File.Exists(ytdlpPath))
            {
                ytdlpPath = "/Users/svenperson/.gemini/antigravity/scratch/yt-dlp";
            }
            return File.Exists(ytdlpPath) ? ytdlpPath : null;
        }

        string GetNodeRuntimeArg()
        {
            if (File.Exists("/opt/homebrew/bin/node")) return "--js-runtimes node:/opt/homebrew/bin/node ";
            if (File.Exists("/usr/local/bin/node")) return "--js-runtimes node:/usr/local/bin/node ";
            return "";
        }

        string ResolveWithYtDlp(string targetUrl)
        {
            string ytdlpPath = GetYtDlpBinaryPath();
            if (ytdlpPath == null)
            {
                UnityEngine.Debug.LogError("[StreamPlayer] yt-dlp binary not found!");
                return null;
            }

            string jsRuntimeArg = GetNodeRuntimeArg();

            // Prioritize Format 18/22 (H.264 avc1 + AAC mp4a progressive MP4) which macOS AVFoundation VideoPlayer supports 100%
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                Arguments = $"{jsRuntimeArg}--no-playlist --geo-bypass -f \"18/22/best[ext=mp4][vcodec^=avc1][acodec^=mp4a]/best[ext=mp4]/best\" -g \"{targetUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(20000);

                if (!string.IsNullOrEmpty(output))
                {
                    string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        return lines[0].Trim();
                    }
                }

                if (!string.IsNullOrEmpty(error))
                {
                    UnityEngine.Debug.LogWarning($"[StreamPlayer] yt-dlp warning: {error}");
                }
            }

            return null;
        }

        string DownloadLocalMp4WithYtDlp(string targetUrl)
        {
            string ytdlpPath = GetYtDlpBinaryPath();
            if (ytdlpPath == null) return null;

            string jsRuntimeArg = GetNodeRuntimeArg();
            string cacheDir = Application.temporaryCachePath;
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
            string outFile = Path.Combine(cacheDir, "ghostclub_stream_cache.mp4");
            if (File.Exists(outFile))
            {
                try { File.Delete(outFile); } catch { }
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                Arguments = $"{jsRuntimeArg}--no-playlist --geo-bypass -f \"18/best[ext=mp4][vcodec^=avc1]/best[ext=mp4]\" --force-overwrites -o \"{outFile}\" \"{targetUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit(35000);
            }

            return File.Exists(outFile) ? outFile : null;
        }

        void UpdateStatus(string status)
        {
            streamStatus = status;
            UnityEngine.Debug.Log($"[StreamPlayer] {status}");
            OnStatusChanged?.Invoke(status);
        }

        void OnDestroy()
        {
            if (videoRenderTexture != null)
            {
                videoRenderTexture.Release();
                Destroy(videoRenderTexture);
            }
        }
    }
}
