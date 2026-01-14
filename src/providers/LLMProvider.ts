import { ModelInfo } from '../types/api.types';

export interface ILLMProvider {
    generateResponse(
        userPrompt: string,
        options?: {
            systemPrompt?: string;
            additionalInstructions?: string;
            ragContext?: string;
            maxResponseChars?: number;
        }
    ): Promise<string>;

    getModelInfo(): ModelInfo;
}