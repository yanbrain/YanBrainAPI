import { Request } from 'express';

declare global {
  namespace Express {
    interface Request {
      user?: {
        uid: string;
        email?: string;
        emailVerified?: boolean;
      };
      creditCost?: number;
      firebaseToken?: string;
    }
  }
}

export {};
