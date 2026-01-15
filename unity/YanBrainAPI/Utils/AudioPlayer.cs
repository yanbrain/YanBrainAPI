using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using YanBrain.YLogger;
using static YanBrain.YLogger.YLog;

namespace YanBrainAPI.Utils
{
    [EnableLogger]
    [RequireComponent(typeof(AudioSource))]
    public class AudioPlayer : MonoBehaviour
    {
        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
        }

        public async Task PlayAudioAsync(byte[] mp3Data)
        {
            if (mp3Data == null || mp3Data.Length == 0)
                throw new ArgumentException("Audio data is null or empty");

            string tempPath = Path.Combine(Application.temporaryCachePath, $"audio_{Guid.NewGuid()}.mp3");

            try
            {
                File.WriteAllBytes(tempPath, mp3Data);

                using var request = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG);
                
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Failed to load audio: {request.error}");

                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                    throw new Exception("AudioClip is null");

                _audioSource.clip = clip;
                _audioSource.Play();

                Log($"[AudioPlayer] Playing {clip.length:F2}s");

                while (_audioSource.isPlaying) await Task.Yield();

                _audioSource.clip = null;
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}