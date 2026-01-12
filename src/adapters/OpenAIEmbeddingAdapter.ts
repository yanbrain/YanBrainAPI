import OpenAI from 'openai';
import { IEmbeddingProvider } from '../providers/EmbeddingProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ProviderInfo } from '../types/api.types';

export class OpenAIEmbeddingAdapter implements IEmbeddingProvider {
    private client: OpenAI;
    private readonly model: string = 'text-embedding-3-small';
    private readonly dimensions: number = 1536;
    private readonly MAX_TEXT_LENGTH = 30000; // ~8k tokens safe limit

    constructor() {
        this.client = new OpenAI({
            apiKey: API_KEYS.OPENAI,
            timeout: 30000, // 30s timeout
            maxRetries: 2
        });
    }

    async generateEmbedding(text: string): Promise<number[]> {
        try {
            // Validate input
            if (!text || typeof text !== 'string') {
                throw AppError.validationError('Text is required and must be a string', ['text']);
            }

            const trimmedText = text.trim();
            if (trimmedText.length === 0) {
                throw AppError.validationError('Text cannot be empty', ['text']);
            }

            if (trimmedText.length > this.MAX_TEXT_LENGTH) {
                throw AppError.validationError(
                    `Text too long: ${trimmedText.length} chars (max ${this.MAX_TEXT_LENGTH})`,
                    ['text']
                );
            }

            console.log(`[OpenAI] Generating embedding: ${trimmedText.length} chars`);

            const response = await this.client.embeddings.create({
                model: this.model,
                input: trimmedText,
                encoding_format: 'float',
                dimensions: this.dimensions
            });

            if (!response.data || response.data.length === 0) {
                throw AppError.providerError('openai', 'Empty response from OpenAI embeddings');
            }

            const embedding = response.data[0].embedding;

            if (!embedding || embedding.length !== this.dimensions) {
                throw AppError.providerError(
                    'openai',
                    `Invalid embedding dimensions: expected ${this.dimensions}, got ${embedding?.length || 0}`
                );
            }

            console.log(`[OpenAI] Embedding generated: ${embedding.length}D`);
            return embedding;

        } catch (error: any) {
            console.error('[OpenAI] Embedding error:', {
                message: error.message,
                code: error.code,
                status: error.status,
                textLength: text?.length
            });

            // Handle known errors
            if (error instanceof AppError) {
                throw error;
            }

            if (error.code === 'insufficient_quota') {
                throw AppError.quotaExceededError('openai', 'OpenAI account has no credits left');
            }

            if (error.status === 429 || error.code === 'rate_limit_exceeded') {
                throw AppError.rateLimitError('openai', 'OpenAI rate limit exceeded');
            }

            if (error.status === 401 || error.code === 'invalid_api_key') {
                throw AppError.providerError('openai', 'Invalid OpenAI API key', error);
            }

            if (error.code === 'context_length_exceeded') {
                throw AppError.validationError(
                    'Text exceeds OpenAI token limit (8191 tokens)',
                    ['text']
                );
            }

            if (error.code === 'ETIMEDOUT' || error.message?.includes('timeout')) {
                throw AppError.providerError('openai', 'OpenAI request timed out', error);
            }

            // Generic error
            throw AppError.providerError(
                'openai',
                error.message || 'OpenAI embeddings request failed',
                error
            );
        }
    }

    getDimensions(): number {
        return this.dimensions;
    }

    getProviderInfo(): ProviderInfo {
        return {
            provider: 'OpenAI',
            defaultModel: this.model,
            availableModels: [this.model]
        };
    }

    getAvailableModels(): string[] {
        return [this.model];
    }
}

export default OpenAIEmbeddingAdapter;