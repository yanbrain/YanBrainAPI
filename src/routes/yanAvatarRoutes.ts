import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIAdapter } from '../adapters/OpenAIAdapter';
import { ElevenLabsAdapter } from '../adapters/ElevenLabsAdapter';
import { AppError } from '../errors/AppError';
import { YanAvatarRequest, ApiResponse, YanAvatarResponse } from '../types/api.types';
import { CREDIT_COSTS } from '../config/constants';

const router = Router();
const llmAdapter = new OpenAIAdapter();
const ttsAdapter = new ElevenLabsAdapter();

/**
 * POST /api/yanavatar
 *
 * Stateless YanAvatar endpoint
 * Cost: 5 credits per request
 */
router.post(
    '/',
    authMiddleware,
    creditsMiddleware(CREDIT_COSTS.YANAVATAR_REQUEST),
    async (
        req: Request<{}, {}, YanAvatarRequest>,
        res: Response<ApiResponse<YanAvatarResponse>>,
        next: NextFunction
    ) => {
        try {
            const { userPrompt, relevantDocuments, systemPrompt, voiceId } = req.body;

            // Validation
            if (typeof userPrompt !== 'string' || userPrompt.trim().length === 0) {
                throw AppError.validationError(
                    'userPrompt is required and must be a non-empty string',
                    ['userPrompt']
                );
            }

            if (!Array.isArray(relevantDocuments) || relevantDocuments.length === 0) {
                throw AppError.validationError(
                    'relevantDocuments must be a non-empty array',
                    ['relevantDocuments']
                );
            }

            for (let i = 0; i < relevantDocuments.length; i++) {
                if (
                    typeof relevantDocuments[i].filename !== 'string' ||
                    typeof relevantDocuments[i].text !== 'string'
                ) {
                    throw AppError.validationError(
                        `relevantDocuments[${i}] missing filename or text`,
                        [`relevantDocuments[${i}]`]
                    );
                }
            }

            console.log(
                `[YanAvatar] User ${req.user?.uid}: "${userPrompt.substring(0, 50)}..."`
            );

            // Build document context
            const documentContext = relevantDocuments
                .map((doc, index) =>
                    `Document ${index + 1}: ${doc.filename}\n${doc.text}`
                )
                .join('\n\n---\n\n');

            const defaultSystemPrompt =
                'You are YanAvatar, an AI assistant that answers questions based on provided documents. Always cite which document you used when answering.';

            // ✅ Stateless LLM call (NO history)
            const llmResponse = await llmAdapter.generateResponse(
                userPrompt,
                {
                    systemPrompt: systemPrompt || defaultSystemPrompt,
                    embeddedText: documentContext
                }
            );

            // Convert to speech
            const audioBuffer = await ttsAdapter.textToSpeech(llmResponse, voiceId);

            // Consume credits
            await consumeCredits(req);

            res.json({
                success: true,
                data: {
                    audio: audioBuffer.toString('base64'),
                    textResponse: llmResponse,
                    documentsUsed: relevantDocuments.length
                }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;
