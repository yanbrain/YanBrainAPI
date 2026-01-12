import officeParser from 'officeparser';
import { AppError } from '../errors/AppError';
import { CREDIT_COSTS } from '../config/constants';

const SUPPORTED_FORMATS = ['pdf', 'docx', 'doc', 'rtf', 'xlsx', 'xls', 'pptx', 'ppt', 'odt', 'odp', 'ods', 'txt', 'md'];

/**
 * Convert document to text
 */
export async function convertDocumentToText(fileBuffer: Buffer, filename: string): Promise<string> {
    try {
        const ext = filename.toLowerCase().split('.').pop() || '';

        // Check if format is supported
        if (!SUPPORTED_FORMATS.includes(ext)) {
            throw AppError.validationError(
                `Unsupported file format: ${filename}. Supported: ${SUPPORTED_FORMATS.join(', ')}`,
                ['filename']
            );
        }

        // Handle plain text directly
        if (ext === 'txt' || ext === 'md') {
            return fileBuffer.toString('utf-8');
        }

        // Use officeParser for other formats
        const ast = await officeParser.parseOffice(fileBuffer, {
            outputErrorToConsole: false,
            newlineDelimiter: '\n',
            ignoreNotes: false,
            putNotesAtLast: false
        });

        // Extract text from AST
        const text = ast.toText();

        if (!text || text.trim().length === 0) {
            throw AppError.validationError(`No text extracted from: ${filename}`, ['file']);
        }

        return text.trim();
    } catch (error: any) {
        if (error instanceof AppError) throw error;
        throw AppError.providerError('officeparser', error.message || 'Conversion failed', error);
    }
}

/**
 * Calculate conversion cost based on file size
 */
export function calculateConversionCost(files: Array<{ contentBase64: string }>): number {
    const totalChars = files.reduce((sum, file) => {
        return sum + Buffer.from(file.contentBase64, 'base64').length;
    }, 0);

    const scaledCost = Math.ceil(totalChars / 1000) * CREDIT_COSTS.DOCUMENT_CONVERTER_CREDITS_PER_1K_CHARS;
    return Math.max(CREDIT_COSTS.DOCUMENT_CONVERTER_MIN, scaledCost);
}

/**
 * Check price before conversion
 */
export function checkConversionPrice(files: Array<{ contentBase64: string }>): {
    estimatedCost: number;
    totalSizeKB: number;
    fileCount: number;
} {
    const totalBytes = files.reduce((sum, file) => {
        return sum + Buffer.from(file.contentBase64, 'base64').length;
    }, 0);

    return {
        estimatedCost: calculateConversionCost(files),
        totalSizeKB: Math.round(totalBytes / 1024),
        fileCount: files.length
    };
}