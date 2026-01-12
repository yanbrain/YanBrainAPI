import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIEmbeddingAdapter } from '../adapters/OpenAIEmbeddingAdapter';
import { AppError } from '../errors/AppError';
import { DocumentConvertRequest, ApiResponse, DocumentConvertResponse } from '../types/api.types';
import { CREDIT_COSTS } from '../config/constants';
import { convertDocumentToText } from '../utils/documentConverter';
import { randomUUID } from 'crypto';

const router = Router();
const embeddingAdapter = new OpenAIEmbeddingAdapter();

/**
 * POST /api/documents/convert-and-embed
 *
 * Converts documents to text and generates embeddings
 * User stores the results locally (server keeps nothing)
 *
 * Cost: 1 credit per file
 */
router.post('/',
    authMiddleware,
    async (req: Request<{}, {}, DocumentConvertRequest>, res: Response<ApiResponse<DocumentConvertResponse>>, next: NextFunction) => {
        try {
            const { files } = req.body;

            // Validation
            if (!files || !Array.isArray(files) || files.length === 0) {
                throw AppError.validationError('files must be a non-empty array', ['files']);
            }

            // Check each file has required fields
            for (let i = 0; i < files.length; i++) {
                if (!files[i].filename || !files[i].contentBase64) {
                    throw AppError.validationError(
                        `files[${i}] missing filename or contentBase64`,
                        [`files[${i}]`]
                    );
                }
            }

            // Calculate cost: 1 credit per file
            const totalCost = files.length * CREDIT_COSTS.DOCUMENT_CONVERT_AND_EMBED_PER_FILE;

            // Apply credits middleware with dynamic cost
            await new Promise<void>((resolve, reject) => {
                creditsMiddleware(totalCost)(req, res, (error?: any) => {
                    error ? reject(error) : resolve();
                });
            });

            console.log(`[DocumentConvert] Processing ${files.length} files for user ${req.user?.uid}`);

            // Process all files in parallel
            const processedFiles = await Promise.all(
                files.map(async (file) => {
                    try {
                        // Convert to text
                        const fileBuffer = Buffer.from(file.contentBase64, 'base64');
                        const text = await convertDocumentToText(fileBuffer, file.filename);

                        // Generate embedding
                        const embedding = await embeddingAdapter.generateEmbedding(text);

                        console.log(`[DocumentConvert] ✓ ${file.filename}: ${text.length} chars, ${embedding.length}D`);

                        return {
                            fileId: `file_${randomUUID()}`,
                            filename: file.filename,
                            text: text,
                            embedding: embedding,
                            dimensions: embeddingAdapter.getDimensions(),
                            characterCount: text.length
                        };
                    } catch (error: any) {
                        console.error(`[DocumentConvert] ✗ ${file.filename}: ${error.message}`);
                        throw AppError.validationError(
                            `Failed to process ${file.filename}: ${error.message}`,
                            ['files']
                        );
                    }
                })
            );

            // Consume credits after successful processing
            await consumeCredits(req);

            console.log(`[DocumentConvert] Success: ${processedFiles.length} files processed, ${totalCost} credits charged`);

            res.json({
                success: true,
                data: {
                    files: processedFiles,
                    totalFiles: processedFiles.length,
                    totalCreditsCharged: totalCost
                }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;