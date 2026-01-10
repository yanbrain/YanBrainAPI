import { ProviderInfo } from '../types/api.types';

/**
 * Abstract STT (Speech-to-Text) Provider Interface
 * Defines the contract that all STT adapters must implement
 * This allows easy swapping between Wit.ai, Whisper, etc.
 */
export interface ISTTProvider {
  /**
   * Convert speech to text
   * @param audioBuffer - Audio data as buffer
   * @param options - Optional configuration (language, contentType, etc.)
   * @returns Transcribed text
   */
  speechToText(
    audioBuffer: Buffer,
    options?: {
      contentType?: string;
      language?: string;
    }
  ): Promise<string>;

  /**
   * Get supported languages
   * @returns List of supported language codes
   */
  getSupportedLanguages(): string[];

  /**
   * Get provider information
   * @returns Provider details
   */
  getProviderInfo(): ProviderInfo;
}
