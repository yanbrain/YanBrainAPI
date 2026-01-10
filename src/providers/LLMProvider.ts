import { ChatMessage, ModelInfo } from '../types/api.types';

/**
 * Abstract LLM Provider Interface
 * Defines the contract that all LLM adapters must implement
 * This allows easy swapping between OpenAI, Anthropic, etc.
 */
export interface ILLMProvider {
  /**
   * Generate a text response from the LLM
   * @param message - User's message
   * @param conversationHistory - Optional conversation history
   * @returns AI generated response
   */
  generateResponse(
    message: string,
    conversationHistory?: ChatMessage[]
  ): Promise<string>;

  /**
   * Get model information
   * @returns Model details
   */
  getModelInfo(): ModelInfo;
}
