// ============================================================================
// API Request Types
// ============================================================================

export interface LLMRequest {
  message: string;
  conversationHistory?: ChatMessage[];
}

export interface TTSRequest {
  text: string;
  voiceId?: string;
}

export interface ImageRequest {
  prompt: string;
  width?: number;
  height?: number;
  productId: string;
  negativePrompt?: string;
}

export interface EmbeddingRequest {
  text: string;
  model?: string;
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
  model: ModelInfo;
}

export interface TTSResponse {
  audio: string; // base64 encoded
  provider: ProviderInfo;
}


export interface ImageResponse {
  imageUrl: string;
  prompt: string;
  dimensions: {
    width: number;
    height: number;
  };
  provider: ProviderInfo;
}

export interface EmbeddingResponse {
  embedding: number[];
  model: string;
  dimensions: number;
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
