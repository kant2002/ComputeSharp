﻿using System;
using System.Runtime.CompilerServices;
using ComputeSharp.D2D1.Helpers;
using ComputeSharp.D2D1.Shaders.Dispatching;
using ComputeSharp.D2D1.Shaders.Interop.Buffers;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

#pragma warning disable CS0618

namespace ComputeSharp.D2D1.Interop;

/// <summary>
/// Provides methods to interop with D2D1 APIs and compile shaders or extract their constant buffer data.
/// </summary>
public static class D2D1PixelShader
{
    /// <summary>
    /// Loads the bytecode from an input D2D1 pixel shader.
    /// </summary>
    /// <typeparam name="T">The type of D2D1 pixel shader to load the bytecode for.</typeparam>
    /// <returns>A <see cref="ReadOnlyMemory{T}"/> instance with the resulting shader bytecode.</returns>
    /// <remarks>
    /// This method will only compile the shader using <see cref="D2D1ShaderProfile.PixelShader50"/> if no precompiled shader is available.
    /// <para>
    /// If the input shader was precompiled, the returned <see cref="ReadOnlyMemory{T}"/> instance will wrap a pinned memory buffer (from the PE section).
    /// If the shader was compiled at runtime, the returned <see cref="ReadOnlyMemory{T}"/> instance will wrap a <see cref="byte"/> array with the bytecode.
    /// </para>
    /// </remarks>
    public static ReadOnlyMemory<byte> LoadBytecode<T>()
        where T : unmanaged, ID2D1PixelShader
    {
        return LoadOrCompileBytecode<T>(null);
    }

    /// <summary>
    /// Loads the bytecode from an input D2D1 pixel shader.
    /// </summary>
    /// <typeparam name="T">The type of D2D1 pixel shader to load the bytecode for.</typeparam>
    /// <param name="shaderProfile">The shader profile to use to get the shader bytecode.</param>
    /// <returns>A <see cref="ReadOnlyMemory{T}"/> instance with the resulting shader bytecode.</returns>
    /// <remarks>
    /// If the input shader was precompiled, the returned <see cref="ReadOnlyMemory{T}"/> instance will wrap a pinned memory buffer (from the PE section).
    /// If the shader was compiled at runtime, the returned <see cref="ReadOnlyMemory{T}"/> instance will wrap a <see cref="byte"/> array with the bytecode.
    /// </remarks>
    public static ReadOnlyMemory<byte> LoadBytecode<T>(D2D1ShaderProfile shaderProfile)
        where T : unmanaged, ID2D1PixelShader
    {
        return LoadOrCompileBytecode<T>(shaderProfile);
    }

    /// <summary>
    /// Loads or compiles the bytecode from an input D2D1 pixel shader.
    /// </summary>
    /// <typeparam name="T">The type of D2D1 pixel shader to load the bytecode for.</typeparam>
    /// <param name="shaderProfile">The shader profile to use to get the shader bytecode.</param>
    /// <returns>A <see cref="ReadOnlyMemory{T}"/> instance with the resulting shader bytecode.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the input shader has not been precompiled.</exception>
    private static unsafe ReadOnlyMemory<byte> LoadOrCompileBytecode<T>(D2D1ShaderProfile? shaderProfile)
        where T : unmanaged, ID2D1PixelShader
    {
        D2D1ShaderBytecodeLoader bytecodeLoader = default;

        Unsafe.SkipInit(out T shader);

        shader.LoadBytecode(ref bytecodeLoader, shaderProfile);

        using ComPtr<ID3DBlob> dynamicBytecode = bytecodeLoader.GetResultingShaderBytecode(out ReadOnlySpan<byte> precompiledBytecode);

        // If a precompiled shader is available, return it
        if (!precompiledBytecode.IsEmpty)
        {
            return new PinnedBufferMemoryManager(precompiledBytecode).Memory;
        }

        // Otherwise, return the dynamic shader instead
        byte* bytecodePtr = (byte*)dynamicBytecode.Get()->GetBufferPointer();
        int bytecodeSize = (int)dynamicBytecode.Get()->GetBufferSize();

        return new ReadOnlySpan<byte>(bytecodePtr, bytecodeSize).ToArray();
    }

    /// <summary>
    /// Gets the number of inputs from an input D2D1 pixel shader.
    /// </summary>
    /// <typeparam name="T">The type of D2D1 pixel shader to get the input count for.</typeparam>
    /// <returns>The number of inputs for the D2D1 pixel shader of type <typeparamref name="T"/>.</returns>
    public static int GetInputCount<T>()
        where T : unmanaged, ID2D1PixelShader
    {
        Unsafe.SkipInit(out T shader);

        return (int)shader.GetInputCount();
    }

    /// <summary>
    /// Gets the type of a given input for a D2D1 pixel shader.
    /// </summary>
    /// <typeparam name="T">The type of D2D1 pixel shader to get the input type for.</typeparam>
    /// <param name="index">The index of the input to get the type for.</param>
    /// <returns>The type of the input of the target D2D1 pixel shader at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is not in range for the available inputs for the shader type.</exception>
    public static D2D1PixelShaderInputType GetInputType<T>(int index)
        where T : unmanaged, ID2D1PixelShader
    {
        Unsafe.SkipInit(out T shader);

        if ((uint)index >= shader.GetInputCount())
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index), "The input index is outside of range for the target pixel shader type.");
        }

        return (D2D1PixelShaderInputType)shader.GetInputType((uint)index);
    }

    /// <summary>
    /// Gets the constant buffer from an input D2D1 pixel shader.
    /// </summary>
    /// <typeparam name="T">The type of D2D1 pixel shader to retrieve info for.</typeparam>
    /// <param name="shader">The input D2D1 pixel shader to retrieve info for.</param>
    /// <returns>A <see cref="ReadOnlyMemory{T}"/> instance with the pixel shader constant buffer.</returns>
    /// <remarks>
    /// This method will allocate a buffer every time it is invoked.
    /// For a zero-allocation alternative, use <see cref="SetConstantBufferForD2D1DrawInfo"/>.</remarks>
    public static ReadOnlyMemory<byte> GetConstantBuffer<T>(in T shader)
        where T : unmanaged, ID2D1PixelShader
    {
        D2D1ByteArrayDispatchDataLoader dataLoader = default;

        Unsafe.AsRef(in shader).LoadDispatchData(ref dataLoader);

        return dataLoader.GetResultingDispatchData();
    }

    /// <summary>
    /// Sets the constant buffer from an input D2D1 pixel shader, by calling <c>ID2D1DrawInfo::SetPixelShaderConstantBuffer</c>.
    /// </summary>
    /// <typeparam name="T">The type of D2D1 pixel shader to set the constant buffer for.</typeparam>
    /// <param name="shader">The input D2D1 pixel shader to set the contant buffer for.</param>
    /// <param name="d2D1DrawInfo">A pointer to the <c>ID2D1DrawInfo</c> instance to use.</param>
    /// <remarks>For more info, see <see href="https://docs.microsoft.com/windows/win32/api/d2d1effectauthor/nf-d2d1effectauthor-id2d1drawinfo-setpixelshaderconstantbuffer"/>.</remarks>
    public static unsafe void SetConstantBufferForD2D1DrawInfo<T>(in T shader, void* d2D1DrawInfo)
        where T : unmanaged, ID2D1PixelShader
    {
        D2D1DrawInfoDispatchDataLoader dataLoader = new((ID2D1DrawInfo*)d2D1DrawInfo);

        Unsafe.AsRef(in shader).LoadDispatchData(ref dataLoader);
    }
}