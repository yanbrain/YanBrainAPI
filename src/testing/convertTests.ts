import axios from 'axios';
import { generateToken } from './generateToken';
import * as fs from 'fs';
import * as path from 'path';

const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:8080';
const TOKEN_CACHE_FILE = path.join(__dirname, '.token-cache.json');
const DOCUMENTS_DIR = path.join(__dirname, 'documents');

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
    const token = await generateToken();
    fs.writeFileSync(TOKEN_CACHE_FILE, JSON.stringify({ token, timestamp: Date.now() }, null, 2));
    console.log('✓ Token cached\n');
    return token;
}

function loadTestFile(filename: string): { filename: string; contentBase64: string } | null {
    const filePath = path.join(DOCUMENTS_DIR, filename);

    if (!fs.existsSync(filePath)) {
        console.log(`⚠️  ${filename} not found, skipping...`);
        return null;
    }

    const buffer = fs.readFileSync(filePath);
    return {
        filename: filename,
        contentBase64: buffer.toString('base64')
    };
}

async function testPriceCheck(files: any[], token: string) {
    try {
        const response = await axios.post(
            `${API_BASE_URL}/api/convert/price-check`,
            { files },
            { headers: { 'Authorization': `Bearer ${token}` } }
        );

        console.log('✅ Price Check - PASSED');
        console.log(`   Estimated Cost: ${response.data.data.estimatedCost} credits`);
        console.log(`   Total Size: ${response.data.data.totalSizeKB} KB`);
        console.log(`   File Count: ${response.data.data.fileCount}\n`);
        return true;
    } catch (error: any) {
        console.log('❌ Price Check - FAILED');
        console.error('   Error:', error.response?.data || error.message);
        return false;
    }
}

async function testConversion(files: any[], token: string) {
    try {
        const response = await axios.post(
            `${API_BASE_URL}/api/convert`,
            { files },
            {
                headers: { 'Authorization': `Bearer ${token}` },
                timeout: 30000
            }
        );

        console.log('✅ Document Conversion - PASSED');
        response.data.data.files.forEach((file: any) => {
            console.log(`   📄 ${file.filename}`);
            console.log(`      Characters: ${file.characterCount}`);
            console.log(`      Preview: ${file.text.substring(0, 100)}...`);
        });
        console.log();
        return true;
    } catch (error: any) {
        console.log('❌ Document Conversion - FAILED');
        console.error('   Error:', error.response?.data || error.message);
        return false;
    }
}

async function runTests() {
    console.log('🧪 Document Converter Tests\n');
    console.log('📁 Loading test files from:', DOCUMENTS_DIR, '\n');

    const testFiles = ['sample.pdf', 'sample.docx', 'sample.xlsx', 'sample.pptx', 'sample.txt'];
    const files = testFiles.map(loadTestFile).filter(f => f !== null);

    if (files.length === 0) {
        console.log('❌ No test files found!');
        console.log('\nCreate test files in:', DOCUMENTS_DIR);
        console.log('Needed files:', testFiles.join(', '));
        process.exit(1);
    }

    console.log(`✓ Loaded ${files.length} test files\n`);

    const token = await getToken();
    const results = [];

    // Test price check
    results.push(await testPriceCheck(files, token));

    // Test conversion
    results.push(await testConversion(files, token));

    const passed = results.filter(r => r).length;
    console.log(`\n📊 Results: ${passed}/${results.length} tests passed\n`);

    process.exit(passed === results.length ? 0 : 1);
}

runTests().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});