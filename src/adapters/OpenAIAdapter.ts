import OpenAI from 'openai';
import { ILLMProvider } from '../providers/LLMProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ModelInfo } from '../types/api.types';

export class OpenAIAdapter implements ILLMProvider {
    private client: OpenAI;
    private model: string = 'gpt-4o-mini';

    private readonly DEFAULT_SYSTEM_PROMPT =
        'You are a helpful AI assistant for YanBrain applications.';

    // gpt-4o-mini: max combined input + output context ~128k tokens (~450k–500k characters)

    // ===== INPUT LIMIT =====
    private readonly MAX_INPUT_CHARACTERS = 50_000;

    // ===== OUTPUT LIMITS =====
    private readonly MAX_ALLOWED_USER_OUTPUT_CHARACTERS = 300; // hard cap for user
    private readonly HARD_LIMIT_BUFFER_CHARACTERS = 100;       // safety margin

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
            if (!userPrompt || !userPrompt.trim()) {
                throw AppError.validationError(
                    'User prompt cannot be empty',
                    ['userPrompt']
                );
            }

            const systemPrompt =
                options?.systemPrompt || this.DEFAULT_SYSTEM_PROMPT;

            const ragContext = options?.ragContext?.trim();
            const requestedChars = options?.maxResponseChars;

            // ===== VALIDATE USER-REQUESTED OUTPUT SIZE =====
            if (
                requestedChars !== undefined &&
                requestedChars > this.MAX_ALLOWED_USER_OUTPUT_CHARACTERS
            ) {
                throw AppError.validationError(
                    `maxResponseChars cannot exceed ${this.MAX_ALLOWED_USER_OUTPUT_CHARACTERS} characters`,
                    ['maxResponseChars']
                );
            }

            const targetOutputChars = requestedChars;

            const finalPrompt = ragContext
                ? this.buildRAGPrompt(userPrompt, ragContext)
                : userPrompt;

            // ===== VALIDATE INPUT SIZE =====
            const totalInputLength =
                systemPrompt.length + finalPrompt.length;

            if (totalInputLength > this.MAX_INPUT_CHARACTERS) {
                throw AppError.validationError(
                    `Input exceeds maximum allowed size of ${this.MAX_INPUT_CHARACTERS} characters`,
                    ['input']
                );
            }

            let finalSystemPrompt = systemPrompt;

            if (targetOutputChars) {
                finalSystemPrompt +=
                    `\n\nIMPORTANT: Keep the response under ${targetOutputChars} characters. ` +
                    `This is a strict requirement.`;
            }

            const messages: OpenAI.Chat.ChatCompletionMessageParam[] = [
                { role: 'system', content: finalSystemPrompt },
                { role: 'user', content: finalPrompt }
            ];

            const hardLimitChars = targetOutputChars
                ? targetOutputChars + this.HARD_LIMIT_BUFFER_CHARACTERS
                : undefined;

            const maxCompletionTokens = hardLimitChars
                ? this.estimateTokens(hardLimitChars)
                : undefined;

            const response = await this.client.chat.completions.create({
                model: this.model,
                messages,
                temperature: 0.7,
                ...(maxCompletionTokens && {
                    max_completion_tokens: maxCompletionTokens
                })
            });

            let content = response.choices[0]?.message?.content;

            if (typeof content !== 'string' || !content.trim()) {
                throw AppError.providerError(
                    'openai',
                    'Empty response from OpenAI'
                );
            }

            content = content.trim();

            // ===== FINAL HARD ENFORCEMENT =====
            if (targetOutputChars && content.length > targetOutputChars) {
                content = content.slice(0, targetOutputChars).trimEnd();
            }

            return content;

        } catch (error: any) {
            console.error('[OpenAI] Error:', {
                message: error.message,
                code: error.code,
                status: error.status
            });

            if (error instanceof AppError) throw error;

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

            throw AppError.providerError(
                'openai',
                error.message || 'OpenAI request failed',
                error
            );
        }
    }

    private estimateTokens(characters: number): number {
        // Conservative approximation: ~4 characters per token
        return Math.ceil(characters / 4);
    }

    private buildRAGPrompt(userPrompt: string, ragContext: string): string {
        return `Context from documents:
---
${ragContext}
---

Based on the context above, answer the following question.
If the answer is not in the context, say so clearly.

Question: ${userPrompt}`;
    }

    getModelInfo(): ModelInfo {
        return {
            provider: 'OpenAI',
            model: this.model
        };
    }
}

export default OpenAIAdapter;