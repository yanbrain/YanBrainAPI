import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { AppError } from '../errors/AppError';
import { LLMRequest, ApiResponse, LLMResponse } from '../types/api.types';

const router = Router();
const llmAdapter = new OpenAIAdapter();

/**
 * POST /api/llm
 * Generate text response from LLM
 *
 * Body:
 * {
 *   "productId": "yanAvatar",
 *   "message": "User's message",
 *   "embeddingFileIds": ["file_123", "file_456"] // Optional
 * }
 */
router.post('/',
  authMiddleware,
  async (req: Request<{}, {}, LLMRequest>, res: Response<ApiResponse<LLMResponse>>, next: NextFunction) => {
    try {
      const { productId, message } = req.body;
      // embeddingFileIds is available in req.body for future use

      // Validate productId
      if (!productId || typeof productId !== 'string') {
        throw AppError.validationError('productId is required and must be a string', ['productId']);
      }

      // Validate message
      if (!message || typeof message !== 'string') {
        throw AppError.validationError('Message is required and must be a string', ['message']);
      }

      // Apply credits middleware dynamically based on productId
      await new Promise<void>((resolve, reject) => {
        creditsMiddleware(productId as any)(req, res, (error?: any) => {
          if (error) reject(error);
          else resolve();
        });
      });

      // Call LLM adapter
      // Note: embeddingFileIds can be used to retrieve context from embedded files
      const response = await llmAdapter.generateResponse(message, []);

      // Consume credits after successful response
      await consumeCredits(req);

      // Return response
      res.json({
        success: true,
        data: {
          response: response
        }
      });
    } catch (error) {
      next(error);
    }
  }
);

export default router;
