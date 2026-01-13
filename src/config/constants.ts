// src/config/constants.ts - UPDATE
import dotenv from 'dotenv';

dotenv.config();

export const CREDIT_COSTS = {
    DOCUMENT_CONVERT_PER_FILE: 1,
    EMBEDDING_PER_ITEM: 1,
    RAG_TEXT_REQUEST: 2,
    RAG_AUDIO_REQUEST: 5,
    IMAGE_GENERATION: 10
} as const;

export const YANBRAIN_SERVER = {
    BASE_URL: process.env.YANBRAIN_SERVER_URL,
    ENDPOINTS: {
        CREDITS_CONSUME_COST: '/credits/consume-cost'
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

export const FIREBASE_CONFIG = {
    PROJECT_ID: process.env.FIREBASE_PROJECT_ID || ''
} as const;