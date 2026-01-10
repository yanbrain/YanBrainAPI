import { ElevenLabsClient } from '@elevenlabs/elevenlabs-js';
import { ITTSProvider } from '../providers/TTSProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ProviderInfo } from '../types/api.types';

/**
 * ElevenLabs implementation of TTSProvider
 * Handles all ElevenLabs-specific logic and error handling
 */
export class ElevenLabsAdapter implements ITTSProvider {
  private client: ElevenLabsClient;
  private defaultVoiceId: string = 'EXAVITQu4vr4xnSDxMaL'; // Sarah voice

  constructor() {
    this.client = new ElevenLabsClient({
      apiKey: API_KEYS.ELEVENLABS
    });
  }

  /**
   * Convert text to speech using ElevenLabs
   */
  async textToSpeech(text: string, voiceId?: string): Promise<Buffer> {
    try {
      const selectedVoiceId = voiceId || this.defaultVoiceId;

      // Call ElevenLabs API
      const audio = await this.client.textToSpeech.convert(selectedVoiceId, {
        text: text,
        model_id: 'eleven_monolingual_v1'
      });

      // Convert stream to buffer
      const chunks: Buffer[] = [];
      for await (const chunk of audio) {
        chunks.push(Buffer.from(chunk));
      }

      return Buffer.concat(chunks);
    } catch (error: any) {
      // Handle ElevenLabs specific errors
      if (error.message?.includes('quota')) {
        throw AppError.quotaExceededError('elevenlabs', 'ElevenLabs quota exceeded');
      }

      if (error.status === 429) {
        throw AppError.rateLimitError('elevenlabs', 'ElevenLabs rate limit exceeded');
      }

      if (error.status === 401) {
        throw AppError.providerError('elevenlabs', 'Invalid ElevenLabs API key', error);
      }

      // Generic ElevenLabs error
      throw AppError.providerError('elevenlabs', error.message || 'ElevenLabs request failed', error);
    }
  }

  /**
   * Get available voices
   */
  async getVoices(): Promise<any[]> {
    try {
      const voices = await this.client.voices.getAll();
      return voices.voices || [];
    } catch (error: any) {
      console.error('Failed to fetch voices:', error);
      return [];
    }
  }

  /**
   * Get provider information
   */
  getProviderInfo(): ProviderInfo {
    return {
      provider: 'ElevenLabs',
      defaultVoice: this.defaultVoiceId
    };
  }
}

export default ElevenLabsAdapter;
