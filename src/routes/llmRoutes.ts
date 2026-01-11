import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { AppError } from '../errors/AppError';
import { LLMRequest, ApiResponse, LLMResponse } from '../types/api.types';
import { CREDIT_COSTS } from '../config/constants';

const router = Router();
const llmAdapter = new OpenAIAdapter();

/**
 * POST /api/llm
 * Generate text response from LLM
 *
 * Body:
 * {
 *   "message": "User's message",
 *   "embeddingFileIds": ["file_123", "file_456"] // Optional
 * }
 */
router.post('/',
  authMiddleware,
  async (req: Request<{}, {}, LLMRequest>, res: Response<ApiResponse<LLMResponse>>, next: NextFunction) => {
    try {
      const { message } = req.body;
      // embeddingFileIds is available in req.body for future use

      // Validate message
      if (!message || typeof message !== 'string') {
        throw AppError.validationError('Message is required and must be a string', ['message']);
      }

      const cost = CREDIT_COSTS.LLM_REQUEST;

      // Apply credits middleware dynamically based on request cost
      await new Promise<void>((resolve, reject) => {
        creditsMiddleware(cost)(req, res, (error?: any) => {
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
