import { Request, Response, NextFunction } from 'express';
import { AppError } from '../errors/AppError';
import { ApiResponse } from '../types/api.types';

/**
 * Centralized error handling middleware
 * Must be defined with 4 parameters (err, req, res, next) to be recognized as error handler
 * Must be added LAST in middleware chain
 */
export function errorMiddleware(
  err: Error | AppError,
  req: Request,
  res: Response,
  _next: NextFunction
): void {
  // Log error for debugging (in production, use proper logger)
  console.error('Error occurred:', {
    message: err.message,
    code: (err as AppError).code,
    statusCode: (err as AppError).statusCode,
    path: req.path,
    method: req.method,
    timestamp: new Date().toISOString(),
    stack: process.env.NODE_ENV === 'development' ? err.stack : undefined
  });

  // Handle operational errors (expected errors)
  if (err instanceof AppError && err.isOperational) {
    const response: ApiResponse<never> = {
      success: false,
      error: {
        code: err.code,
        message: err.message,
        statusCode: err.statusCode,
        details: err.details || {}
      }
    };

    res.status(err.statusCode || 500).json(response);
    return;
  }

  // Handle unexpected errors (programmer errors)
  // Don't leak internal error details to client
  const response: ApiResponse<never> = {
    success: false,
    error: {
      code: 'INTERNAL_SERVER_ERROR',
      message: process.env.NODE_ENV === 'development' 
        ? err.message 
        : 'An unexpected error occurred',
      statusCode: 500
    }
  };

  res.status(500).json(response);
}
