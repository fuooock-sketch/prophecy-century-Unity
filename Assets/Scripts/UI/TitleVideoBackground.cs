using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ProphecyCentury.UI
{
    public sealed class TitleVideoBackground : MonoBehaviour
    {
        private const string VideoRelativePath = "Video/Login/login_background.mp4";
        private RenderTexture _renderTexture;
        private VideoPlayer _player;

        public static void Create(Transform canvasRoot)
        {
            if (canvasRoot == null || canvasRoot.Find("TitleVideoBackground") != null) return;
            var root = new GameObject("TitleVideoBackground", typeof(RectTransform), typeof(RawImage), typeof(VideoPlayer), typeof(TitleVideoBackground));
            root.transform.SetParent(canvasRoot, false);
            root.transform.SetAsFirstSibling();
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            root.GetComponent<RawImage>().raycastTarget = false;
            root.GetComponent<TitleVideoBackground>().StartPlayback();
        }

        private void StartPlayback()
        {
            var path = Path.Combine(Application.streamingAssetsPath, VideoRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[TitleVideo] Background video not found: {path}");
                return;
            }

            _renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
            _player = GetComponent<VideoPlayer>();
            _player.source = VideoSource.Url;
            _player.url = path;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _renderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.isLooping = true;
            _player.waitForFirstFrame = true;
            _player.skipOnDrop = true;
            _player.prepareCompleted += OnPrepared;
            _player.errorReceived += OnError;
            _player.Prepare();
        }

        private void OnPrepared(VideoPlayer player)
        {
            GetComponent<RawImage>().texture = _renderTexture;
            player.Play();
        }

        private static void OnError(VideoPlayer player, string message)
        {
            Debug.LogWarning($"[TitleVideo] Playback failed: {message}");
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.prepareCompleted -= OnPrepared;
                _player.errorReceived -= OnError;
            }
            if (_renderTexture == null) return;
            _renderTexture.Release();
            Destroy(_renderTexture);
        }
    }
}
