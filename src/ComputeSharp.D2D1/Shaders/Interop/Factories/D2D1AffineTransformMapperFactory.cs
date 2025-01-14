using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ComputeSharp.D2D1.Interop;
using ComputeSharp.D2D1.Shaders.Interop.Factories.Abstract;
using ComputeSharp.D2D1.Shaders.Interop.Helpers;

namespace ComputeSharp.D2D1.Shaders.Interop.Factories;

/// <summary>
/// A container for shader-specific affine transform mapper factory.
/// </summary>
/// <remarks>
/// This non-generic type is used so that <see cref="Transform"/> and <see cref="SubtractAndClampToMaxIfOverflow"/> are not recompiled for each generic type instantiation.
/// </remarks>
internal sealed class D2D1AffineTransformMapperFactory
{
    /// <summary>
    /// Transforms a target rectangle using a given transform matrix.
    /// </summary>
    /// <param name="matrix">The transform matrix to use.</param>
    /// <param name="rectangle">The rectangle to transform.</param>
    private static void Transform(in Matrix3x2 matrix, ref Rectangle64 rectangle)
    {
        // Get the rectangle corners
        Point64 topLeft = new(rectangle.Left, rectangle.Top);
        Point64 topRight = new(rectangle.Right, rectangle.Top);
        Point64 bottomLeft = new(rectangle.Left, rectangle.Bottom);
        Point64 bottomRight = new(rectangle.Right, rectangle.Bottom);

        // Transform them with the current matrix
        (double X, double Y) transformedTopLeft = topLeft.Transform(in matrix);
        (double X, double Y) transformedTopRight = topRight.Transform(in matrix);
        (double X, double Y) transformedBottomLeft = bottomLeft.Transform(in matrix);
        (double X, double Y) transformedBottomRight = bottomRight.Transform(in matrix);

        // Calculate the bounding box of the transformed points
        double transformedLeft = Math.Min(Math.Min(transformedTopLeft.X, transformedTopRight.X), Math.Min(transformedBottomLeft.X, transformedBottomRight.X));
        double transformedTop = Math.Min(Math.Min(transformedTopLeft.Y, transformedTopRight.Y), Math.Min(transformedBottomLeft.Y, transformedBottomRight.Y));
        double transformedRight = Math.Max(Math.Max(transformedTopLeft.X, transformedTopRight.X), Math.Max(transformedBottomLeft.X, transformedBottomRight.X));
        double transformedBottom = Math.Max(Math.Max(transformedTopLeft.Y, transformedTopRight.Y), Math.Max(transformedBottomLeft.Y, transformedBottomRight.Y));

        // Round each coordinate as needed and compute the rectangle bounds
        long left = (long)Math.Floor(Math.Max(transformedLeft, long.MinValue));
        long top = (long)Math.Floor(Math.Max(transformedTop, long.MinValue));
        long right = (long)Math.Ceiling(Math.Min(transformedRight, long.MaxValue));
        long bottom = (long)Math.Ceiling(Math.Min(transformedBottom, long.MaxValue));

        // Calculate the width and height, with clamping in case of overflows
        long width = SubtractAndClampToMaxIfOverflow(right, left);
        long height = SubtractAndClampToMaxIfOverflow(bottom, top);

        rectangle = new(left, top, width, height);
    }

    /// <summary>
    /// Subtracts two values and clamps to <see cref="long.MaxValue"/> if there is an overflow.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The value to subtract.</param>
    /// <returns>The result of the subtraction, clamped to <see cref="long.MaxValue"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SubtractAndClampToMaxIfOverflow(long x, long y)
    {
        long z = unchecked(x - y);

        // Overflow in (x - y) with carry occurs if and only if x and y have opposite signs
        // and the sign of (x - y) with carry is opposite of x (or equivalently same as y).
        if (((x ^ y) & (z ^ x)) < 0)
        {
            return long.MaxValue;
        }

        return z;
    }

    /// <summary>
    /// A custom <see cref="D2D1TransformMapperFactory{T, TParameters, TTransformMapper}"/> implementation for an affine transform.
    /// </summary>
    /// <typeparam name="T">The type of D2D1 pixel shader associated to the transform mapper.</typeparam>
    public sealed class For<T> : D2D1TransformMapperFactory<T, Matrix3x2, For<T>.TransformMapper>
        where T : unmanaged, ID2D1PixelShader
    {
        /// <summary>
        /// A <see cref="D2D1TransformMapperParametersAccessor{T, TParameters}"/> implementation for a constant affine transform matrix.
        /// </summary>
        public sealed class ConstantMatrix : D2D1TransformMapperParametersAccessor<T, Matrix3x2>
        {
            /// <summary>
            /// Gets the fixed affine transform matrix.
            /// </summary>
            public Matrix3x2 Amount { get; init; }

            /// <inheritdoc/>
            public override Matrix3x2 Get(in T shader)
            {
                return Amount;
            }
        }

        /// <summary>
        /// A <see cref="D2D1TransformMapperParametersAccessor{T, TParameters}"/> implementation for a dynamic affine transform matrix.
        /// </summary>
        public sealed class DynamicMatrix : D2D1TransformMapperParametersAccessor<T, Matrix3x2>
        {
            /// <summary>
            /// Gets the <see cref="D2D1TransformMapperFactory{T}.Accessor{TResult}"/> instance to get the dynamic affine transform matrix.
            /// </summary>
            public required D2D1TransformMapperFactory<T>.Accessor<Matrix3x2> Accessor { get; init; }

            /// <inheritdoc/>
            public override Matrix3x2 Get(in T shader)
            {
                return Accessor(in shader);
            }
        }

        /// <summary>
        /// A custom <see cref="D2D1TransformMapper{T, TParameters}"/> implementation for an affine transform.
        /// </summary>
        public sealed class TransformMapper : D2D1TransformMapper<T, Matrix3x2>
        {
            /// <inheritdoc/>
            protected override void TransformInputToOutput(in Matrix3x2 parameters, ref Rectangle64 rectangle)
            {
                Transform(in parameters, ref rectangle);
            }

            /// <inheritdoc/>
            protected override void TransformOutputToInput(in Matrix3x2 parameters, ref Rectangle64 rectangle)
            {
                if (!Matrix3x2.Invert(parameters, out Matrix3x2 matrix))
                {
                    static void Throw()
                    {
                        throw new ArgumentException("The input transform matrix can't be inverted, so it can't be used for a transform mapper.");
                    }

                    Throw();
                }

                Transform(in matrix, ref rectangle);
            }
        }
    }
}