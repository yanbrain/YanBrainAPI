import axios from 'axios';
import { generateAuthToken } from './generateAuthToken';
import * as fs from 'fs';
import * as path from 'path';

const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:8080';
const TOKEN_CACHE_FILE = path.join(__dirname, '.token-cache.json');
const OUTPUT_DIR = path.join(__dirname, '..', 'output', 'audio');

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

    const token = await generateAuthToken();
    fs.writeFileSync(TOKEN_CACHE_FILE, JSON.stringify({ token, timestamp: Date.now() }));
    return token;
}

async function testYanAvatar() {
    console.log('\n🤖 YanAvatar End-to-End Test\n');

    try {
        const token = await getToken();

        // Mock relevant documents (normally user would search their local embeddings)
        const relevantDocuments = [
            {
                filename: 'company_info.md',
                text: 'YanBrain is an AI company founded in 2024. We build AI-powered avatar assistants.'
            },
            {
                filename: 'products.md',
                text: 'Our main products include: YanAvatar (voice AI assistant), YanDraw (image generation), YanPhotobooth (AI photos).'
            },
            {
                filename: 'team.md',
                text: 'Our team consists of AI engineers and product designers focused on user experience.'
            }
        ];

        const question = "What does YanBrain do and what are the main products?";

        console.log(`💬 Question: "${question}"`);
        console.log(`📄 Using ${relevantDocuments.length} relevant documents\n`);

        const response = await axios.post(
            `${API_BASE_URL}/api/yanavatar`,
            {
                userPrompt: question,
                relevantDocuments: relevantDocuments
            },
            {
                headers: { Authorization: `Bearer ${token}` },
                timeout: 60000
            }
        );

        const { audio, textResponse, documentsUsed } = response.data.data;

        console.log('✅ YanAvatar Request - PASSED\n');
        console.log('📝 Text Response:');
        console.log('   ─────────────────────────────────────');
        console.log(`   ${textResponse}`);
        console.log('   ─────────────────────────────────────\n');

        // Save audio
        const timestamp = Date.now();
        const audioPath = path.join(OUTPUT_DIR, `response_${timestamp}.mp3`);
        const audioBuffer = Buffer.from(audio, 'base64');
        fs.writeFileSync(audioPath, audioBuffer);

        console.log(`🔊 Audio: ${Math.round(audioBuffer.length / 1024)} KB`);
        console.log(`   💾 Saved: ${audioPath}`);
        console.log(`📊 Documents Used: ${documentsUsed}`);
        console.log(`💰 Credits Charged: 5\n`);

        console.log('✅ Test Complete!\n');
        process.exit(0);

    } catch (error: any) {
        console.error('\n❌ Test Failed:', error.response?.data || error.message);
        process.exit(1);
    }
}

testYanAvatar();