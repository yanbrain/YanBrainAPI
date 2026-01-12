import OpenAI from 'openai';
import { ILLMProvider } from '../providers/LLMProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ModelInfo } from '../types/api.types';

export class OpenAIAdapter implements ILLMProvider {
    private client: OpenAI;
    private model: string = 'gpt-4o-mini';
    private readonly MAX_TOKENS = 1000;
    private readonly DEFAULT_SYSTEM_PROMPT =
        'You are a helpful AI assistant for YanBrain applications.';

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
            embeddedText?: string;
            maxResponseChars?: number;
        }
    ): Promise<string> {
        try {
            if (!userPrompt || !userPrompt.trim()) {
                throw AppError.validationError('User prompt cannot be empty', ['userPrompt']);
            }

            const systemPrompt = options?.systemPrompt || this.DEFAULT_SYSTEM_PROMPT;
            const embeddedText = options?.embeddedText?.trim();
            const maxResponseChars = options?.maxResponseChars;

            let finalSystemPrompt = systemPrompt;
            if (maxResponseChars) {
                finalSystemPrompt +=
                    `\n\nIMPORTANT: Keep your response under ${maxResponseChars} characters.`;
            }

            const finalPrompt = embeddedText
                ? this.buildRAGPrompt(userPrompt, embeddedText)
                : userPrompt;

            const messages: OpenAI.Chat.ChatCompletionMessageParam[] = [
                { role: 'system', content: finalSystemPrompt },
                { role: 'user', content: finalPrompt }
            ];

            const response = await this.client.chat.completions.create({
                model: this.model,
                messages,
                max_tokens: this.MAX_TOKENS,
                temperature: 0.7
            });

            const content = response.choices[0]?.message?.content;

            if (typeof content !== 'string' || content.trim().length === 0) {
                throw AppError.providerError('openai', 'Empty response from OpenAI');
            }

            return content.trim();


        } catch (error: any) {
            console.error('[OpenAI] Error:', {
                message: error.message,
                code: error.code,
                status: error.status
            });

            if (error instanceof AppError) throw error;

            if (error.code === 'insufficient_quota') {
                throw AppError.quotaExceededError('openai', 'OpenAI account has no credits left');
            }

            if (error.status === 429) {
                throw AppError.rateLimitError('openai', 'Rate limit exceeded');
            }

            if (error.status === 401) {
                throw AppError.providerError('openai', 'Invalid OpenAI API key', error);
            }

            throw AppError.providerError(
                'openai',
                error.message || 'OpenAI request failed',
                error
            );
        }
    }

    private buildRAGPrompt(userPrompt: string, embeddedText: string): string {
        return `Context from documents:
---
${embeddedText}
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
