// Assets/Scripts/YanBrainAPI/Networking/YanModels.cs - UPDATE
using System.Collections.Generic;
using Newtonsoft.Json;

namespace YanBrainAPI.Networking
{
    public sealed class ApiResponse<T>
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("data")] public T Data { get; set; }
        [JsonProperty("error")] public ApiError Error { get; set; }
    }

    public sealed class ApiError
    {
        [JsonProperty("code")] public string Code { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("statusCode")] public int StatusCode { get; set; }
        [JsonProperty("details")] public object Details { get; set; }
    }

    public sealed class HealthPayload
    {
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("service")] public string Service { get; set; }
        [JsonProperty("timestamp")] public string Timestamp { get; set; }
        [JsonProperty("version")] public string Version { get; set; }
    }

    public sealed class LlmRequest
    {
        [JsonProperty("prompt")] public string Prompt { get; set; }

        [JsonProperty("systemPrompt", NullValueHandling = NullValueHandling.Ignore)]
        public string SystemPrompt { get; set; }

        [JsonProperty("ragContext", NullValueHandling = NullValueHandling.Ignore)]
        public string RagContext { get; set; }
    }

    public sealed class ModelInfo
    {
        [JsonProperty("provider")] public string Provider { get; set; }
        [JsonProperty("model")] public string Model { get; set; }
    }

    public sealed class LlmPayload
    {
        [JsonProperty("response")] public string Response { get; set; }
        [JsonProperty("model")] public ModelInfo Model { get; set; }
    }

    public sealed class TtsRequest
    {
        [JsonProperty("text")] public string Text { get; set; }

        [JsonProperty("voiceId", NullValueHandling = NullValueHandling.Ignore)]
        public string VoiceId { get; set; }
    }

    public sealed class TtsPayload
    {
        [JsonProperty("audio")] public string AudioBase64 { get; set; }
    }

    public sealed class ImageRequest
    {
        [JsonProperty("prompt")] public string Prompt { get; set; }

        [JsonProperty("imageBase64", NullValueHandling = NullValueHandling.Ignore)]
        public string ImageBase64 { get; set; }
    }

    public sealed class ImagePayload
    {
        [JsonProperty("imageUrl")] public string ImageUrl { get; set; }
    }

    public sealed class FileUpload
    {
        [JsonProperty("filename")] public string Filename { get; set; }
        [JsonProperty("contentBase64")] public string ContentBase64 { get; set; }
    }

    public sealed class DocumentConvertRequest
    {
        [JsonProperty("files")] public List<FileUpload> Files { get; set; }
    }

    public sealed class ConvertedDocumentText
    {
        [JsonProperty("fileId")] public string FileId { get; set; }
        [JsonProperty("filename")] public string Filename { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("characterCount")] public int CharacterCount { get; set; }
    }

    public sealed class DocumentConvertPayload
    {
        [JsonProperty("files")] public List<ConvertedDocumentText> Files { get; set; }
        [JsonProperty("totalFiles")] public int TotalFiles { get; set; }
        [JsonProperty("totalCreditsCharged")] public int TotalCreditsCharged { get; set; }
    }

    public sealed class EmbeddingItem
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("filename", NullValueHandling = NullValueHandling.Ignore)]
        public string Filename { get; set; }

        [JsonProperty("text")] public string Text { get; set; }
    }

    public sealed class EmbeddingRequest
    {
        [JsonProperty("items")] public List<EmbeddingItem> Items { get; set; }
    }

    public sealed class EmbeddedItem
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("filename")] public string Filename { get; set; }
        [JsonProperty("embedding")] public float[] Embedding { get; set; }
        [JsonProperty("dimensions")] public int Dimensions { get; set; }
        [JsonProperty("characterCount")] public int CharacterCount { get; set; }
    }

    public sealed class EmbeddingProviderInfo
    {
        [JsonProperty("provider")] public string Provider { get; set; }
        [JsonProperty("defaultModel")] public string DefaultModel { get; set; }
    }

    public sealed class EmbeddingPayload
    {
        [JsonProperty("items")] public List<EmbeddedItem> Items { get; set; }
        [JsonProperty("totalItems")] public int TotalItems { get; set; }
        [JsonProperty("totalCreditsCharged")] public int TotalCreditsCharged { get; set; }
        [JsonProperty("provider")] public EmbeddingProviderInfo Provider { get; set; }
    }

    public sealed class RagTextRequest
    {
        [JsonProperty("userPrompt")] public string UserPrompt { get; set; }
        [JsonProperty("ragContext")] public string RagContext { get; set; }

        [JsonProperty("systemPrompt", NullValueHandling = NullValueHandling.Ignore)]
        public string SystemPrompt { get; set; }

        [JsonProperty("maxResponseChars", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxResponseChars { get; set; }
    }

    public sealed class RagTextPayload
    {
        [JsonProperty("textResponse")] public string TextResponse { get; set; }
        [JsonProperty("model")] public ModelInfo Model { get; set; }
    }

    public sealed class RagAudioRequest
    {
        [JsonProperty("userPrompt")] public string UserPrompt { get; set; }
        [JsonProperty("ragContext")] public string RagContext { get; set; }

        [JsonProperty("systemPrompt", NullValueHandling = NullValueHandling.Ignore)]
        public string SystemPrompt { get; set; }

        [JsonProperty("voiceId", NullValueHandling = NullValueHandling.Ignore)]
        public string VoiceId { get; set; }

        [JsonProperty("maxResponseChars", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxResponseChars { get; set; }
    }

    public sealed class RagAudioPayload
    {
        [JsonProperty("audio")] public string AudioBase64 { get; set; }
        [JsonProperty("textResponse")] public string TextResponse { get; set; }
    }

    public sealed class RelevantDocument
    {
        public string Filename { get; set; }
        public string Text { get; set; }
    }
}