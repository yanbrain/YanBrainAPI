// ============================================================================
// API Request Types
// ============================================================================

export interface LLMRequest {
  productId: string;
  message: string;
  embeddingFileIds?: string[];
}

export interface TTSRequest {
  productId: string;
  text: string;
  voiceId?: string;
}

export interface ImageRequest {
  productId: string;
  prompt: string;
  imageBase64: string;
}

export interface FileUpload {
  filename: string;
  contentBase64: string;
}

export interface EmbeddingRequest {
  productId: string;
  files: FileUpload[];
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

export interface LLMResponse {
  response: string;
}

export interface TTSResponse {
  audio: string; // base64 encoded
}

export interface ImageResponse {
  imageUrl: string;
}

export interface EmbeddingFile {
  fileId: string;
  text: string;
}

export interface EmbeddingResponse {
  files: EmbeddingFile[];
}

// ============================================================================
// Common Types
// ============================================================================

export interface ChatMessage {
  role: 'system' | 'user' | 'assistant';
  content: string;
}

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

export interface CreditsBalanceResponse {
  creditsBalance: number;
}

export interface CreditsConsumeRequest {
  productId: string;
}

export interface CreditsConsumeResponse {
  success: boolean;
  remainingCredits: number;
}
