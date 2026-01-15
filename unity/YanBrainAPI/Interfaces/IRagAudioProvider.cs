
using System.Threading;
using System.Threading.Tasks;
using YanBrainAPI.Networking;

namespace YanBrainAPI.Interfaces
{
    public interface IRagAudioProvider
    {
        Task<RagAudioPayload> GetRagAudioAsync(
            string userPrompt,
            string ragContext,
            string systemPrompt = null,
            string additionalInstructions = null,
            string voiceId = null,
            int? maxResponseChars = null,
            CancellationToken ct = default);
    }
}