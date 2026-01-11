import dotenv from 'dotenv';

dotenv.config();

export const CREDIT_COSTS = {
    LLM_REQUEST: 1,
    IMAGE_GENERATION: 10,
    TTS_REQUEST: 2,
    EMBEDDING_MIN: 5,
    EMBEDDING_CREDITS_PER_1K_CHARS: 1
} as const;

export const YANBRAIN_SERVER = {
    BASE_URL: process.env.YANBRAIN_SERVER_URL || 'http://localhost:5001/api',
    ENDPOINTS: {
        CONSUME_COST: '/credits/consume-cost'
    }
} as const;

export const INTERNAL_AUTH = {
    SECRET: process.env.YANBRAIN_INTERNAL_SECRET || ''
} as const;

export const API_KEYS = {
    OPENAI: process.env.OPENAI_API_KEY || '',
    ELEVENLABS: process.env.ELEVENLABS_API_KEY || '',
    RUNWARE: process.env.RUNWARE_API_KEY || ''
} as const;
