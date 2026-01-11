import { Request, Response, NextFunction } from 'express';
import * as admin from 'firebase-admin';
import { AppError } from '../errors/AppError';

/**
 * Middleware to verify Firebase Auth tokens
 * Extracts token from Authorization header and verifies it
 */
export async function authMiddleware(
  req: Request,
  _res: Response,
  next: NextFunction
): Promise<void> {
  try {
    const authHeader = req.headers.authorization;

    if (!authHeader || !authHeader.startsWith('Bearer ')) {
      throw AppError.authError('Missing or invalid authorization header');
    }

    const token = authHeader.split('Bearer ')[1];

    if (!token) {
      throw AppError.authError('No token provided');
    }

    // Verify Firebase token
    const decodedToken = await admin.auth().verifyIdToken(token);

    // Attach user info to request
    req.user = {
      uid: decodedToken.uid,
      email: decodedToken.email,
      emailVerified: decodedToken.email_verified
    };

    next();
  } catch (error: any) {
    if (error.code === 'auth/id-token-expired') {
      return next(AppError.authError('Token expired'));
    }
    if (error.code === 'auth/argument-error') {
      return next(AppError.authError('Invalid token format'));
    }
    if (error instanceof AppError) {
      return next(error);
    }
    next(AppError.authError('Authentication failed'));
  }
}
