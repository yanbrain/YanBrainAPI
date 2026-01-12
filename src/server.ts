import express, { Request, Response } from 'express';
import cors from 'cors';
import dotenv from 'dotenv';
import * as admin from 'firebase-admin';

// Import middleware
import { errorMiddleware } from './middleware/errorMiddleware';

// Import PRODUCTION routes
import documentConvertRoutes from './routes/documentConvertRoutes';
import embeddingRoutes from './routes/embeddingRoutes';
import yanAvatarRoutes from './routes/yanAvatarRoutes';
import imageRoutes from './routes/imageRoutes';
import llmRoutes from './routes/llmRoutes';
import ttsRoutes from './routes/ttsRoutes';

// Load environment variables
dotenv.config();

// Initialize Firebase Admin (for token verification)
admin.initializeApp({
    projectId: process.env.FIREBASE_PROJECT_ID
});

// Create Express app
const app = express();
const PORT = process.env.PORT || 8080;

// Middleware
app.use(cors());
app.use(express.json({ limit: '50mb' }));
app.use(express.urlencoded({ extended: true, limit: '50mb' }));

// Health check endpoint
app.get('/health', (_req: Request, res: Response) => {
    res.json({
        status: 'ok',
        service: 'YanBrain API Client',
        timestamp: new Date().toISOString(),
        version: '1.0.0'
    });
});

// ============================================================================
// PRODUCTION ROUTES
// ============================================================================
app.use('/api/documents/convert', documentConvertRoutes);
app.use('/api/embeddings', embeddingRoutes);

app.use('/api/yanavatar', yanAvatarRoutes);
app.use('/api/image', imageRoutes);

// Keep LLM + TTS as production routes (callable externally)
app.use('/api/llm', llmRoutes);
app.use('/api/tts', ttsRoutes);

// 404 handler
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

// Error handling middleware (must be last)
app.use(errorMiddleware);

// Start server
app.listen(PORT, () => {
    console.log(`🚀 YanBrain API Client running on port ${PORT}`);
    console.log(`📍 Health check: http://localhost:${PORT}/health`);
    console.log(`🔧 Environment: ${process.env.NODE_ENV || 'development'}`);

    console.log(`\n✅ Endpoints:`);
    console.log(`   POST /api/documents/convert - Convert documents to text`);
    console.log(`   POST /api/embeddings - Generate embeddings from text`);
    console.log(`   POST /api/yanavatar - Query with voice response`);
    console.log(`   POST /api/image - Generate images`);
    console.log(`   POST /api/llm - Stateless LLM call`);
    console.log(`   POST /api/tts - Text-to-speech`);
});

// Graceful shutdown
process.on('SIGTERM', () => {
    console.log('SIGTERM signal received: closing HTTP server');
    process.exit(0);
});

process.on('SIGINT', () => {
    console.log('SIGINT signal received: closing HTTP server');
    process.exit(0);
});
