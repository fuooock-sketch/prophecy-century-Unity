using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ProphecyCentury.UI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class RuntimeSfxPlayer : MonoBehaviour
    {
        private static RuntimeSfxPlayer _instance;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private AudioSource _audioSource;

        private void Awake()
        {
            _instance = this;
            _audioSource = GetComponent<AudioSource>();
            _audioSource.loop = false;
            _audioSource.playOnAwake = false;
            _audioSource.volume = 0.65f;
        }

        public static void PlayClick()
        {
            _instance?.Play("Combat01.mp3", 0.25f);
        }

        public static void PlayBattleResult(bool victory)
        {
            _instance?.Play(victory ? "Win Battle.mp3" : "LoseCombat.mp3", 0.75f);
        }

        public static void PlaySaveLoad(bool success)
        {
            _instance?.Play(success ? "SecretTheme.mp3" : "Surrender Battle.mp3", 0.35f);
        }

        private void Play(string fileName, float volume)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            if (_clips.TryGetValue(fileName, out var clip) && clip != null)
            {
                _audioSource.PlayOneShot(clip, volume);
                return;
            }

            StartCoroutine(LoadThenPlay(fileName, volume));
        }

        private IEnumerator LoadThenPlay(string fileName, float volume)
        {
            var fullPath = Path.Combine(Application.dataPath, "Audio", fileName);
            if (!File.Exists(fullPath))
            {
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
                    yield break;
                }

                var clip = DownloadHandlerAudioClip.GetContent(request);
                _clips[fileName] = clip;
                _audioSource.PlayOneShot(clip, volume);
            }
        }
    }
}
