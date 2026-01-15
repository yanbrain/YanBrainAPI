// Assets/Scripts/YanBrainAPI/Adapters/MockRagAudioAdapter.cs

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using YanBrain.YLogger;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;
using static YanBrain.YLogger.YLog;

namespace YanBrainAPI.Adapters
{
    [EnableLogger]
    public sealed class MockRagAudioProvider : MonoBehaviour, IRagAudioProvider
    {
        [Title("Mock Audio Configuration")]
        [FolderPath, Tooltip("Folder containing MP3 files")]
        [SerializeField] private string mockAudioFolderPath;
        
        [Title("Loaded Files")]
        [ShowInInspector, ReadOnly, ListDrawerSettings(Expanded = false)]
        [InfoBox("No audio files loaded. Select a folder with MP3 files.", InfoMessageType.Warning, "HasNoFiles")]
        private string[] mockAudioPaths = new string[0];
        
        [Title("Behavior")]
        [SerializeField, Range(0f, 5f), Tooltip("Simulated network delay in seconds")]
        private float simulatedDelaySeconds = 0.5f;
        
        [SerializeField, TextArea(3, 10), Tooltip("Mock text response returned with audio")]
        private string mockTextResponse = "This is a mock response.";
        
        [Title("Status")]
        [ShowInInspector, ReadOnly]
        private int CurrentIndex => _currentIndex;
        
        [ShowInInspector, ReadOnly]
        private int TotalFiles => mockAudioPaths?.Length ?? 0;
        
        private int _currentIndex = 0;
        
        private bool HasNoFiles() => mockAudioPaths == null || mockAudioPaths.Length == 0;
        
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(mockAudioFolderPath) || !Directory.Exists(mockAudioFolderPath))
            {
                mockAudioPaths = new string[0];
                return;
            }
            
            mockAudioPaths = Directory.GetFiles(mockAudioFolderPath, "*.mp3", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f)
                .ToArray();
            
            if (mockAudioPaths.Length > 0)
            {
                Log($"[MockRagAudioAdapter] Loaded {mockAudioPaths.Length} MP3 files from {mockAudioFolderPath}");
            }
        }
        
        public async Task<RagAudioPayload> GetRagAudioAsync(
            string userPrompt,
            string ragContext,
            string systemPrompt = null,
            string additionalInstructions = null,
            string voiceId = null,
            int? maxResponseChars = null,
            CancellationToken ct = default)
        {
            if (mockAudioPaths == null || mockAudioPaths.Length == 0)
            {
                LogError("[MockRagAudioAdapter] No audio paths configured");
                throw new InvalidOperationException("No mock audio files configured. Set a folder with MP3 files.");
            }
            
            Log($"[MockRagAudioAdapter] Mock response for: '{userPrompt}'");
            
            // Simulate network delay
            if (simulatedDelaySeconds > 0)
            {
                await Task.Delay((int)(simulatedDelaySeconds * 1000), ct);
            }
            
            // Get next audio file (cycle through)
            int index = _currentIndex % mockAudioPaths.Length;
            var audioPath = mockAudioPaths[index];
            _currentIndex++;
            
            if (!File.Exists(audioPath))
            {
                LogError($"[MockRagAudioAdapter] File not found: {audioPath}");
                throw new FileNotFoundException($"Mock audio file not found: {audioPath}");
            }
            
            byte[] audioBytes = await Task.Run(() => File.ReadAllBytes(audioPath), ct);
            
            Log($"[MockRagAudioAdapter] Returning mock audio ({audioBytes.Length / 1024}KB) from {Path.GetFileName(audioPath)}");
            
            return new RagAudioPayload
            {
                TextResponse = mockTextResponse,
                AudioBase64 = Convert.ToBase64String(audioBytes)
            };
        }
    }
}