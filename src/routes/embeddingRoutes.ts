import { Router, Request, Response, NextFunction } from 'express';
import { authMiddleware } from '../middleware/authMiddleware';
import { creditsMiddleware, consumeCredits } from '../middleware/creditsMiddleware';
import { OpenAIEmbeddingAdapter } from '../adapters/OpenAIEmbeddingAdapter';
import { AppError } from '../errors/AppError';
import { EmbeddingRequest, ApiResponse, EmbeddingResponse } from '../types/api.types';
import { calculateEmbeddingCost } from '../utils/costs';

const router = Router();
const embeddingAdapter = new OpenAIEmbeddingAdapter();

/**
 * POST /api/embeddings
 * Extract text from files and generate embeddings
 *
 * Body:
 * {
 *   "files": [
 *     {
 *       "filename": "document.pdf",
 *       "contentBase64": "base64 encoded file content"
 *     }
 *   ]
 * }
 */
router.post('/',
  authMiddleware,
  async (req: Request<{}, {}, EmbeddingRequest>, res: Response<ApiResponse<EmbeddingResponse>>, next: NextFunction) => {
    try {
      const { files } = req.body;

      // Validate files
      if (!files || !Array.isArray(files) || files.length === 0) {
        throw AppError.validationError('files is required and must be a non-empty array', ['files']);
      }

      // Validate each file
      for (let i = 0; i < files.length; i++) {
        const file = files[i];
        if (!file.filename || typeof file.filename !== 'string') {
          throw AppError.validationError(`files[${i}].filename is required and must be a string`, [`files[${i}].filename`]);
        }
        if (!file.contentBase64 || typeof file.contentBase64 !== 'string') {
          throw AppError.validationError(`files[${i}].contentBase64 is required and must be a string`, [`files[${i}].contentBase64`]);
        }
      }

      const cost = calculateEmbeddingCost(files);

      // Apply credits middleware dynamically based on request cost
      await new Promise<void>((resolve, reject) => {
        creditsMiddleware(cost)(req, res, (error?: any) => {
          if (error) reject(error);
          else resolve();
        });
      });

      // Process files and extract text
      const processedFiles = await embeddingAdapter.processFiles(files);

      // Consume credits after successful response
      await consumeCredits(req);

      // Return processed files with fileId and extracted text
      res.json({
        success: true,
        data: {
          files: processedFiles
        }
      });
    } catch (error) {
      next(error);
    }
  }
);

export default router;
