import OpenAI from 'openai';
import { IEmbeddingProvider } from '../providers/EmbeddingProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ProviderInfo, FileUpload, EmbeddingFile } from '../types/api.types';
import { randomUUID } from 'crypto';

/**
 * OpenAI implementation of EmbeddingProvider
 * Handles all OpenAI embedding-specific logic and error handling
 */
export class OpenAIEmbeddingAdapter implements IEmbeddingProvider {
  private client: OpenAI;
  private defaultModel: string = 'text-embedding-3-small';
  
  // Model dimensions mapping
  private modelDimensions: Record<string, number> = {
    'text-embedding-3-small': 1536,
    'text-embedding-3-large': 3072,
    'text-embedding-ada-002': 1536
  };

  constructor() {
    this.client = new OpenAI({
      apiKey: API_KEYS.OPENAI
    });
  }

  /**
   * Generate embeddings using OpenAI
   */
  async generateEmbedding(
    text: string,
    model?: string
  ): Promise<number[]> {
    try {
      const selectedModel = model || this.defaultModel;

      const response = await this.client.embeddings.create({
        model: selectedModel,
        input: text,
        encoding_format: 'float'
      });

      return response.data[0].embedding;
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
      throw AppError.providerError('openai', error.message || 'OpenAI embeddings request failed', error);
    }
  }

  /**
   * Get embedding dimensions for the model
   */
  getDimensions(model?: string): number {
    const selectedModel = model || this.defaultModel;
    return this.modelDimensions[selectedModel] || 1536;
  }

  /**
   * Get provider information
   */
  getProviderInfo(): ProviderInfo {
    return {
      provider: 'OpenAI',
      defaultModel: this.defaultModel,
      availableModels: this.getAvailableModels()
    };
  }

  /**
   * Get available embedding models
   */
  getAvailableModels(): string[] {
    return Object.keys(this.modelDimensions);
  }

  /**
   * Process files and extract text for embedding
   * Returns array of files with unique IDs and extracted text
   */
  async processFiles(files: FileUpload[]): Promise<EmbeddingFile[]> {
    try {
      const processedFiles: EmbeddingFile[] = [];

      for (const file of files) {
        // Generate unique file ID
        const fileId = `file_${randomUUID()}`;

        // Decode base64 content
        const buffer = Buffer.from(file.contentBase64, 'base64');

        // Extract text based on file type
        let extractedText = '';
        const filename = file.filename.toLowerCase();

        if (filename.endsWith('.txt') || filename.endsWith('.md')) {
          // Plain text files
          extractedText = buffer.toString('utf-8');
        } else if (filename.endsWith('.json')) {
          // JSON files
          extractedText = buffer.toString('utf-8');
        } else {
          // For other file types, attempt UTF-8 decoding
          // In production, you would use specialized libraries for PDF, DOCX, etc.
          try {
            extractedText = buffer.toString('utf-8');
          } catch (error) {
            throw AppError.validationError(
              `Unable to extract text from file: ${file.filename}. Unsupported file type.`,
              ['files']
            );
          }
        }

        // Validate extracted text
        if (!extractedText || extractedText.trim().length === 0) {
          throw AppError.validationError(
            `No text could be extracted from file: ${file.filename}`,
            ['files']
          );
        }

        processedFiles.push({
          fileId,
          text: extractedText.trim()
        });
      }

      return processedFiles;
    } catch (error: any) {
      if (error instanceof AppError) {
        throw error;
      }
      throw AppError.providerError('openai', error.message || 'File processing failed', error);
    }
  }
}

export default OpenAIEmbeddingAdapter;
