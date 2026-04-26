using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;

namespace Kaleidoscope;

public sealed class GpuKaleidoscopeRenderer : IDisposable
{
    private int _program;
    private int _vertexShader;
    private int _fragmentShader;
    private int _vao;
    private int _vbo;
    private int _ebo;
    private int _texture;

    private int _uSource;
    private int _uSegments;
    private int _uRotation;
    private int _uResolution;
    private int _uTexSize;

    private int _textureWidth;
    private int _textureHeight;
    private bool _initialized;

    public bool IsInitialized => _initialized;

    public void Initialize()
    {
        if (_initialized)
            return;

        _vertexShader = CompileShader(ShaderType.VertexShader, VertexShaderSource);
        _fragmentShader = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);

        _program = GL.CreateProgram();
        GL.AttachShader(_program, _vertexShader);
        GL.AttachShader(_program, _fragmentShader);
        GL.LinkProgram(_program);

        GL.GetProgram(_program, GetProgramParameterName.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
            throw new InvalidOperationException($"GL program link failed: {GL.GetProgramInfoLog(_program)}");

        float[] vertices =
        [
            // pos.x, pos.y, uv.x, uv.y
            -1f, -1f, 0f, 0f,
             1f, -1f, 1f, 0f,
             1f,  1f, 1f, 1f,
            -1f,  1f, 0f, 1f,
        ];

        uint[] indices = [0u, 1u, 2u, 2u, 3u, 0u];

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

        GL.BindVertexArray(0);

        _uSource = GL.GetUniformLocation(_program, "uSource");
        _uSegments = GL.GetUniformLocation(_program, "uSegments");
        _uRotation = GL.GetUniformLocation(_program, "uRotation");
        _uResolution = GL.GetUniformLocation(_program, "uResolution");
        _uTexSize = GL.GetUniformLocation(_program, "uTexSize");

        _initialized = true;
    }

    public void SetSourceImage(Bitmap source)
    {
        if (!_initialized)
            throw new InvalidOperationException("Renderer must be initialized before uploading textures.");

        if (_texture == 0)
            _texture = GL.GenTexture();

        _textureWidth = source.Width;
        _textureHeight = source.Height;

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _texture);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        var data = source.LockBits(
            new Rectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba,
                source.Width,
                source.Height,
                0,
                OpenTK.Graphics.OpenGL4.PixelFormat.Bgra,
                PixelType.UnsignedByte,
                data.Scan0);
        }
        finally
        {
            source.UnlockBits(data);
        }
    }

    public void Render(Size viewportSize, int segments, float rotation)
    {
        if (!_initialized || _texture == 0 || viewportSize.Width <= 0 || viewportSize.Height <= 0)
            return;

        GL.Viewport(0, 0, viewportSize.Width, viewportSize.Height);
        GL.ClearColor(0f, 0f, 0f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        GL.UseProgram(_program);

        GL.Uniform1(_uSource, 0);
        GL.Uniform1(_uSegments, Math.Max(2, segments));
        GL.Uniform1(_uRotation, rotation);
        GL.Uniform2(_uResolution, (float)viewportSize.Width, (float)viewportSize.Height);
        GL.Uniform2(_uTexSize, (float)_textureWidth, (float)_textureHeight);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _texture);

        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    public Bitmap CaptureCurrentFrame(Size viewportSize, int segments, float rotation)
    {
        Render(viewportSize, segments, rotation);

        int width = viewportSize.Width;
        int height = viewportSize.Height;
        byte[] pixels = new byte[width * height * 4];

        GL.ReadPixels(0, 0, width, height, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, pixels);

        var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            int srcRowBytes = width * 4;
            int dstStride = data.Stride;

            for (int y = 0; y < height; y++)
            {
                int srcOffset = (height - 1 - y) * srcRowBytes;
                IntPtr dstPtr = IntPtr.Add(data.Scan0, y * dstStride);
                System.Runtime.InteropServices.Marshal.Copy(pixels, srcOffset, dstPtr, srcRowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    public void Dispose()
    {
        if (_texture != 0)
        {
            GL.DeleteTexture(_texture);
            _texture = 0;
        }

        if (_vao != 0)
        {
            GL.DeleteVertexArray(_vao);
            _vao = 0;
        }

        if (_vbo != 0)
        {
            GL.DeleteBuffer(_vbo);
            _vbo = 0;
        }

        if (_ebo != 0)
        {
            GL.DeleteBuffer(_ebo);
            _ebo = 0;
        }

        if (_program != 0)
        {
            GL.DeleteProgram(_program);
            _program = 0;
        }

        if (_vertexShader != 0)
        {
            GL.DeleteShader(_vertexShader);
            _vertexShader = 0;
        }

        if (_fragmentShader != 0)
        {
            GL.DeleteShader(_fragmentShader);
            _fragmentShader = 0;
        }

        _initialized = false;
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
            throw new InvalidOperationException($"{type} compile failed: {GL.GetShaderInfoLog(shader)}");

        return shader;
    }

    private const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec2 aPosition;
        layout(location = 1) in vec2 aUv;

        out vec2 vUv;

        void main()
        {
            vUv = aUv;
            gl_Position = vec4(aPosition, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 vUv;

        uniform sampler2D uSource;
        uniform int uSegments;
        uniform float uRotation;
        uniform vec2 uResolution;
        uniform vec2 uTexSize;

        out vec4 fragColor;

        const float PI = 3.141592653589793;

        void main()
        {
            vec2 center = uResolution * 0.5;
            vec2 d = gl_FragCoord.xy - center;

            float r = length(d);
            float theta = atan(d.y, d.x) + uRotation;

            float twoPi = PI * 2.0;
            theta = mod(theta, twoPi);
            if (theta < 0.0)
                theta += twoPi;

            float wedgeAngle = twoPi / float(max(uSegments, 2));
            float thetaInWedge = mod(theta, wedgeAngle);

            int wedgeIndex = int(floor(theta / wedgeAngle));
            if ((wedgeIndex % 2) == 1)
                thetaInWedge = wedgeAngle - thetaInWedge;

            float sampleRadius = min(uTexSize.x, uTexSize.y) * 0.45;
            float scale = sampleRadius / max(center.x, center.y);

            vec2 srcCenter = uTexSize * 0.5;
            vec2 src = srcCenter + vec2(cos(thetaInWedge), sin(thetaInWedge)) * r * scale;
            src = clamp(src, vec2(0.0), uTexSize - vec2(1.0));

            vec2 uv = src / uTexSize;
            fragColor = texture(uSource, uv);
        }
        """;
}
