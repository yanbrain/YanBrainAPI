import { ProviderInfo } from '../types/api.types';

/**
 * Abstract TTS (Text-to-Speech) Provider Interface
 * Defines the contract that all TTS adapters must implement
 * This allows easy swapping between ElevenLabs, Google TTS, etc.
 */
export interface ITTSProvider {
  /**
   * Convert text to speech
   * @param text - Text to convert to speech
   * @param voiceId - Optional voice ID
   * @returns Audio data as buffer
   */
  textToSpeech(text: string, voiceId?: string): Promise<Buffer>;

  /**
   * Get available voices
   * @returns List of available voices
   */
  getVoices?(): Promise<any[]>;

  /**
   * Get provider information
   * @returns Provider details
   */
  getProviderInfo(): ProviderInfo;
}
