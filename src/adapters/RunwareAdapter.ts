import { Runware } from '@runware/sdk-js';
import { IImageProvider } from '../providers/ImageProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ProviderInfo } from '../types/api.types';

/**
 * Runware implementation of ImageProvider
 * Handles all Runware-specific logic and error handling
 */
export class RunwareAdapter implements IImageProvider {
  private client: Runware;
  private defaultModel: string = 'civitai:4384@128713'; // Dreamshaper 8

  constructor() {
    this.client = new Runware({
      apiKey: API_KEYS.RUNWARE
    });
  }

  /**
   * Generate image using Runware
   */
  async generateImage(
    prompt: string,
    options: {
      width?: number;
      height?: number;
      negativePrompt?: string;
      model?: string;
    } = {}
  ): Promise<string> {
    try {
      const width = options.width || 512;
      const height = options.height || 512;
      const model = options.model || this.defaultModel;

      const images = await this.client.imageInference({
        positivePrompt: prompt,
        negativePrompt: options.negativePrompt || '',
        width: width,
        height: height,
        model: model,
        numberResults: 1,
        outputType: 'URL' as any,
        outputFormat: 'PNG' as any
      });

      if (!images || images.length === 0) {
        throw new Error('No image generated');
      }

      return (images[0] as any).imageURL;
    } catch (error: any) {
      // Handle Runware specific errors
      if (error.message?.includes('insufficient')) {
        throw AppError.quotaExceededError('runware', 'Runware credits insufficient');
      }

      if (error.message?.includes('rate limit')) {
        throw AppError.rateLimitError('runware', 'Runware rate limit exceeded');
      }

      if (error.message?.includes('invalid') && error.message?.includes('key')) {
        throw AppError.providerError('runware', 'Invalid Runware API key', error);
      }

      // Generic Runware error
      throw AppError.providerError('runware', error.message || 'Runware request failed', error);
    }
  }

  /**
   * Get supported image sizes
   */
  getSupportedSizes(): Array<{ width: number; height: number }> {
    return [
      { width: 512, height: 512 },
      { width: 768, height: 768 },
      { width: 1024, height: 1024 },
      { width: 512, height: 768 },
      { width: 768, height: 512 }
    ];
  }

  /**
   * Get provider information
   */
  getProviderInfo(): ProviderInfo {
    return {
      provider: 'Runware',
      defaultModel: this.defaultModel,
      supportedFormats: ['PNG', 'JPG', 'WEBP']
    };
  }
}

export default RunwareAdapter;
