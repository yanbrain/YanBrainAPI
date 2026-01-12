import OpenAI from 'openai';
import { ILLMProvider } from '../providers/LLMProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ChatMessage, ModelInfo } from '../types/api.types';

export class OpenAIAdapter implements ILLMProvider {
    private client: OpenAI;
    private model: string = 'gpt-4o-mini';
    private readonly MAX_TOKENS = 1000;
    private readonly DEFAULT_SYSTEM_PROMPT = 'You are a helpful AI assistant for YanBrain applications.';

    constructor() {
        this.client = new OpenAI({
            apiKey: API_KEYS.OPENAI,
            timeout: 60000,
            maxRetries: 2
        });
    }

    async generateResponse(
        userPrompt: string,
        conversationHistory: ChatMessage[] = [],
        options?: {
            systemPrompt?: string;
            embeddedText?: string;
            maxResponseChars?: number;
        }
    ): Promise<string> {
        try {
            if (!userPrompt || typeof userPrompt !== 'string' || userPrompt.trim().length === 0) {
                throw AppError.validationError('User prompt cannot be empty', ['userPrompt']);
            }

            const systemPrompt = options?.systemPrompt || this.DEFAULT_SYSTEM_PROMPT;
            const embeddedText = options?.embeddedText?.trim();
            const maxResponseChars = options?.maxResponseChars;

            let finalSystemPrompt = systemPrompt;
            if (maxResponseChars) {
                finalSystemPrompt += `\n\nIMPORTANT: Keep your response under ${maxResponseChars} characters. Provide complete, well-formed answers without cutting off mid-sentence.`;
            }

            const finalPrompt = embeddedText
                ? this.buildRAGPrompt(userPrompt, embeddedText)
                : userPrompt;

            if (embeddedText) {
                console.log(`[OpenAI] RAG mode: ${embeddedText.length} chars context`);
            }

            const messages: OpenAI.Chat.ChatCompletionMessageParam[] = [
                { role: 'system', content: finalSystemPrompt },
                ...conversationHistory.map(msg => ({
                    role: msg.role as 'system' | 'user' | 'assistant',
                    content: msg.content
                })),
                { role: 'user', content: finalPrompt }
            ];

            const estimatedTokens = JSON.stringify(messages).length / 4;
            if (estimatedTokens > 120000) {
                throw AppError.validationError(
                    `Context too large: ~${Math.round(estimatedTokens)} tokens (max ~120k)`,
                    ['conversationHistory', 'embeddedText']
                );
            }

            console.log(`[OpenAI] Generating response: ~${Math.round(estimatedTokens)} tokens context`);

            const response = await this.client.chat.completions.create({
                model: this.model,
                messages: messages,
                max_tokens: this.MAX_TOKENS,
                temperature: 0.7
            });

            const content = response.choices[0].message.content;

            if (!content || content.trim().length === 0) {
                throw AppError.providerError('openai', 'Empty response from OpenAI');
            }

            console.log(`[OpenAI] Response generated: ${content.length} chars`);
            return content.trim();

        } catch (error: any) {
            console.error('[OpenAI] LLM error:', {
                message: error.message,
                code: error.code,
                status: error.status,
                hasEmbeddedText: !!options?.embeddedText
            });

            if (error instanceof AppError) throw error;

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
                    'Context exceeds model token limit',
                    ['conversationHistory', 'embeddedText']
                );
            }

            if (error.code === 'ETIMEDOUT' || error.message?.includes('timeout')) {
                throw AppError.providerError('openai', 'OpenAI request timed out', error);
            }

            throw AppError.providerError('openai', error.message || 'OpenAI request failed', error);
        }
    }

    private buildRAGPrompt(userPrompt: string, embeddedText: string): string {
        return `Context from documents:
---
${embeddedText}
---

Based on the context above, answer the following question. If the answer is not in the context, say so clearly.

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