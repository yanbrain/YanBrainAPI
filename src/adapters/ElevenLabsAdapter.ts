import { ElevenLabsClient } from '@elevenlabs/elevenlabs-js';
import { ITTSProvider } from '../providers/TTSProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ProviderInfo } from '../types/api.types';

export class ElevenLabsAdapter implements ITTSProvider {
    private readonly client: ElevenLabsClient;
    private readonly model = 'eleven_flash_v2_5';
    private readonly defaultVoiceId = 'EXAVITQu4vr4xnSDxMaL';

    constructor() {
        this.client = new ElevenLabsClient({
            apiKey: API_KEYS.ELEVENLABS
        });
    }

    async textToSpeech(text: string, voiceId?: string): Promise<Buffer> {
        const trimmedText = text?.trim();
        if (!trimmedText) {
            throw AppError.validationError('Text cannot be empty', ['text']);
        }

        if (trimmedText.length > 5000) {
            throw AppError.validationError(
                `Text too long: ${trimmedText.length} chars (max 5000)`,
                ['text']
            );
        }

        const selectedVoiceId = voiceId || this.defaultVoiceId;

        try {
            const audio = await this.client.textToSpeech.convert(selectedVoiceId, {
                text: trimmedText,
                modelId: this.model
            });

            const chunks: Buffer[] = [];
            const startTime = Date.now();
            let totalBytes = 0;

            for await (const chunk of audio) {
                if (Date.now() - startTime > 30000) {
                    throw new Error('Stream timeout');
                }

                const buffer = Buffer.from(chunk);
                totalBytes += buffer.length;

                if (totalBytes > 52428800) {
                    throw new Error('Audio size exceeded 50MB');
                }

                chunks.push(buffer);
            }

            const audioBuffer = Buffer.concat(chunks);

            if (audioBuffer.length === 0) {
                throw new Error('Empty audio buffer');
            }

            return audioBuffer;

        } catch (error: any) {
            if (error instanceof AppError) throw error;

            if (error.message?.includes('quota') || error.message?.includes('character limit')) {
                throw AppError.quotaExceededError('elevenlabs', 'Quota exceeded');
            }

            if (error.status === 429) {
                throw AppError.rateLimitError('elevenlabs', 'Rate limit exceeded');
            }

            if (error.status === 401) {
                throw AppError.providerError('elevenlabs', 'Invalid API key', error);
            }

            if (error.status === 404) {
                throw AppError.validationError(`Voice not found: ${selectedVoiceId}`, ['voiceId']);
            }

            throw AppError.providerError('elevenlabs', error.message || 'TTS failed', error);
        }
    }

    async getVoices(): Promise<any[]> {
        try {
            const response = await this.client.voices.getAll();
            return response?.voices || [];
        } catch (error: any) {
            console.error('[ElevenLabs] Failed to fetch voices:', error.message);
            throw AppError.providerError('elevenlabs', 'Failed to retrieve voices', error);
        }
    }

    getProviderInfo(): ProviderInfo {
        return {
            provider: 'ElevenLabs',
            model: this.model,
            defaultVoice: this.defaultVoiceId
        };
    }
}

export default ElevenLabsAdapter;