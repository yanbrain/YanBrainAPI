// src/adapters/OpenAIEmbeddingAdapter.ts

import OpenAI from 'openai';
import { IEmbeddingProvider } from '../providers/EmbeddingProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ProviderInfo } from '../types/api.types';

export class OpenAIEmbeddingAdapter implements IEmbeddingProvider {
    private client: OpenAI;
    private readonly model: string = 'text-embedding-3-small';
    private readonly dimensions: number = 1536;

    // OpenAI embedding limits
    // Based on: 8,192 tokens per input, ~4 chars/token, with 10% safety margin
    private readonly MAX_INPUT_CHARS_PER_ITEM = 30_000;

    // Based on: 300,000 tokens per request, ~4 chars/token, with 10% safety margin
    private readonly MAX_TOTAL_INPUT_CHARS_PER_REQUEST = 1_080_000;

    constructor() {
        this.client = new OpenAI({
            apiKey: API_KEYS.OPENAI,
            timeout: 100000,
            maxRetries: 0
        });
    }

    /**
     * Generate embedding for a single text input
     */
    async generateEmbedding(text: string): Promise<number[]> {
        const [embedding] = await this.generateEmbeddings([text]);
        return embedding;
    }

    /**
     * Generate embeddings for multiple text inputs in a single batch request
     */
    async generateEmbeddings(texts: string[]): Promise<number[][]> {
        try {
            // Validate input array
            if (!Array.isArray(texts) || texts.length === 0) {
                throw AppError.validationError(
                    'texts must be a non-empty array',
                    ['texts']
                );
            }

            // Validate and prepare each text input
            const preparedInputs = this.validateAndPrepareInputs(texts);

            // Validate total request size
            this.validateTotalSize(preparedInputs);

            // Call OpenAI API
            console.log(
                `[OpenAI] Generating embeddings: ${preparedInputs.length} items, ` +
                `${this.calculateTotalChars(preparedInputs)} chars total`
            );

            const response = await this.client.embeddings.create({
                model: this.model,
                input: preparedInputs,
                encoding_format: 'float',
                dimensions: this.dimensions
            });

            // Validate and extract embeddings
            const embeddings = this.validateAndExtractEmbeddings(
                response,
                preparedInputs.length
            );

            console.log(`[OpenAI] Embeddings generated: ${embeddings.length} vectors`);
            return embeddings;

        } catch (error: any) {
            console.error('[OpenAI] Embedding error:', {
                message: error.message,
                code: error.code,
                status: error.status
            });

            // Re-throw AppErrors as-is
            if (error instanceof AppError) throw error;

            // Handle specific OpenAI errors
            this.handleOpenAIError(error);
        }
    }

    /**
     * Validate each input text and prepare for API call
     */
    private validateAndPrepareInputs(texts: string[]): string[] {
        return texts.map((text, index) => {
            // Check type
            if (typeof text !== 'string') {
                throw AppError.validationError(
                    `texts[${index}] must be a string`,
                    [`texts[${index}]`]
                );
            }

            const trimmed = text.trim();

            // Check if empty
            if (!trimmed) {
                throw AppError.validationError(
                    `texts[${index}] cannot be empty`,
                    [`texts[${index}]`]
                );
            }

            // Check individual size limit
            if (trimmed.length > this.MAX_INPUT_CHARS_PER_ITEM) {
                throw AppError.validationError(
                    `texts[${index}] too long: ${trimmed.length} chars (max ${this.MAX_INPUT_CHARS_PER_ITEM})`,
                    [`texts[${index}]`]
                );
            }

            return trimmed;
        });
    }

    /**
     * Validate total request size across all inputs
     */
    private validateTotalSize(inputs: string[]): void {
        const totalChars = this.calculateTotalChars(inputs);

        if (totalChars > this.MAX_TOTAL_INPUT_CHARS_PER_REQUEST) {
            throw AppError.validationError(
                `Request too large: ${totalChars} chars total (max ${this.MAX_TOTAL_INPUT_CHARS_PER_REQUEST}). Reduce batch size.`,
                ['texts']
            );
        }
    }

    /**
     * Calculate total character count across all inputs
     */
    private calculateTotalChars(inputs: string[]): number {
        return inputs.reduce((sum, text) => sum + text.length, 0);
    }

    /**
     * Validate API response and extract embedding vectors
     */
    private validateAndExtractEmbeddings(
        response: OpenAI.Embeddings.CreateEmbeddingResponse,
        expectedCount: number
    ): number[][] {
        // Validate response structure
        if (!response.data || response.data.length !== expectedCount) {
            throw AppError.providerError(
                'openai',
                `Invalid embeddings response length: expected ${expectedCount}, got ${response.data?.length ?? 0}`
            );
        }

        // Extract and validate each embedding
        return response.data.map((item, index) => {
            const embedding = item.embedding;

            if (!embedding || embedding.length !== this.dimensions) {
                throw AppError.providerError(
                    'openai',
                    `Invalid embedding dimensions at index ${index}: expected ${this.dimensions}, got ${embedding?.length || 0}`
                );
            }

            return embedding;
        });
    }

    /**
     * Handle OpenAI-specific errors and convert to AppErrors
     */
    private handleOpenAIError(error: any): never {
        if (error.code === 'insufficient_quota') {
            throw AppError.quotaExceededError(
                'openai',
                'OpenAI account has no credits left'
            );
        }

        if (error.status === 429 || error.code === 'rate_limit_exceeded') {
            throw AppError.rateLimitError(
                'openai',
                'OpenAI rate limit exceeded'
            );
        }

        if (error.status === 401 || error.code === 'invalid_api_key') {
            throw AppError.providerError(
                'openai',
                'Invalid OpenAI API key',
                error
            );
        }

        if (error.code === 'context_length_exceeded') {
            throw AppError.validationError(
                'Text exceeds OpenAI token limit',
                ['texts']
            );
        }

        if (error.code === 'ETIMEDOUT' || error.message?.includes('timeout')) {
            throw AppError.providerError(
                'openai',
                'OpenAI request timed out',
                error
            );
        }

        throw AppError.providerError(
            'openai',
            error.message || 'OpenAI embeddings request failed',
            error
        );
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