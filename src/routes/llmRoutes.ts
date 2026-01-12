import { Router, Request, Response } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { AppError } from '../errors/AppError';

const router = Router();
const llmAdapter = new OpenAIAdapter();

router.post('/llm', authMiddleware, async (req: Request, res: Response) => {
    try {
        const { prompt, conversationHistory, systemPrompt } = req.body;

        if (!prompt || typeof prompt !== 'string' || prompt.trim().length === 0) {
            throw AppError.validationError('Prompt is required', ['prompt']);
        }

        const response = await llmAdapter.generateResponse(
            prompt,
            conversationHistory || [],
            { systemPrompt }
        );

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
        } else {
            console.error('[LLM Routes] Unexpected error:', error);
            res.status(500).json({
                success: false,
                error: {
                    code: 'INTERNAL_ERROR',
                    message: 'An unexpected error occurred'
                }
            });
        }
    }
});

export default router;