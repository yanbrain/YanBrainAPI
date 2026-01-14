import { ElevenLabsClient } from '@elevenlabs/elevenlabs-js';
import { ITTSProvider } from '../providers/TTSProvider';
import { AppError } from '../errors/AppError';
import { API_KEYS } from '../config/constants';
import { ProviderInfo } from '../types/api.types';

export class ElevenLabsAdapter implements ITTSProvider {
    private readonly client: ElevenLabsClient;
    private readonly model = 'eleven_flash_v2_5';
    private readonly defaultVoiceId = 'EXAVITQu4vr4xnSDxMaL';

    // Text limits
    private readonly MAX_TEXT_LENGTH = 400;

    // Stream limits
    private readonly STREAM_TIMEOUT_MS = 30_000;
    private readonly MAX_AUDIO_SIZE_BYTES = 52_428_800; // 50MB

    constructor() {
        this.client = new ElevenLabsClient({
            apiKey: API_KEYS.ELEVENLABS
        });
    }

    /**
     * Convert text to speech audio
     */
    async textToSpeech(text: string, voiceId?: string): Promise<Buffer> {
        try {
            // Validate and prepare input
            const trimmedText = this.validateText(text);
            const selectedVoiceId = voiceId || this.defaultVoiceId;

            // Call ElevenLabs API
            const audioStream = await this.client.textToSpeech.convert(
                selectedVoiceId,
                {
                    text: trimmedText,
                    modelId: this.model
                }
            );

            // Process audio stream
            const audioBuffer = await this.processAudioStream(audioStream);

            return audioBuffer;

        } catch (error: any) {
            console.error('[ElevenLabs] TTS error:', {
                message: error.message,
                status: error.status
            });

            // Re-throw AppErrors as-is
            if (error instanceof AppError) throw error;

            // Handle specific ElevenLabs errors
            this.handleElevenLabsError(error, voiceId);
        }
    }

    /**
     * Get list of available voices
     */
    async getVoices(): Promise<any[]> {
        try {
            const response = await this.client.voices.getAll();
            return response?.voices || [];
        } catch (error: any) {
            console.error('[ElevenLabs] Failed to fetch voices:', error.message);
            throw AppError.providerError(
                'elevenlabs',
                'Failed to retrieve voices',
                error
            );
        }
    }

    /**
     * Validate input text
     */
    private validateText(text: string): string {
        const trimmedText = text?.trim();

        if (!trimmedText) {
            throw AppError.validationError('Text cannot be empty', ['text']);
        }

        if (trimmedText.length > this.MAX_TEXT_LENGTH) {
            throw AppError.validationError(
                `Text too long: ${trimmedText.length} chars (max ${this.MAX_TEXT_LENGTH})`,
                ['text']
            );
        }

        return trimmedText;
    }

    /**
     * Process audio stream and collect chunks into a single buffer
     */
    private async processAudioStream(
        audioStream: AsyncIterable<Uint8Array>
    ): Promise<Buffer> {
        const chunks: Buffer[] = [];
        const startTime = Date.now();
        let totalBytes = 0;

        for await (const chunk of audioStream) {
            // Check timeout
            if (Date.now() - startTime > this.STREAM_TIMEOUT_MS) {
                throw new Error('Audio stream timeout');
            }

            // Convert and track size
            const buffer = Buffer.from(chunk);
            totalBytes += buffer.length;

            // Check size limit
            if (totalBytes > this.MAX_AUDIO_SIZE_BYTES) {
                throw new Error('Audio size exceeded 50MB limit');
            }

            chunks.push(buffer);
        }

        // Combine all chunks
        const audioBuffer = Buffer.concat(chunks);

        // Validate result
        if (audioBuffer.length === 0) {
            throw new Error('Empty audio buffer received');
        }

        return audioBuffer;
    }

    /**
     * Handle ElevenLabs-specific errors and convert to AppErrors
     */
    private handleElevenLabsError(error: any, voiceId?: string): never {
        if (
            error.message?.includes('quota') ||
            error.message?.includes('character limit')
        ) {
            throw AppError.quotaExceededError(
                'elevenlabs',
                'ElevenLabs quota exceeded'
            );
        }

        if (error.status === 429) {
            throw AppError.rateLimitError(
                'elevenlabs',
                'Rate limit exceeded'
            );
        }

        if (error.status === 401) {
            throw AppError.providerError(
                'elevenlabs',
                'Invalid ElevenLabs API key',
                error
            );
        }

        if (error.status === 404) {
            throw AppError.validationError(
                `Voice not found: ${voiceId || this.defaultVoiceId}`,
                ['voiceId']
            );
        }

        throw AppError.providerError(
            'elevenlabs',
            error.message || 'Text-to-speech request failed',
            error
        );
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