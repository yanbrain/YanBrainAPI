import { ProviderInfo } from '../types/api.types';

/**
 * Abstract Image Generation Provider Interface
 * Defines the contract that all image generation adapters must implement
 * This allows easy swapping between Runware, DALL-E, Stable Diffusion, etc.
 */
export interface IImageProvider {
  /**
   * Generate an image from a text prompt
   * @param prompt - Text description of desired image
   * @param options - Optional configuration (width, height, etc.)
   * @returns Image URL or base64 data
   */
  generateImage(
    prompt: string,
    options?: {
      width?: number;
      height?: number;
      negativePrompt?: string;
      model?: string;
    }
  ): Promise<string>;

  /**
   * Get supported image sizes
   * @returns List of supported dimensions
   */
  getSupportedSizes(): Array<{ width: number; height: number }>;

  /**
   * Get provider information
   * @returns Provider details
   */
  getProviderInfo(): ProviderInfo;
}
