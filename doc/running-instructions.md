# Ollama Test Application - Running Instructions

## Prerequisites

Before running the test application, ensure you have:

1. **Ollama installed and running**
   - Download Ollama from: https://ollama.ai/
   - Install and start the Ollama service

2. **Qwen2.5VL model downloaded**
   - Open command prompt/PowerShell
   - Run: `ollama pull qwen2.5-vl:3b`
   - Wait for the model to download (this may take several minutes)

3. **.NET SDK installed**
   - The project is built with .NET 9.0
   - Download from: https://dotnet.microsoft.com/download

## Step-by-Step Running Instructions

### Step 1: Start Ollama Service
```bash
# Open Command Prompt or PowerShell
ollama serve
```
- Keep this window open while testing
- You should see: "Ollama is running on localhost:11434"

### Step 2: Verify Model is Available
```bash
# In another command prompt window
ollama list
```
- Confirm "qwen2.5-vl:3b" appears in the list

### Step 3: Navigate to Test Application
```bash
# Navigate to the test application directory
cd D:\AiLab\Qwen25vl\OllamaTest
```

### Step 4: Run the Test Application
```bash
# Run the application
dotnet run
```

## Expected Output

The application will perform three tests:

### Test 1: Connection Test
```
1. Testing Ollama connection...
✅ Ollama connection successful
```
- **Success**: Ollama is running and accessible
- **Failure**: Check if Ollama service is running on port 11434

### Test 2: Text Communication Test
```
2. Testing simple text prompt...
Sending text request to Qwen2.5VL...
✅ Model response: [Greeting message from the AI model]
```
- **Success**: Basic text communication works
- **Failure**: Model may not be loaded or API request failed

### Test 3: Image Analysis Test
```
3. Testing image analysis...
Sending image analysis request...
(This may take longer as the model processes the image)
✅ Image analysis: [Description of the blue test image]
🔵 Model successfully detected blue color!
```
- **Success**: Vision capabilities are working
- **Failure**: Model may not support vision or image processing failed

## Troubleshooting

### Error: "Cannot connect to Ollama"
**Solution:**
1. Ensure Ollama is running: `ollama serve`
2. Check if port 11434 is available
3. Try restarting Ollama service

### Error: "Model not found"
**Solution:**
1. Download the model: `ollama pull qwen2.5-vl:3b`
2. Verify model name matches exactly: `ollama list`
3. Check for typos in model name

### Error: "API Error: 404"
**Solution:**
1. Verify model is pulled correctly
2. Check Ollama service is running
3. Try: `ollama run qwen2.5-vl:3b` to test model manually

### Slow Response Times
**Expected Behavior:**
- First request may be slower (model loading)
- Image analysis takes longer than text requests
- Subsequent requests should be faster

### Memory Issues
**Solution:**
1. Ensure sufficient RAM (at least 8GB recommended)
2. Close other applications
3. Try smaller model variants if available

## Performance Expectations

- **Connection Test**: < 1 second
- **Text Request**: 2-10 seconds (depending on hardware)
- **Image Analysis**: 5-30 seconds (depending on hardware)

## Next Steps

Once all tests pass successfully:
1. ✅ Ollama communication verified
2. ✅ Text generation working
3. ✅ Vision capabilities confirmed
4. ✅ Blue color detection working

You're ready to proceed with the full Windows Forms application for screen capture and blue button detection!

## Manual Testing Commands

If you want to test manually outside the application:

```bash
# Test text generation
curl -X POST http://localhost:11434/api/generate -d '{
  "model": "qwen2.5-vl:3b",
  "prompt": "Hello, how are you?",
  "stream": false
}'
```

## Log Files

The application outputs to console only. To save logs:
```bash
dotnet run > test_output.log 2>&1
```