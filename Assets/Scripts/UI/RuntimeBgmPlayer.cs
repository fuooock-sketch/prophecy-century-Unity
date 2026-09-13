using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ProphecyCentury.UI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class RuntimeBgmPlayer : MonoBehaviour
    {
        [SerializeField] private string relativeAssetPath = "Audio/Mp3/CstleTown.mp3";
        [SerializeField] private float volume = 0.45f;
        [SerializeField] private float pauseBetweenLoopsSeconds = 10f;

        private AudioSource _audioSource;
        private Coroutine _playbackRoutine;
        private bool _titleMusicRequested;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.loop = false;
            _audioSource.playOnAwake = false;
            _audioSource.volume = volume;
        }

        public void SetTitleMusicPlaying(bool shouldPlay)
        {
            _titleMusicRequested = shouldPlay;
            if (!shouldPlay)
            {
                _audioSource.Stop();
                if (_playbackRoutine != null)
                {
                    StopCoroutine(_playbackRoutine);
                    _playbackRoutine = null;
                }
                return;
            }

            if (_playbackRoutine == null)
            {
                _playbackRoutine = StartCoroutine(LoadAndPlay());
            }
        }

        private IEnumerator LoadAndPlay()
        {
            var fullPath = Path.Combine(Application.dataPath, relativeAssetPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"BGM file not found: {fullPath}");
                _playbackRoutine = null;
                yield break;
            }

            using (var request = UnityWebRequestMultimedia.GetAudioClip("file:///" + fullPath.Replace("\\", "/"), AudioType.MPEG))
            {
                yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.LogWarning($"Failed to load BGM: {request.error}");
                _playbackRoutine = null;
                yield break;
            }

                _audioSource.clip = DownloadHandlerAudioClip.GetContent(request);
            }

            while (_titleMusicRequested)
            {
                _audioSource.Play();
                yield return new WaitWhile(() => _titleMusicRequested && _audioSource.isPlaying);
                if (!_titleMusicRequested) break;
                yield return new WaitForSeconds(pauseBetweenLoopsSeconds);
            }

            _playbackRoutine = null;
        }

        private void OnDisable() => SetTitleMusicPlaying(false);
    }
}
