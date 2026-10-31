using System;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

namespace Ghostclub
{
    /// <summary>
    /// High-End Media Stream Player for Ghostclub 5.0:
    /// - Plays YouTube and Twitch video/audio streams on the 16:9 Widescreen Video Wall ("Leinwand")
    /// - Uses bundled yt-dlp with H.264/AAC MP4 format selection (18/22/avc1) + --no-playlist
    /// - Includes automatic HTTP 302 redirect resolution for direct AVFoundation streaming
    /// - Includes fast local MP4 caching with instant lookup by YouTube ID
    /// - Seamlessly hands off to GhostclubVJScreen when idle/stopped so the Video Wall is NEVER black
    /// - Routes audio into an AudioSource for 100% synchronized VRChat AudioLink reactivity
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class GhostclubStreamPlayer : MonoBehaviour
    {
        private static GhostclubStreamPlayer _instance;
        public static GhostclubStreamPlayer Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<GhostclubStreamPlayer>();
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Target Videowall")]
        public Renderer vjScreenRenderer;
        public RenderTexture videoRenderTexture;

        [Header("Audio Output")]
        public AudioSource videoAudioSource;

        [Header("Default Streams")]
        public string defaultUrl = "https://www.youtube.com/watch?v=b10gQQ_pyQo"; // bed Live at The George Tavern

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
        private Coroutine _watchdogCoroutine = null;

        void Awake()
        {
            _instance = this;
            _videoPlayer = GetComponent<VideoPlayer>();
        }

        void Start()
        {
            SetupVideoPlayer();
        }

        void Update()
        {
            if (isPlaying && vjScreenRenderer != null)
            {
                Material mat = vjScreenRenderer.material;
                if (mat != null && mat.mainTexture != videoRenderTexture)
                {
                    BindVideoTextureToWall();
                }
            }
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
                    UpdateStatus("Direct URL abgelehnt — Caching MP4 via yt-dlp...");
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

        public void BindVideoTextureToWall()
        {
            if (vjScreenRenderer == null || videoRenderTexture == null) return;
            Material mat = vjScreenRenderer.material;
            if (mat == null) return;

            mat.mainTexture = videoRenderTexture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", videoRenderTexture);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", videoRenderTexture);
                mat.SetColor("_EmissionColor", Color.white * 2.5f);
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

            // Quick check: If this video was already cached locally, play instantly without resolving!
            string videoId = ExtractVideoId(url);
            if (!string.IsNullOrEmpty(videoId))
            {
                string cachedPath = CheckExistingCache(videoId);
                if (!string.IsNullOrEmpty(cachedPath))
                {
                    UnityEngine.Debug.Log($"[StreamPlayer] Instant playback from local cache: {cachedPath}");
                    UpdateStatus("Lokaler Cache gefunden — Starte Video Wall...");
                    PlayDirectUrl("file://" + cachedPath);
                    return;
                }
            }

            _isResolving = true;
            UpdateStatus("Löse YouTube/Twitch Stream via yt-dlp auf...");

            try
            {
                string resolved = await Task.Run(() => ResolveWithYtDlp(url));
                if (!string.IsNullOrEmpty(resolved))
                {
                    string effectiveUrl = await Task.Run(() => FollowRedirects(resolved));
                    UpdateStatus("Stream aufgelöst — Starte Video Wall...");
                    PlayDirectUrl(effectiveUrl);
                }
                else
                {
                    UpdateStatus("Direktlink fehlgeschlagen — Lade MP4 Cache herunter...");
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
            if (string.IsNullOrWhiteSpace(url)) return url;
            url = url.Trim();
            // Handle plain 11-character video ID input
            if (url.Length == 11 && !url.Contains("/") && !url.Contains(".") && !url.Contains(" "))
            {
                return $"https://www.youtube.com/watch?v={url}";
            }

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

        public string ExtractVideoId(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (url.Length == 11 && !url.Contains("/") && !url.Contains(".") && !url.Contains(" ")) return url;
            try
            {
                if (url.Contains("v="))
                {
                    int idx = url.IndexOf("v=") + 2;
                    int amp = url.IndexOf('&', idx);
                    return amp > 0 ? url.Substring(idx, amp - idx) : url.Substring(idx);
                }
                if (url.Contains("youtu.be/"))
                {
                    int idx = url.IndexOf("youtu.be/") + 9;
                    int q = url.IndexOf('?', idx);
                    return q > 0 ? url.Substring(idx, q - idx) : url.Substring(idx);
                }
            }
            catch { }
            return null;
        }

        string CheckExistingCache(string videoId)
        {
            if (string.IsNullOrEmpty(videoId)) return null;
            string fileName = $"ghostclub_stream_{videoId}.mp4";
            string[] searchPaths = new string[]
            {
                Path.Combine(Application.temporaryCachePath, fileName),
                Path.Combine("/tmp", fileName)
            };

            foreach (var p in searchPaths)
            {
                if (File.Exists(p) && new FileInfo(p).Length > 1024 * 1024)
                {
                    return p;
                }
            }
            return null;
        }

        void PlayDirectUrl(string directUrl)
        {
            if (string.IsNullOrEmpty(directUrl)) return;
            if (directUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                directUrl = directUrl.Substring(7);
            }
            _resolvedStreamUrl = directUrl;
            _videoPlayer.Stop();
            videoAudioSource.Stop();
            _videoPlayer.url = directUrl;
            _videoPlayer.Prepare();

            if (_watchdogCoroutine != null) StopCoroutine(_watchdogCoroutine);
            if (directUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                _watchdogCoroutine = StartCoroutine(PrepareWatchdogCoroutine(_lastOriginalUrl));
            }
        }

        private IEnumerator PrepareWatchdogCoroutine(string originalUrl)
        {
            float elapsed = 0f;
            while (elapsed < 8f)
            {
                if (_videoPlayer.isPrepared || _videoPlayer.isPlaying) yield break;
                elapsed += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            if (!_videoPlayer.isPrepared && !_triedLocalFallback && !string.IsNullOrEmpty(originalUrl))
            {
                UnityEngine.Debug.LogWarning("[StreamPlayer] Direct stream prepare timed out — triggering local cache fallback...");
                _triedLocalFallback = true;
                UpdateStatus("Stream Pufferung langsam — Lade MP4 Cache herunter...");

                string localFile = null;
                bool done = false;
                Task.Run(() => DownloadLocalMp4WithYtDlp(originalUrl)).ContinueWith(t =>
                {
                    localFile = t.Result;
                    done = true;
                });

                while (!done)
                {
                    yield return new WaitForSeconds(0.5f);
                }

                if (!string.IsNullOrEmpty(localFile) && File.Exists(localFile))
                {
                    PlayDirectUrl("file://" + localFile);
                }
                else
                {
                    isPlaying = false;
                    RestoreVJVisuals();
                    UpdateStatus("Fehler beim Herunterladen des Streams.");
                }
            }
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
            if (_watchdogCoroutine != null) StopCoroutine(_watchdogCoroutine);
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

        public void SetVolume(float vol)
        {
            if (videoAudioSource != null)
            {
                videoAudioSource.volume = Mathf.Clamp01(vol);
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
            string[] candidates = new string[]
            {
                "/opt/homebrew/bin/node",
                "/usr/local/bin/node",
                "/usr/bin/node",
                "/opt/homebrew/bin/deno",
                "/usr/local/bin/deno"
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c))
                {
                    string binName = Path.GetFileName(c);
                    return $"--js-runtimes {binName}:{c} ";
                }
            }
            return "";
        }

        string FollowRedirects(string initialUrl)
        {
            if (string.IsNullOrEmpty(initialUrl) || !initialUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return initialUrl;

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(initialUrl);
                request.Method = "HEAD";
                request.AllowAutoRedirect = true;
                request.Timeout = 8000;
                request.UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)";
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return response.ResponseUri.ToString();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[StreamPlayer] FollowRedirects fallback: {ex.Message}");
                return initialUrl;
            }
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

            // Use -4 (force IPv4) to bypass YouTube's IPv6 bot-detection wall ("Sign in to confirm you're not a bot").
            // Prioritize Format 18/22 (H.264 avc1 + AAC mp4a progressive MP4) which macOS AVFoundation VideoPlayer streams directly.
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                Arguments = $"-4 {jsRuntimeArg}--no-playlist --geo-bypass --no-cache-dir --socket-timeout 20 -f \"18/22/best[ext=mp4][vcodec^=avc1][acodec^=mp4a]/best[ext=mp4]/best\" -g \"{targetUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(25000);

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
            string videoId = ExtractVideoId(targetUrl);
            string baseFileName = !string.IsNullOrEmpty(videoId) ? $"ghostclub_stream_{videoId}.mp4" : "ghostclub_stream_cache.mp4";

            string cacheDir = Application.temporaryCachePath;
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

            string[] searchPaths = new string[]
            {
                Path.Combine(cacheDir, baseFileName),
                Path.Combine("/tmp", baseFileName)
            };

            foreach (var p in searchPaths)
            {
                if (File.Exists(p) && new FileInfo(p).Length > 1024 * 1024)
                {
                    UnityEngine.Debug.Log($"[StreamPlayer] Using existing cached MP4: {p}");
                    return p;
                }
            }

            string ytdlpPath = GetYtDlpBinaryPath();
            if (ytdlpPath == null) return null;

            string jsRuntimeArg = GetNodeRuntimeArg();
            string outFile = Path.Combine(cacheDir, baseFileName);
            if (File.Exists(outFile))
            {
                try { File.Delete(outFile); } catch { }
            }

            // Use -4 (force IPv4) and 120s timeout so long sets (150MB+) have enough time to finish caching locally
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                Arguments = $"-4 {jsRuntimeArg}--no-playlist --geo-bypass --no-cache-dir --socket-timeout 30 -f \"18/best[ext=mp4][vcodec^=avc1]/best[ext=mp4]\" --force-overwrites -o \"{outFile}\" \"{targetUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit(120000);
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
