// Required namespaces for HTTP communication and JSON serialization
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace OllamaTest;

/// <summary>
/// Represents a saved screen region for reuse
/// </summary>
public class SavedRegion
{
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime CreatedAt { get; set; }

    public SavedRegion() { }

    public SavedRegion(string name, Rectangle region)
    {
        Name = name;
        X = region.X;
        Y = region.Y;
        Width = region.Width;
        Height = region.Height;
        CreatedAt = DateTime.Now;
    }

    public Rectangle ToRectangle() => new Rectangle(X, Y, Width, Height);

    public override string ToString() => $"{Name} ({Width}×{Height} at {X},{Y})";
}

/// <summary>
/// Represents a detected blue button with its properties
/// </summary>
public class BlueButton
{
    public string Text { get; set; } = "";
    public int CenterX { get; set; }
    public int CenterY { get; set; }
    public string Size { get; set; } = "";
    public string Shape { get; set; } = "";
    
    // Screen coordinates (calculated from image coordinates + region offset)
    public int ScreenX { get; set; }
    public int ScreenY { get; set; }
    
    public override string ToString() => $"'{Text}' at Image({CenterX}, {CenterY}) -> Screen({ScreenX}, {ScreenY}) - {Size} {Shape}";
}

/// <summary>
/// Represents the main content analysis of an image
/// </summary>
public class MainContentAnalysis
{
    public string ContentType { get; set; } = "";
    public string Description { get; set; } = "";
    public Rectangle MainContentRegion { get; set; }
    public float ConfidenceScore { get; set; }
    public List<string> ImportantElements { get; set; } = new List<string>();
    
    public override string ToString() => $"{ContentType}: {Description} (Confidence: {ConfidenceScore:P0})";
}

/// <summary>
/// Test application to verify communication with Ollama's Qwen2.5VL model
/// This program tests three key functionalities:
/// 1. Basic connection to Ollama service
/// 2. Text-based communication with the model
/// 3. Image analysis capabilities for vision tasks
/// </summary>
class Program
{
    // Static HTTP client for making API requests to Ollama
    // Using static to avoid creating multiple instances
    private static readonly HttpClient httpClient = new HttpClient();

    // Ollama API endpoint for generating responses
    private const string OLLAMA_URL = "http://localhost:11434/api/generate";

    // List to store saved regions for reuse
    private static List<SavedRegion> savedRegions = new List<SavedRegion>();

    // File path for persisting saved regions
    private static readonly string SAVED_REGIONS_FILE = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OllamaTest", "saved_regions.json");

    // Windows API declarations for screen capture
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    // Constants for BitBlt operation
    private const uint SRCCOPY = 0x00CC0020;

    // Rectangle structure for Windows API
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// Loads saved regions from file
    /// </summary>
    static void LoadSavedRegions()
    {
        try
        {
            if (File.Exists(SAVED_REGIONS_FILE))
            {
                var json = File.ReadAllText(SAVED_REGIONS_FILE);
                savedRegions = JsonConvert.DeserializeObject<List<SavedRegion>>(json) ?? new List<SavedRegion>();
                Console.WriteLine($"📁 Loaded {savedRegions.Count} saved regions");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not load saved regions: {ex.Message}");
            savedRegions = new List<SavedRegion>();
        }
    }

    /// <summary>
    /// Saves regions to file
    /// </summary>
    static void SaveRegionsToFile()
    {
        try
        {
            var directory = Path.GetDirectoryName(SAVED_REGIONS_FILE);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(savedRegions, Formatting.Indented);
            File.WriteAllText(SAVED_REGIONS_FILE, json);
            Console.WriteLine($"💾 Saved {savedRegions.Count} regions to file");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not save regions: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a new region to the saved list
    /// </summary>
    static void AddSavedRegion(string name, Rectangle region)
    {
        // Remove any existing region with the same name
        savedRegions.RemoveAll(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        // Add the new region
        savedRegions.Add(new SavedRegion(name, region));
        SaveRegionsToFile();

        Console.WriteLine($"✅ Region '{name}' saved successfully!");
    }

    /// <summary>
    /// Analyzes the main content of an image to distinguish it from background/secondary elements
    /// </summary>
    static async Task<MainContentAnalysis> AnalyzeMainContent(string base64Image, string regionName)
    {
        try
        {
            Console.WriteLine("\n🎯 Analyzing main content...");

            var request = new
            {
                model = "qwen2.5vl:3b",
                prompt = $"Look at this {regionName} image. What is the main thing you see? Answer in one sentence.",
                images = new[] { base64Image },
                stream = false
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(OLLAMA_URL, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<OllamaResponse>(responseText);
                
                Console.WriteLine($"\n🎯 Main Content: {result?.Response?.Trim()}");

                return new MainContentAnalysis { Description = result?.Response?.Trim() ?? "No response" };
            }
            else
            {
                Console.WriteLine($"❌ Main content analysis failed: {response.StatusCode}");
                return new MainContentAnalysis { Description = "Analysis failed" };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Main content analysis error: {ex.Message}");
            return new MainContentAnalysis { Description = ex.Message };
        }
    }

    /// <summary>
    /// Parses the AI response to extract main content analysis information
    /// </summary>
    static MainContentAnalysis ParseMainContentAnalysis(string aiResponse)
    {
        var analysis = new MainContentAnalysis();
        var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            var lowerLine = trimmedLine.ToLower();
            
            if (lowerLine.StartsWith("type:"))
            {
                analysis.ContentType = ExtractValueAfterColon(trimmedLine);
            }
            else if (lowerLine.StartsWith("main content:"))
            {
                analysis.Description = ExtractValueAfterColon(trimmedLine);
            }
            else if (lowerLine.StartsWith("confidence:"))
            {
                var confidenceText = ExtractValueAfterColon(trimmedLine).ToLower();
                analysis.ConfidenceScore = confidenceText switch
                {
                    "high" => 0.9f,
                    "medium" => 0.6f,
                    "low" => 0.3f,
                    _ when confidenceText.Contains("high") => 0.9f,
                    _ when confidenceText.Contains("medium") => 0.6f,
                    _ when confidenceText.Contains("low") => 0.3f,
                    _ => 0.5f
                };
            }
        }
        
        // Set defaults if not found
        if (string.IsNullOrEmpty(analysis.ContentType))
            analysis.ContentType = "Unknown";
        if (string.IsNullOrEmpty(analysis.Description))
            analysis.Description = "Quick content summary";
        if (analysis.ConfidenceScore == 0)
            analysis.ConfidenceScore = 0.5f;
            
        return analysis;
    }

    /// <summary>
    /// Extracts value after colon from a line
    /// </summary>
    static string ExtractValueAfterColon(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < line.Length - 1)
        {
            return line.Substring(colonIndex + 1).Trim();
        }
        return "";
    }

    /// <summary>
    /// Parses region coordinates from text like "(x, y, width, height)"
    /// </summary>
    static Rectangle ParseRegionCoordinates(string line)
    {
        try
        {
            var coordMatch = System.Text.RegularExpressions.Regex.Match(line, @"\((\d+),\s*(\d+),\s*(\d+),\s*(\d+)\)");
            if (coordMatch.Success)
            {
                int x = int.Parse(coordMatch.Groups[1].Value);
                int y = int.Parse(coordMatch.Groups[2].Value);
                int width = int.Parse(coordMatch.Groups[3].Value);
                int height = int.Parse(coordMatch.Groups[4].Value);
                return new Rectangle(x, y, width, height);
            }
        }
        catch (Exception)
        {
            // Fall back to empty rectangle
        }
        return Rectangle.Empty;
    }

    /// <summary>
    /// Runs debugging analysis to understand why blue buttons weren't detected
    /// </summary>
    static async Task DebugUIAnalysis(string base64Image, string regionName)
    {
        try
        {
            Console.WriteLine("\n🔍 Running debug analysis...");

            // Create general UI analysis request
            var debugRequest = new
            {
                model = "qwen2.5vl:3b",
                prompt = $"Analyze this {regionName} image and describe ALL visible elements, colors, and buttons. Pay special attention to:\n1. What colors do you see in buttons or clickable elements?\n2. List ALL buttons and their colors (even if not blue)\n3. Are there any elements that might be blue but you're not sure?\n4. What shades and tones are present?\n\nBe very detailed about colors and UI elements.",
                images = new[] { base64Image },
                stream = false
            };

            var json = JsonConvert.SerializeObject(debugRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(OLLAMA_URL, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<OllamaResponse>(responseText);

                Console.WriteLine($"\n🐛 Debug Analysis Results:");
                Console.WriteLine($"═══════════════════════════");
                Console.WriteLine($"{result?.Response}");
                Console.WriteLine($"═══════════════════════════");

                // Suggest next steps
                Console.WriteLine("\n💡 Suggestions:");
                Console.WriteLine("   1. If buttons are described but not as 'blue', they might be a different shade");
                Console.WriteLine("   2. Try selecting a more specific region around the button");
                Console.WriteLine("   3. The button might be using a blue that the AI doesn't recognize as 'blue'");
                Console.WriteLine("   4. Check if the button changes color on hover/focus");
            }
            else
            {
                Console.WriteLine($"❌ Debug analysis failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Debug analysis error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tries alternative color detection strategies for buttons
    /// </summary>
    static async Task TryAlternativeColorDetection(string base64Image, string regionName)
    {
        try
        {
            Console.WriteLine("\n🎨 Trying alternative color detection...");

            // Create request with broader color search
            var altRequest = new
            {
                model = "qwen2.5vl:3b",
                prompt = $"Look at this {regionName} image and find ALL clickable buttons or elements that have ANY of these colors or characteristics:\n- Any shade of blue (light blue, dark blue, navy, cyan, teal, azure, etc.)\n- Blue-ish colors (blue-gray, blue-green, purple-blue)\n- Elements that might be blue but appear different due to lighting\n- Buttons with blue text, blue borders, or blue highlights\n- Elements that could be considered 'blue-themed'\n\nFor EACH element you find, describe:\n1. The exact text in quotes\n2. The position (x, y)\n3. What makes it blue or blue-ish\n\nDon't be strict - if there's ANY blue tint, report it!",
                images = new[] { base64Image },
                stream = false
            };

            var json = JsonConvert.SerializeObject(altRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(OLLAMA_URL, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<OllamaResponse>(responseText);

                Console.WriteLine($"\n🔍 Alternative Detection Results:");
                Console.WriteLine($"═══════════════════════════════════");
                Console.WriteLine($"{result?.Response}");
                Console.WriteLine($"═══════════════════════════════════");

                // Try to parse any buttons found
                var analysis = result?.Response?.ToLower() ?? "";
                if (analysis.Contains("button") || analysis.Contains("click") || analysis.Contains("element"))
                {
                    Console.WriteLine("\n🎯 Attempting to parse found elements:");
                    ParseBlueButtonInfo(result?.Response ?? "", 0, 0); // Alternative detection, assume no offset for now
                }
                else
                {
                    Console.WriteLine("\n❌ Still no clickable elements detected");
                    Console.WriteLine("💡 The region might not contain any buttons, or they may be very subtle");
                }
            }
            else
            {
                Console.WriteLine($"❌ Alternative detection failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Alternative detection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses AI response to extract multiple blue button information
    /// </summary>
    static void ParseBlueButtonInfo(string aiResponse, int regionX = 0, int regionY = 0)
    {
        try
        {
            var buttons = new List<BlueButton>();
            var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            BlueButton? currentButton = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                var lowerLine = trimmedLine.ToLower();

                // Check for button start markers
                if (lowerLine.StartsWith("button ") && lowerLine.Contains(":"))
                {
                    // Save previous button if it exists
                    if (currentButton != null && !string.IsNullOrEmpty(currentButton.Text))
                    {
                        buttons.Add(currentButton);
                    }
                    // Start new button
                    currentButton = new BlueButton();
                    continue;
                }

                // Parse text field
                if (lowerLine.StartsWith("text:"))
                {
                    if (currentButton == null) currentButton = new BlueButton();

                    // Extract text between quotes
                    var textMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"[""']([^""']+)[""']");
                    if (textMatch.Success)
                    {
                        currentButton.Text = textMatch.Groups[1].Value;
                    }
                    else
                    {
                        // Fallback: extract text after "Text:"
                        var colonIndex = trimmedLine.IndexOf(':');
                        if (colonIndex >= 0 && colonIndex < trimmedLine.Length - 1)
                        {
                            currentButton.Text = trimmedLine.Substring(colonIndex + 1).Trim().Trim('"', '\'');
                        }
                    }
                    continue;
                }

                // Parse position field
                if (lowerLine.StartsWith("position:"))
                {
                    if (currentButton == null) currentButton = new BlueButton();

                    // Extract coordinates (x, y) patterns
                    var coordMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"\((\d+),\s*(\d+)\)");
                    if (coordMatch.Success)
                    {
                        if (int.TryParse(coordMatch.Groups[1].Value, out int x) &&
                            int.TryParse(coordMatch.Groups[2].Value, out int y))
                        {
                            currentButton.CenterX = x;
                            currentButton.CenterY = y;
                            // Calculate screen coordinates
                            var screenPos = ConvertToScreenCoordinates(x, y, regionX, regionY);
                            currentButton.ScreenX = screenPos.X;
                            currentButton.ScreenY = screenPos.Y;
                        }
                    }
                    continue;
                }

                // Parse appearance field
                if (lowerLine.StartsWith("appearance:"))
                {
                    if (currentButton == null) currentButton = new BlueButton();

                    var colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex >= 0 && colonIndex < trimmedLine.Length - 1)
                    {
                        currentButton.Shape = trimmedLine.Substring(colonIndex + 1).Trim();
                    }
                    continue;
                }

                // Fallback parsing for unstructured responses
                if (currentButton != null)
                {
                    // Look for any text in quotes
                    if (string.IsNullOrEmpty(currentButton.Text) && (trimmedLine.Contains("\"") || trimmedLine.Contains("'")))
                    {
                        var textMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"[""']([^""']+)[""']");
                        if (textMatch.Success)
                        {
                            currentButton.Text = textMatch.Groups[1].Value;
                        }
                    }

                    // Look for coordinates
                    if (currentButton.CenterX == 0 && currentButton.CenterY == 0)
                    {
                        var coordMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"\((\d+),\s*(\d+)\)");
                        if (coordMatch.Success)
                        {
                            if (int.TryParse(coordMatch.Groups[1].Value, out int x) &&
                                int.TryParse(coordMatch.Groups[2].Value, out int y))
                            {
                                currentButton.CenterX = x;
                                currentButton.CenterY = y;
                                // Calculate screen coordinates
                                var screenPos = ConvertToScreenCoordinates(x, y, regionX, regionY);
                                currentButton.ScreenX = screenPos.X;
                                currentButton.ScreenY = screenPos.Y;
                            }
                        }
                    }
                }
            }

            // Add the last button if it exists
            if (currentButton != null && !string.IsNullOrEmpty(currentButton.Text))
            {
                buttons.Add(currentButton);
            }

            // Display parsed results
            if (buttons.Count > 0)
            {
                Console.WriteLine($"\n🎯 Parsed {buttons.Count} blue button(s):");
                for (int i = 0; i < buttons.Count; i++)
                {
                    var btn = buttons[i];
                    Console.WriteLine($"   {i + 1}. {btn}");
                }
                
                // Offer mouse interaction if we have valid screen coordinates
                if (buttons.Any(b => b.ScreenX > 0 && b.ScreenY > 0))
                {
                    OfferMouseInteraction(buttons);
                }
            }
            else
            {
                Console.WriteLine("\n⚠️ Could not parse specific button details from AI response");
                Console.WriteLine("💡 The AI might have found buttons but not in the expected format");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n⚠️ Error parsing blue button info: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Offers mouse interaction with detected buttons
    /// </summary>
    static void OfferMouseInteraction(List<BlueButton> buttons)
    {
        try
        {
            Console.WriteLine("\n🖱️  Mouse Interaction Options:");
            Console.WriteLine("1. Move mouse to a button (no click)");
            Console.WriteLine("2. Click on a button");
            Console.WriteLine("3. Skip mouse interaction");
            Console.Write("Choose option (1-3): ");
            
            var choice = Console.ReadLine()?.Trim();
            
            if (choice == "1" || choice == "2")
            {
                // Show available buttons with valid coordinates
                var validButtons = buttons.Where(b => b.ScreenX > 0 && b.ScreenY > 0).ToList();
                
                if (validButtons.Count == 0)
                {
                    Console.WriteLine("❌ No buttons with valid screen coordinates found.");
                    return;
                }
                
                Console.WriteLine("\nAvailable buttons:");
                for (int i = 0; i < validButtons.Count; i++)
                {
                    var btn = validButtons[i];
                    Console.WriteLine($"{i + 1}. {btn.Text} -> Screen({btn.ScreenX}, {btn.ScreenY})");
                }
                
                Console.Write($"Select button (1-{validButtons.Count}): ");
                if (int.TryParse(Console.ReadLine(), out int buttonIndex) && 
                    buttonIndex >= 1 && buttonIndex <= validButtons.Count)
                {
                    var selectedButton = validButtons[buttonIndex - 1];
                    
                    if (choice == "1")
                    {
                        // Just move mouse
                        Console.WriteLine($"🖱️  Moving mouse to '{selectedButton.Text}' at ({selectedButton.ScreenX}, {selectedButton.ScreenY})...");
                        MoveMouse(selectedButton.ScreenX, selectedButton.ScreenY);
                        Console.WriteLine("✅ Mouse moved! Check your screen.");
                    }
                    else if (choice == "2")
                    {
                        // Move and click
                        Console.WriteLine($"🖱️  Clicking on '{selectedButton.Text}' at ({selectedButton.ScreenX}, {selectedButton.ScreenY})...");
                        Console.WriteLine("⚠️  CLICKING IN 3 SECONDS! Switch to target window if needed...");
                        
                        for (int i = 3; i > 0; i--)
                        {
                            Console.WriteLine($"Clicking in {i}...");
                            Thread.Sleep(1000);
                        }
                        
                        ClickAt(selectedButton.ScreenX, selectedButton.ScreenY);
                        Console.WriteLine("✅ Button clicked!");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Invalid button selection.");
                }
            }
            else
            {
                Console.WriteLine("Mouse interaction skipped.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Mouse interaction error: {ex.Message}");
        }
    }

    /// <summary>
    /// Main entry point of the application
    /// Runs three sequential tests to validate Ollama communication
    /// </summary>
    [STAThread]
    static async Task Main(string[] args)
    {
        // Load saved regions at startup
        LoadSavedRegions();

        // Display application header
        Console.WriteLine("Ollama Qwen2.5VL Communication Test");
        Console.WriteLine("====================================");

        // Test 1: Verify Ollama service is running and accessible
        Console.WriteLine("\n1. Testing Ollama connection...");
        if (!await TestOllamaConnection())
        {
            Console.WriteLine("❌ Cannot connect to Ollama. Make sure it's running on localhost:11434");
            Console.WriteLine("To start Ollama: run 'ollama serve' in command prompt");
            return;
        }
        Console.WriteLine("✅ Ollama connection successful");

        // Test 2: Test basic text communication with the model
        Console.WriteLine("\n2. Testing simple text prompt...");
        await TestTextPrompt();

        // Test 3: Test image analysis capabilities (vision model)
        Console.WriteLine("\n3. Testing image analysis...");

        // Check if user wants to use auto screenshot
        if (args.Length == 0)
        {
            Console.WriteLine("Choose test mode:");
            Console.WriteLine("1. Full screen screenshot (entire desktop)");
            Console.WriteLine("2. 🖱️  Interactive region selection (click and drag)");
            if (savedRegions.Count > 0)
            {
                Console.WriteLine($"3. 📋 Use saved region ({savedRegions.Count} available)");
                Console.WriteLine("4. 🎯 Main content analysis (analyze image focus)");
                Console.WriteLine("5. Load image file");
                Console.WriteLine("6. Skip image test");
                Console.Write("Enter your choice (1-6): ");
            }
            else
            {
                Console.WriteLine("3. 🎯 Main content analysis (analyze image focus)");
                Console.WriteLine("4. Load image file");
                Console.WriteLine("5. Skip image test");
                Console.Write("Enter your choice (1-5): ");
            }

            try
            {
                var choice = Console.ReadLine();
                if (savedRegions.Count > 0)
                {
                    switch (choice)
                    {
                        case "1":
                            await TestAutoScreenshot();
                            break;
                        case "2":
                            await TestInteractiveRegionSelection();
                            break;
                        case "3":
                            await TestSavedRegionSelection();
                            break;
                        case "4":
                            await TestMainContentAnalysis();
                            break;
                        case "5":
                            await TestImageAnalysisWithFile(args);
                            break;
                        case "6":
                        default:
                            Console.WriteLine("Skipping image test.");
                            break;
                    }
                }
                else
                {
                    switch (choice)
                    {
                        case "1":
                            await TestAutoScreenshot();
                            break;
                        case "2":
                            await TestInteractiveRegionSelection();
                            break;
                        case "3":
                            await TestMainContentAnalysis();
                            break;
                        case "4":
                            await TestImageAnalysisWithFile(args);
                            break;
                        case "5":
                        default:
                            Console.WriteLine("Skipping image test.");
                            break;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Fallback to auto screenshot in non-interactive mode
                Console.WriteLine("Running auto screenshot test...");
                await TestAutoScreenshot();
            }
        }
        else
        {
            // Command line argument provided - use file mode
            await TestImageAnalysisWithFile(args);
        }

        // Wait for user input before closing (if running interactively)
        Console.WriteLine("\nTests completed. Press Enter to exit...");
        try
        {
            Console.ReadLine();
        }
        catch (InvalidOperationException)
        {
            // Console input not available (e.g., running in CI/automated environment)
            Console.WriteLine("Running in non-interactive mode, exiting...");
        }
    }

    /// <summary>
    /// Tests if Ollama service is running and accessible
    /// Makes a simple GET request to the /api/tags endpoint
    /// </summary>
    /// <returns>True if connection successful, false otherwise</returns>
    static async Task<bool> TestOllamaConnection()
    {
        try
        {
            // Try to connect to Ollama's tags endpoint to verify service is running
            var response = await httpClient.GetAsync("http://localhost:11434/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            // Display connection error details for troubleshooting
            Console.WriteLine($"Connection error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Tests basic text communication with the Qwen2.5VL model
    /// Sends a simple greeting prompt and displays the response
    /// </summary>
    static async Task TestTextPrompt()
    {
        try
        {
            // Create request object with model name, prompt, and disable streaming
            var request = new
            {
                model = "qwen2.5vl:3b",                                    // Specify the exact model name (no hyphen)
                prompt = "Hello! Can you respond with a simple greeting?", // Simple test prompt
                stream = false                                             // Get complete response, not streaming
            };

            // Convert request object to JSON string
            var json = JsonConvert.SerializeObject(request);

            // Create HTTP content with JSON payload and proper content type
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Console.WriteLine("Sending text request to Qwen2.5VL...");

            // Send POST request to Ollama API
            var response = await httpClient.PostAsync(OLLAMA_URL, content);
            var responseText = await response.Content.ReadAsStringAsync();

            // Check if request was successful
            if (response.IsSuccessStatusCode)
            {
                // Parse JSON response and display the model's answer
                var result = JsonConvert.DeserializeObject<OllamaResponse>(responseText);
                Console.WriteLine($"✅ Model response: {result?.Response}");
            }
            else
            {
                // Display error information if request failed
                Console.WriteLine($"❌ API Error: {response.StatusCode}");
                Console.WriteLine($"Response: {responseText}");
            }
        }
        catch (Exception ex)
        {
            // Handle any exceptions during the request process
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Captures a screenshot of the entire screen and analyzes it with Qwen2.5VL
    /// This demonstrates the auto screenshot functionality for blue button detection
    /// </summary>
    static async Task TestAutoScreenshot()
    {
        try
        {
            Console.WriteLine("Capturing screenshot in 3 seconds...");
            Console.WriteLine("Switch to the window/screen you want to analyze!");

            // Give user time to switch windows
            for (int i = 3; i > 0; i--)
            {
                Console.WriteLine($"Capturing in {i}...");
                await Task.Delay(1000);
            }

            Console.WriteLine("📸 Taking screenshot...");

            // Capture full screen
            using (Bitmap screenshot = CaptureScreen())
            {
                // Save screenshot to temp file (optional, for debugging)
                string tempPath = Path.Combine(Path.GetTempPath(), $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                screenshot.Save(tempPath, ImageFormat.Png);
                Console.WriteLine($"Screenshot saved to: {tempPath}");

                // Get screenshot info
                Console.WriteLine($"Screenshot size: {screenshot.Width}x{screenshot.Height}");
                Console.WriteLine($"File size: ~{new FileInfo(tempPath).Length / 1024}KB");

                // Convert to base64 for API
                using (var ms = new MemoryStream())
                {
                    screenshot.Save(ms, ImageFormat.Png);
                    string base64Image = Convert.ToBase64String(ms.ToArray());

                    // Enhanced request for multiple blue button detection
                    var request = new
                    {
                        model = "qwen2.5vl:3b",
                        prompt = "Find blue buttons or clickable blue elements.\n\nFor each blue button you find, answer:\nBUTTON 1:\nText: \"button text\"\nPosition: (x, y)\n\nBUTTON 2:\nText: \"button text\"\nPosition: (x, y)",
                        images = new[] { base64Image },
                        stream = false
                    };

                    var json = JsonConvert.SerializeObject(request);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    Console.WriteLine("🔍 Analyzing screenshot for blue buttons...");
                    Console.WriteLine("(Focusing specifically on blue button detection)");

                    var response = await httpClient.PostAsync(OLLAMA_URL, content);
                    var responseText = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<OllamaResponse>(responseText);

                        Console.WriteLine($"\n🔵 Blue Button Analysis Results:");
                        Console.WriteLine($"═══════════════════════════════════");
                        Console.WriteLine($"{result?.Response}");
                        Console.WriteLine($"═══════════════════════════════════");

                        // Parse blue button information
                        var analysis = result?.Response?.ToLower() ?? "";
                        if (analysis.Contains("no blue buttons detected") || !analysis.Contains("button"))
                        {
                            Console.WriteLine("\n❌ No blue buttons found in this screenshot");
                        }
                        else
                        {
                            Console.WriteLine("\n✅ Blue button(s) detected!");
                            ParseBlueButtonInfo(result?.Response ?? "", 0, 0); // Full screen, so no offset
                        }

                        Console.WriteLine($"\n📈 Performance: {result?.EvalCount} tokens in {result?.EvalDuration / 1000000}ms");
                        Console.WriteLine($"💾 Screenshot available at: {tempPath}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Analysis Error: {response.StatusCode}");
                        Console.WriteLine($"Response: {responseText}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Screenshot Error: {ex.Message}");
            if (ex.Message.Contains("GDI"))
            {
                Console.WriteLine("💡 This may be a graphics driver or permission issue.");
            }
        }
    }

    /// <summary>
    /// Captures the entire screen using Windows GDI API
    /// Returns a Bitmap object containing the screenshot
    /// </summary>
    /// <returns>Bitmap of the entire screen</returns>
    static Bitmap CaptureScreen()
    {
        // Get the device context of the entire screen
        IntPtr desktopWindow = GetDesktopWindow();
        IntPtr desktopDC = GetWindowDC(desktopWindow);

        // Get screen dimensions
        RECT desktopRect;
        GetWindowRect(desktopWindow, out desktopRect);

        int width = desktopRect.Right - desktopRect.Left;
        int height = desktopRect.Bottom - desktopRect.Top;

        // Create compatible device context and bitmap
        IntPtr compatibleDC = CreateCompatibleDC(desktopDC);
        IntPtr compatibleBitmap = CreateCompatibleBitmap(desktopDC, width, height);

        // Select the bitmap into the compatible DC
        IntPtr oldBitmap = SelectObject(compatibleDC, compatibleBitmap);

        // Copy the screen to our bitmap
        BitBlt(compatibleDC, 0, 0, width, height, desktopDC, 0, 0, SRCCOPY);

        // Create managed bitmap from the handle
        Bitmap screenshot = Image.FromHbitmap(compatibleBitmap);

        // Clean up resources
        SelectObject(compatibleDC, oldBitmap);
        DeleteDC(compatibleDC);
        DeleteObject(compatibleBitmap);
        ReleaseDC(desktopWindow, desktopDC);

        return screenshot;
    }

    /// <summary>
    /// Captures a specific region of the screen (for future use)
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="width">Width of region</param>
    /// <param name="height">Height of region</param>
    /// <returns>Bitmap of the specified region</returns>
    static Bitmap CaptureRegion(int x, int y, int width, int height)
    {
        using (var screenBitmap = new Bitmap(width, height))
        using (var graphics = Graphics.FromImage(screenBitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
            return new Bitmap(screenBitmap);
        }
    }

    /// <summary>
    /// Tests main content analysis functionality
    /// </summary>
    static async Task TestMainContentAnalysis()
    {
        try
        {
            Console.WriteLine("🎯 Main Content Analysis");
            Console.WriteLine("═══════════════════════════");
            Console.WriteLine("This feature analyzes images to identify the main content and distinguish it from background elements.");
            Console.WriteLine();
            Console.WriteLine("Choose content analysis mode:");
            Console.WriteLine("1. Analyze full screen screenshot");
            Console.WriteLine("2. Analyze selected region");
            Console.WriteLine("3. Analyze image file");
            Console.WriteLine("4. Cancel");
            Console.Write("Enter your choice (1-4): ");

            var choice = Console.ReadLine()?.Trim();
            
            switch (choice)
            {
                case "1":
                    await AnalyzeFullScreenMainContent();
                    break;
                case "2":
                    await AnalyzeRegionMainContent();
                    break;
                case "3":
                    await AnalyzeFileMainContent();
                    break;
                case "4":
                default:
                    Console.WriteLine("Main content analysis cancelled.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Main content analysis error: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyzes main content of full screen
    /// </summary>
    static async Task AnalyzeFullScreenMainContent()
    {
        try
        {
            Console.WriteLine("Capturing full screen in 3 seconds...");
            Console.WriteLine("Switch to the window you want to analyze!");

            for (int i = 3; i > 0; i--)
            {
                Console.WriteLine($"Capturing in {i}...");
                await Task.Delay(1000);
            }

            Console.WriteLine("📸 Taking screenshot...");

            using (Bitmap screenshot = CaptureScreen())
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"main_content_analysis_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                screenshot.Save(tempPath, ImageFormat.Png);

                using (var ms = new MemoryStream())
                {
                    screenshot.Save(ms, ImageFormat.Png);
                    string base64Image = Convert.ToBase64String(ms.ToArray());

                    var analysis = await AnalyzeMainContent(base64Image, "full screen");
                    
                    // Offer to focus on main content region
                    if (!analysis.MainContentRegion.IsEmpty)
                    {
                        Console.Write("\n🔍 Would you like to focus on the main content region for detailed analysis? (y/n): ");
                        var focusChoice = Console.ReadLine()?.ToLower();
                        if (focusChoice == "y" || focusChoice == "yes")
                        {
                            await CaptureAndAnalyzeMainContentRegion(analysis.MainContentRegion, "main content");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Full screen main content analysis error: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyzes main content of selected region
    /// </summary>
    static async Task AnalyzeRegionMainContent()
    {
        try
        {
            Console.WriteLine("🖱️  Select region for main content analysis...");
            
            Rectangle selectedRegion = Rectangle.Empty;
            bool regionSelected = false;

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var thread = new Thread(() =>
            {
                using (var selector = new RegionSelector())
                {
                    var result = selector.ShowDialog();
                    if (result == DialogResult.OK)
                    {
                        selectedRegion = selector.SelectedRegion;
                        regionSelected = true;
                    }
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (regionSelected && !selectedRegion.IsEmpty)
            {
                await CaptureAndAnalyzeMainContentRegion(selectedRegion, "selected region");
            }
            else
            {
                Console.WriteLine("❌ No region selected.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Region main content analysis error: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyzes main content of image file
    /// </summary>
    static async Task AnalyzeFileMainContent()
    {
        try
        {
            Console.WriteLine("Enter the path to an image file:");
            var imagePath = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                Console.WriteLine("❌ Invalid or non-existent file path.");
                return;
            }

            Console.WriteLine($"📁 Loading image: {imagePath}");
            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);

            await AnalyzeMainContent(base64Image, Path.GetFileName(imagePath));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ File main content analysis error: {ex.Message}");
        }
    }

    /// <summary>
    /// Captures and analyzes main content of a specific region
    /// </summary>
    static async Task CaptureAndAnalyzeMainContentRegion(Rectangle region, string regionName)
    {
        try
        {
            Console.WriteLine($"📸 Capturing {regionName} for main content analysis...");

            using (Bitmap regionScreenshot = CaptureRegion(region.X, region.Y, region.Width, region.Height))
            {
                string tempPath = Path.Combine(Path.GetTempPath(),
                    $"main_content_{regionName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                regionScreenshot.Save(tempPath, ImageFormat.Png);

                using (var ms = new MemoryStream())
                {
                    regionScreenshot.Save(ms, ImageFormat.Png);
                    string base64Image = Convert.ToBase64String(ms.ToArray());

                    await AnalyzeMainContent(base64Image, regionName);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Region main content analysis error: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows saved regions and allows user to select one
    /// </summary>
    static async Task TestSavedRegionSelection()
    {
        try
        {
            Console.WriteLine("📋 Saved Region Selection");
            Console.WriteLine("═══════════════════════════");
            Console.WriteLine($"You have {savedRegions.Count} saved region(s):");
            Console.WriteLine();

            for (int i = 0; i < savedRegions.Count; i++)
            {
                var region = savedRegions[i];
                Console.WriteLine($"{i + 1}. {region}");
                Console.WriteLine($"   Created: {region.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();
            }

            Console.Write($"Select a region (1-{savedRegions.Count}) or 0 to cancel: ");

            if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 1 && choice <= savedRegions.Count)
            {
                var selectedRegion = savedRegions[choice - 1];
                Console.WriteLine($"✅ Using saved region: {selectedRegion.Name}");
                Console.WriteLine($"📐 Coordinates: X={selectedRegion.X}, Y={selectedRegion.Y}");
                Console.WriteLine($"📏 Dimensions: {selectedRegion.Width}×{selectedRegion.Height} pixels");
                Console.WriteLine();

                await CaptureAndAnalyzeRegion(selectedRegion.X, selectedRegion.Y,
                    selectedRegion.Width, selectedRegion.Height, selectedRegion.Name);
            }
            else
            {
                Console.WriteLine("❌ Invalid selection or cancelled.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Saved region selection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Interactive region selection with click-and-drag mouse interface
    /// Provides visual overlay for easy region selection
    /// </summary>
    static async Task TestInteractiveRegionSelection()
    {
        try
        {
            Console.WriteLine("🖱️  Interactive Region Selection");
            Console.WriteLine("═══════════════════════════════════");
            Console.WriteLine("This will open a visual overlay where you can:");
            Console.WriteLine("• Click and drag to select any region");
            Console.WriteLine("• See real-time dimensions and coordinates");
            Console.WriteLine("• Visual feedback with selection rectangle");
            Console.WriteLine("• Press ESC to cancel");
            Console.WriteLine();
            Console.WriteLine("Press Enter when ready to start selection...");
            Console.ReadLine();

            Rectangle selectedRegion = Rectangle.Empty;
            bool regionSelected = false;

            // Run region selector on UI thread
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var thread = new Thread(() =>
            {
                using (var selector = new RegionSelector())
                {
                    var result = selector.ShowDialog();
                    if (result == DialogResult.OK)
                    {
                        selectedRegion = selector.SelectedRegion;
                        regionSelected = true;
                    }
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (regionSelected && !selectedRegion.IsEmpty)
            {
                Console.WriteLine($"✅ Region selected successfully!");
                Console.WriteLine($"📐 Coordinates: X={selectedRegion.X}, Y={selectedRegion.Y}");
                Console.WriteLine($"📏 Dimensions: {selectedRegion.Width}×{selectedRegion.Height} pixels");
                Console.WriteLine();

                // Ask if user wants to save this region
                Console.Write("💾 Would you like to save this region for future use? (y/n): ");
                var saveChoice = Console.ReadLine()?.ToLower();
                if (saveChoice == "y" || saveChoice == "yes")
                {
                    Console.Write("📝 Enter a name for this region: ");
                    var regionName = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(regionName))
                    {
                        AddSavedRegion(regionName, selectedRegion);
                    }
                    else
                    {
                        Console.WriteLine("❌ Invalid name. Region not saved.");
                    }
                }

                await CaptureAndAnalyzeRegion(selectedRegion.X, selectedRegion.Y,
                    selectedRegion.Width, selectedRegion.Height, "interactive");
            }
            else
            {
                Console.WriteLine("❌ No region selected or selection cancelled.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Interactive selection error: {ex.Message}");
            Console.WriteLine("💡 Make sure you're running on Windows with desktop access.");
        }
    }



    /// <summary>
    /// Captures and analyzes a specific screen region
    /// Optimized for UI element detection with smaller file sizes
    /// </summary>
    static async Task CaptureAndAnalyzeRegion(int x, int y, int width, int height, string regionName)
    {
        try
        {
            Console.WriteLine($"📸 Capturing {regionName} region...");

            // Capture the specified region
            using (Bitmap regionScreenshot = CaptureRegion(x, y, width, height))
            {
                // Save screenshot with descriptive name
                string tempPath = Path.Combine(Path.GetTempPath(),
                    $"region_{regionName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                regionScreenshot.Save(tempPath, ImageFormat.Png);

                // Show region info
                Console.WriteLine($"✅ Region captured successfully!");
                Console.WriteLine($"📏 Region size: {width}x{height} pixels");
                Console.WriteLine($"📍 Location: X={x}, Y={y}");

                var fileInfo = new FileInfo(tempPath);
                Console.WriteLine($"💾 File size: {fileInfo.Length / 1024}KB (much smaller than full screen!)");

                // Convert to base64 for API
                using (var ms = new MemoryStream())
                {
                    regionScreenshot.Save(ms, ImageFormat.Png);
                    string base64Image = Convert.ToBase64String(ms.ToArray());

                    // Enhanced prompt for multiple blue button detection
                    var request = new
                    {
                        model = "qwen2.5vl:3b",
                        prompt = "Find blue buttons or clickable blue elements.\n\nFor each blue button you find, answer:\nBUTTON 1:\nText: \"button text\"\nPosition: (x, y)\n\nBUTTON 2:\nText: \"button text\"\nPosition: (x, y)",
                        images = new[] { base64Image },
                        stream = false
                    };

                    var json = JsonConvert.SerializeObject(request);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    Console.WriteLine("🔍 Analyzing region for blue buttons only...");
                    Console.WriteLine("(Faster processing due to smaller image size!)");

                    var response = await httpClient.PostAsync(OLLAMA_URL, content);
                    var responseText = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<OllamaResponse>(responseText);

                        Console.WriteLine($"\n🔵 {regionName.ToUpper()} Blue Button Analysis:");
                        Console.WriteLine($"═══════════════════════════════════");
                        Console.WriteLine($"{result?.Response}");
                        Console.WriteLine($"═══════════════════════════════════");

                        // Parse blue button information
                        var analysis = result?.Response?.ToLower() ?? "";
                        if (analysis.Contains("no blue buttons detected") ||
                            (!analysis.Contains("button") && !analysis.Contains("blue")))
                        {
                            Console.WriteLine("\n❌ No blue buttons found in this region");
                            Console.WriteLine("   Try a different region or adjust the coordinates.");

                            // Offer debugging analysis
                            Console.Write("\n🔍 Run general UI analysis for debugging? (y/n): ");
                            try
                            {
                                var debugChoice = Console.ReadLine()?.ToLower();
                                if (debugChoice == "y" || debugChoice == "yes")
                                {
                                    await DebugUIAnalysis(base64Image, regionName);

                                    // Offer alternative detection
                                    Console.Write("\n🎨 Try alternative color detection (broader search)? (y/n): ");
                                    var altChoice = Console.ReadLine()?.ToLower();
                                    if (altChoice == "y" || altChoice == "yes")
                                    {
                                        await TryAlternativeColorDetection(base64Image, regionName);
                                    }
                                }
                            }
                            catch
                            {
                                // Skip debug in non-interactive mode
                            }
                        }
                        else
                        {
                            Console.WriteLine("\n✅ Blue button(s) detected in region!");
                            ParseBlueButtonInfo(result?.Response ?? "", x, y); // Pass region coordinates
                        }

                        Console.WriteLine($"\n⚡ Performance: {result?.EvalCount} tokens in {result?.EvalDuration / 1000000}ms");
                        Console.WriteLine($"💾 Region image: {tempPath}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Analysis Error: {response.StatusCode}");
                        Console.WriteLine($"Response: {responseText}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Region analysis error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets current cursor position for user reference
    /// </summary>
    static Point GetCursorPosition()
    {
        if (GetCursorPos(out Point point))
            return point;
        return new Point(0, 0);
    }
    
    /// <summary>
    /// Moves mouse to specified screen coordinates
    /// </summary>
    static bool MoveMouse(int x, int y)
    {
        return SetCursorPos(x, y);
    }
    
    /// <summary>
    /// Performs a left mouse click at current position
    /// </summary>
    static void ClickMouse()
    {
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        Thread.Sleep(50); // Small delay between down and up
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }
    
    /// <summary>
    /// Moves mouse to coordinates and clicks
    /// </summary>
    static void ClickAt(int x, int y)
    {
        MoveMouse(x, y);
        Thread.Sleep(100); // Small delay to ensure mouse moved
        ClickMouse();
    }
    
    /// <summary>
    /// Converts image coordinates to screen coordinates based on region offset
    /// </summary>
    static Point ConvertToScreenCoordinates(int imageX, int imageY, int regionX, int regionY)
    {
        return new Point(regionX + imageX, regionY + imageY);
    }

    /// <summary>
    /// Gets screen dimensions
    /// </summary>
    static Rectangle GetScreenBounds()
    {
        var desktopWindow = GetDesktopWindow();
        GetWindowRect(desktopWindow, out RECT rect);
        return new Rectangle(rect.Left, rect.Top,
            rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    // Additional Windows API for cursor position and mouse control
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);
    
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
    
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
    
    // Mouse event constants
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_MOVE = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Tests image analysis with a real image file
    /// Accepts image file path from command line arguments or prompts user
    /// </summary>
    /// <param name="args">Command line arguments</param>
    static async Task TestImageAnalysisWithFile(string[] args)
    {
        try
        {
            string imagePath = null;

            // Check if image path provided as command line argument
            if (args.Length > 0)
            {
                imagePath = args[0];
                Console.WriteLine($"Using image from command line: {imagePath}");
            }
            else
            {
                // Prompt user for image path
                Console.WriteLine("Enter the path to an image file (or press Enter to skip):");
                try
                {
                    imagePath = Console.ReadLine();
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("No image path provided. Skipping image test.");
                    Console.WriteLine("To test with an image, run: dotnet run \"path/to/your/image.jpg\"");
                    return;
                }
            }

            // Skip if no path provided
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                Console.WriteLine("No image path provided. Skipping image test.");
                Console.WriteLine("To test with an image, run: dotnet run \"path/to/your/image.jpg\"");
                return;
            }

            // Check if file exists
            if (!File.Exists(imagePath))
            {
                Console.WriteLine($"❌ Image file not found: {imagePath}");
                return;
            }

            // Read image file and convert to base64
            Console.WriteLine($"Reading image file: {imagePath}");
            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);

            // Get file info for display
            var fileInfo = new FileInfo(imagePath);
            Console.WriteLine($"Image size: {fileInfo.Length / 1024}KB");
            Console.WriteLine($"File type: {Path.GetExtension(imagePath).ToUpper()}");

            // Enhanced request for multiple blue button detection
            var request = new
            {
                model = "qwen2.5vl:3b",                                               // Vision model name (no hyphen)
                prompt = "Find blue buttons or clickable blue elements.\n\nFor each blue button you find, answer:\nBUTTON 1:\nText: \"button text\"\nPosition: (x, y)\n\nBUTTON 2:\nText: \"button text\"\nPosition: (x, y)",
                images = new[] { base64Image },                                        // Array of base64 encoded images
                stream = false                                                         // Complete response mode
            };

            // Serialize request to JSON
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Console.WriteLine("Sending image to Qwen2.5VL for blue button analysis...");
            Console.WriteLine("(This may take 10-30 seconds depending on image size and hardware)");

            // Send the vision analysis request
            var response = await httpClient.PostAsync(OLLAMA_URL, content);
            var responseText = await response.Content.ReadAsStringAsync();

            // Process the response
            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<OllamaResponse>(responseText);
                Console.WriteLine($"\n🔵 Blue Button Analysis Results:");
                Console.WriteLine($"═══════════════════════════");
                Console.WriteLine($"{result?.Response}");
                Console.WriteLine($"═══════════════════════════");

                // Parse blue button information
                var analysis = result?.Response?.ToLower() ?? "";
                if (analysis.Contains("no blue buttons detected") || !analysis.Contains("button"))
                {
                    Console.WriteLine("\n❌ No blue buttons found in this image");
                }
                else
                {
                    Console.WriteLine("\n✅ Blue button(s) detected!");
                    ParseBlueButtonInfo(result?.Response ?? "", 0, 0); // Image file, no screen offset
                }

                Console.WriteLine($"\n📊 Performance: Generated {result?.EvalCount} tokens in {result?.EvalDuration / 1000000}ms");
            }
            else
            {
                Console.WriteLine($"❌ API Error: {response.StatusCode}");
                Console.WriteLine($"Response: {responseText}");

                if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError &&
                    responseText.Contains("png") || responseText.Contains("format"))
                {
                    Console.WriteLine("💡 Tip: Try with a different image format (JPG, PNG, WebP)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (ex.Message.Contains("file") || ex.Message.Contains("path"))
            {
                Console.WriteLine("💡 Make sure the image file path is correct and accessible");
            }
        }
    }

    /// <summary>
    /// Creates a simple test image with blue color for testing vision capabilities
    /// Returns a base64 encoded PNG image that the model can analyze
    /// This simulates what we'll do with real screen captures later
    /// </summary>
    /// <returns>Base64 encoded PNG image data</returns>
    static string CreateTestBlueImage()
    {
        // Create a simple 16x16 blue PNG image for testing
        // This is a minimal but valid PNG with blue pixels
        byte[] pngBytes = {
            // PNG signature
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            
            // IHDR chunk
            0x00, 0x00, 0x00, 0x0D, // chunk length: 13 bytes
            0x49, 0x48, 0x44, 0x52, // chunk type: IHDR
            0x00, 0x00, 0x00, 0x10, // width: 16
            0x00, 0x00, 0x00, 0x10, // height: 16
            0x08,                   // bit depth: 8
            0x02,                   // color type: RGB
            0x00,                   // compression: deflate
            0x00,                   // filter: adaptive
            0x00,                   // interlace: none
            0x3A, 0xD6, 0xCA, 0x73, // CRC32
            
            // IDAT chunk with blue pixel data
            0x00, 0x00, 0x00, 0x38, // chunk length: 56 bytes
            0x49, 0x44, 0x41, 0x54, // chunk type: IDAT
            // Compressed data (deflate) for 16x16 blue pixels
            0x78, 0x9C, 0xED, 0xC1, 0x01, 0x01, 0x00, 0x00,
            0x00, 0x80, 0x90, 0xFE, 0x37, 0x10, 0x00, 0x00,
            0x01, 0xE0, 0x27, 0xDE, 0xFC, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x1D, 0x0A, 0x00, 0x01, // CRC32
            
            // IEND chunk
            0x00, 0x00, 0x00, 0x00, // chunk length: 0
            0x49, 0x45, 0x4E, 0x44, // chunk type: IEND
            0xAE, 0x42, 0x60, 0x82  // CRC32
        };

        // For now, let's use a different approach - create a simple text-based test
        // that asks the model to describe colors without an actual image
        // This will test the model's text capabilities instead
        return Convert.ToBase64String(pngBytes);
    }
}

/// <summary>
/// Data model for Ollama API responses
/// Contains all the fields that Ollama returns when generating text/vision responses
/// This helps us parse JSON responses into strongly-typed objects
/// </summary>
public class OllamaResponse
{
    // The model name that generated the response
    [JsonProperty("model")]
    public string? Model { get; set; }

    // The actual text response from the model
    [JsonProperty("response")]
    public string? Response { get; set; }

    // Whether the response generation is complete
    [JsonProperty("done")]
    public bool Done { get; set; }

    // Context tokens for conversation continuity (optional)
    [JsonProperty("context")]
    public int[]? Context { get; set; }

    // Performance metrics - total time taken for the request
    [JsonProperty("total_duration")]
    public long TotalDuration { get; set; }

    // Time taken to load the model
    [JsonProperty("load_duration")]
    public long LoadDuration { get; set; }

    // Number of tokens in the prompt
    [JsonProperty("prompt_eval_count")]
    public int PromptEvalCount { get; set; }

    // Time taken to evaluate the prompt
    [JsonProperty("prompt_eval_duration")]
    public long PromptEvalDuration { get; set; }

    // Number of tokens generated in the response
    [JsonProperty("eval_count")]
    public int EvalCount { get; set; }

    // Time taken to generate the response
    [JsonProperty("eval_duration")]
    public long EvalDuration { get; set; }
}
