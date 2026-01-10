# YanBrainAPIClient (TypeScript Edition)

A professional, type-safe API wrapper for OpenAI, ElevenLabs, Wit.ai, and Runware. Built with TypeScript, Express.js, and the Provider/Adapter pattern for easy provider swapping.

## 🚀 Features

- **✨ Full TypeScript**: Complete type safety with interfaces and type definitions
- **🔌 Provider Abstraction**: Easy to swap between AI providers (OpenAI → Anthropic, etc.)
- **💰 Credit Management**: Integrates with YanBrainServer for credit tracking
- **🔐 Firebase Authentication**: Secure token-based auth
- **🎯 Centralized Error Handling**: Professional error responses with typed errors
- **📊 5 AI Capabilities**: LLM, TTS, STT, Image Generation, and Embeddings
- **🏗️ Industry Standard Structure**: Follows Node.js/TypeScript best practices

## 📂 Project Structure

```
YanBrainAPIClient/
├── src/
│   ├── types/              # TypeScript type definitions
│   │   ├── express.d.ts    # Express request extensions
│   │   └── api.types.ts    # API request/response types
│   ├── config/             # Configuration and constants
│   │   └── constants.ts
│   ├── middleware/         # Auth, credits, error handling
│   │   ├── authMiddleware.ts
│   │   ├── creditsMiddleware.ts
│   │   └── errorMiddleware.ts
│   ├── errors/             # Custom error classes
│   │   ├── AppError.ts
│   │   └── errorCodes.ts
│   ├── providers/          # Abstract interfaces (contracts)
│   │   ├── LLMProvider.ts
│   │   ├── TTSProvider.ts
│   │   ├── STTProvider.ts
│   │   ├── ImageProvider.ts
│   │   └── EmbeddingProvider.ts
│   ├── adapters/           # Concrete implementations
│   │   ├── OpenAIAdapter.ts
│   │   ├── ElevenLabsAdapter.ts
│   │   ├── WitAdapter.ts
│   │   ├── RunwareAdapter.ts
│   │   └── OpenAIEmbeddingAdapter.ts
│   ├── routes/             # API endpoints
│   │   ├── llmRoutes.ts
│   │   ├── ttsRoutes.ts
│   │   ├── sttRoutes.ts
│   │   ├── imageRoutes.ts
│   │   └── embeddingRoutes.ts
│   └── server.ts           # Main entry point
├── dist/                   # Compiled JavaScript (generated)
├── .env                    # Environment variables
├── .gitignore
├── package.json
├── tsconfig.json           # TypeScript configuration
└── README.md
```

## 🔧 Installation

1. **Install dependencies:**
```bash
npm install
```

2. **Configure environment variables:**
Copy `.env` and fill in your API keys:
```env
OPENAI_API_KEY=sk-...
ELEVENLABS_API_KEY=...
WIT_API_KEY=...
RUNWARE_API_KEY=...
YANBRAIN_SERVER_URL=https://your-server.com/api
FIREBASE_PROJECT_ID=your-project-id
```

3. **Build TypeScript:**
```bash
npm run build
```

4. **Start the server:**
```bash
# Production
npm start

# Development (with hot reload)
npm run dev
```

## 📡 API Endpoints

### 1. POST /api/llm
Generate text response from LLM (OpenAI)

**Request:**
```typescript
{
  message: string;
  conversationHistory?: Array<{
    role: 'system' | 'user' | 'assistant';
    content: string;
  }>;
}
```

**Response:**
```typescript
{
  success: true;
  data: {
    response: string;
    model: {
      provider: string;
      model: string;
    };
  };
}
```

### 2. POST /api/tts
Convert text to speech (ElevenLabs)

**Request:**
```typescript
{
  text: string;
  voiceId?: string;
}
```

**Response:**
```typescript
{
  success: true;
  data: {
    audio: string; // base64 encoded
    provider: { provider: string; defaultVoice: string; };
  };
}
```

### 3. POST /api/stt
Convert speech to text (Wit.ai)

**Request:** `multipart/form-data`
- Field: `audio` (audio file)

**Response:**
```typescript
{
  success: true;
  data: {
    text: string;
    provider: { provider: string; };
  };
}
```

### 4. POST /api/image
Generate image from prompt (Runware)

**Request:**
```typescript
{
  prompt: string;
  width?: number;
  height?: number;
  productId: 'yanDraw' | 'yanPhotobooth';
}
```

**Response:**
```typescript
{
  success: true;
  data: {
    imageUrl: string;
    prompt: string;
    dimensions: { width: number; height: number; };
    provider: { provider: string; };
  };
}
```

### 5. POST /api/embeddings ✨ NEW!
Generate embeddings from text (OpenAI)

**Request:**
```typescript
{
  text: string;
  model?: 'text-embedding-3-small' | 'text-embedding-3-large' | 'text-embedding-ada-002';
}
```

**Response:**
```typescript
{
  success: true;
  data: {
    embedding: number[]; // 1536 or 3072 dimensions
    model: string;
    dimensions: number;
  };
}
```

## 🔐 Authentication

All endpoints require Firebase Auth token:
```typescript
Authorization: Bearer <firebase-token>
```

## 🎯 TypeScript Benefits

### Type-Safe Requests
```typescript
// ✅ Compiler catches typos
const request: LLMRequest = {
  message: "Hello",
  conversationHistory: [] // Auto-complete available!
};

// ❌ Compiler error - wrong type
const badRequest: LLMRequest = {
  mesage: "Hello" // Error: Property 'mesage' does not exist
};
```

### Interface Enforcement
```typescript
// All adapters MUST implement the interface
class CustomLLMAdapter implements ILLMProvider {
  // Compiler forces you to implement all methods
  generateResponse(message: string): Promise<string> { ... }
  getModelInfo(): ModelInfo { ... }
}
```

### Auto-Complete Everywhere
```typescript
// IDE shows all available properties
req.user?.uid         // ✅ Auto-complete
req.productId         // ✅ Auto-complete
req.firebaseToken     // ✅ Auto-complete
```

## 🔄 Switching Providers

Change providers via environment variables:
```env
LLM_PROVIDER=openai           # or anthropic (when implemented)
TTS_PROVIDER=elevenlabs       # or google (when implemented)
STT_PROVIDER=wit              # or whisper (when implemented)
IMAGE_PROVIDER=runware        # or dalle (when implemented)
EMBEDDING_PROVIDER=openai     # or cohere (when implemented)
```

## 📊 Error Responses

All errors follow this typed structure:

```typescript
{
  success: false;
  error: {
    code: ErrorCode;           // Type-safe error codes
    message: string;
    statusCode: number;
    details?: Record<string, any>;
  };
}
```

**Example Error:**
```json
{
  "success": false,
  "error": {
    "code": "INSUFFICIENT_CREDITS",
    "message": "You don't have enough credits",
    "statusCode": 402,
    "details": {
      "required": 1,
      "available": 0
    }
  }
}
```

## 🛠️ Development

```bash
# Install dependencies
npm install

# Run in development mode (hot reload)
npm run dev

# Build TypeScript to JavaScript
npm run build

# Watch mode (rebuild on changes)
npm run watch

# Clean build folder
npm run clean
```

## 🧪 Unity Integration

```csharp
// C# knows the exact response structure
[Serializable]
public class LLMResponse {
    public bool success;
    public LLMData data;
}

[Serializable]
public class LLMData {
    public string response;
    public ModelInfo model;
}

// Type-safe API calls
StartCoroutine(apiClient.SendChatMessage("Hello", response => {
    Debug.Log(response.data.response); // ✅ Strongly typed
}));
```

## 📝 TypeScript Configuration

The project uses strict TypeScript settings:
- `strict: true` - All strict checks enabled
- `noImplicitAny: true` - No implicit any types
- `strictNullChecks: true` - Null safety
- `noUnusedLocals: true` - Catch unused variables
- `noImplicitReturns: true` - All code paths return

## 🎓 Use Cases

### LLM (Chat)
- Conversational AI in yanAvatar
- Question answering
- Text generation

### TTS (Text-to-Speech)
- Voice responses in yanAvatar
- Audio narration
- Accessibility features

### STT (Speech-to-Text)
- Voice commands in yanAvatar
- Transcription services
- Voice input

### Image Generation
- Art creation in yanDraw
- Photo effects in yanPhotobooth
- Creative tools

### Embeddings ✨ NEW!
- Semantic search
- RAG (Retrieval Augmented Generation)
- Similarity matching
- Content recommendations

## 📦 Build Output

After running `npm run build`, JavaScript files are in `dist/`:
```
dist/
├── server.js
├── config/
├── middleware/
├── errors/
├── adapters/
├── routes/
└── ...
```

## 📖 License

MIT License - Created by Artem for YanBrain

## 🔗 Related Projects

- **YanBrainServer**: User and credit management backend
- **yanAvatar**: Unity voice avatar application
- **yanDraw**: Unity drawing application
- **yanPhotobooth**: Unity photobooth application

---

**Built with ❤️ using TypeScript, Express, and modern best practices**
