import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIEmbeddingAdapter } from '../adapters/OpenAIEmbeddingAdapter';
import { PRODUCT_IDS } from '../config/constants';
import { AppError } from '../errors/AppError';
import { EmbeddingRequest, ApiResponse, EmbeddingResponse } from '../types/api.types';

const router = Router();
const embeddingAdapter = new OpenAIEmbeddingAdapter();

/**
 * POST /api/embeddings
 * Generate embeddings from text
 * 
 * Body:
 * {
 *   "text": "Text to convert to embeddings",
 *   "model": "text-embedding-3-small" // Optional
 * }
 */
router.post('/', 
  authMiddleware,
  creditsMiddleware(PRODUCT_IDS.YANAVATAR),
  async (req: Request<{}, {}, EmbeddingRequest>, res: Response<ApiResponse<EmbeddingResponse>>, next: NextFunction) => {
    try {
      const { text, model } = req.body;

      // Validate input
      if (!text || typeof text !== 'string') {
        throw AppError.validationError('Text is required and must be a string', ['text']);
      }

      if (text.length > 8000) {
        throw AppError.validationError('Text must be less than 8000 characters', ['text']);
      }

      // Call embedding adapter
      const embedding = await embeddingAdapter.generateEmbedding(text, model);

      // Consume credits after successful response
      await consumeCredits(req);

      // Return embedding vector
      res.json({
        success: true,
        data: {
          embedding: embedding,
          model: model || 'text-embedding-3-small',
          dimensions: embeddingAdapter.getDimensions(model)
        }
      });
    } catch (error) {
      next(error);
    }
  }
);

export default router;
