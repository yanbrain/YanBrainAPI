// src/middleware/internalSecretMiddleware.ts
import { Request, Response, NextFunction } from 'express';
import { AppError } from '../errors/AppError';
import { INTERNAL_AUTH } from '../config/constants';

/**
 * Middleware to verify internal service-to-service requests
 * Checks for X-Yanbrain-Internal-Secret header
 */
export function internalSecretMiddleware(
    req: Request,
    _res: Response,
    next: NextFunction
): void {
    try {
        const secret = req.headers['x-yanbrain-internal-secret'];

        if (!secret || secret !== INTERNAL_AUTH.SECRET) {
            throw AppError.authError('Invalid or missing internal secret');
        }

        next();
    } catch (error: any) {
        if (error instanceof AppError) {
            return next(error);
        }
        next(AppError.authError('Internal authentication failed'));
    }
}