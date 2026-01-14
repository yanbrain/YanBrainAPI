// ============================================================================
// API Request Types
// ============================================================================

export interface TTSRequest {
    text: string;
    voiceId?: string;
}

export interface ImageRequest {
    prompt: string;
    imageBase64?: string;
}

export interface FileUpload {
    filename: string;
    contentBase64: string;
}

// ============================================================================
// Document Convert Types (convert-only)
// ============================================================================

export interface DocumentConvertRequest {
    files: FileUpload[];
}

export interface ConvertedDocumentText {
    fileId: string;
    filename: string;
    text: string;
    characterCount: number;
}

export interface DocumentConvertResponse {
    files: ConvertedDocumentText[];
    totalFiles: number;
    totalCreditsCharged: number;
}

// ============================================================================
// Embeddings Types (embed-only)
// ============================================================================

export interface EmbeddingItem {
    id?: string;
    filename?: string;
    text: string;
}

export interface EmbeddingRequest {
    items: EmbeddingItem[];
}

export interface EmbeddedItem {
    id?: string;
    filename?: string;
    embedding: number[];
    dimensions: number;
    characterCount: number;
}

export interface EmbeddingResponse {
    items: EmbeddedItem[];
    totalItems: number;
    totalCreditsCharged: number;
    provider: {
        provider: string;
        defaultModel: string;
    };
}

// ============================================================================
// RAG Types
// ============================================================================

export interface RagTextRequest {
    userPrompt: string;
    ragContext: string;
    systemPrompt?: string;
    maxResponseChars?: number;
}

export interface RagTextResponse {
    textResponse: string;
    model: ModelInfo;
}

export interface RagAudioRequest {
    userPrompt: string;
    ragContext: string;
    systemPrompt?: string;
    voiceId?: string;
    maxResponseChars?: number;
}

export interface RagAudioResponse {
    audio: string;
    textResponse: string;
}

// ============================================================================
// API Response Types
// ============================================================================

export interface ApiResponse<T> {
    success: boolean;
    data?: T;
    error?: ApiError;
}

export interface ApiError {
    code: string;
    message: string;
    statusCode: number;
    details?: Record<string, any>;
}

export interface TTSResponse {
    audio: string;
}

export interface ImageResponse {
    imageUrl: string;
}

// ============================================================================
// Provider / Model Types
// ============================================================================

export interface ModelInfo {
    provider: string;
    model: string;
}

export interface ProviderInfo {
    provider: string;
    [key: string]: any;
}

// ============================================================================
// YanBrainServer Types
// ============================================================================

export interface CreditsConsumeRequest {
    cost: number;
}