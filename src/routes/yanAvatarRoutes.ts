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
 * Main YanAvatar endpoint:
 * - Receives user prompt + relevant documents (user did vector search locally)
 * - LLM generates answer using document context
 * - TTS converts answer to audio
 * - Returns audio MP3
 *
 * Cost: 5 credits per request (fixed)
 */
router.post('/',
    authMiddleware,
    creditsMiddleware(CREDIT_COSTS.YANAVATAR_REQUEST),
    async (req: Request<{}, {}, YanAvatarRequest>, res: Response<ApiResponse<YanAvatarResponse>>, next: NextFunction) => {
        try {
            const { userPrompt, relevantDocuments, systemPrompt, voiceId } = req.body;

            // Validation
            if (!userPrompt || typeof userPrompt !== 'string' || userPrompt.trim().length === 0) {
                throw AppError.validationError('userPrompt is required and must be a non-empty string', ['userPrompt']);
            }

            if (!relevantDocuments || !Array.isArray(relevantDocuments) || relevantDocuments.length === 0) {
                throw AppError.validationError('relevantDocuments must be a non-empty array', ['relevantDocuments']);
            }

            // Validate each document
            for (let i = 0; i < relevantDocuments.length; i++) {
                if (!relevantDocuments[i].filename || !relevantDocuments[i].text) {
                    throw AppError.validationError(
                        `relevantDocuments[${i}] missing filename or text`,
                        [`relevantDocuments[${i}]`]
                    );
                }
            }

            console.log(`[YanAvatar] User ${req.user?.uid}: "${userPrompt.substring(0, 50)}..."`);
            console.log(`[YanAvatar] Documents: ${relevantDocuments.length}`);

            // Step 1: Build context from relevant documents
            const documentContext = relevantDocuments.map((doc, index) => {
                return `Document ${index + 1}: ${doc.filename}\n${doc.text}`;
            }).join('\n\n---\n\n');

            console.log(`[YanAvatar] Context size: ${documentContext.length} characters`);

            // Step 2: Generate LLM response with document context
            const defaultSystemPrompt = 'You are YanAvatar, an AI assistant that answers questions based on provided documents. Always cite which document you used when answering.';

            const llmResponse = await llmAdapter.generateResponse(
                userPrompt,
                [], // No conversation history
                {
                    systemPrompt: systemPrompt || defaultSystemPrompt,
                    embeddedText: documentContext
                }
            );

            console.log(`[YanAvatar] LLM response: ${llmResponse.length} characters`);

            // Step 3: Convert to speech
            const audioBuffer = await ttsAdapter.textToSpeech(llmResponse, voiceId);

            console.log(`[YanAvatar] Audio generated: ${Math.round(audioBuffer.length / 1024)} KB`);

            // Step 4: Consume credits after successful completion
            await consumeCredits(req);

            console.log(`[YanAvatar] Success: ${CREDIT_COSTS.YANAVATAR_REQUEST} credits charged`);

            // Return response
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