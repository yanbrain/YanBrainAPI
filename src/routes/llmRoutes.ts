// src/routes/llmRoutes.ts - UPDATE
import { Router, Request, Response, NextFunction } from 'express';
import { internalSecretMiddleware } from '../middleware/internalSecretMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { AppError } from '../errors/AppError';
import { ApiResponse } from '../types/api.types';

const router = Router();
const llmAdapter = new OpenAIAdapter();

interface LLMRequestBody {
    prompt: string;
    systemPrompt?: string;
    ragContext?: string;
}

/**
 * POST /api/llm
 * Internal-only LLM endpoint - no RAG, no length limits
 * Requires X-Yanbrain-Internal-Secret header
 */
router.post(
    '/',
    internalSecretMiddleware,
    async (
        req: Request<{}, {}, LLMRequestBody>,
        res: Response<ApiResponse<{ response: string; model: any }>>,
        next: NextFunction
    ) => {
        try {
            const { prompt, systemPrompt, ragContext } = req.body;

            if (typeof prompt !== 'string' || prompt.trim().length === 0) {
                throw AppError.validationError('Prompt is required', ['prompt']);
            }

            if (systemPrompt !== undefined && systemPrompt.trim().length === 0) {
                throw AppError.validationError('systemPrompt cannot be empty', ['systemPrompt']);
            }

            if (ragContext !== undefined && ragContext.trim().length === 0) {
                throw AppError.validationError('ragContext cannot be empty', ['ragContext']);
            }

            const responseText = await llmAdapter.generateResponse(prompt, {
                systemPrompt,
                ragContext
            });

            res.json({
                success: true,
                data: {
                    response: responseText,
                    model: llmAdapter.getModelInfo()
                }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;