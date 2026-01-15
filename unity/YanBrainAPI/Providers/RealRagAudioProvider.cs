using System.Threading;
using System.Threading.Tasks;
using Sisus.Init;
using UnityEngine;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;

namespace YanBrainAPI.Providers
{
    public sealed class RealRagAudioProvider : MonoBehaviour<YanBrainService>, IRagAudioProvider
    {
        private YanBrainService _service;
        
        protected override void Init(YanBrainService service)
        {
            _service = service;
        }
        
        public Task<RagAudioPayload> GetRagAudioAsync(
            string userPrompt,
            string ragContext,
            string systemPrompt = null,
            string additionalInstructions = null,
            string voiceId = null,
            int? maxResponseChars = null,
            CancellationToken ct = default)
        {
            return _service.Api.RagAudioAsync(
                userPrompt,
                ragContext,
                systemPrompt,
                additionalInstructions,
                voiceId,
                maxResponseChars,
                ct
            );
        }
    }
}