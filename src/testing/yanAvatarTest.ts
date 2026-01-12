import axios from 'axios';
import { generateToken } from './generateToken';
import * as fs from 'fs';
import * as path from 'path';

const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:8080';
const TOKEN_CACHE_FILE = path.join(__dirname, '.token-cache.json');
const DOCUMENTS_DIR = path.join(__dirname, 'documents');
const OUTPUT_DIR = path.join(__dirname, 'output');

// Ensure output directory exists
if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

async function getToken(): Promise<string> {
    if (fs.existsSync(TOKEN_CACHE_FILE)) {
        const cache = JSON.parse(fs.readFileSync(TOKEN_CACHE_FILE, 'utf-8'));
        const ageMinutes = (Date.now() - cache.timestamp) / 1000 / 60;
        if (ageMinutes < 55) return cache.token;
    }

    const token = await generateToken();
    fs.writeFileSync(TOKEN_CACHE_FILE, JSON.stringify({ token, timestamp: Date.now() }));
    return token;
}

function loadDocuments(): Array<{ filename: string; contentBase64: string }> {
    const files = fs.readdirSync(DOCUMENTS_DIR);
    return files.map(filename => ({
        filename,
        contentBase64: fs.readFileSync(path.join(DOCUMENTS_DIR, filename)).toString('base64')
    }));
}

async function generateEmbeddings(files: any[], token: string) {
    console.log('\n📄 Converting documents and generating embeddings...');

    const response = await axios.post(
        `${API_BASE_URL}/api/embeddings`,
        { files },
        { headers: { Authorization: `Bearer ${token}` }, timeout: 60000 }
    );

    const results = response.data.data.files;
    results.forEach((file: any) => {
        console.log(`   ✓ ${file.filename} - ${file.dimensions}D embedding`);
    });

    return results;
}

async function queryLLM(files: any[], question: string, token: string) {
    console.log('\n💬 Asking LLM with document context...');
    console.log(`   Question: "${question}"\n`);

    // Build context from documents
    const context = files.map((file: any, i: number) => {
        const text = Buffer.from(file.contentBase64, 'base64').toString('utf-8');
        return `Document ${i + 1} (${file.filename}):\n${text}`;
    }).join('\n\n---\n\n');

    const prompt = `Based on these documents, answer the question.

${context}

Question: ${question}

Answer:`;

    const response = await axios.post(
        `${API_BASE_URL}/api/llm`,
        { message: prompt },
        { headers: { Authorization: `Bearer ${token}` }, timeout: 30000 }
    );

    const answer = response.data.data.response;
    console.log('   Answer:');
    console.log('   ─────────────────────────────────────');
    console.log(`   ${answer}`);
    console.log('   ─────────────────────────────────────');

    return answer;
}

async function convertToSpeech(text: string, token: string) {
    console.log('\n🔊 Converting to speech...');

    const response = await axios.post(
        `${API_BASE_URL}/api/tts`,
        { text },
        { headers: { Authorization: `Bearer ${token}` }, timeout: 60000 }
    );

    const audioBuffer = Buffer.from(response.data.data.audio, 'base64');
    const audioPath = path.join(OUTPUT_DIR, 'response.mp3');
    fs.writeFileSync(audioPath, audioBuffer);

    console.log(`   ✓ Audio saved: ${audioPath} (${Math.round(audioBuffer.length / 1024)} KB)`);

    return audioPath;
}

async function main() {
    console.log('\n🤖 YanAvatar End-to-End Test\n');

    try {
        const token = await getToken();

        // Step 1: Load documents
        console.log('📁 Loading documents...');
        const files = loadDocuments();
        console.log(`   ✓ Loaded ${files.length} documents`);

        // Step 2: Generate embeddings
        const embeddings = await generateEmbeddings(files, token);

        // Step 3: Query LLM
        const question = "Tell me about our business - what does YanBrain do and who is on the team?";
        const answer = await queryLLM(files, question, token);

        // Step 4: Convert to speech
        const audioPath = await convertToSpeech(answer, token);

        // Success
        console.log('\n✅ Test Complete!\n');
        console.log(`📊 Summary:`);
        console.log(`   • Documents: ${files.length}`);
        console.log(`   • Embeddings: ${embeddings.length}`);
        console.log(`   • Audio: ${audioPath}`);
        console.log('');

        process.exit(0);
    } catch (error: any) {
        console.error('\n❌ Test Failed:', error.response?.data || error.message);
        process.exit(1);
    }
}

void main();