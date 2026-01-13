// src/routes/ttsRoutes.ts - UPDATE
import { Router, Request, Response, NextFunction } from 'express';
import { internalSecretMiddleware } from '../middleware/internalSecretMiddleware';
import { ElevenLabsAdapter } from '../adapters/ElevenLabsAdapter';
import { AppError } from '../errors/AppError';
import { TTSRequest, ApiResponse, TTSResponse } from '../types/api.types';

const router = Router();
const ttsAdapter = new ElevenLabsAdapter();

/**
 * POST /api/tts
 * Internal-only TTS endpoint
 * Requires X-Yanbrain-Internal-Secret header
 */
router.post(
    '/',
    internalSecretMiddleware,
    async (
        req: Request<{}, {}, TTSRequest>,
        res: Response<ApiResponse<TTSResponse>>,
        next: NextFunction
    ) => {
        try {
            const { text, voiceId } = req.body;

            if (!text?.trim()) {
                throw AppError.validationError('Text is required', ['text']);
            }

            const audioBuffer = await ttsAdapter.textToSpeech(text, voiceId);

            res.json({
                success: true,
                data: { audio: audioBuffer.toString('base64') }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;