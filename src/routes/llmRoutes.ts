import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { PRODUCT_IDS } from '../config/constants';
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
 *   "message": "User's message",
 *   "conversationHistory": [] // Optional
 * }
 */
router.post('/', 
  authMiddleware,
  creditsMiddleware(PRODUCT_IDS.YANAVATAR),
  async (req: Request<{}, {}, LLMRequest>, res: Response<ApiResponse<LLMResponse>>, next: NextFunction) => {
    try {
      const { message, conversationHistory } = req.body;

      // Validate input
      if (!message || typeof message !== 'string') {
        throw AppError.validationError('Message is required and must be a string', ['message']);
      }

      // Call LLM adapter
      const response = await llmAdapter.generateResponse(message, conversationHistory || []);

      // Consume credits after successful response
      await consumeCredits(req);

      // Return response
      res.json({
        success: true,
        data: {
          response: response,
          model: llmAdapter.getModelInfo()
        }
      });
    } catch (error) {
      next(error);
    }
  }
);

export default router;
