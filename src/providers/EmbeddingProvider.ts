import { ProviderInfo } from '../types/api.types';

/**
 * Abstract Embedding Provider Interface
 * Defines the contract that all embedding adapters must implement
 * This allows easy swapping between OpenAI, Cohere, Voyage, etc.
 */
export interface IEmbeddingProvider {
  /**
   * Generate embeddings from text
   * @param text - Text to convert to embeddings
   * @param model - Optional model name
   * @returns Array of embedding values (floats)
   */
  generateEmbedding(
    text: string,
    model?: string
  ): Promise<number[]>;

  /**
   * Get embedding dimensions for the current model
   * @returns Number of dimensions in the embedding vector
   */
  getDimensions(model?: string): number;

  /**
   * Get provider information
   * @returns Provider details including default model
   */
  getProviderInfo(): ProviderInfo;

  /**
   * Get available models
   * @returns List of available embedding models
   */
  getAvailableModels(): string[];
}
