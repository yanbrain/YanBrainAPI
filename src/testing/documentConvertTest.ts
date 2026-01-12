import axios from 'axios';
import { generateAuthToken } from './generateAuthToken';
import * as fs from 'fs';
import * as path from 'path';

const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:8080';
const TOKEN_CACHE_FILE = path.join(__dirname, '.token-cache.json');
const DOCUMENTS_DIR = path.join(__dirname, 'documents');
const OUTPUT_DIR = path.join(
    __dirname,
    '..',
    '..',
    'testing',
    'output',
    'convertedDocuments'
);

// Ensure output directory exists
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

async function testDocumentConvert(files: any[], token: string) {
    try {
        console.log(`📄 Converting ${files.length} documents...`);

        const response = await axios.post(
            `${API_BASE_URL}/api/documents/convert-and-embed`,
            { files },
            {
                headers: { Authorization: `Bearer ${token}` },
                timeout: 60000
            }
        );

        console.log('✅ Document Conversion - PASSED\n');
        console.log(`   Total Files: ${response.data.data.totalFiles}`);
        console.log(`   Credits Charged: ${response.data.data.totalCreditsCharged}\n`);

        const timestamp = Date.now();
        const outputPath = path.join(OUTPUT_DIR, `converted_${timestamp}.json`);
        fs.writeFileSync(outputPath, JSON.stringify(response.data.data, null, 2));
        console.log(`   💾 Results saved: ${outputPath}\n`);

        response.data.data.files.forEach((file: any) => {
            console.log(`   📄 ${file.filename}`);
            console.log(`      File ID: ${file.fileId}`);
            console.log(`      Characters: ${file.characterCount}`);
            console.log(`      Embedding: ${file.dimensions}D vector`);
            console.log(`      Text preview: ${file.text.substring(0, 100)}...\n`);
        });

        return true;
    } catch (error: any) {
        console.log('❌ Document Conversion - FAILED');
        console.error('   Error:', error.response?.data || error.message);
        return false;
    }
}

async function runTests() {
    console.log('🧪 Document Convert & Embed Tests\n');
    console.log('📁 Loading test files from:', DOCUMENTS_DIR, '\n');

    const testFiles = fs.readdirSync(DOCUMENTS_DIR);
    const files = testFiles
        .map(loadTestFile)
        .filter((f): f is { filename: string; contentBase64: string } => f !== null);

    if (files.length === 0) {
        console.log('❌ No test files found!');
        console.log('\nAdd documents to:', DOCUMENTS_DIR);
        process.exit(1);
    }

    console.log(`✓ Loaded ${files.length} test files\n`);

    const token = await getToken();
    const passed = await testDocumentConvert(files, token);

    console.log(`\n📊 Result: ${passed ? 'PASSED' : 'FAILED'}\n`);
    process.exit(passed ? 0 : 1);
}

runTests().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});
