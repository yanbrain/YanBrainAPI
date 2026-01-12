import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { AppError } from '../errors/AppError';
import { LLMRequest, ApiResponse, LLMResponse } from '../types/api.types';
import { CREDIT_COSTS } from '../config/constants';

const router = Router();
const llmAdapter = new OpenAIAdapter();

router.post('/',
    authMiddleware,
    creditsMiddleware(CREDIT_COSTS.LLM_REQUEST),
    async (req: Request<{}, {}, LLMRequest>, res: Response<ApiResponse<LLMResponse>>, next: NextFunction) => {
        try {
            const { message, systemPrompt, embeddedText } = req.body;

            if (!message?.trim()) {
                throw AppError.validationError('Message is required', ['message']);
            }

            const response = await llmAdapter.generateResponse(message, [], {
                systemPrompt,
                embeddedText
            });

            await consumeCredits(req);

            res.json({
                success: true,
                data: { response }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;