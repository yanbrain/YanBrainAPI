import { Runware } from '@runware/sdk-js';
import { IImageProvider } from '../providers/ImageProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ProviderInfo } from '../types/api.types';

export class RunwareAdapter implements IImageProvider {
    private client;
    private defaultModel: string = 'gemini:GEMINI_2_5_FLASH_IMAGE';
    private readonly MAX_IMAGE_SIZE = 10 * 1024 * 1024; // 10MB

    constructor() {
        this.client = new Runware({
            apiKey: API_KEYS.RUNWARE
        });
    }

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
            if (!prompt || prompt.trim().length === 0) {
                throw AppError.validationError('Prompt cannot be empty', ['prompt']);
            }

            const model = options.model || this.defaultModel;

            if (options.imageBase64) {
                // Validate size
                const imageSize = Buffer.from(options.imageBase64, 'base64').length;
                if (imageSize > this.MAX_IMAGE_SIZE) {
                    throw AppError.validationError(
                        `Image too large: ${Math.round(imageSize / 1024 / 1024)}MB (max 10MB)`,
                        ['imageBase64']
                    );
                }

                console.log(`[Runware] Image-to-image: ${Math.round(imageSize / 1024)}KB`);

                // Direct image-to-image with seedImage
                const images = await this.client.imageInference({
                    positivePrompt: prompt,
                    negativePrompt: options.negativePrompt || '',
                    model: model,
                    seedImage: options.imageBase64, // Pass base64 directly
                    strength: 0.7,
                    numberResults: 1,
                    outputType: 'URL',
                    outputFormat: 'PNG'
                } as any);

                if (!images || images.length === 0 || !(images[0] as any)?.imageURL) {
                    throw AppError.providerError('runware', 'No image generated');
                }

                console.log(`[Runware] Success`);
                return (images[0] as any).imageURL;
            } else {
                // Text-to-image
                const width = options.width || 512;
                const height = options.height || 512;

                const images = await this.client.imageInference({
                    positivePrompt: prompt,
                    negativePrompt: options.negativePrompt || '',
                    width: width,
                    height: height,
                    model: model,
                    numberResults: 1,
                    outputType: 'URL',
                    outputFormat: 'PNG'
                } as any);

                if (!images || images.length === 0 || !(images[0] as any)?.imageURL) {
                    throw AppError.providerError('runware', 'No image generated');
                }

                console.log(`[Runware] Text-to-image success`);
                return (images[0] as any).imageURL;
            }
        } catch (error: any) {
            console.error('[Runware] Error:', error.message);

            if (error instanceof AppError) throw error;

            if (error.message?.includes('insufficient') || error.message?.includes('credits')) {
                throw AppError.quotaExceededError('runware', 'Runware credits insufficient');
            }

            if (error.message?.includes('rate limit') || error.status === 429) {
                throw AppError.rateLimitError('runware', 'Runware rate limit exceeded');
            }

            if (error.message?.includes('invalid') && error.message?.includes('key')) {
                throw AppError.providerError('runware', 'Invalid Runware API key', error);
            }

            throw AppError.providerError('runware', error.message || 'Runware request failed', error);
        }
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