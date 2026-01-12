# YanBrain API Documentation

Base URL: `http://localhost:8080`

## Authentication

All endpoints require Firebase token:
```
Authorization: Bearer <firebase-token>
```

---

## Endpoints

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

### LLM (Chat)
```
POST /api/llm
```
**Cost:** 1 credit

**Request:**
```json
{
  "message": "What is 2+2?"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "response": "2+2 equals 4."
  }
}
```

---

### Text-to-Speech
```
POST /api/tts
```
**Cost:** 2 credits

**Request:**
```json
{
  "text": "Hello world",
  "voiceId": "optional-voice-id"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "audio": "base64-encoded-audio"
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

### Embeddings
```
POST /api/embeddings
```
**Cost:** 5 credits minimum + 1 credit per 1K chars

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
        "embedding": [0.1, 0.2, ...],
        "dimensions": 1536
      }
    ]
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

| Endpoint | Cost                           |
|----------|--------------------------------|
| LLM | 1 credit                       |
| TTS | 2 credits                      |
| Image | 10 credits                     |
| Embeddings | 5 credits min + 1 per 1K chars |

---

## Supported File Formats (Embeddings)

PDF, DOCX, DOC, RTF, XLSX, XLS, PPTX, PPT, ODT, ODP, ODS, TXT, MD
