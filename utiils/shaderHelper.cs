using Silk.NET.OpenGL;

namespace VoxEngine.Utils;

public static class ShaderHelper
{
    public static uint Compile(GL gl, string source, ShaderType type)
    {
        uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        // In a real engine, check for compilation errors here
        return shader;
    }
}
