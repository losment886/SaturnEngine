using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SaturnEngine.SEMath
{
    /// <summary>
    /// 高性能 FFT 实现，支持 SIMD 加速（AVX2/SSE2）。
    /// count = 数据组数，将 data 数组均分为 count 组，每组分别做 FFT。
    /// 例如传入 RGBA 数据时 count=4，分别对 R、G、B、A 四个通道做 FFT。
    /// </summary>
    public static class SEFFT
    {
        /// <summary>
        /// 执行 FFT / IFFT（原 API 签名，完全兼容）
        /// </summary>
        /// <param name="forward">true = FFT, false = IFFT</param>
        /// <param name="count">数据组数，data 数组将被均分为 count 组，每组分别做 FFT</param>
        /// <param name="data">复数数组，长度必须为 count * 2^k</param>
        public static void FFT(bool forward, long count, ref SEComplex[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            int groupCount = (int)count;
            if (groupCount <= 0)
                throw new ArgumentException("count must be positive", nameof(count));

            int totalLen = data.Length;
            if (totalLen % groupCount != 0)
                throw new ArgumentException($"data length ({totalLen}) must be divisible by count ({groupCount})");

            int groupLen = totalLen / groupCount;

            // 检查每组长度是否为 2 的幂
            if ((groupLen & (groupLen - 1)) != 0)
                throw new ArgumentException($"each group length ({groupLen}) must be a power of 2");

            // 对每组分别做 FFT
            for (int g = 0; g < groupCount; g++)
            {
                Span<SEComplex> group = data.AsSpan(g * groupLen, groupLen);
                FftCore(group, forward);
            }
        }

        /// <summary>
        /// 便捷重载：count=1，对整个数组做 FFT
        /// </summary>
        public static void FFT(bool forward, ref SEComplex[] data)
        {
            FFT(forward, 1, ref data);
        }

        /// <summary>
        /// 使用 System.Numerics.Complex 的重载
        /// </summary>
        public static void FFT(bool forward, Span<Complex> data)
        {
            int n = data.Length;
            if ((n & (n - 1)) != 0)
                throw new ArgumentException("data length must be a power of 2");

            // 复用核心实现：将 Complex Span 当作 SEComplex Span 处理（内存布局相同）
            ref SEComplex refData = ref Unsafe.As<Complex, SEComplex>(ref MemoryMarshal.GetReference(data));
            Span<SEComplex> span = MemoryMarshal.CreateSpan(ref refData, n);
            FftCore(span, forward);
        }

        // ====================================================================
        // 核心 FFT 实现
        // ====================================================================

        private static void FftCore(Span<SEComplex> data, bool forward)
        {
            int n = data.Length;
            if (n <= 1) return;

            // 位反转重排
            BitReverse(data, n);

            // 预计算旋转因子表
            double[] cosTable = new double[n / 2];
            double[] sinTable = new double[n / 2];
            PrecomputeTwiddleFactors(cosTable, sinTable, n, forward);

            // 蝶形运算 - 自动选择最优实现
            if (Avx2.IsSupported && n >= 4)
                FftButterflyAvx2(data, n, cosTable, sinTable);
            else if (Sse2.IsSupported && n >= 4)
                FftButterflySse2(data, n, cosTable, sinTable);
            else
                FftButterflyScalar(data, n, cosTable, sinTable);

            // 归一化（FFT 时除以 N）
            if (forward)
            {
                double invN = 1.0 / n;
                for (int i = 0; i < n; i++)
                {
                    data[i].X *= invN;
                    data[i].Y *= invN;
                }
            }
        }

        // ====================================================================
        // 位反转重排
        // ====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BitReverse(Span<SEComplex> data, int n)
        {
            int bits = 31 - BitOperations.LeadingZeroCount((uint)n);

            unsafe
            {
                fixed (SEComplex* pData = data)
                {
                    for (int i = 0; i < n; i++)
                    {
                        int rev = BitReverse32(i, bits);
                        if (rev > i)
                        {
                            SEComplex tmp = pData[i];
                            pData[i] = pData[rev];
                            pData[rev] = tmp;
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BitReverse32(int x, int bits)
        {
            // 使用分治法进行 32 位位反转
            uint ux = (uint)x;
            ux = ((ux & 0x55555555u) << 1) | ((ux & 0xAAAAAAAAu) >> 1);
            ux = ((ux & 0x33333333u) << 2) | ((ux & 0xCCCCCCCCu) >> 2);
            ux = ((ux & 0x0F0F0F0Fu) << 4) | ((ux & 0xF0F0F0F0u) >> 4);
            ux = ((ux & 0x00FF00FFu) << 8) | ((ux & 0xFF00FF00u) >> 8);
            ux = (ux << 16) | (ux >> 16);
            return (int)(ux >> (32 - bits));
        }

        // ====================================================================
        // 预计算旋转因子
        // ====================================================================

        private static void PrecomputeTwiddleFactors(double[] cosTable, double[] sinTable, int n, bool forward)
        {
            double sign = forward ? -1.0 : 1.0;
            int halfN = n / 2;

            for (int k = 0; k < halfN; k++)
            {
                double angle = sign * 2.0 * Math.PI * k / n;
                cosTable[k] = Math.Cos(angle);
                sinTable[k] = Math.Sin(angle);
            }
        }

        // ====================================================================
        // 标量蝶形运算（回退方案，所有平台可用）
        // ====================================================================

        private static void FftButterflyScalar(Span<SEComplex> data, int n, double[] cosTable, double[] sinTable)
        {
            unsafe
            {
                fixed (SEComplex* pData = data)
                fixed (double* pCos = cosTable)
                fixed (double* pSin = sinTable)
                {
                    for (int len = 2; len <= n; len <<= 1)
                    {
                        int halfLen = len >> 1;
                        int stride = n / len;

                        for (int i = 0; i < n; i += len)
                        {
                            for (int j = 0; j < halfLen; j++)
                            {
                                int idxW = j * stride;
                                double wRe = pCos[idxW];
                                double wIm = pSin[idxW];

                                int i1 = i + j;
                                int i2 = i1 + halfLen;

                                // 蝶形运算
                                double tRe = wRe * pData[i2].X - wIm * pData[i2].Y;
                                double tIm = wRe * pData[i2].Y + wIm * pData[i2].X;

                                pData[i2].X = pData[i1].X - tRe;
                                pData[i2].Y = pData[i1].Y - tIm;
                                pData[i1].X += tRe;
                                pData[i1].Y += tIm;
                            }
                        }
                    }
                }
            }
        }

        // ====================================================================
        // SSE2 加速蝶形运算
        // ====================================================================

        private static void FftButterflySse2(Span<SEComplex> data, int n, double[] cosTable, double[] sinTable)
        {
            unsafe
            {
                fixed (SEComplex* pData = data)
                fixed (double* pCos = cosTable)
                fixed (double* pSin = sinTable)
                {
                    for (int len = 2; len <= n; len <<= 1)
                    {
                        int halfLen = len >> 1;
                        int stride = n / len;

                        for (int i = 0; i < n; i += len)
                        {
                            for (int j = 0; j < halfLen; j++)
                            {
                                int idxW = j * stride;
                                int i1 = i + j;
                                int i2 = i1 + halfLen;

                                double wr = pCos[idxW];
                                double wi = pSin[idxW];

                                // 用 SSE2 加载 data[i2]
                                Vector128<double> v2 = Sse2.LoadVector128((double*)(pData + i2));
                                double x = v2.GetElement(0);
                                double y = v2.GetElement(1);

                                // 复数乘法
                                double tRe = wr * x - wi * y;
                                double tIm = wr * y + wi * x;

                                // 用 SSE2 加载 data[i1] 并计算加减
                                Vector128<double> v1 = Sse2.LoadVector128((double*)(pData + i1));
                                Vector128<double> t = Vector128.Create(tRe, tIm);

                                // data[i2] = v1 - t
                                Sse2.Store((double*)(pData + i2), Sse2.Subtract(v1, t));
                                // data[i1] = v1 + t
                                Sse2.Store((double*)(pData + i1), Sse2.Add(v1, t));
                            }
                        }
                    }
                }
            }
        }

        // ====================================================================
        // AVX2 加速蝶形运算
        // ====================================================================

        private static void FftButterflyAvx2(Span<SEComplex> data, int n, double[] cosTable, double[] sinTable)
        {
            unsafe
            {
                fixed (SEComplex* pData = data)
                fixed (double* pCos = cosTable)
                fixed (double* pSin = sinTable)
                {
                    for (int len = 2; len <= n; len <<= 1)
                    {
                        int halfLen = len >> 1;
                        int stride = n / len;

                        for (int i = 0; i < n; i += len)
                        {
                            int j = 0;

                            // AVX2 主循环：一次处理 2 个复数
                            if (halfLen >= 2)
                            {
                                for (; j <= halfLen - 2; j += 2)
                                {
                                    int idxW0 = j * stride;
                                    int idxW1 = (j + 1) * stride;

                                    int i1_0 = i + j;
                                    int i2_0 = i1_0 + halfLen;
                                    int i1_1 = i + j + 1;
                                    int i2_1 = i1_1 + halfLen;

                                    double wr0 = pCos[idxW0];
                                    double wi0 = pSin[idxW0];
                                    double wr1 = pCos[idxW1];
                                    double wi1 = pSin[idxW1];

                                    // 加载 data[i2] 的 2 个复数: [x0, y0, x1, y1]
                                    Vector256<double> v2 = Avx.LoadVector256((double*)(pData + i2_0));

                                    double x0 = v2.GetElement(0);
                                    double y0 = v2.GetElement(1);
                                    double x1 = v2.GetElement(2);
                                    double y1 = v2.GetElement(3);

                                    double tRe0 = wr0 * x0 - wi0 * y0;
                                    double tIm0 = wr0 * y0 + wi0 * x0;
                                    double tRe1 = wr1 * x1 - wi1 * y1;
                                    double tIm1 = wr1 * y1 + wi1 * x1;

                                    // 加载 data[i1] 的 2 个复数
                                    Vector256<double> v1 = Avx.LoadVector256((double*)(pData + i1_0));

                                    // 构造 t = [tRe0, tIm0, tRe1, tIm1]
                                    Vector256<double> t = Vector256.Create(tRe0, tIm0, tRe1, tIm1);

                                    // data[i2] = v1 - t
                                    Avx.Store((double*)(pData + i2_0), Avx.Subtract(v1, t));
                                    // data[i1] = v1 + t
                                    Avx.Store((double*)(pData + i1_0), Avx.Add(v1, t));
                                }
                            }

                            // 处理剩余的一个复数
                            for (; j < halfLen; j++)
                            {
                                int idxW = j * stride;
                                int i1 = i + j;
                                int i2 = i1 + halfLen;

                                double wr = pCos[idxW];
                                double wi = pSin[idxW];
                                double x = pData[i2].X;
                                double y = pData[i2].Y;

                                double tRe = wr * x - wi * y;
                                double tIm = wr * y + wi * x;

                                pData[i2].X = pData[i1].X - tRe;
                                pData[i2].Y = pData[i1].Y - tIm;
                                pData[i1].X += tRe;
                                pData[i1].Y += tIm;
                            }
                        }
                    }
                }
            }
        }
    }
}
