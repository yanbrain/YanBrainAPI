import OpenAI from 'openai';
import { ILLMProvider } from '../providers/LLMProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ModelInfo } from '../types/api.types';

export class OpenAIAdapter implements ILLMProvider {
    private client: OpenAI;
    private model: string = 'gpt-4o-mini';

    private readonly DEFAULT_SYSTEM_PROMPT = 'You are a helpful AI assistant.';

    // Input limits
    private readonly MAX_INPUT_CHARACTERS = 50_000;

    // Output limits
    private readonly MAX_ALLOWED_USER_OUTPUT_CHARACTERS = 300;
    private readonly HARD_LIMIT_BUFFER_CHARACTERS = 100;

    constructor() {
        this.client = new OpenAI({
            apiKey: API_KEYS.OPENAI,
            timeout: 60000,
            maxRetries: 2
        });
    }

    async generateResponse(
        userPrompt: string,
        options?: {
            systemPrompt?: string;
            ragContext?: string;
            maxResponseChars?: number;
        }
    ): Promise<string> {
        try {
            // Validate user prompt
            if (!userPrompt || !userPrompt.trim()) {
                throw AppError.validationError(
                    'User prompt cannot be empty',
                    ['userPrompt']
                );
            }

            // Validate character limit if provided
            const requestedChars = options?.maxResponseChars;
            if (
                requestedChars !== undefined &&
                requestedChars > this.MAX_ALLOWED_USER_OUTPUT_CHARACTERS
            ) {
                throw AppError.validationError(
                    `maxResponseChars cannot exceed ${this.MAX_ALLOWED_USER_OUTPUT_CHARACTERS} characters`,
                    ['maxResponseChars']
                );
            }

            // Build system prompt
            let systemPrompt = options?.systemPrompt || this.DEFAULT_SYSTEM_PROMPT;

            if (requestedChars) {
                systemPrompt += `\n\nIMPORTANT: Keep your response under ${requestedChars} characters. This is a strict requirement.`;
            }

            // Build user prompt (with or without RAG context)
            const ragContext = options?.ragContext?.trim();
            const userMessage = ragContext
                ? this.buildRAGPrompt(userPrompt, ragContext)
                : userPrompt;

            // Validate total input size
            const totalInputLength = systemPrompt.length + userMessage.length;
            if (totalInputLength > this.MAX_INPUT_CHARACTERS) {
                throw AppError.validationError(
                    `Input exceeds maximum allowed size of ${this.MAX_INPUT_CHARACTERS} characters`,
                    ['input']
                );
            }

            // Prepare messages
            const messages: OpenAI.Chat.ChatCompletionMessageParam[] = [
                { role: 'system', content: systemPrompt },
                { role: 'user', content: userMessage }
            ];

            // Calculate token limit if character limit specified
            const hardLimitChars = requestedChars
                ? requestedChars + this.HARD_LIMIT_BUFFER_CHARACTERS
                : undefined;

            const maxCompletionTokens = hardLimitChars
                ? this.estimateTokens(hardLimitChars)
                : undefined;

            // Call OpenAI
            const response = await this.client.chat.completions.create({
                model: this.model,
                messages,
                temperature: 0.7,
                ...(maxCompletionTokens && {
                    max_completion_tokens: maxCompletionTokens
                })
            });

            // Extract and validate response
            let content = response.choices[0]?.message?.content;

            if (typeof content !== 'string' || !content.trim()) {
                throw AppError.providerError(
                    'openai',
                    'Empty response from OpenAI'
                );
            }

            content = content.trim();

            // Enforce character limit if specified
            if (requestedChars && content.length > requestedChars) {
                content = content.slice(0, requestedChars).trimEnd();
            }

            return content;

        } catch (error: any) {
            console.error('[OpenAI] Error:', {
                message: error.message,
                code: error.code,
                status: error.status
            });

            // Re-throw AppErrors as-is
            if (error instanceof AppError) throw error;

            // Handle specific OpenAI errors
            if (error.code === 'insufficient_quota') {
                throw AppError.quotaExceededError(
                    'openai',
                    'OpenAI account has no credits left'
                );
            }

            if (error.status === 429) {
                throw AppError.rateLimitError(
                    'openai',
                    'Rate limit exceeded'
                );
            }

            if (error.status === 401) {
                throw AppError.providerError(
                    'openai',
                    'Invalid OpenAI API key',
                    error
                );
            }

            // Generic error fallback
            throw AppError.providerError(
                'openai',
                error.message || 'OpenAI request failed',
                error
            );
        }
    }

    private buildRAGPrompt(userPrompt: string, ragContext: string): string {
        return `You are a helpful technical assistant.

## Voice Input Handling
The question is from voice transcription and may contain errors. If technical terms in the context sound similar to words in the question, treat them as matches.

## Response Format
- Write in clear, natural paragraphs
- Never use bullet points or numbered lists
- Answer directly with no preamble
- Do not mention misspellings, transcription errors, or term differences

## Context
${ragContext}

## Question
${userPrompt}

## Answer`;
    }

    private estimateTokens(characters: number): number {
        // Rough estimate: ~4 characters per token
        return Math.ceil(characters / 4);
    }

    getModelInfo(): ModelInfo {
        return {
            provider: 'OpenAI',
            model: this.model
        };
    }
}

export default OpenAIAdapter;