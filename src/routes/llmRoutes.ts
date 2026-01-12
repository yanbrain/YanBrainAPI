import { Router, Request, Response } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { AppError } from '../errors/AppError';

const router = Router();
const llmAdapter = new OpenAIAdapter();

// ✅ Explicit request body type
interface LLMRequestBody {
    prompt: string;
    systemPrompt?: string;
}

/**
 * POST /api/llm
 * Stateless LLM endpoint (no memory)
 */
router.post(
    '/llm',
    authMiddleware,
    async (
        req: Request<{}, {}, LLMRequestBody>,
        res: Response
    ) => {
        try {
            const { prompt, systemPrompt } = req.body;

            // ✅ Now TypeScript KNOWS prompt is string
            if (prompt.trim().length === 0) {
                throw AppError.validationError('Prompt is required', ['prompt']);
            }

            if (
                systemPrompt !== undefined &&
                systemPrompt.trim().length === 0
            ) {
                throw AppError.validationError(
                    'systemPrompt cannot be empty',
                    ['systemPrompt']
                );
            }

            const response = await llmAdapter.generateResponse(prompt, {
                systemPrompt
            });

            res.json({
                success: true,
                data: {
                    response,
                    model: llmAdapter.getModelInfo()
                }
            });
        } catch (error) {
            if (error instanceof AppError) {
                res.status(error.statusCode).json({
                    success: false,
                    error: {
                        code: error.code,
                        message: error.message,
                        details: error.details
                    }
                });
                return;
            }

            console.error('[LLM Route] Unexpected error:', error);

            res.status(500).json({
                success: false,
                error: {
                    code: 'INTERNAL_ERROR',
                    message: 'An unexpected error occurred'
                }
            });
        }
    }
);

export default router;
