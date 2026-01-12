import { ChatMessage, ModelInfo } from '../types/api.types';

export interface ILLMProvider {
    generateResponse(
        userPrompt: string,
        conversationHistory?: ChatMessage[]
    ): Promise<string>;

    getModelInfo(): ModelInfo;
}