// src/routes/ragTextRoutes.ts - UPDATE
import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { AppError } from '../errors/AppError';
import { RagTextRequest, ApiResponse, RagTextResponse } from '../types/api.types';
import { CREDIT_COSTS } from '../config/constants';

const router = Router();
const llmAdapter = new OpenAIAdapter();

router.post(
    '/',
    authMiddleware,
    creditsMiddleware(CREDIT_COSTS.RAG_TEXT_REQUEST),
    async (
        req: Request<{}, {}, RagTextRequest>,
        res: Response<ApiResponse<RagTextResponse>>,
        next: NextFunction
    ) => {
        try {
            const { userPrompt, ragContext, systemPrompt, maxResponseChars } = req.body;

            if (typeof userPrompt !== 'string' || userPrompt.trim().length === 0) {
                throw AppError.validationError('userPrompt is required', ['userPrompt']);
            }

            if (typeof ragContext !== 'string' || ragContext.trim().length === 0) {
                throw AppError.validationError('ragContext is required', ['ragContext']);
            }

            const defaultSystemPrompt = 'You are a helpful AI assistant that answers questions based on provided context.';

            const textResponse = await llmAdapter.generateResponse(
                userPrompt,
                {
                    systemPrompt: systemPrompt || defaultSystemPrompt,
                    ragContext: ragContext,
                    maxResponseChars: maxResponseChars || 300
                }
            );

            await consumeCredits(req);

            res.json({
                success: true,
                data: {
                    textResponse,
                    model: llmAdapter.getModelInfo()
                }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;