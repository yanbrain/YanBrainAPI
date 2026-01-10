import { Router, Request, Response, NextFunction } from 'express';
import multer from 'multer';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { WitAdapter } from '../adapters/WitAdapter';
import { PRODUCT_IDS } from '../config/constants';
import { AppError } from '../errors/AppError';
import { ApiResponse, STTResponse } from '../types/api.types';

const router = Router();
const sttAdapter = new WitAdapter();

// Configure multer for file upload (memory storage)
const upload = multer({ 
  storage: multer.memoryStorage(),
  limits: {
    fileSize: 10 * 1024 * 1024 // 10MB max
  }
});

/**
 * POST /api/stt
 * Convert speech to text
 * 
 * Body (multipart/form-data):
 * {
 *   "audio": File (audio file)
 * }
 */
router.post('/', 
  authMiddleware,
  creditsMiddleware(PRODUCT_IDS.YANAVATAR),
  upload.single('audio'),
  async (req: Request, res: Response<ApiResponse<STTResponse>>, next: NextFunction) => {
    try {
      // Validate audio file
      if (!req.file) {
        throw AppError.validationError('Audio file is required', ['audio']);
      }

      const audioBuffer = req.file.buffer;
      const contentType = req.file.mimetype;

      // Call STT adapter
      const text = await sttAdapter.speechToText(audioBuffer, { contentType });

      // Consume credits after successful response
      await consumeCredits(req);

      // Return transcribed text
      res.json({
        success: true,
        data: {
          text: text,
          provider: sttAdapter.getProviderInfo()
        }
      });
    } catch (error) {
      next(error);
    }
  }
);

export default router;
