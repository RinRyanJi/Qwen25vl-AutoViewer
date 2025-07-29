# Blue Button Detection and Auto-Click Application Plan

## Project Overview
A Windows C# Forms application that captures screen regions, uses Ollama's Qwen2.5VL:3b model to detect blue buttons, and automatically moves the mouse to click them.

## Architecture Components

### 1. Windows Forms Application Structure
- **MainForm**: Primary UI with screen capture controls
- **ScreenCaptureService**: Handles partial screen capture functionality
- **OllamaService**: Manages communication with Ollama API
- **MouseAutomationService**: Controls mouse movement and clicking
- **Models**: Data structures for API responses and coordinates

### 2. Core Functionality

#### Screen Capture Module
- **Technology**: `Graphics.CopyFromScreen()` method
- **Features**:
  - Region selection UI (drag to select area)
  - Real-time preview of selected region
  - Save captured region as image (PNG/JPEG)
  - Coordinate tracking for absolute positioning

#### Ollama Integration
- **API Endpoint**: `http://localhost:11434/api/generate`
- **Model**: `qwen2.5vl:3b`
- **Request Format**:
  ```json
  {
    "model": "qwen2.5vl:3b",
    "prompt": "Find the location of any blue buttons in this image. Return the coordinates as x,y pixel positions relative to the image.",
    "images": ["base64_encoded_image"],
    "stream": false
  }
  ```

#### Mouse Automation
- **Technology**: Windows API (`user32.dll`)
- **Functions**:
  - `SetCursorPos()` for mouse positioning
  - `mouse_event()` for click simulation
  - Screen coordinate translation

### 3. User Interface Design

#### Main Window Components
- **Capture Button**: Initiates screen region selection
- **Preview Panel**: Shows captured image
- **Status Label**: Displays current operation status
- **Results TextBox**: Shows Qwen2.5VL response
- **Auto-Click Checkbox**: Enable/disable automatic clicking
- **Refresh Button**: Retry detection on same image

#### Region Selection Overlay
- **Transparent overlay window** covering entire screen
- **Rubber band selection** with visual feedback
- **Coordinate display** showing selection bounds
- **Confirm/Cancel buttons**

### 4. Implementation Steps

#### Phase 1: Basic Structure
1. Create Windows Forms project
2. Design main UI layout
3. Implement basic screen capture
4. Test image saving functionality

#### Phase 2: Ollama Integration
1. Create HTTP client for Ollama API
2. Implement image-to-base64 conversion
3. Parse JSON responses for coordinates
4. Handle API errors and timeouts

#### Phase 3: Mouse Automation
1. Import Windows API functions
2. Convert relative coordinates to absolute
3. Implement smooth mouse movement
4. Add click simulation with configurable delay

#### Phase 4: Enhanced UI
1. Create region selection overlay
2. Add real-time preview
3. Implement drag-to-select functionality
4. Add keyboard shortcuts (Esc to cancel)

### 5. Required NuGet Packages
- `Newtonsoft.Json` - JSON serialization
- `System.Drawing.Common` - Image manipulation
- `System.Net.Http` - HTTP client for API calls

### 6. Configuration Settings
- **Ollama Server URL**: Default `http://localhost:11434`
- **Model Name**: `qwen2.5vl:3b`
- **Click Delay**: Configurable delay before auto-click
- **Capture Quality**: JPEG quality for API transmission
- **Retry Attempts**: Number of API retry attempts

### 7. Error Handling
- **Ollama Connection**: Verify service availability
- **Model Loading**: Check if Qwen2.5VL model is available
- **Screen Capture**: Handle multi-monitor scenarios
- **API Responses**: Parse and validate coordinate data
- **Mouse Automation**: Handle permission issues

### 8. Security Considerations
- **Local API Only**: No external network requests
- **User Confirmation**: Optional confirmation before mouse automation
- **Access Control**: Request appropriate Windows permissions
- **Data Privacy**: No image data stored permanently

### 9. Testing Strategy
- **Unit Tests**: Individual service components
- **Integration Tests**: Full workflow testing
- **Manual Testing**: Various screen scenarios and button types
- **Performance Tests**: API response times and accuracy

### 10. Future Enhancements
- **Multi-button Detection**: Handle multiple blue buttons
- **Button Type Classification**: Distinguish button types
- **Hotkey Support**: Global keyboard shortcuts
- **Configuration UI**: Settings management interface
- **Logging System**: Debug and usage tracking

## Development Timeline
- **Week 1**: Basic structure and screen capture
- **Week 2**: Ollama integration and API testing
- **Week 3**: Mouse automation and UI polish
- **Week 4**: Testing, debugging, and documentation

## Prerequisites
- Visual Studio 2019/2022
- .NET Framework 4.8 or .NET 6+
- Ollama installed with Qwen2.5VL:3b model
- Windows 10/11 (for API compatibility)