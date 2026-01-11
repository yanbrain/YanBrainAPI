import { Request, Response, NextFunction } from 'express';
import axios from 'axios';
import { YANBRAIN_SERVER, INTERNAL_AUTH } from '../config/constants';
import { AppError } from '../errors/AppError';

export function creditsMiddleware(cost: number) {
    return async (req: Request, _res: Response, next: NextFunction) => {
        try {
            if (!req.user?.uid) {
                throw AppError.authError('User not authenticated');
            }

            if (!req.headers.authorization) {
                throw AppError.authError('Missing authorization header');
            }

            req.creditCost = cost;
            req.firebaseToken = req.headers.authorization.split('Bearer ')[1];

            next();
        } catch (error) {
            next(error);
        }
    };
}

export async function consumeCredits(req: Request): Promise<void> {
    try {
        const { creditCost, firebaseToken } = req;

        if (!creditCost || !firebaseToken) {
            throw new Error('Missing creditCost or firebaseToken');
        }

        await axios.post(
            `${YANBRAIN_SERVER.BASE_URL}${YANBRAIN_SERVER.ENDPOINTS.CONSUME_COST}`,
            { cost: creditCost },
            {
                headers: {
                    Authorization: `Bearer ${firebaseToken}`,
                    'X-Yanbrain-Internal-Secret': INTERNAL_AUTH.SECRET
                }
            }
        );
    } catch (error: any) {
        console.error('CREDIT CONSUMPTION FAILED:', error.message);
        // intentionally do not throw
    }
}
