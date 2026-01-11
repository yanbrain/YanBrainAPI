import OpenAI from 'openai';
import { ILLMProvider } from '../providers/LLMProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ChatMessage, ModelInfo } from '../types/api.types';

/**
 * OpenAI implementation of LLMProvider
 * Handles all OpenAI-specific logic and error handling
 */
export class OpenAIAdapter implements ILLMProvider {
  private client: OpenAI;
  private model: string = 'gpt-4.1-mini'; // Latest cost-effective model

  constructor() {
    this.client = new OpenAI({
      apiKey: API_KEYS.OPENAI
    });
  }

  /**
   * Generate response using OpenAI Chat Completions
   */
  async generateResponse(
    message: string,
    conversationHistory: ChatMessage[] = []
  ): Promise<string> {
    try {
      // Build messages array
      const messages: OpenAI.Chat.ChatCompletionMessageParam[] = [
        { role: 'system', content: 'You are a helpful AI assistant for YanBrain applications.' },
        ...conversationHistory.map(msg => ({
          role: msg.role as 'system' | 'user' | 'assistant',
          content: msg.content
        })),
        { role: 'user', content: message }
      ];

      const response = await this.client.chat.completions.create({
        model: this.model,
        messages: messages,
        max_tokens: 500,
        temperature: 0.7
      });

      return response.choices[0].message.content || '';
    } catch (error: any) {
      // Handle OpenAI specific errors
      if (error.code === 'insufficient_quota') {
        throw AppError.quotaExceededError('openai', 'OpenAI account has no credits left');
      }
      
      if (error.status === 429) {
        throw AppError.rateLimitError('openai', 'OpenAI rate limit exceeded');
      }

      if (error.status === 401) {
        throw AppError.providerError('openai', 'Invalid OpenAI API key', error);
      }

      // Generic OpenAI error
      throw AppError.providerError('openai', error.message || 'OpenAI request failed', error);
    }
  }

  /**
   * Get model information
   */
  getModelInfo(): ModelInfo {
    return {
      provider: 'OpenAI',
      model: this.model
    };
  }
}

export default OpenAIAdapter;
