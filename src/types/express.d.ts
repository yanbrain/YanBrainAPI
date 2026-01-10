import { Request } from 'express';

declare global {
  namespace Express {
    interface Request {
      user?: {
        uid: string;
        email?: string;
        emailVerified?: boolean;
      };
      productId?: string;
      firebaseToken?: string;
    }
  }
}

export {};
