import { Request, Response, NextFunction } from 'express';
import axios from 'axios';
import { YANBRAIN_SERVER, ProductId } from '../config/constants';
import { AppError } from '../errors/AppError';
import { CreditsBalanceResponse, CreditsConsumeRequest } from '../types/api.types';

/**
 * Middleware to check and consume credits from YanBrainServer
 * Should be used AFTER authMiddleware
 */
export function creditsMiddleware(productId: ProductId) {
  return async (req: Request, _res: Response, next: NextFunction): Promise<void> => {
    try {
      const userId = req.user?.uid;

      if (!userId) {
        throw AppError.authError('User not authenticated');
      }

      // Get Firebase token from request
      const token = req.headers.authorization!.split('Bearer ')[1];

      // Check credits balance
      const balanceResponse = await axios.get<CreditsBalanceResponse>(
        `${YANBRAIN_SERVER.BASE_URL}${YANBRAIN_SERVER.ENDPOINTS.CREDITS_BALANCE}`,
        {
          headers: {
            Authorization: `Bearer ${token}`
          }
        }
      );

      const { creditsBalance } = balanceResponse.data;

      if (creditsBalance < 1) {
        throw AppError.insufficientCredits(1, creditsBalance);
      }

      // Store productId for later consumption
      req.productId = productId;
      req.firebaseToken = token;

      next();
    } catch (error: any) {
      if (error instanceof AppError) {
        return next(error);
      }
      if (error.response?.status === 401) {
        return next(AppError.authError('Invalid credentials'));
      }
      next(AppError.providerError('yanbrain-server', 'Failed to check credits', error));
    }
  };
}

/**
 * Helper function to consume credits after successful AI operation
 * Call this in your route handlers after the AI operation succeeds
 */
export async function consumeCredits(req: Request): Promise<boolean> {
  try {
    const { productId, firebaseToken } = req;

    if (!productId || !firebaseToken) {
      throw new Error('Missing productId or firebaseToken');
    }

    const requestBody: CreditsConsumeRequest = { productId };

    await axios.post(
      `${YANBRAIN_SERVER.BASE_URL}${YANBRAIN_SERVER.ENDPOINTS.CREDITS_CONSUME}`,
      requestBody,
      {
        headers: {
          Authorization: `Bearer ${firebaseToken}`
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
