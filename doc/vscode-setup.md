# VS Code Setup and Usage Guide

## VS Code Configuration Files Created

The following VS Code configuration files have been created in `.vscode/` directory:

### 1. `launch.json` - Debug Configurations
- **Debug OllamaTest**: Full debugging with breakpoints and step-through
- **Run OllamaTest (No Debug)**: Fast execution without debugging overhead
- **Attach to Process**: Attach debugger to running process

### 2. `tasks.json` - Build and Run Tasks
- **build-ollamatest**: Compile the application
- **run-ollamatest**: Execute the application
- **clean-ollamatest**: Clean build artifacts
- **restore-ollamatest**: Restore NuGet packages
- **watch-ollamatest**: Auto-rebuild and run on file changes

### 3. `settings.json` - Workspace Settings
- C# IntelliSense and formatting configuration
- Auto-format on save
- File exclusions (bin, obj folders)
- Debug console settings

### 4. `extensions.json` - Recommended Extensions
- C# extension for syntax highlighting and IntelliSense
- .NET runtime support
- Additional C# development tools

## How to Use

### Method 1: Using Debug Panel (F5)
1. Open VS Code in the project root directory
2. Press `Ctrl+Shift+D` to open Debug panel
3. Select configuration from dropdown:
   - **"Debug OllamaTest"** - For debugging with breakpoints
   - **"Run OllamaTest (No Debug)"** - For quick execution
4. Press `F5` or click "Start Debugging"

### Method 2: Using Command Palette
1. Press `Ctrl+Shift+P` to open Command Palette
2. Type "Tasks: Run Task"
3. Select from available tasks:
   - `build-ollamatest`
   - `run-ollamatest`
   - `clean-ollamatest`
   - `restore-ollamatest`
   - `watch-ollamatest`

### Method 3: Using Keyboard Shortcuts
- `F5` - Start debugging (uses selected configuration)
- `Ctrl+F5` - Run without debugging
- `Shift+F5` - Stop debugging
- `F9` - Toggle breakpoint
- `F10` - Step over
- `F11` - Step into
- `Shift+F11` - Step out

## Debugging Features

### Setting Breakpoints
1. Click in the left margin next to line numbers
2. Red dots indicate active breakpoints
3. Breakpoints pause execution for inspection

### Debug Console
- View variable values
- Execute expressions
- See application output
- Exception details

### Watch Variables
1. Right-click on variables in code
2. Select "Add to Watch"
3. Monitor values in Debug panel

### Call Stack
- View function call hierarchy
- Navigate between stack frames
- Inspect local variables at each level

## Terminal Integration

The configuration uses VS Code's integrated terminal:
- Application output appears in terminal
- Interactive prompts work correctly
- Color output and emojis display properly

## Auto-Watch Mode

Use the "watch-ollamatest" task for development:
1. Open Command Palette (`Ctrl+Shift+P`)
2. Type "Tasks: Run Task"
3. Select "watch-ollamatest"
4. Application rebuilds and restarts on file changes

## Troubleshooting

### Extension Not Working
1. Install recommended extensions:
   - Press `Ctrl+Shift+X`
   - Search for "C#" and install Microsoft's C# extension
   - Restart VS Code

### Debugging Not Starting
1. Ensure project builds successfully
2. Check `launch.json` paths are correct
3. Try "Reload Window" from Command Palette

### IntelliSense Issues
1. Open Command Palette
2. Run "OmniSharp: Restart OmniSharp"
3. Or run "Developer: Reload Window"

### Build Errors
1. Run restore task first: `restore-ollamatest`
2. Check .NET SDK is installed
3. Verify project file paths

## Configuration Customization

### Modify Debug Settings
Edit `.vscode/launch.json`:
- Change console type: `"console": "externalTerminal"`
- Add environment variables in `"env"` section
- Modify working directory with `"cwd"`

### Add Custom Tasks
Edit `.vscode/tasks.json`:
- Add new task configurations
- Create compound tasks
- Set up pre/post build actions

### Workspace Settings
Edit `.vscode/settings.json`:
- Adjust formatting preferences
- Configure file associations
- Set up custom keybindings

## Quick Start

1. **Open VS Code** in project directory: `code .`
2. **Install extensions** when prompted
3. **Press F5** to debug or **Ctrl+F5** to run
4. **Set breakpoints** by clicking line numbers
5. **Use integrated terminal** for interaction

The configuration is optimized for C# development with Ollama integration testing.