import dotenv from 'dotenv';

dotenv.config();

// ============================================================================
// Credit Costs (defined in YanBrainAPI)
// ============================================================================

export const CREDIT_COSTS = {
    // Production endpoints
    DOCUMENT_CONVERT_PER_FILE: 1, // Cost per file for conversion to text
    EMBEDDING_PER_ITEM: 1,        // Cost per embedding item (one text -> one embedding)

    // Existing production endpoints
    YANAVATAR_REQUEST: 5, // Fixed cost per YanAvatar request (includes LLM + TTS)
    IMAGE_GENERATION: 10  // For other app
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
