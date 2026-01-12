import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { AppError } from '../errors/AppError';
import { ApiResponse } from '../types/api.types';

const router = Router();
const llmAdapter = new OpenAIAdapter();

interface LLMRequestBody {
    prompt: string;
    systemPrompt?: string;
}

/**
 * POST /api/llm
 * Stateless LLM endpoint (no memory)
 */
router.post(
    '/',
    authMiddleware,
    async (
        req: Request<{}, {}, LLMRequestBody>,
        res: Response<ApiResponse<{ response: string; model: any }>>,
        next: NextFunction
    ) => {
        try {
            const { prompt, systemPrompt } = req.body;

            if (typeof prompt !== 'string' || prompt.trim().length === 0) {
                throw AppError.validationError('Prompt is required', ['prompt']);
            }

            if (systemPrompt !== undefined && systemPrompt.trim().length === 0) {
                throw AppError.validationError('systemPrompt cannot be empty', ['systemPrompt']);
            }

            const responseText = await llmAdapter.generateResponse(prompt, {
                systemPrompt
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