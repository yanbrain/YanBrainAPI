import dotenv from 'dotenv';

dotenv.config();

// ============================================================================
// Product IDs (must match YanBrainServer)
// ============================================================================

export const PRODUCT_IDS = {
  YANAVATAR: 'yanAvatar',
  YANDRAW: 'yanDraw',
  YANPHOTOBOOTH: 'yanPhotobooth'
} as const;

export type ProductId = typeof PRODUCT_IDS[keyof typeof PRODUCT_IDS];

// ============================================================================
// Credit Costs (reference only - actual costs defined in YanBrainServer)
// ============================================================================

export const CREDIT_COSTS: Record<ProductId, number> = {
  [PRODUCT_IDS.YANAVATAR]: 1,
  [PRODUCT_IDS.YANDRAW]: 1,
  [PRODUCT_IDS.YANPHOTOBOOTH]: 1
};

// ============================================================================
// YanBrainServer API Configuration
// ============================================================================

export const YANBRAIN_SERVER = {
  BASE_URL: process.env.YANBRAIN_SERVER_URL || 'http://localhost:5001/api',
  ENDPOINTS: {
    CREDITS_BALANCE: '/credits/balance',
    CREDITS_CONSUME: '/credits/consume'
  }
} as const;

// ============================================================================
// Provider Configuration
// ============================================================================

export const PROVIDERS = {
  LLM: process.env.LLM_PROVIDER || 'openai',
  TTS: process.env.TTS_PROVIDER || 'elevenlabs',
    IMAGE: process.env.IMAGE_PROVIDER || 'runware',
  EMBEDDING: process.env.EMBEDDING_PROVIDER || 'openai'
} as const;

// ============================================================================
// API Keys Configuration
// ============================================================================

export const API_KEYS = {
  OPENAI: process.env.OPENAI_API_KEY || '',
  ELEVENLABS: process.env.ELEVENLABS_API_KEY || '',
  RUNWARE: process.env.RUNWARE_API_KEY || ''
} as const;

// ============================================================================
// Firebase Configuration
// ============================================================================

export const FIREBASE_CONFIG = {
  PROJECT_ID: process.env.FIREBASE_PROJECT_ID || ''
} as const;

// ============================================================================
// Configuration Validation
// ============================================================================

export function validateConfig(): void {
  const required = [
    'YANBRAIN_SERVER_URL',
    'FIREBASE_PROJECT_ID'
  ];

  const missing = required.filter(key => !process.env[key]);
  
  if (missing.length > 0) {
    throw new Error(`Missing required environment variables: ${missing.join(', ')}`);
  }

  // Validate provider API keys
  if (PROVIDERS.LLM === 'openai' && !API_KEYS.OPENAI) {
    throw new Error('OPENAI_API_KEY is required when LLM_PROVIDER=openai');
  }
  if (PROVIDERS.TTS === 'elevenlabs' && !API_KEYS.ELEVENLABS) {
    throw new Error('ELEVENLABS_API_KEY is required when TTS_PROVIDER=elevenlabs');
  }
  if (PROVIDERS.IMAGE === 'runware' && !API_KEYS.RUNWARE) {
    throw new Error('RUNWARE_API_KEY is required when IMAGE_PROVIDER=runware');
  }
  if (PROVIDERS.EMBEDDING === 'openai' && !API_KEYS.OPENAI) {
    throw new Error('OPENAI_API_KEY is required when EMBEDDING_PROVIDER=openai');
  }
}
