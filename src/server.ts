// src/server.ts
import express, { Request, Response } from 'express';
import cors from 'cors';
import dotenv from 'dotenv';
import * as admin from 'firebase-admin';

import { errorMiddleware } from './middleware/errorMiddleware';

import documentConvertRoutes from './routes/documentConvertRoutes';
import embeddingRoutes from './routes/embeddingRoutes';
import ragTextRoutes from './routes/ragTextRoutes';
import ragAudioRoutes from './routes/ragAudioRoutes';
import imageRoutes from './routes/imageRoutes';
import llmRoutes from './routes/llmRoutes';
import ttsRoutes from './routes/ttsRoutes';

dotenv.config();

admin.initializeApp({
    projectId: process.env.FIREBASE_PROJECT_ID
});

const app = express();
const PORT = process.env.PORT || 8080;

app.use(cors());
app.use(express.json({ limit: '50mb' }));
app.use(express.urlencoded({ extended: true, limit: '50mb' }));

app.get('/health', (_req: Request, res: Response) => {
    res.json({
        status: 'ok',
        service: 'YanBrain API Client',
        timestamp: new Date().toISOString(),
        version: '1.0.0'
    });
});

// ============================================================================
// PUBLIC ROUTES (Firebase Auth Required)
// ============================================================================
app.use('/api/documents/convert', documentConvertRoutes);
app.use('/api/embeddings', embeddingRoutes);
app.use('/api/rag/text', ragTextRoutes);
app.use('/api/rag/audio', ragAudioRoutes);
app.use('/api/image', imageRoutes);

// ============================================================================
// INTERNAL ROUTES (Internal Secret Required)
// ============================================================================
app.use('/api/llm', llmRoutes);
app.use('/api/tts', ttsRoutes);

app.use((_req: Request, res: Response) => {
    res.status(404).json({
        success: false,
        error: {
            code: 'NOT_FOUND',
            message: 'Endpoint not found',
            statusCode: 404
        }
    });
});

app.use(errorMiddleware);

app.listen(PORT, () => {
    console.log(`🚀 YanBrain API Client running on port ${PORT}`);
    console.log(`📍 Health check: http://localhost:${PORT}/health`);
    console.log(`🔧 Environment: ${process.env.NODE_ENV || 'development'}`);

    console.log(`\n✅ PUBLIC Endpoints (Firebase Auth):`);
    console.log(`   POST /api/documents/convert - Convert documents to text`);
    console.log(`   POST /api/embeddings - Generate embeddings from text`);
    console.log(`   POST /api/rag/text - RAG query with text response`);
    console.log(`   POST /api/rag/audio - RAG query with audio response`);
    console.log(`   POST /api/image - Generate images`);

    console.log(`\n🔒 INTERNAL Endpoints (Internal Secret):`);
    console.log(`   POST /api/llm - Low-level LLM call`);
    console.log(`   POST /api/tts - Low-level TTS call`);
});

process.on('SIGTERM', () => {
    console.log('SIGTERM signal received: closing HTTP server');
    process.exit(0);
});

process.on('SIGINT', () => {
    console.log('SIGINT signal received: closing HTTP server');
    process.exit(0);
});