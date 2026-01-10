import { ERROR_CODES, ErrorCode } from './errorCodes';

/**
 * Base error class for all application errors
 * Extends built-in Error with additional properties
 */
export class AppError extends Error {
  public readonly statusCode: number;
  public readonly code: ErrorCode;
  public readonly details: Record<string, any>;
  public readonly isOperational: boolean;

  constructor(
    message: string,
    statusCode: number,
    code: ErrorCode,
    details: Record<string, any> = {}
  ) {
    super(message);
    this.name = 'AppError';
    this.statusCode = statusCode;
    this.code = code;
    this.details = details;
    this.isOperational = true; // Distinguishes operational vs programmer errors
    Error.captureStackTrace(this, this.constructor);
  }

  /**
   * Factory method: Insufficient credits error (402)
   */
  static insufficientCredits(required: number = 1, available: number = 0): AppError {
    return new AppError(
      'You don\'t have enough credits',
      402,
      ERROR_CODES.INSUFFICIENT_CREDITS,
      { required, available }
    );
  }

  /**
   * Factory method: AI Provider error (503)
   */
  static providerError(provider: string, message: string, originalError: any = null): AppError {
    const code = `${provider.toUpperCase()}_ERROR` as ErrorCode;
    return new AppError(
      message,
      503,
      ERROR_CODES[code] || code,
      { 
        provider,
        originalMessage: originalError?.message,
        originalCode: originalError?.code
      }
    );
  }

  /**
   * Factory method: Authentication error (401)
   */
  static authError(message: string = 'Unauthorized'): AppError {
    return new AppError(
      message,
      401,
      ERROR_CODES.UNAUTHORIZED
    );
  }

  /**
   * Factory method: Validation error (400)
   */
  static validationError(message: string, fields: string[] = []): AppError {
    return new AppError(
      message,
      400,
      ERROR_CODES.VALIDATION_ERROR,
      { fields }
    );
  }

  /**
   * Factory method: Rate limit error (429)
   */
  static rateLimitError(provider: string, message: string = 'Rate limit exceeded'): AppError {
    const code = `${provider.toUpperCase()}_RATE_LIMIT` as ErrorCode;
    return new AppError(
      message,
      429,
      ERROR_CODES[code] || 'RATE_LIMIT_ERROR' as ErrorCode,
      { provider }
    );
  }

  /**
   * Factory method: Quota exceeded error (503)
   */
  static quotaExceededError(provider: string, message: string = 'Quota exceeded'): AppError {
    const code = `${provider.toUpperCase()}_QUOTA_EXCEEDED` as ErrorCode;
    return new AppError(
      message,
      503,
      ERROR_CODES[code] || 'QUOTA_EXCEEDED' as ErrorCode,
      { provider }
    );
  }
}

export default AppError;
