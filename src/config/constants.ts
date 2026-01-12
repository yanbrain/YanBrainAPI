import dotenv from 'dotenv';

dotenv.config();

// ============================================================================
// Credit Costs (defined in YanBrainAPI)
// ============================================================================

export const CREDIT_COSTS = {
    // Production endpoints
    DOCUMENT_CONVERT_AND_EMBED_PER_FILE: 1, // Cost per file for conversion + embedding
    YANAVATAR_REQUEST: 5, // Fixed cost per YanAvatar request (includes LLM + TTS)
    IMAGE_GENERATION: 10 // For other app
} as const;

// ============================================================================
// YanBrainServer API Configuration
// ============================================================================

export const YANBRAIN_SERVER = {
    BASE_URL: process.env.YANBRAIN_SERVER_URL,
    ENDPOINTS: {
        // Internal endpoint for APIClient cost-based spending
        CREDITS_CONSUME_COST: '/credits/consume-cost'
    }
} as const;

// ============================================================================
// Internal service-to-service auth (APIClient -> YanBrainServer)
// ============================================================================

export const INTERNAL_AUTH = {
    SECRET: process.env.YANBRAIN_INTERNAL_SECRET || ''
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
        'FIREBASE_PROJECT_ID',
        'YANBRAIN_INTERNAL_SECRET'
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