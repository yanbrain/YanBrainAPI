import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIEmbeddingAdapter } from '../adapters/OpenAIEmbeddingAdapter';
import { AppError } from '../errors/AppError';
import { CREDIT_COSTS } from '../config/constants';
import { ApiResponse, EmbeddingRequest, EmbeddingResponse } from '../types/api.types';

const router = Router();
const embeddingAdapter = new OpenAIEmbeddingAdapter();

/**
 * POST /api/embeddings
 *
 * Generates embeddings from provided text items.
 * Stateless: server stores nothing.
 *
 * Cost: 1 credit per item (EMBEDDING_PER_ITEM)
 */
router.post(
    '/',
    authMiddleware,
    async (
        req: Request<{}, {}, EmbeddingRequest>,
        res: Response<ApiResponse<EmbeddingResponse>>,
        next: NextFunction
    ) => {
        try {
            const { items } = req.body;

            if (!items || !Array.isArray(items) || items.length === 0) {
                throw AppError.validationError('items must be a non-empty array', ['items']);
            }

            for (let i = 0; i < items.length; i++) {
                if (typeof items[i]?.text !== 'string' || items[i].text.trim().length === 0) {
                    throw AppError.validationError(`items[${i}].text is required`, [`items[${i}].text`]);
                }
            }

            const totalCost = items.length * CREDIT_COSTS.EMBEDDING_PER_ITEM;

            // Apply credits middleware with dynamic cost
            await new Promise<void>((resolve, reject) => {
                creditsMiddleware(totalCost)(req, res, (error?: any) => {
                    error ? reject(error) : resolve();
                });
            });

            console.log(`[Embeddings] Generating ${items.length} embeddings for user ${req.user?.uid}`);

            const embeddedItems = await Promise.all(
                items.map(async (item, index) => {
                    try {
                        const trimmedText = item.text.trim();
                        const embedding = await embeddingAdapter.generateEmbedding(trimmedText);

                        console.log(`[Embeddings] ✓ item[${index}] ${trimmedText.length} chars, ${embedding.length}D`);

                        return {
                            id: item.id,
                            filename: item.filename,
                            embedding,
                            dimensions: embeddingAdapter.getDimensions(),
                            characterCount: trimmedText.length
                        };
                    } catch (error: any) {
                        console.error(`[Embeddings] ✗ item[${index}]: ${error.message}`);
                        throw AppError.validationError(
                            `Failed to embed item[${index}]: ${error.message}`,
                            ['items']
                        );
                    }
                })
            );

            // Consume credits after successful operation
            await consumeCredits(req);

            res.json({
                success: true,
                data: {
                    items: embeddedItems,
                    totalItems: embeddedItems.length,
                    totalCreditsCharged: totalCost,
                    provider: {
                        provider: embeddingAdapter.getProviderInfo().provider,
                        defaultModel: embeddingAdapter.getProviderInfo().defaultModel
                    }
                }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;
