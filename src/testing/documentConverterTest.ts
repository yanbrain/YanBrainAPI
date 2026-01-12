import { convertDocumentToText } from '../utils/documentConverter';

import * as fs from 'fs';
import * as path from 'path';

const DOCUMENTS_DIR = path.join(__dirname, 'documents');
const OUTPUT_DIR = path.join(__dirname, 'output', 'documentConverter');

if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

async function testDocumentConverter() {
    console.log('🧪 Document Converter Test\n');
    console.log('📁 Loading files from:', DOCUMENTS_DIR, '\n');

    const files = fs.readdirSync(DOCUMENTS_DIR);

    if (files.length === 0) {
        console.log('❌ No files found in:', DOCUMENTS_DIR);
        process.exit(1);
    }

    console.log(`✓ Found ${files.length} files\n`);

    let passed = 0;
    let failed = 0;

    for (const filename of files) {
        try {
            const filePath = path.join(DOCUMENTS_DIR, filename);
            const fileBuffer = fs.readFileSync(filePath);

            console.log(`📄 Converting: ${filename}`);
            const text = await convertDocumentToText(fileBuffer, filename);

            console.log(`   ✓ Extracted ${text.length} characters`);
            console.log(`   Preview: ${text.substring(0, 100)}...\n`);

            // Save converted text
            const outputPath = path.join(OUTPUT_DIR, `converted_${filename}.txt`);
            fs.writeFileSync(outputPath, text);
            console.log(`   💾 Saved to: ${outputPath}\n`);

            passed++;
        } catch (error: any) {
            console.log(`   ✗ Failed: ${error.message}\n`);
            failed++;
        }
    }

    console.log(`\n📊 Results: ${passed} passed, ${failed} failed\n`);
    process.exit(failed > 0 ? 1 : 0);
}

testDocumentConverter().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});