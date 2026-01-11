import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { ElevenLabsAdapter } from '../adapters/ElevenLabsAdapter';
import { AppError } from '../errors/AppError';
import { TTSRequest, ApiResponse, TTSResponse } from '../types/api.types';

const router = Router();
const ttsAdapter = new ElevenLabsAdapter();

/**
 * POST /api/tts
 * Convert text to speech
 *
 * Body:
 * {
 *   "productId": "yanAvatar",
 *   "text": "Text to convert",
 *   "voiceId": "optional-voice-id"
 * }
 */
router.post('/',
  authMiddleware,
  async (req: Request<{}, {}, TTSRequest>, res: Response<ApiResponse<TTSResponse>>, next: NextFunction) => {
    try {
      const { productId, text, voiceId } = req.body;

      // Validate productId
      if (!productId || typeof productId !== 'string') {
        throw AppError.validationError('productId is required and must be a string', ['productId']);
      }

      // Validate text
      if (!text || typeof text !== 'string') {
        throw AppError.validationError('Text is required and must be a string', ['text']);
      }

      if (text.length > 5000) {
        throw AppError.validationError('Text must be less than 5000 characters', ['text']);
      }

      // Apply credits middleware dynamically based on productId
      await new Promise<void>((resolve, reject) => {
        creditsMiddleware(productId as any)(req, res, (error?: any) => {
          if (error) reject(error);
          else resolve();
        });
      });

      // Call TTS adapter
      const audioBuffer = await ttsAdapter.textToSpeech(text, voiceId);

      // Consume credits after successful response
      await consumeCredits(req);

      // Return audio as base64
      res.json({
        success: true,
        data: {
          audio: audioBuffer.toString('base64')
        }
      });
    } catch (error) {
      next(error);
    }
  }
);

export default router;
