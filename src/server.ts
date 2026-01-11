import express, { Request, Response } from 'express';
import cors from 'cors';
import dotenv from 'dotenv';
import * as admin from 'firebase-admin';

// Import middleware
import { errorMiddleware } from './middleware/errorMiddleware';

// Import routes
import llmRoutes from './routes/llmRoutes';
import ttsRoutes from './routes/ttsRoutes';
import imageRoutes from './routes/imageRoutes';
import embeddingRoutes from './routes/embeddingRoutes';

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

// API Routes
app.use('/api/llm', llmRoutes);
app.use('/api/tts', ttsRoutes);
app.use('/api/image', imageRoutes);
app.use('/api/embeddings', embeddingRoutes);

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
  console.log(`\n✅ Available Endpoints:`);
  console.log(`   POST /api/llm - Generate text from LLM`);
  console.log(`   POST /api/tts - Text to speech`);
  console.log(`   POST /api/stt - Speech to text`);
  console.log(`   POST /api/image - Generate images`);
  console.log(`   POST /api/embeddings - Generate embeddings`);
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
