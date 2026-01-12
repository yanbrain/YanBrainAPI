import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { RunwareAdapter } from '../adapters/RunwareAdapter';
import { AppError } from '../errors/AppError';
import { ImageRequest, ApiResponse, ImageResponse } from '../types/api.types';
import { CREDIT_COSTS } from '../config/constants';

const router = Router();
const imageAdapter = new RunwareAdapter();

router.post('/',
    authMiddleware,
    creditsMiddleware(CREDIT_COSTS.IMAGE_GENERATION),
    async (req: Request<{}, {}, ImageRequest>, res: Response<ApiResponse<ImageResponse>>, next: NextFunction) => {
        try {
            const { prompt, imageBase64 } = req.body;

            if (!prompt?.trim()) {
                throw AppError.validationError('Prompt is required', ['prompt']);
            }

            const imageUrl = await imageAdapter.generateImage(prompt, { imageBase64 });

            await consumeCredits(req);

            res.json({
                success: true,
                data: { imageUrl }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;