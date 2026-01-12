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
// Document Convert & Embed Types
// ============================================================================

export interface DocumentConvertRequest {
    files: FileUpload[];
}

export interface ConvertedDocument {
    fileId: string;
    filename: string;
    text: string;
    embedding: number[];
    dimensions: number;
    characterCount: number;
}

export interface DocumentConvertResponse {
    files: ConvertedDocument[];
    totalFiles: number;
    totalCreditsCharged: number;
}

// ============================================================================
// YanAvatar Types
// ============================================================================

export interface RelevantDocument {
    filename: string;
    text: string;
}

export interface YanAvatarRequest {
    userPrompt: string;
    relevantDocuments: RelevantDocument[];
    systemPrompt?: string;
    voiceId?: string;
}

export interface YanAvatarResponse {
    audio: string;          // base64 encoded MP3
    textResponse: string;   // LLM response text
    documentsUsed: number;
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
    audio: string; // base64 encoded
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
