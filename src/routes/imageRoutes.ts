import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { RunwareAdapter } from '../adapters/RunwareAdapter';
import { PRODUCT_IDS } from '../config/constants';
import { AppError } from '../errors/AppError';
import { ImageRequest, ApiResponse, ImageResponse } from '../types/api.types';

const router = Router();
const imageAdapter = new RunwareAdapter();

/**
 * POST /api/image
 * Generate image from text prompt
 * 
 * Body:
 * {
 *   "prompt": "Description of image",
 *   "width": 512,  // Optional
 *   "height": 512, // Optional
 *   "productId": "yanDraw" or "yanPhotobooth" // Required
 * }
 */
router.post('/', 
  authMiddleware,
  async (req: Request<{}, {}, ImageRequest>, res: Response<ApiResponse<ImageResponse>>, next: NextFunction) => {
    try {
      const { prompt, width, height, productId } = req.body;

      // Validate input
      if (!prompt || typeof prompt !== 'string') {
        throw AppError.validationError('Prompt is required and must be a string', ['prompt']);
      }

      if (!productId || ![PRODUCT_IDS.YANDRAW, PRODUCT_IDS.YANPHOTOBOOTH].includes(productId as any)) {
        throw AppError.validationError(
          `productId must be either '${PRODUCT_IDS.YANDRAW}' or '${PRODUCT_IDS.YANPHOTOBOOTH}'`,
          ['productId']
        );
      }

      // Apply credits middleware dynamically based on productId
      await new Promise<void>((resolve, reject) => {
        creditsMiddleware(productId as any)(req, res, (error?: any) => {
          if (error) reject(error);
          else resolve();
        });
      });

      // Call image generation adapter
      const imageUrl = await imageAdapter.generateImage(prompt, { width, height });

      // Consume credits after successful response
      await consumeCredits(req);

      // Return image URL
      res.json({
        success: true,
        data: {
          imageUrl: imageUrl,
          prompt: prompt,
          dimensions: { width: width || 512, height: height || 512 },
          provider: imageAdapter.getProviderInfo()
        }
      });
    } catch (error) {
      next(error);
    }
  }
);

export default router;
