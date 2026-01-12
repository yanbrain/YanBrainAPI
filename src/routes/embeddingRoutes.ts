import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIEmbeddingAdapter } from '../adapters/OpenAIEmbeddingAdapter';
import { AppError } from '../errors/AppError';
import { EmbeddingRequest, ApiResponse, EmbeddingResponse } from '../types/api.types';
import { calculateEmbeddingCost } from '../utils/costs';
import { convertDocumentToText } from '../utils/documentConverter';
import { randomUUID } from 'crypto';

const router = Router();
const embeddingAdapter = new OpenAIEmbeddingAdapter();

router.post('/',
    authMiddleware,
    async (req: Request<{}, {}, EmbeddingRequest>, res: Response<ApiResponse<EmbeddingResponse>>, next: NextFunction) => {
        try {
            const { files } = req.body;

            if (!files || !Array.isArray(files) || files.length === 0) {
                throw AppError.validationError('files must be a non-empty array', ['files']);
            }

            for (let i = 0; i < files.length; i++) {
                if (!files[i].filename || !files[i].contentBase64) {
                    throw AppError.validationError(`files[${i}] missing filename or contentBase64`, [`files[${i}]`]);
                }
            }

            const cost = calculateEmbeddingCost(files);

            // Apply middleware manually for dynamic cost
            await new Promise<void>((resolve, reject) => {
                creditsMiddleware(cost)(req, res, (error?: any) => {
                    error ? reject(error) : resolve();
                });
            });

            const processedFiles = await Promise.all(
                files.map(async (file) => {
                    const fileBuffer = Buffer.from(file.contentBase64, 'base64');
                    const text = await convertDocumentToText(fileBuffer, file.filename);
                    const embedding = await embeddingAdapter.generateEmbedding(text);

                    return {
                        fileId: `file_${randomUUID()}`,
                        filename: file.filename,
                        embedding,
                        dimensions: embeddingAdapter.getDimensions()
                    };
                })
            );

            await consumeCredits(req);

            res.json({
                success: true,
                data: { files: processedFiles }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;