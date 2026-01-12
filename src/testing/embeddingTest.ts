import { OpenAIEmbeddingAdapter } from '../adapters/OpenAIEmbeddingAdapter';
import * as fs from 'fs';
import * as path from 'path';

const INPUT_DIR = path.join(__dirname, 'output', 'documentConverter');
const OUTPUT_DIR = path.join(__dirname, 'output', 'embeddings');

if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

const embeddingAdapter = new OpenAIEmbeddingAdapter();

async function testEmbeddings() {
    console.log('🧪 Embedding Tests\n');
    console.log('📁 Reading converted documents from:', INPUT_DIR, '\n');

    if (!fs.existsSync(INPUT_DIR)) {
        console.log('❌ No converted documents found!');
        console.log('   Run: npm run test:documentConverter first\n');
        process.exit(1);
    }

    const files = fs.readdirSync(INPUT_DIR).filter(f => f.startsWith('converted_'));

    if (files.length === 0) {
        console.log('❌ No converted documents found in:', INPUT_DIR);
        console.log('   Run: npm run test:documentConverter first\n');
        process.exit(1);
    }

    console.log(`✓ Found ${files.length} converted documents\n`);

    let passed = 0;
    let failed = 0;

    for (const filename of files) {
        try {
            const filePath = path.join(INPUT_DIR, filename);
            const text = fs.readFileSync(filePath, 'utf-8');

            console.log(`📝 Embedding: ${filename}`);
            console.log(`   Text length: ${text.length} characters`);

            const embedding = await embeddingAdapter.generateEmbedding(text);

            console.log(`   ✓ Generated ${embedding.length}D vector`);
            console.log(`   Preview: [${embedding.slice(0, 5).join(', ')}...]\n`);

            // Save embedding
            const outputFilename = filename.replace('converted_', 'embedded_');
            const outputPath = path.join(OUTPUT_DIR, outputFilename.replace('.txt', '.json'));
            fs.writeFileSync(outputPath, JSON.stringify({
                sourceFile: filename,
                textPreview: text.substring(0, 200) + '...',
                embedding,
                dimensions: embedding.length
            }, null, 2));
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

testEmbeddings().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});