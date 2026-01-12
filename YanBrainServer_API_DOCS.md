# YanBrain API Documentation

Base URL: `http://localhost:8080`

## Authentication

All endpoints require Firebase token:
```
Authorization: Bearer <firebase-token>
```

---

## Production Endpoints

### Health Check
```
GET /health
```
**Response:**
```json
{
  "status": "ok",
  "service": "YanBrain API Client",
  "timestamp": "2025-01-12T00:00:00.000Z",
  "version": "1.0.0"
}
```

---

### Document Convert & Embed
```
POST /api/documents/convert-and-embed
```
**Cost:** 1 credit per file

**Description:** Converts documents (PDF, DOCX, etc.) to text and generates embeddings. User stores results locally.

**Request:**
```json
{
  "files": [
    {
      "filename": "document.pdf",
      "contentBase64": "base64-encoded-file"
    }
  ]
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "files": [
      {
        "fileId": "file_123",
        "filename": "document.pdf",
        "text": "Full document text...",
        "embedding": [0.1, 0.2, ...],
        "dimensions": 1536,
        "characterCount": 5420
      }
    ],
    "totalFiles": 1,
    "totalCreditsCharged": 1
  }
}
```

**Supported Formats:** PDF, DOCX, DOC, RTF, XLSX, XLS, PPTX, PPT, ODT, ODP, ODS, TXT, MD

---

### YanAvatar Query
```
POST /api/yanavatar
```
**Cost:** 5 credits per request (fixed)

**Description:** AI voice assistant that answers questions based on provided documents. User performs vector search locally and sends only relevant documents.

**Request:**
```json
{
  "userPrompt": "What products do we have?",
  "relevantDocuments": [
    {
      "filename": "products.md",
      "text": "Full document text..."
    }
  ],
  "systemPrompt": "Optional custom instructions",
  "voiceId": "optional-voice-id"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "audio": "base64-encoded-mp3",
    "textResponse": "Based on the documents...",
    "documentsUsed": 1
  }
}
```

---

### Image Generation
```
POST /api/image
```
**Cost:** 10 credits

**Request:**
```json
{
  "prompt": "A beautiful sunset",
  "imageBase64": "base64-encoded-image"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "imageUrl": "https://..."
  }
}
```

---

## Error Response
```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "Error description",
    "statusCode": 400
  }
}
```

### Common Error Codes
- `UNAUTHORIZED` (401) - Invalid/missing token
- `INSUFFICIENT_CREDITS` (402) - Not enough credits
- `VALIDATION_ERROR` (400) - Invalid input
- `OPENAI_ERROR` (503) - OpenAI failed
- `ELEVENLABS_ERROR` (503) - ElevenLabs failed
- `RUNWARE_ERROR` (503) - Runware failed

---

## Credit Costs

| Endpoint | Cost |
|----------|------|
| Document Convert & Embed | 1 credit per file |
| YanAvatar Query | 5 credits (fixed) |
| Image Generation | 10 credits |

---

## YanAvatar Workflow

**Client Side:**
1. User uploads 200 documents
2. Call `/api/documents/convert-and-embed` (costs 200 credits)
3. Store all texts + embeddings locally
4. When user asks question:
    - Convert question to embedding locally
    - Search local embeddings for top 5 relevant docs
    - Send only those 5 docs to `/api/yanavatar`

**Server Side:**
1. Receives question + 5 relevant documents
2. LLM generates answer using document context
3. TTS converts answer to audio
4. Returns audio MP3 (costs 5 credits)

**Benefits:**
- Low server costs (only process relevant docs)
- Fast responses (small request size)
- User privacy (documents stored locally)