using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SharpDX.XInput;
using SkiaSharp;

namespace ImaginaryConsoleEmulator
{

    public enum GPUInstruction
    {
        CLEAR_SCREEN,
        DRAW_RECTANGLE,
        DRAW_CIRCLE,
        SET_PIXEL,
        LOAD_TEXTURE
    }

    public class ShaderProgram
{
    public int ProgramId { get; private set; }

    public ShaderProgram(string vertexShaderSource, string fragmentShaderSource)
    {
        int vertexShader = CompileShader(OpenGL.GL_VERTEX_SHADER, vertexShaderSource);
        int fragmentShader = CompileShader(OpenGL.GL_FRAGMENT_SHADER, fragmentShaderSource);
        ProgramId = LinkProgram(vertexShader, fragmentShader);
    }

    private int CompileShader(int type, string source)
    {
        int shader = OpenGL.glCreateShader(type);
        OpenGL.glShaderSource(shader, source);
        OpenGL.glCompileShader(shader);
        CheckShaderCompilation(shader);
        return shader;
    }

    private void CheckShaderCompilation(int shader)
    {
        int success;
        OpenGL.glGetShaderiv(shader, OpenGL.GL_COMPILE_STATUS, out success);
        if (success == 0)
        {
            string infoLog = GetShaderInfoLog(shader);
            Console.WriteLine($"Shader Compilation Failed: {infoLog}");
            throw new Exception($"Shader Compilation Failed: {infoLog}");
        }
    }

    private string GetShaderInfoLog(int shader)
    {
        int maxLength;
        OpenGL.glGetShaderiv(shader, OpenGL.GL_INFO_LOG_LENGTH, out maxLength);
        byte[] infoLog = new byte[maxLength];
        OpenGL.glGetShaderInfoLog(shader, maxLength, out maxLength, infoLog);
        return System.Text.Encoding.UTF8.GetString(infoLog, 0, maxLength);
    }

    private int LinkProgram(int vertexShader, int fragmentShader)
    {
        int program = OpenGL.glCreateProgram();
        OpenGL.glAttachShader(program, vertexShader);
        OpenGL.glAttachShader(program, fragmentShader);
        OpenGL.glLinkProgram(program);
        CheckProgramLinking(program);
        return program;
    }

    private void CheckProgramLinking(int program)
    {
        int success;
        OpenGL.glGetProgramiv(program, OpenGL.GL_LINK_STATUS, out success);
        if (success == 0)
        {
            string infoLog = GetProgramInfoLog(program);
            Console.WriteLine($"Program Linking Failed: {infoLog}");
            throw new Exception($"Program Linking Failed: {infoLog}");
        }
    }

    private string GetProgramInfoLog(int program)
    {
        int maxLength;
        OpenGL.glGetProgramiv(program, OpenGL.GL_INFO_LOG_LENGTH, out maxLength);
        byte[] infoLog = new byte[maxLength];
        OpenGL.glGetProgramInfoLog(program, maxLength, out maxLength, infoLog);
        return System.Text.Encoding.UTF8.GetString(infoLog, 0, maxLength);
    }

    public void Use()
    {
        OpenGL.glUseProgram(ProgramId);
    }
}

public class MatrixHelper
{
    public static float[] CreateProjectionMatrix(float fov, float aspectRatio, float near, float far)
    {
        float tanHalfFovy = (float)Math.Tan(fov / 2.0);
        return new float[]
        {
            1.0f / (aspectRatio * tanHalfFovy), 0, 0, 0,
            0, 1.0f / tanHalfFovy, 0, 0,
            0, 0, -(far + near) / (far - near), -1,
            0, 0, -(2.0f * far * near) / (far - near), 0
        };
    }

    public static float[] CreateViewMatrix(Vector3 cameraPosition, Vector3 cameraTarget, Vector3 up)
    {
        Vector3 zAxis = (cameraPosition - cameraTarget).Normalized();
        Vector3 xAxis = Vector3.Cross(up, zAxis).Normalized();
        Vector3 yAxis = Vector3.Cross(zAxis, xAxis);

        return new float[]
        {
            xAxis.X, yAxis.X, zAxis.X, 0,
            xAxis.Y, yAxis.Y, zAxis.Y, 0,
            xAxis.Z, yAxis.Z, zAxis.Z, 0,
            -Vector3.Dot(xAxis, cameraPosition), -Vector3.Dot(yAxis, cameraPosition), -Vector3.Dot(zAxis, cameraPosition), 1
        };
    }

    public static float[] CreateModelMatrix(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        // Combine translation, rotation, and scaling into a model matrix
        var translationMatrix = Matrix4x4.CreateTranslation(position);
        var rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
        var scaleMatrix = Matrix4x4.CreateScale(scale);

        return (translationMatrix * rotationMatrix * scaleMatrix).ToArray();
    }
}

public class Texture
{
    public int TextureId { get; private set; }

    public Texture(string filePath)
    {
        TextureId = LoadTexture(filePath);
    }

    private int LoadTexture(string filePath)
    {
        // Load texture from file
        using (var bitmap = new Bitmap(filePath))
        {
            var textureData = BitmapToBytes(bitmap);
            int textureId = GenerateTexture(textureData);
            return textureId;
        }
    }

    private byte[] BitmapToBytes(Bitmap bitmap)
    {
        using (var ms = new MemoryStream())
        {
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    private int GenerateTexture(byte[] textureData)
    {
        // Upload the texture data to the GPU and return the texture ID
        int textureId = OpenGL.glGenTextures(1);
        OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, textureId);
        OpenGL.glTexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, width, height, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, textureData);
        OpenGL.glGenerateMipmap(OpenGL.GL_TEXTURE_2D);
        return textureId;
    }

    public void Bind()
    {
        OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, TextureId);
    }
}




    public class GPUInterpreter
{
    private GPU _gpu;
    private SKCanvas _canvas;

    public GPUInterpreter(GPU gpu)
    {
        _gpu = gpu;
        _canvas = _gpu.GetCanvas();
    }

    // Interpret and execute GPU instruction
    public void ExecuteInstruction(GPUInstruction instruction, params object[] parameters)
    {
        switch (instruction)
        {
            case GPUInstruction.CLEAR_SCREEN:
                ExecuteClearScreen();
                break;
            case GPUInstruction.DRAW_RECTANGLE:
                ExecuteDrawRectangle((int)parameters[0], (int)parameters[1], (int)parameters[2], (int)parameters[3], (SKColor)parameters[4]);
                break;
            case GPUInstruction.DRAW_CIRCLE:
                ExecuteDrawCircle((int)parameters[0], (int)parameters[1], (int)parameters[2], (SKColor)parameters[3]);
                break;
            case GPUInstruction.SET_PIXEL:
                ExecuteSetPixel((int)parameters[0], (int)parameters[1], (SKColor)parameters[2]);
                break;
            case GPUInstruction.LOAD_TEXTURE:
                ExecuteLoadTexture((string)parameters[0]);
                break;
            default:
                throw new InvalidOperationException("Unsupported GPU Instruction");
        }
    }

    private void ExecuteClearScreen()
    {
        _canvas.Clear(SKColors.Black);
    }

    private void ExecuteDrawRectangle(int x, int y, int width, int height, SKColor color)
    {
        using (SKPaint paint = new SKPaint { Color = color })
        {
            _canvas.DrawRect(x, y, width, height, paint);
        }
    }

    private void ExecuteDrawCircle(int x, int y, int radius, SKColor color)
    {
        using (SKPaint paint = new SKPaint { Color = color })
        {
            _canvas.DrawCircle(x, y, radius, paint);
        }
    }

    private void ExecuteSetPixel(int x, int y, SKColor color)
    {
        _canvas.DrawPoint(x, y, new SKPaint { Color = color });
    }

    private void ExecuteLoadTexture(string texturePath)
    {
        SKBitmap texture = SKBitmap.Decode(texturePath);
        if (texture != null)
        {
            _canvas.DrawBitmap(texture, new SKRect(0, 0, texture.Width, texture.Height));
        }
        else
        {
            Console.WriteLine("Failed to load texture.");
        }
    }
}


    // RAM Emulation Class: Stores memory for the system
    public class RAM
    {
        private byte[] _memory;

        public RAM(int size)
        {
            _memory = new byte[size];
        }

        public byte ReadByte(int address)
        {
            if (address < 0 || address >= _memory.Length)
                throw new InvalidOperationException("Memory access out of bounds");
            return _memory[address];
        }

        public void WriteByte(int address, byte value)
        {
            if (address < 0 || address >= _memory.Length)
                throw new InvalidOperationException("Memory access out of bounds");
            _memory[address] = value;
        }

        public byte[] ReadBlock(int startAddress, int size)
        {
            if (startAddress + size > _memory.Length)
                throw new InvalidOperationException("Memory access out of bounds");
            return _memory.Skip(startAddress).Take(size).ToArray();
        }

        public void WriteBlock(int startAddress, byte[] data)
        {
            if (startAddress + data.Length > _memory.Length)
                throw new InvalidOperationException("Memory access out of bounds");
            Array.Copy(data, 0, _memory, startAddress, data.Length);
        }
    }

    // Keys Class: Load and manage key files for decryption (emulating Switch's keys)
    public class Keys
    {
        public byte[] Key1 { get; private set; }
        public byte[] Key2 { get; private set; }

        public Keys(string keyFilePath)
        {
            // Load the keys from a file
            if (!File.Exists(keyFilePath))
                throw new FileNotFoundException($"Key file not found: {keyFilePath}");

            var keyData = File.ReadAllBytes(keyFilePath);
            if (keyData.Length < 32)
                throw new InvalidOperationException("Invalid key file format");

            Key1 = keyData.Take(16).ToArray();
            Key2 = keyData.Skip(16).Take(16).ToArray();
        }
    }

    // Firmware Class: Load and manage the system firmware (like Nintendo Switch firmware)
    public class Firmware
    {
        public byte[] FirmwareData { get; private set; }

        public Firmware(string firmwareFilePath)
        {
            // Load the firmware from a file
            if (!File.Exists(firmwareFilePath))
                throw new FileNotFoundException($"Firmware file not found: {firmwareFilePath}");

            FirmwareData = File.ReadAllBytes(firmwareFilePath);
        }
    }

    // ROM Class: Load and manage the game ROM (like Switch ROM)
    public class ROM
{
    private byte[] _data;

    public ROM(string path)
    {
        _data = File.ReadAllBytes(path); // Read ROM file into memory
    }

    public byte[] GetTextureData(int textureIndex)
    {
        // Assume each texture is of fixed size, extract texture data based on index
        int textureSize = 256 * 256 * 4; // Example: 256x256 RGBA texture
        int offset = textureIndex * textureSize;
        return _data.Skip(offset).Take(textureSize).ToArray();
    }
}


    // GPU Class: Emulate GPU for rendering frames (via SkiaSharp)
    public class GPU
    {
        private SKSurface _surface;
        private SKCanvas _canvas;
        private SKImageInfo _imageInfo;
        private SKBitmap _frameBuffer;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public GPU(int width, int height)
        {
            Width = width;
            Height = height;
            _imageInfo = new SKImageInfo(width, height);
            _frameBuffer = new SKBitmap(width, height);
            _surface = SKSurface.Create(_imageInfo);
            _canvas = _surface.Canvas;
            Clear();
        }

        public void Render(GPUInstruction instruction, params object[] parameters)
        {
            GPUInterpreter interpreter = new GPUInterpreter(this);

            // Execute the provided GPU instruction
            interpreter.ExecuteInstruction(instruction, parameters);

            // After executing GPU instruction, finalize the rendering to the screen
            SKImage img = _surface.Snapshot();
            SKData data = img.Encode();
            img.Dispose();
        }


        public void Clear()
        {
            _canvas.Clear(SKColors.Black);
        }

        public void DrawRect(int x, int y, int width, int height, SKColor color)
        {
            using (SKPaint paint = new SKPaint())
            {
                paint.Color = color;
                _canvas.DrawRect(x, y, width, height, paint);
            }
        }

        public void Render()
        {
            SKImage img = _surface.Snapshot();
            SKData data = img.Encode();
            img.Dispose();
        }

        public SKBitmap GetFrameBuffer()
        {
            return _frameBuffer;
        }
    }

    // InputHandler for handling mouse and Xbox controller inputs
    public class InputHandler
    {
        public event EventHandler MouseMoved;
        public event EventHandler MouseClick;
        public event EventHandler JoyConButtonPressed;  // Event for button press (Joy-Con/Pro Controller)
        public event EventHandler ControllerButtonPressed; // Event for Xbox controller

        // Mouse input handling (used for emulating Joy-Con movement)
        public void OnMouseMove(object sender, MouseEventArgs e)
        {
            MouseMoved?.Invoke(this, e);
        }

        // Mouse click handling (used for emulating Joy-Con button presses)
        public void OnMouseClick(object sender, MouseEventArgs e)
        {
            MouseClick?.Invoke(this, e);
        }

        // Handling Xbox controller button presses (as Pro controller)
        public void HandleXboxControllerInput()
        {
            var controller = new Controller(UserIndex.One); // Assuming one controller is connected
            if (controller.IsConnected)
            {
                var state = controller.GetState();

                if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A))
                {
                    ControllerButtonPressed?.Invoke(this, new ButtonEventArgs("A"));
                }
                if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B))
                {
                    ControllerButtonPressed?.Invoke(this, new ButtonEventArgs("B"));
                }
                if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X))
                {
                    ControllerButtonPressed?.Invoke(this, new ButtonEventArgs("X"));
                }
                if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y))
                {
                    ControllerButtonPressed?.Invoke(this, new ButtonEventArgs("Y"));
                }
                if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Start))
                {
                    ControllerButtonPressed?.Invoke(this, new ButtonEventArgs("Start"));
                }
                if (state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back))
                {
                    ControllerButtonPressed?.Invoke(this, new ButtonEventArgs("Back"));
                }
            }
        }

        // To call in each update cycle to continuously check controller state
        public void UpdateControllerInput()
        {
            HandleXboxControllerInput();
        }
    }

    // Custom EventArgs for passing controller button info
    public class ButtonEventArgs : EventArgs
    {
        public string Button { get; }
        public ButtonEventArgs(string button)
        {
            Button = button;
        }
    }

    // ARM64 CPU Emulation for ARMv8.2-A with full instruction set
    using System;

public class ARM64CPU
{
    public long[] Registers = new long[32];  // 32 general-purpose registers (X0 to X31)
    public double[] FRegisters = new double[32]; // Floating-point registers (S0 to S31 or D0 to D31)
    public long ProgramCounter; // PC register (X0)
    public long CycleCount; // To track CPU cycles
    public bool ExceptionOccurred; // Flag for exceptions (e.g., Invalid Opcode, etc.)
    public long StackPointer;
    public long ExceptionLinkRegister;

    public RAM Ram { get; private set; }

    public int ExceptionLevel { get; private set; }
    public bool IsBigEndian { get; private set; }  // Example flag for byte ordering
    public bool IsSVEEnabled { get; private set; }  // Scalable Vector Extension (SVE) support (ARMv8.2-A)

    public ARM64CPU(RAM ram)
    {
        Ram = ram;
        ProgramCounter = 0x1000;  // Initial Program Counter (example)
        StackPointer = 0x2000;     // Example initial stack pointer address
        ExceptionLinkRegister = 0;
        ExceptionLevel = 0;       // EL0 by default
        IsBigEndian = false;      // Little-endian default for ARM64
        IsSVEEnabled = false;     // SVE disabled by default
    }

    // Helper to handle exception levels (EL0-EL3)
    private void HandleExceptionLevel()
    {
        // Exception handling based on the current ExceptionLevel (EL0-EL3)
        Console.WriteLine($"Handling exception at EL{ExceptionLevel}");
        // Placeholder for actual exception vector handling, such as handling IRQ, FIQ, etc.
    }

    public void ExecuteInstruction(string instruction)
    {
        string[] parts = instruction.Split(' ');

        switch (parts[0].ToUpper())
        {
            case "MOV":
                ExecuteMOV(parts);
                break;
            case "ADD":
                ExecuteADD(parts);
                break;
            case "SUB":
                ExecuteSUB(parts);
                break;
            case "MUL":
                ExecuteMUL(parts);
                break;
            case "AND":
                ExecuteAND(parts);
                break;
            case "ORR":
                ExecuteORR(parts);
                break;
            case "EOR":
                ExecuteEOR(parts);
                break;
            case "LDR":
                ExecuteLDR(parts);
                break;
            case "STR":
                ExecuteSTR(parts);
                break;
            case "LDADD":
                ExecuteLDADD(parts);  // Atomic add
                break;
            case "LDCLR":
                ExecuteLDCLR(parts);  // Atomic clear
                break;
            case "LDSET":
                ExecuteLDSET(parts);  // Atomic set
                break;
            case "FADD":
                ExecuteFADD(parts);  // Floating-point add
                break;
            case "FCMPE":
                ExecuteFCMPE(parts);  // Floating-point compare
                break;
            case "LDUR":
                ExecuteLDUR(parts);  // Load with unscaled immediate offset
                break;
            case "STUR":
                ExecuteSTUR(parts);  // Store with unscaled immediate offset
                break;
            case "CBZ":
                ExecuteCBZ(parts);  // Compare and Branch on Zero
                break;
            case "B":
                ExecuteB(parts);
                break;
            case "BL":
                ExecuteBL(parts);
                break;
            case "ISB":
                ExecuteISB();  // Instruction Synchronization Barrier
                break;
            case "DSB":
                ExecuteDSB();  // Data Synchronization Barrier
                break;
            case "DMB":
                ExecuteDMB();  // Data Memory Barrier
                break;
            case "SVE":
                EnableSVE();  // Enable Scalable Vector Extensions (SVE)
                break;
            default:
                Console.WriteLine($"Invalid instruction: {instruction}");
                break;
        }
    }

    // MOV - Move instruction
    private void ExecuteMOV(string[] parts)
    {
        Registers[Array.IndexOf(RegisterNames, parts[1])] = Convert.ToInt64(parts[2]);
    }

    // ADD - Add instruction
    private void ExecuteADD(string[] parts)
    {
        long value1 = Registers[Array.IndexOf(RegisterNames, parts[1])];
        long value2 = Registers[Array.IndexOf(RegisterNames, parts[2])];
        Registers[Array.IndexOf(RegisterNames, parts[3])] = value1 + value2;
    }

    // SUB - Subtract instruction
    private void ExecuteSUB(string[] parts)
    {
        long value1 = Registers[Array.IndexOf(RegisterNames, parts[1])];
        long value2 = Registers[Array.IndexOf(RegisterNames, parts[2])];
        Registers[Array.IndexOf(RegisterNames, parts[3])] = value1 - value2;
    }

    // MUL - Multiply instruction
    private void ExecuteMUL(string[] parts)
    {
        long value1 = Registers[Array.IndexOf(RegisterNames, parts[1])];
        long value2 = Registers[Array.IndexOf(RegisterNames, parts[2])];
        Registers[Array.IndexOf(RegisterNames, parts[3])] = value1 * value2;
    }

    // Floating-Point ADD
    private void ExecuteFADD(string[] parts)
    {
        double f1 = FRegisters[Array.IndexOf(RegisterNames, parts[1])];
        double f2 = FRegisters[Array.IndexOf(RegisterNames, parts[2])];
        FRegisters[Array.IndexOf(RegisterNames, parts[3])] = f1 + f2;
    }

    // Floating-Point Compare
    private void ExecuteFCMPE(string[] parts)
    {
        double f1 = FRegisters[Array.IndexOf(RegisterNames, parts[1])];
        double f2 = FRegisters[Array.IndexOf(RegisterNames, parts[2])];
        // Compare flags could be set based on results (not implemented here)
    }

    // Load with Unscaled Immediate Offset (LDR)
    private void ExecuteLDR(string[] parts)
    {
        long address = Registers[Array.IndexOf(RegisterNames, parts[1])] + Convert.ToInt64(parts[2].TrimStart('#'));
        Registers[Array.IndexOf(RegisterNames, parts[3])] = Ram.ReadByte((int)address);
    }

    // Store with Unscaled Immediate Offset (STR)
    private void ExecuteSTR(string[] parts)
    {
        long address = Registers[Array.IndexOf(RegisterNames, parts[1])] + Convert.ToInt64(parts[2].TrimStart('#'));
        Ram.WriteByte((int)address, (byte)Registers[Array.IndexOf(RegisterNames, parts[3])]);
    }

    // Atomic Add (LDADD)
    private void ExecuteLDADD(string[] parts)
    {
        long address = Registers[Array.IndexOf(RegisterNames, parts[1])] + Convert.ToInt64(parts[2].TrimStart('#'));
        long originalValue = Ram.ReadByte((int)address);
        long newValue = originalValue + Registers[Array.IndexOf(RegisterNames, parts[3])];
        Ram.WriteByte((int)address, (byte)newValue);
    }

    // Atomic Clear (LDCLR)
    private void ExecuteLDCLR(string[] parts)
    {
        long address = Registers[Array.IndexOf(RegisterNames, parts[1])] + Convert.ToInt64(parts[2].TrimStart('#'));
        Ram.WriteByte((int)address, 0);  // Clear the memory at the address
    }

    // Atomic Set (LDSET)
    private void ExecuteLDSET(string[] parts)
    {
        long address = Registers[Array.IndexOf(RegisterNames, parts[1])] + Convert.ToInt64(parts[2].TrimStart('#'));
        Ram.WriteByte((int)address, 1);  // Set the memory at the address
    }

    // Conditional Branch on Zero (CBZ)
    private void ExecuteCBZ(string[] parts)
    {
        long registerValue = Registers[Array.IndexOf(RegisterNames, parts[1])];
        if (registerValue == 0)
        {
            ProgramCounter = Convert.ToInt64(parts[2], 16);
        }
    }

    // Branch (B)
    private void ExecuteB(string[] parts)
    {
        ProgramCounter = Convert.ToInt64(parts[1], 16);
    }

    // Branch with Link (BL)
    private void ExecuteBL(string[] parts)
    {
        Registers[30] = ProgramCounter;  // Store return address in X30 (link register)
        ProgramCounter = Convert.ToInt64(parts[1], 16);
    }

        // Execute Data Memory Barrier (DMB)
    private void ExecuteDMB()
    {
        Console.WriteLine("Executing DMB (Data Memory Barrier).");
        // Simulate memory ordering: Ensure all prior memory accesses are completed before continuing
        // In an actual CPU, we would flush the memory pipeline and wait until all memory operations complete
        while (InstructionPipeline.Count > 0)
        {
            // Execute any pending instructions in the pipeline before continuing
            string instruction = InstructionPipeline.Dequeue();
            Console.WriteLine($"Executing instruction: {instruction}");
        }
    }

    // Execute Data Synchronization Barrier (DSB)
    private void ExecuteDSB()
    {
        Console.WriteLine("Executing DSB (Data Synchronization Barrier).");
        // Stronger barrier: Ensure all prior memory accesses, including side effects, are fully completed
        // Simulate a "blocking" behavior where no further instructions are processed until DSB is done
        while (InstructionPipeline.Count > 0)
        {
            string instruction = InstructionPipeline.Dequeue();
            Console.WriteLine($"Executing instruction: {instruction}");
        }
    }

    // Execute Instruction Synchronization Barrier (ISB)
    private void ExecuteISB()
    {
        Console.WriteLine("Executing ISB (Instruction Synchronization Barrier).");
        // Invalidate the instruction pipeline: ensure that no instructions are executed until fresh ones are fetched
        InstructionPipeline.Clear();
        Console.WriteLine("Instruction pipeline invalidated. Fetching fresh instructions.");
    }

    // Enable Scalable Vector Extensions (SVE) feature
    private void EnableSVE()
    {
        IsSVEEnabled = true;
        Console.WriteLine("SVE (Scalable Vector Extensions) enabled.");
    }

    public void RunCycle()
    {
        if (ExceptionOccurred)
        {
            HandleExceptionLevel();
        }
        else
        {
            ProgramCounter++;
            CycleCount++;
        }
    }

    private static readonly string[] RegisterNames = new string[]
    {
        "X0", "X1", "X2", "X3", "X4", "X5", "X6", "X7", "X8", "X9", "X10", "X11", "X12", "X13", "X14", "X15",
        "X16", "X17", "X18", "X19", "X20", "X21", "X22", "X23", "X24", "X25", "X26", "X27", "X28", "X29", "X30", "X31"
    };
}

public class RAM
{
    private byte[] memory;

    public RAM(int size)
    {
        memory = new byte[size];
    }

    public byte ReadByte(int address)
    {
        return memory[address];
    }

    public void WriteByte(int address, byte value)
    {
        memory[address] = value;
    }
}


    // Emulator Window (Main application window)
    public class EmulatorWindow : Form
    {
        private GPU _gpu;
        private InputHandler _inputHandler;
        private ARM64CPU _cpu;
        private Timer _renderTimer;

        public EmulatorWindow()
        {
            _gpu = new GPU(1920, 1080);
            _inputHandler = new InputHandler();

            // Initialize RAM with a size (for simplicity, 4GB of RAM)
            RAM ram = new RAM(4 * 1024 * 1024 * 1024); 

            // Load keys, firmware, and ROM from files (just a placeholder path here)
            Keys keys = new Keys("path_to_keys");
            Firmware firmware = new Firmware("path_to_firmware");
            ROM rom = new ROM("path_to_rom");
            byte[] textureData = rom.GetTextureData(0); // Get first texture from ROM
            Texture texture = new Texture(textureData);

            // Bind texture and render
            texture.Bind();

            _cpu = new ARM64CPU(ram);

            // Subscribe to input events
            _inputHandler.MouseMoved += HandleMouseMove;
            _inputHandler.MouseClick += HandleMouseClick;
            _inputHandler.ControllerButtonPressed += HandleControllerButtonPress;

            this.MouseMove += new MouseEventHandler((sender, e) => _inputHandler.OnMouseMove(sender, e));
            this.MouseClick += new MouseEventHandler((sender, e) => _inputHandler.OnMouseClick(sender, e));

            // Timer to simulate rendering (60Hz)
            _renderTimer = new Timer();
            _renderTimer.Interval = 16; // 60 FPS
            _renderTimer.Tick += (sender, e) => Render();
            _renderTimer.Start();
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            Console.WriteLine($"Mouse moved to {e.Location}");
        }

        private void HandleMouseClick(object sender, MouseEventArgs e)
        {
            Console.WriteLine($"Mouse clicked at {e.Location}");
        }

        private void HandleControllerButtonPress(object sender, ButtonEventArgs e)
        {
            Console.WriteLine($"Controller button pressed: {e.Button}");
        }

        // Render method for the emulator window
        private void Render()
        {
            // Render the GPU frame
            _gpu.Clear();
            _gpu.DrawRect(100, 100, 200, 200, SKColors.Red);
            _gpu.Render();
            this.Invalidate(); // Trigger window redraw
        }

        // Override OnPaint to render to the window
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            SKCanvas canvas = new SKCanvas(_gpu.GetFrameBuffer());
            e.Graphics.DrawImage(new Bitmap(canvas.Bitmap), 0, 0);
        }
    }

    // Main Program Entry Point
    public static class Program
    {
        public static void Main()
        {
            Application.Run(new EmulatorWindow());
        }
    }
}
