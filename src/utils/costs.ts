import { CREDIT_COSTS } from '../config/constants';

export function calculateEmbeddingCost(files: Array<{ contentBase64: string }>): number {
  const totalChars = files.reduce((sum, file) => {
    const buffer = Buffer.from(file.contentBase64, 'base64');
    return sum + buffer.length;
  }, 0);

  const scaledCost = Math.ceil(totalChars / 1000) * CREDIT_COSTS.EMBEDDING_CREDITS_PER_1K_CHARS;

  return Math.max(CREDIT_COSTS.EMBEDDING_MIN, scaledCost);
}
