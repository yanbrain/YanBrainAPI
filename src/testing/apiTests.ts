import axios from 'axios';
import { generateAuthToken } from './generateAuthToken';
import * as fs from 'fs';
import * as path from 'path';

const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:8080';
const TOKEN_CACHE_FILE = path.join(__dirname, '.token-cache.json');
const DOCUMENTS_DIR = path.join(__dirname, 'documents');
const OUTPUT_DIR = path.join(__dirname, 'output', 'api');

if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

async function getToken(): Promise<string> {
    if (fs.existsSync(TOKEN_CACHE_FILE)) {
        const cache = JSON.parse(fs.readFileSync(TOKEN_CACHE_FILE, 'utf-8'));
        const ageMinutes = (Date.now() - cache.timestamp) / 1000 / 60;
        if (ageMinutes < 55) {
            console.log('✓ Using cached token\n');
            return cache.token;
        }
    }

    console.log('🔐 Generating new token...');
    const token = await generateAuthToken();
    fs.writeFileSync(
        TOKEN_CACHE_FILE,
        JSON.stringify({ token, timestamp: Date.now() }, null, 2)
    );
    console.log('✓ Token cached\n');
    return token;
}

function loadTestFile(filename: string): { filename: string; contentBase64: string } | null {
    const filePath = path.join(DOCUMENTS_DIR, filename);

    if (!fs.existsSync(filePath)) {
        return null;
    }

    const buffer = fs.readFileSync(filePath);
    return {
        filename,
        contentBase64: buffer.toString('base64')
    };
}

async function testHealthCheck() {
    try {
        console.log('🏥 Testing Health Check...');
        const response = await axios.get(`${API_BASE_URL}/health`);
        console.log('✅ Health Check - PASSED\n');
        console.log('   Status:', response.data.status);
        console.log('   Service:', response.data.service);
        console.log('   Version:', response.data.version, '\n');
        return true;
    } catch (error: any) {
        console.log('❌ Health Check - FAILED');
        console.error('   Error:', error.message, '\n');
        return false;
    }
}

async function testDocumentConvertAndEmbed(files: any[], token: string) {
    try {
        console.log(`📄 Testing Document Convert & Embed (${files.length} files)...`);

        const response = await axios.post(
            `${API_BASE_URL}/api/documents/convert-and-embed`,
            { files },
            {
                headers: { Authorization: `Bearer ${token}` },
                timeout: 60000
            }
        );

        console.log('✅ Document Convert & Embed - PASSED\n');
        console.log(`   Total Files: ${response.data.data.totalFiles}`);
        console.log(`   Credits Charged: ${response.data.data.totalCreditsCharged}\n`);

        const timestamp = Date.now();
        const outputPath = path.join(OUTPUT_DIR, `converted_${timestamp}.json`);
        fs.writeFileSync(outputPath, JSON.stringify(response.data.data, null, 2));
        console.log(`   💾 Results saved: ${outputPath}\n`);

        response.data.data.files.forEach((file: any) => {
            console.log(`   📄 ${file.filename}`);
            console.log(`      Characters: ${file.characterCount}`);
            console.log(`      Embedding: ${file.dimensions}D vector`);
            console.log(`      Text preview: ${file.text.substring(0, 100)}...\n`);
        });

        return true;
    } catch (error: any) {
        console.log('❌ Document Convert & Embed - FAILED');
        console.error('   Error:', error.response?.data || error.message, '\n');
        return false;
    }
}

async function testYanAvatar(token: string) {
    try {
        console.log('🤖 Testing YanAvatar...');

        const response = await axios.post(
            `${API_BASE_URL}/api/yanavatar`,
            {
                userPrompt: 'What is the weather like?',
                relevantDocuments: [
                    {
                        filename: 'test.txt',
                        text: 'The weather today is sunny and warm with temperatures around 25 degrees Celsius.'
                    }
                ]
            },
            {
                headers: { Authorization: `Bearer ${token}` },
                timeout: 60000
            }
        );

        console.log('✅ YanAvatar - PASSED\n');
        console.log(`   Text Response: ${response.data.data.textResponse}`);
        console.log(`   Documents Used: ${response.data.data.documentsUsed}`);
        console.log(`   Audio Size: ${Math.round(response.data.data.audio.length / 1024)} KB\n`);

        const timestamp = Date.now();
        const audioPath = path.join(OUTPUT_DIR, `yanavatar_${timestamp}.mp3`);
        fs.writeFileSync(audioPath, Buffer.from(response.data.data.audio, 'base64'));
        console.log(`   💾 Audio saved: ${audioPath}\n`);

        return true;
    } catch (error: any) {
        console.log('❌ YanAvatar - FAILED');
        console.error('   Error:', error.response?.data || error.message, '\n');
        return false;
    }
}

async function testImageGeneration(token: string) {
    try {
        console.log('🎨 Testing Image Generation...');

        const response = await axios.post(
            `${API_BASE_URL}/api/image`,
            {
                prompt: 'A beautiful sunset over mountains',
                imageBase64: ''
            },
            {
                headers: { Authorization: `Bearer ${token}` },
                timeout: 60000
            }
        );

        console.log('✅ Image Generation - PASSED\n');
        console.log(`   Image URL: ${response.data.data.imageUrl}\n`);

        return true;
    } catch (error: any) {
        console.log('❌ Image Generation - FAILED');
        console.error('   Error:', error.response?.data || error.message, '\n');
        return false;
    }
}

async function runTests() {
    console.log('🧪 API Tests\n');
    console.log('🌐 API Base URL:', API_BASE_URL, '\n');

    const results = {
        health: false,
        documentConvert: false,
        yanAvatar: false,
        imageGeneration: false
    };

    // Test 1: Health Check (no auth)
    results.health = await testHealthCheck();

    if (!results.health) {
        console.log('❌ API not responding. Stopping tests.\n');
        process.exit(1);
    }

    // Get auth token
    const token = await getToken();

    // Test 2: Document Convert & Embed
    const testFiles = fs.readdirSync(DOCUMENTS_DIR);
    const files = testFiles
        .map(loadTestFile)
        .filter((f): f is { filename: string; contentBase64: string } => f !== null);

    if (files.length > 0) {
        results.documentConvert = await testDocumentConvertAndEmbed(files, token);
    } else {
        console.log('⚠️  No test files found in:', DOCUMENTS_DIR, '\n');
    }

    // Test 3: YanAvatar
    results.yanAvatar = await testYanAvatar(token);

    // Test 4: Image Generation
    results.imageGeneration = await testImageGeneration(token);

    // Summary
    const passed = Object.values(results).filter(r => r).length;
    const total = Object.values(results).length;

    console.log('📊 Test Summary:');
    console.log(`   Health Check: ${results.health ? '✅' : '❌'}`);
    console.log(`   Document Convert & Embed: ${results.documentConvert ? '✅' : '❌'}`);
    console.log(`   YanAvatar: ${results.yanAvatar ? '✅' : '❌'}`);
    console.log(`   Image Generation: ${results.imageGeneration ? '✅' : '❌'}`);
    console.log(`\n   Total: ${passed}/${total} passed\n`);

    process.exit(passed === total ? 0 : 1);
}

runTests().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});