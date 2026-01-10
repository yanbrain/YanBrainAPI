import axios from 'axios';
import { ISTTProvider } from '../providers/STTProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ProviderInfo } from '../types/api.types';

/**
 * Wit.ai implementation of STTProvider
 * Handles all Wit.ai-specific logic and error handling
 */
export class WitAdapter implements ISTTProvider {
  private apiKey: string;
  private apiUrl: string = 'https://api.wit.ai/speech';

  constructor() {
    this.apiKey = API_KEYS.WIT;
  }

  /**
   * Convert speech to text using Wit.ai
   */
  async speechToText(
    audioBuffer: Buffer,
    options: {
      contentType?: string;
      language?: string;
    } = {}
  ): Promise<string> {
    try {
      const response = await axios.post(this.apiUrl, audioBuffer, {
        headers: {
          'Authorization': `Bearer ${this.apiKey}`,
          'Content-Type': options.contentType || 'audio/wav'
        },
        params: {
          v: '20240304' // API version
        }
      });

      // Wit.ai returns text in different formats depending on response
      const text = response.data.text || response.data._text || '';

      if (!text) {
        throw new Error('No transcription returned from Wit.ai');
      }

      return text;
    } catch (error: any) {
      // Handle Wit.ai specific errors
      if (error.response?.status === 429) {
        throw AppError.rateLimitError('wit', 'Wit.ai rate limit exceeded');
      }

      if (error.response?.status === 401) {
        throw AppError.providerError('wit', 'Invalid Wit.ai API key', error);
      }

      if (error.response?.status === 400) {
        throw AppError.providerError('wit', 'Invalid audio format', error);
      }

      // Generic Wit.ai error
      throw AppError.providerError(
        'wit',
        error.response?.data?.error || error.message || 'Wit.ai request failed',
        error
      );
    }
  }

  /**
   * Get supported languages
   */
  getSupportedLanguages(): string[] {
    return [
      'en', // English
      'es', // Spanish
      'fr', // French
      'de', // German
      'it', // Italian
      'pt', // Portuguese
      'ru', // Russian
      'zh', // Chinese
      'ja', // Japanese
      'ko'  // Korean
    ];
  }

  /**
   * Get provider information
   */
  getProviderInfo(): ProviderInfo {
    return {
      provider: 'Wit.ai',
      supportedFormats: ['audio/wav', 'audio/mpeg', 'audio/mp4']
    };
  }
}

export default WitAdapter;
