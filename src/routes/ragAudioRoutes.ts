// src/routes/ragAudioRoutes.ts - UPDATE
import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { ElevenLabsAdapter } from '../adapters/ElevenLabsAdapter';
import { AppError } from '../errors/AppError';
import { RagAudioRequest, ApiResponse, RagAudioResponse } from '../types/api.types';
import { CREDIT_COSTS } from '../config/constants';

const router = Router();
const llmAdapter = new OpenAIAdapter();
const ttsAdapter = new ElevenLabsAdapter();

router.post(
    '/',
    authMiddleware,
    creditsMiddleware(CREDIT_COSTS.RAG_AUDIO_REQUEST),
    async (
        req: Request<{}, {}, RagAudioRequest>,
        res: Response<ApiResponse<RagAudioResponse>>,
        next: NextFunction
    ) => {
        try {
            const { userPrompt, ragContext, systemPrompt, additionalInstructions, voiceId, maxResponseChars } = req.body;

            if (typeof userPrompt !== 'string' || userPrompt.trim().length === 0) {
                throw AppError.validationError('userPrompt is required', ['userPrompt']);
            }

            if (typeof ragContext !== 'string' || ragContext.trim().length === 0) {
                throw AppError.validationError('ragContext is required', ['ragContext']);
            }

            const textResponse = await llmAdapter.generateResponse(
                userPrompt,
                {
                    systemPrompt: systemPrompt,
                    additionalInstructions: additionalInstructions,
                    ragContext: ragContext,
                    maxResponseChars: maxResponseChars || 300
                }
            );

            const audioBuffer = await ttsAdapter.textToSpeech(textResponse, voiceId);

            await consumeCredits(req);

            res.json({
                success: true,
                data: {
                    audio: audioBuffer.toString('base64'),
                    textResponse
                }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;