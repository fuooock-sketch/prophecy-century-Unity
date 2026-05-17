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
        private readonly Dictionary<string, AudioClip> _proceduralClips = new Dictionary<string, AudioClip>();
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
            _instance?.Play("Sfx/UI/ui_click.wav", 0.55f);
        }

        public static void PlayError()
        {
            _instance?.Play("Sfx/UI/ui_error.ogg", 0.65f);
        }

        public static void PlayNotEnoughGold()
        {
            _instance?.Play("Sfx/Shop/not_enough_gold.ogg", 0.75f);
        }

        public static void PlayBuyCard()
        {
            _instance?.Play("Sfx/Shop/shop_buy_card.ogg", 0.7f);
        }

        public static void PlayCardSelect()
        {
            _instance?.Play("Sfx/Card/card_select.ogg", 0.55f);
        }

        public static void PlayMove()
        {
            _instance?.Play("Sfx/Card/card_place.wav", 0.65f);
        }

        public static void PlaySell()
        {
            _instance?.Play("Sfx/Resource/gold_gain.ogg", 0.7f);
        }

        public static void PlaySynthesis()
        {
            _instance?.Play("Sfx/Card/card_synthesis.ogg", 0.8f);
        }

        public static void PlayAbilityTrigger()
        {
            _instance?.Play("Sfx/Card/card_ability_trigger.ogg", 0.7f);
        }

        public static void PlayDevour()
        {
            _instance?.PlayProcedural("card_devour", 0.78f);
        }

        public static void PlayGoldGain()
        {
            _instance?.Play("Sfx/Resource/gold_gain.ogg", 0.65f);
        }

        public static void PlayBattleResult(bool victory)
        {
            _instance?.PlayProcedural(victory ? "battle_win" : "battle_lose", victory ? 0.75f : 0.68f);
        }

        public static void PlayAttack(float range = 1f)
        {
            _instance?.PlayProcedural(range >= 3f ? "battle_attack_ranged" : "battle_attack_melee", 0.7f);
        }

        public static void PlayHit()
        {
            _instance?.PlayProcedural("battle_hit", 0.62f);
        }

        public static void PlayDeath()
        {
            _instance?.PlayProcedural("unit_death", 0.7f);
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

        private void PlayProcedural(string key, float volume)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!_proceduralClips.TryGetValue(key, out var clip) || clip == null)
            {
                clip = BuildProceduralClip(key);
                if (clip == null)
                {
                    return;
                }

                _proceduralClips[key] = clip;
            }

            _audioSource.PlayOneShot(clip, volume);
        }

        private IEnumerator LoadThenPlay(string fileName, float volume)
        {
            var fullPath = Path.Combine(Application.dataPath, "Audio", fileName);
            if (!File.Exists(fullPath))
            {
                fullPath = Path.Combine(Application.dataPath, "Audio", "Mp3", fileName);
            }

            if (!File.Exists(fullPath))
            {
                yield break;
            }

            using (var request = UnityWebRequestMultimedia.GetAudioClip("file:///" + fullPath.Replace("\\", "/"), ResolveAudioType(fullPath)))
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

        private static AudioType ResolveAudioType(string path)
        {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            switch (extension)
            {
                case ".wav":
                    return AudioType.WAV;
                case ".ogg":
                    return AudioType.OGGVORBIS;
                case ".mp3":
                    return AudioType.MPEG;
                default:
                    return AudioType.UNKNOWN;
            }
        }

        private static AudioClip BuildProceduralClip(string key)
        {
            const int sampleRate = 44100;
            var duration = ResolveProceduralDuration(key);
            if (duration <= 0f)
            {
                return null;
            }

            var samples = new float[Mathf.CeilToInt(sampleRate * duration)];
            switch (key)
            {
                case "battle_attack_ranged":
                    AddTone(samples, sampleRate, 0f, 0.08f, 880f, WaveType.Triangle, 0.13f, 0.62f);
                    AddTone(samples, sampleRate, 0.022f, 0.06f, 620f, WaveType.Sine, 0.1f, 0.7f);
                    break;
                case "battle_attack_melee":
                    AddTone(samples, sampleRate, 0f, 0.1f, 170f, WaveType.Square, 0.2f, 0.42f);
                    AddTone(samples, sampleRate, 0.018f, 0.08f, 120f, WaveType.Triangle, 0.12f, 0.7f);
                    break;
                case "battle_hit":
                    AddTone(samples, sampleRate, 0f, 0.08f, 220f, WaveType.Sawtooth, 0.12f, 0.58f);
                    break;
                case "unit_death":
                    AddTone(samples, sampleRate, 0f, 0.3f, 300f, WaveType.Sawtooth, 0.2f, 0.5f);
                    break;
                case "card_devour":
                    AddTone(samples, sampleRate, 0f, 0.24f, 180f, WaveType.Sawtooth, 0.18f, 0.45f);
                    AddTone(samples, sampleRate, 0.05f, 0.18f, 420f, WaveType.Triangle, 0.11f, 0.72f);
                    AddTone(samples, sampleRate, 0.18f, 0.12f, 90f, WaveType.Square, 0.12f, 0.5f);
                    break;
                case "battle_win":
                    AddTone(samples, sampleRate, 0f, 1f, 523.25f, WaveType.Triangle, 0.15f, 1f);
                    AddTone(samples, sampleRate, 0f, 1f, 659.25f, WaveType.Triangle, 0.15f, 1f);
                    AddTone(samples, sampleRate, 0f, 1f, 783.99f, WaveType.Triangle, 0.15f, 1f);
                    AddTone(samples, sampleRate, 0f, 1f, 1046.5f, WaveType.Triangle, 0.15f, 1f);
                    break;
                case "battle_lose":
                    AddTone(samples, sampleRate, 0f, 0.8f, 300f, WaveType.Sawtooth, 0.3f, 0.33f);
                    break;
            }

            Limit(samples);
            var clip = AudioClip.Create("ProceduralSfx_" + key, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float ResolveProceduralDuration(string key)
        {
            switch (key)
            {
                case "battle_attack_ranged":
                    return 0.1f;
                case "battle_attack_melee":
                    return 0.12f;
                case "battle_hit":
                    return 0.09f;
                case "unit_death":
                    return 0.32f;
                case "card_devour":
                    return 0.34f;
                case "battle_win":
                    return 1.05f;
                case "battle_lose":
                    return 0.85f;
                default:
                    return 0f;
            }
        }

        private static void AddTone(float[] samples, int sampleRate, float start, float duration, float frequency, WaveType type, float gain, float frequencyRatio)
        {
            var startSample = Mathf.Clamp(Mathf.RoundToInt(start * sampleRate), 0, samples.Length);
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * sampleRate));
            var endSample = Mathf.Min(samples.Length, startSample + sampleCount);
            var phase = 0f;
            for (var i = startSample; i < endSample; i += 1)
            {
                var t = (i - startSample) / (float)sampleCount;
                var freq = frequencyRatio > 0f && !Mathf.Approximately(frequencyRatio, 1f)
                    ? frequency * Mathf.Pow(frequencyRatio, t)
                    : frequency;
                phase += freq / sampleRate;
                phase -= Mathf.Floor(phase);

                var envelope = Mathf.Pow(1f - Mathf.Clamp01(t), 2f);
                samples[i] += Wave(phase, type) * gain * envelope;
            }
        }

        private static float Wave(float phase, WaveType type)
        {
            switch (type)
            {
                case WaveType.Square:
                    return phase < 0.5f ? 1f : -1f;
                case WaveType.Triangle:
                    return 1f - 4f * Mathf.Abs(Mathf.Round(phase - 0.25f) - (phase - 0.25f));
                case WaveType.Sawtooth:
                    return phase * 2f - 1f;
                default:
                    return Mathf.Sin(phase * Mathf.PI * 2f);
            }
        }

        private static void Limit(float[] samples)
        {
            for (var i = 0; i < samples.Length; i += 1)
            {
                samples[i] = Mathf.Clamp(samples[i], -0.95f, 0.95f);
            }
        }

        private enum WaveType
        {
            Sine,
            Triangle,
            Square,
            Sawtooth
        }
    }
}
