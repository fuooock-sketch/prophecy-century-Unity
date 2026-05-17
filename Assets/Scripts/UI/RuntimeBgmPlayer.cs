using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ProphecyCentury.UI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class RuntimeBgmPlayer : MonoBehaviour
    {
        [SerializeField] private bool playBgm;
        [SerializeField] private string relativeAssetPath = "Audio/manage-bgm.mp3";
        [SerializeField] private float volume = 0.45f;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.volume = volume;
        }

        private void Start()
        {
            if (!playBgm)
            {
                _audioSource.Stop();
                return;
            }

            StartCoroutine(LoadAndPlay());
        }

        private IEnumerator LoadAndPlay()
        {
            var fullPath = Path.Combine(Application.dataPath, relativeAssetPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"BGM file not found: {fullPath}");
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
                    yield break;
                }

                _audioSource.clip = DownloadHandlerAudioClip.GetContent(request);
                _audioSource.Play();
            }
        }
    }
}
