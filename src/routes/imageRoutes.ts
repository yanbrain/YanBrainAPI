import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { RunwareAdapter } from '../adapters/RunwareAdapter';
import { AppError } from '../errors/AppError';
import { ImageRequest, ApiResponse, ImageResponse } from '../types/api.types';
import { CREDIT_COSTS } from '../config/constants';

const router = Router();
const imageAdapter = new RunwareAdapter();

/**
 * POST /api/image
 * Generate image from text prompt
 *
 * Body:
 * {
 *   "prompt": "Description of image",
 *   "imageBase64": "base64 encoded image" // Image to edit
 * }
 */
router.post('/',
  authMiddleware,
  async (req: Request<{}, {}, ImageRequest>, res: Response<ApiResponse<ImageResponse>>, next: NextFunction) => {
    try {
      const { prompt, imageBase64 } = req.body;

      // Validate prompt
      if (!prompt || typeof prompt !== 'string') {
        throw AppError.validationError('Prompt is required and must be a string', ['prompt']);
      }

      // Validate imageBase64
      if (!imageBase64 || typeof imageBase64 !== 'string') {
        throw AppError.validationError('imageBase64 is required and must be a string', ['imageBase64']);
      }

      const cost = CREDIT_COSTS.IMAGE_GENERATION;

      // Apply credits middleware dynamically based on request cost
      await new Promise<void>((resolve, reject) => {
        creditsMiddleware(cost)(req, res, (error?: any) => {
          if (error) reject(error);
          else resolve();
        });
      });

      // Call image generation adapter with base64 image
      const imageUrl = await imageAdapter.generateImage(prompt, { imageBase64 });

      // Consume credits after successful response
      await consumeCredits(req);

      // Return image URL
      res.json({
        success: true,
        data: {
          imageUrl: imageUrl
        }
      });
    } catch (error) {
      next(error);
    }
  }
);

export default router;
