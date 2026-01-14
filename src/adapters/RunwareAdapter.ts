import { Runware } from '@runware/sdk-js';
import { IImageProvider } from '../providers/ImageProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ProviderInfo } from '../types/api.types';

export class RunwareAdapter implements IImageProvider {
    private client;
    private readonly defaultModel: string = 'gemini:GEMINI_2_5_FLASH_IMAGE';

    // Image limits
    private readonly MAX_IMAGE_SIZE_BYTES = 10 * 1024 * 1024; // 10MB
    private readonly DEFAULT_WIDTH = 512;
    private readonly DEFAULT_HEIGHT = 512;
    private readonly DEFAULT_STRENGTH = 0.7;

    constructor() {
        this.client = new Runware({
            apiKey: API_KEYS.RUNWARE
        });
    }

    /**
     * Generate image from text prompt or modify existing image
     */
    async generateImage(
        prompt: string,
        options: {
            width?: number;
            height?: number;
            negativePrompt?: string;
            model?: string;
            imageBase64?: string;
        } = {}
    ): Promise<string> {
        try {
            // Validate prompt
            this.validatePrompt(prompt);

            const model = options.model || this.defaultModel;

            // Route to appropriate generation method
            if (options.imageBase64) {
                return await this.generateImageToImage(
                    prompt,
                    options.imageBase64,
                    model,
                    options.negativePrompt
                );
            } else {
                return await this.generateTextToImage(
                    prompt,
                    model,
                    options.width,
                    options.height,
                    options.negativePrompt
                );
            }

        } catch (error: any) {
            console.error('[Runware] Error:', error.message);

            // Re-throw AppErrors as-is
            if (error instanceof AppError) throw error;

            // Handle specific Runware errors
            this.handleRunwareError(error);
        }
    }

    /**
     * Generate image from text prompt only
     */
    private async generateTextToImage(
        prompt: string,
        model: string,
        width?: number,
        height?: number,
        negativePrompt?: string
    ): Promise<string> {
        const finalWidth = width || this.DEFAULT_WIDTH;
        const finalHeight = height || this.DEFAULT_HEIGHT;

        console.log(`[Runware] Text-to-image: ${finalWidth}x${finalHeight}`);

        const images = await this.client.imageInference({
            positivePrompt: prompt,
            negativePrompt: negativePrompt || '',
            width: finalWidth,
            height: finalHeight,
            model: model,
            numberResults: 1,
            outputType: 'URL',
            outputFormat: 'PNG'
        } as any);

        const imageUrl = this.extractImageUrl(images);
        console.log(`[Runware] Text-to-image success`);

        return imageUrl;
    }

    /**
     * Generate image from existing image and prompt
     */
    private async generateImageToImage(
        prompt: string,
        imageBase64: string,
        model: string,
        negativePrompt?: string
    ): Promise<string> {
        // Validate image size
        const imageSize = this.validateImageSize(imageBase64);
        console.log(`[Runware] Image-to-image: ${Math.round(imageSize / 1024)}KB`);

        const images = await this.client.imageInference({
            positivePrompt: prompt,
            negativePrompt: negativePrompt || '',
            model: model,
            seedImage: imageBase64,
            strength: this.DEFAULT_STRENGTH,
            numberResults: 1,
            outputType: 'URL',
            outputFormat: 'PNG'
        } as any);

        const imageUrl = this.extractImageUrl(images);
        console.log(`[Runware] Image-to-image success`);

        return imageUrl;
    }

    /**
     * Validate prompt is not empty
     */
    private validatePrompt(prompt: string): void {
        if (!prompt || prompt.trim().length === 0) {
            throw AppError.validationError('Prompt cannot be empty', ['prompt']);
        }
    }

    /**
     * Validate image size and return size in bytes
     */
    private validateImageSize(imageBase64: string): number {
        const imageSize = Buffer.from(imageBase64, 'base64').length;

        if (imageSize > this.MAX_IMAGE_SIZE_BYTES) {
            throw AppError.validationError(
                `Image too large: ${Math.round(imageSize / 1024 / 1024)}MB (max 10MB)`,
                ['imageBase64']
            );
        }

        return imageSize;
    }

    /**
     * Extract and validate image URL from API response
     */
    private extractImageUrl(images: any): string {
        if (!images || images.length === 0 || !images[0]?.imageURL) {
            throw AppError.providerError(
                'runware',
                'No image URL in response'
            );
        }

        return images[0].imageURL;
    }

    /**
     * Handle Runware-specific errors and convert to AppErrors
     */
    private handleRunwareError(error: any): never {
        if (
            error.message?.includes('insufficient') ||
            error.message?.includes('credits')
        ) {
            throw AppError.quotaExceededError(
                'runware',
                'Runware credits insufficient'
            );
        }

        if (error.message?.includes('rate limit') || error.status === 429) {
            throw AppError.rateLimitError(
                'runware',
                'Runware rate limit exceeded'
            );
        }

        if (error.message?.includes('invalid') && error.message?.includes('key')) {
            throw AppError.providerError(
                'runware',
                'Invalid Runware API key',
                error
            );
        }

        throw AppError.providerError(
            'runware',
            error.message || 'Image generation failed',
            error
        );
    }

    getSupportedSizes(): Array<{ width: number; height: number }> {
        return [
            { width: 512, height: 512 },
            { width: 768, height: 768 },
            { width: 1024, height: 1024 },
            { width: 512, height: 768 },
            { width: 768, height: 512 }
        ];
    }

    getProviderInfo(): ProviderInfo {
        return {
            provider: 'Runware',
            defaultModel: this.defaultModel,
            supportedFormats: ['PNG', 'JPG', 'WEBP']
        };
    }
}

export default RunwareAdapter;