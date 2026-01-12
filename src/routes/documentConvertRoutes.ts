import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { AppError } from '../errors/AppError';
import { DocumentConvertRequest, ApiResponse, DocumentConvertResponse } from '../types/api.types';
import { CREDIT_COSTS } from '../config/constants';
import { convertDocumentToText } from '../utils/documentConverter';
import { randomUUID } from 'crypto';

const router = Router();

/**
 * POST /api/documents/convert
 *
 * Converts documents to text.
 * Server stores nothing; user stores results locally.
 *
 * Cost: 1 credit per file
 */
router.post(
    '/',
    authMiddleware,
    async (
        req: Request<{}, {}, DocumentConvertRequest>,
        res: Response<ApiResponse<DocumentConvertResponse>>,
        next: NextFunction
    ) => {
        try {
            const { files } = req.body;

            if (!files || !Array.isArray(files) || files.length === 0) {
                throw AppError.validationError('files must be a non-empty array', ['files']);
            }

            for (let i = 0; i < files.length; i++) {
                if (!files[i].filename || !files[i].contentBase64) {
                    throw AppError.validationError(
                        `files[${i}] missing filename or contentBase64`,
                        [`files[${i}]`]
                    );
                }
            }

            const totalCost = files.length * CREDIT_COSTS.DOCUMENT_CONVERT_PER_FILE;

            // Apply credits middleware with dynamic cost
            await new Promise<void>((resolve, reject) => {
                creditsMiddleware(totalCost)(req, res, (error?: any) => {
                    error ? reject(error) : resolve();
                });
            });

            console.log(`[DocumentConvert] Converting ${files.length} files for user ${req.user?.uid}`);

            const convertedFiles = await Promise.all(
                files.map(async (file) => {
                    try {
                        const fileBuffer = Buffer.from(file.contentBase64, 'base64');
                        const text = await convertDocumentToText(fileBuffer, file.filename);

                        console.log(`[DocumentConvert] ✓ ${file.filename}: ${text.length} chars`);

                        return {
                            fileId: `file_${randomUUID()}`,
                            filename: file.filename,
                            text,
                            characterCount: text.length
                        };
                    } catch (error: any) {
                        console.error(`[DocumentConvert] ✗ ${file.filename}: ${error.message}`);
                        throw AppError.validationError(
                            `Failed to convert ${file.filename}: ${error.message}`,
                            ['files']
                        );
                    }
                })
            );

            await consumeCredits(req);

            res.json({
                success: true,
                data: {
                    files: convertedFiles,
                    totalFiles: convertedFiles.length,
                    totalCreditsCharged: totalCost
                }
            });
        } catch (error) {
            next(error);
        }
    }
);

export default router;
