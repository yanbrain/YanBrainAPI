import { Request, Response, NextFunction } from 'express';
import axios from 'axios';
import { YANBRAIN_SERVER, INTERNAL_AUTH } from '../config/constants';
import { AppError } from '../errors/AppError';
import { CreditsConsumeRequest } from '../types/api.types';

/**
 * Middleware to attach credit cost & firebase token to request.
 * Should be used AFTER authMiddleware.
 *
 * We intentionally do NOT call YanBrainServer balance here to reduce latency.
 * YanBrainServer will enforce "insufficient credits" at consume time.
 */
export function creditsMiddleware(cost: number) {
    return async (req: Request, _res: Response, next: NextFunction): Promise<void> => {
        try {
            const userId = req.user?.uid;

            if (!userId) {
                throw AppError.authError('User not authenticated');
            }

            const authHeader = req.headers.authorization;
            if (!authHeader || !authHeader.startsWith('Bearer ')) {
                throw AppError.authError('Missing or invalid authorization header');
            }

            const token = authHeader.split('Bearer ')[1];
            if (!token) {
                throw AppError.authError('No token provided');
            }

            // Store cost for later consumption
            req.creditCost = cost;
            req.firebaseToken = token;

            next();
        } catch (error: any) {
            if (error instanceof AppError) {
                return next(error);
            }
            next(AppError.providerError('yanbrain-server', 'Failed to prepare credit consumption', error));
        }
    };
}

/**
 * Helper function to consume credits after successful AI operation
 * Call this in your route handlers after the AI operation succeeds.
 *
 * This calls YanBrainServer /credits/consume-cost (internal-only spend endpoint).
 */
export async function consumeCredits(req: Request): Promise<boolean> {
    try {
        const { creditCost, firebaseToken } = req;

        if (!creditCost || !firebaseToken) {
            throw new Error('Missing creditCost or firebaseToken');
        }

        if (!INTERNAL_AUTH.SECRET) {
            throw new Error('Missing YANBRAIN_INTERNAL_SECRET in APIClient env');
        }

        const requestBody: CreditsConsumeRequest = { cost: creditCost };

        await axios.post(
            `${YANBRAIN_SERVER.BASE_URL}${YANBRAIN_SERVER.ENDPOINTS.CREDITS_CONSUME_COST}`,
            requestBody,
            {
                headers: {
                    Authorization: `Bearer ${firebaseToken}`,
                    'X-Yanbrain-Internal-Secret': INTERNAL_AUTH.SECRET
                }
            }
        );

        return true;
    } catch (error: any) {
        console.error('Failed to consume credits:', error.message);
        // Don't throw - credits consumption failure shouldn't block response
        return false;
    }
}
