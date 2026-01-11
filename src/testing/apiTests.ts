import axios from 'axios';
import { generateToken } from './generateToken';
import * as fs from 'fs';
import * as path from 'path';

const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:8080';
const TOKEN_CACHE_FILE = path.join(__dirname, '.token-cache.json');
const TOKEN_VALIDITY_MINUTES = 55;

interface TokenCache {
    token: string;
    timestamp: number;
}

async function getToken(): Promise<string> {
    if (fs.existsSync(TOKEN_CACHE_FILE)) {
        try {
            const cache: TokenCache = JSON.parse(fs.readFileSync(TOKEN_CACHE_FILE, 'utf-8'));
            const ageMinutes = (Date.now() - cache.timestamp) / 1000 / 60;

            if (ageMinutes < TOKEN_VALIDITY_MINUTES) {
                console.log('✓ Using cached token\n');
                return cache.token;
            }
        } catch (error) {
            // Invalid cache
        }
    }

    console.log('🔐 Generating new token...');
    const token = await generateToken();
    fs.writeFileSync(TOKEN_CACHE_FILE, JSON.stringify({ token, timestamp: Date.now() }, null, 2));
    console.log('✓ Token cached\n');

    return token;
}

const TEST_DATA = {
    llm: { message: 'What is 2+2?' },
    tts: { text: 'Hello from YanBrain' },
    image: {
        prompt: 'A beautiful sunset',
        imageBase64: 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='
    },
    embeddings: {
        files: [{
            filename: 'test.txt',
            contentBase64: Buffer.from('Hello world! This is a test file.').toString('base64')
        }]
    }
};

async function testEndpoint(name: string, endpoint: string, data: any, token: string) {
    try {
        const response = await axios.post(
            `${API_BASE_URL}${endpoint}`,
            data,
            {
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                },
                timeout: 30000
            }
        );

        console.log(`✅ ${name} - PASSED`);
        if (process.argv[2]) {
            console.log('Response:', JSON.stringify(response.data, null, 2));
        }
        return true;
    } catch (error: any) {
        console.log(`❌ ${name} - FAILED`);
        console.error('Error details:', {
            status: error.response?.status,
            statusText: error.response?.statusText,
            data: error.response?.data,
            message: error.message,
            code: error.code
        });
        return false;
    }
}

async function testHealth() {
    try {
        const response = await axios.get(`${API_BASE_URL}/health`, { timeout: 5000 });
        const passed = response.data.status === 'ok';
        console.log(`${passed ? '✅' : '❌'} Health - ${passed ? 'PASSED' : 'FAILED'}`);
        return passed;
    } catch (error: any) {
        console.log(`❌ Health - FAILED`);
        console.error('Error details:', {
            status: error.response?.status,
            message: error.message,
            code: error.code
        });
        return false;
    }
}

async function runTests() {
    const token = await getToken();
    const testName = process.argv[2];

    console.log('🧪 Running tests...\n');

    const results = [];

    if (!testName || testName === 'health') {
        results.push(await testHealth());
    }

    if (!testName || testName === 'llm') {
        results.push(await testEndpoint('LLM', '/api/llm', TEST_DATA.llm, token));
    }

    if (!testName || testName === 'tts') {
        results.push(await testEndpoint('TTS', '/api/tts', TEST_DATA.tts, token));
    }

    if (!testName || testName === 'image') {
        results.push(await testEndpoint('Image', '/api/image', TEST_DATA.image, token));
    }

    if (!testName || testName === 'embeddings') {
        results.push(await testEndpoint('Embeddings', '/api/embeddings', TEST_DATA.embeddings, token));
    }

    const passed = results.filter(r => r).length;
    console.log(`\n${passed}/${results.length} tests passed\n`);

    process.exit(passed === results.length ? 0 : 1);
}

runTests().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});