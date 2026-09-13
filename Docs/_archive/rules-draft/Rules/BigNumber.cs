using System;
using System.Globalization;

namespace Pinball.Rules
{
    /// <summary>
    /// 大數：value = Mantissa × 10^Exponent。
    /// Mantissa 正規化到 [1, 10) 或 [−10, −1)；0 表示為 (0, 0)。
    ///
    /// 純值型別，不依賴 UnityEngine —— 見 Docs/Design.md 4.1。
    ///
    /// 為什麼不用 double：肉鴿大數字遊戲的分數會到 1e87 這種量級，
    /// double 到 1e308 就會溢位成 Infinity。BigNumber 的指數是 int，理論上無上限
    /// （這裡 clamp 到 ±300 只是為了防止 int 溢位變成負數 → 分數憑空變 0）。
    /// </summary>
    [Serializable]
    public struct BigNumber : IEquatable<BigNumber>, IComparable<BigNumber>
    {
        /// <summary>指數下限。低於此值視為 0。</summary>
        public const int MinExponent = -300;

        /// <summary>指數上限。高於此值會飽和（防止 int 溢位）。</summary>
        public const int MaxExponent = 300;

        /// <summary>
        /// 加法時，兩者指數差超過此值就直接回傳較大者。
        /// 因為小的那一方已經落在 double 的精度之外，加進去也不會改變結果。
        /// </summary>
        public const int AddCutoff = 17;

        public double Mantissa;
        public int Exponent;

        public BigNumber(double mantissa, int exponent)
        {
            Mantissa = mantissa;
            Exponent = exponent;
            Normalize();
        }

        public static BigNumber Zero
        {
            get { return new BigNumber(0.0, 0); }
        }

        public static BigNumber One
        {
            get { return new BigNumber(1.0, 0); }
        }

        public static BigNumber FromDouble(double value)
        {
            return new BigNumber(value, 0);
        }

        public bool IsZero
        {
            get { return Mantissa == 0.0; }
        }

        public bool IsNegative
        {
            get { return Mantissa < 0.0; }
        }

        /// <summary>相對於 double 的近似值。指數很大時會飽和成 ±Infinity，僅供顯示用。</summary>
        public double ApproximateValue
        {
            get
            {
                if (IsZero) return 0.0;
                if (Exponent > 308) return Mantissa > 0 ? double.PositiveInfinity : double.NegativeInfinity;
                if (Exponent < -324) return 0.0;
                return Mantissa * Math.Pow(10.0, Exponent);
            }
        }

        // ---------------------------------------------------------------- 正規化

        private void Normalize()
        {
            if (double.IsNaN(Mantissa) || double.IsInfinity(Mantissa))
            {
                Mantissa = 0.0;
                Exponent = 0;
                return;
            }

            if (Mantissa == 0.0)
            {
                Exponent = 0;
                return;
            }

            int shift = (int)Math.Floor(Math.Log10(Math.Abs(Mantissa)));
            double m = Mantissa / Math.Pow(10.0, shift);

            // Math.Log10 / Math.Pow 會有浮點誤差（例如 1000 可能算出 shift = 2），
            // 用迴圈修正，確保 m 落在 [1, 10)。
            while (Math.Abs(m) >= 10.0)
            {
                m /= 10.0;
                shift++;
            }
            while (Math.Abs(m) < 1.0)
            {
                m *= 10.0;
                shift--;
            }

            ClampExponent(ref m, ref shift);

            Mantissa = m;
            Exponent = shift;
        }

        /// <summary>指數超出範圍時飽和或歸零 —— 防止 int 溢位把分數變成 0。</summary>
        private static void ClampExponent(ref double mantissa, ref int exponent)
        {
            if (exponent > MaxExponent)
            {
                exponent = MaxExponent;
                mantissa = mantissa > 0 ? 9.999999999 : -9.999999999;
            }
            else if (exponent < MinExponent)
            {
                // 太小了，直接當 0。遊戲裡不會有這種數字。
                exponent = 0;
                mantissa = 0.0;
            }
        }

        // ---------------------------------------------------------------- 運算

        public static BigNumber operator +(BigNumber a, BigNumber b)
        {
            if (a.IsZero) return b;
            if (b.IsZero) return a;

            // 讓 a 是指數較大的一方
            if (a.Exponent < b.Exponent)
            {
                BigNumber tmp = a;
                a = b;
                b = tmp;
            }

            int diff = a.Exponent - b.Exponent;
            if (diff > AddCutoff) return a; // b 已經小到不影響 a

            double m = a.Mantissa + b.Mantissa / Math.Pow(10.0, diff);
            return new BigNumber(m, a.Exponent);
        }

        public static BigNumber operator -(BigNumber a, BigNumber b)
        {
            return a + (-b);
        }

        public static BigNumber operator -(BigNumber a)
        {
            return a.IsZero ? Zero : new BigNumber(-a.Mantissa, a.Exponent);
        }

        public static BigNumber operator *(BigNumber a, BigNumber b)
        {
            if (a.IsZero || b.IsZero) return Zero;
            return new BigNumber(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);
        }

        public static BigNumber operator /(BigNumber a, BigNumber b)
        {
            if (b.IsZero) throw new DivideByZeroException("BigNumber: 除以零");
            if (a.IsZero) return Zero;
            return new BigNumber(a.Mantissa / b.Mantissa, a.Exponent - b.Exponent);
        }

        public static BigNumber operator *(BigNumber a, double scalar)
        {
            return a * FromDouble(scalar);
        }

        public static BigNumber operator /(BigNumber a, double scalar)
        {
            return a / FromDouble(scalar);
        }

        /// <summary>整數次方。用於「指數成長」的肉鴉效果。</summary>
        public static BigNumber Pow(BigNumber value, int exponent)
        {
            if (exponent == 0) return One;
            if (exponent < 0)
            {
                if (value.IsZero) throw new DivideByZeroException("BigNumber: 0 的負次方");
                return One / Pow(value, -exponent);
            }

            BigNumber result = One;
            BigNumber baseValue = value;
            int e = exponent;
            while (e > 0)
            {
                if ((e & 1) == 1) result = result * baseValue;
                e >>= 1;
                if (e > 0) baseValue = baseValue * baseValue;
            }
            return result;
        }

        public static BigNumber Max(BigNumber a, BigNumber b)
        {
            return a.CompareTo(b) >= 0 ? a : b;
        }

        public static BigNumber Min(BigNumber a, BigNumber b)
        {
            return a.CompareTo(b) <= 0 ? a : b;
        }

        // ---------------------------------------------------------------- 比較

        public int CompareTo(BigNumber other)
        {
            if (IsZero && other.IsZero) return 0;
            if (IsZero) return other.Mantissa > 0 ? -1 : 1;
            if (other.IsZero) return Mantissa > 0 ? 1 : -1;

            if (Mantissa > 0 && other.Mantissa < 0) return 1;
            if (Mantissa < 0 && other.Mantissa > 0) return -1;

            // 同號。負數時「指數大」代表「值更小」，所以要翻轉。
            int sign = Mantissa > 0 ? 1 : -1;
            if (Exponent != other.Exponent)
            {
                return (Exponent > other.Exponent ? 1 : -1) * sign;
            }

            // 指數相同時直接比尾數 —— 尾數本身已帶正負號，不能再乘 sign。
            return Mantissa.CompareTo(other.Mantissa);
        }

        public bool Equals(BigNumber other)
        {
            return Mantissa == other.Mantissa && Exponent == other.Exponent;
        }

        public override bool Equals(object obj)
        {
            return obj is BigNumber && Equals((BigNumber)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Mantissa.GetHashCode() * 397) ^ Exponent;
            }
        }

        public static bool operator ==(BigNumber a, BigNumber b) { return a.Equals(b); }
        public static bool operator !=(BigNumber a, BigNumber b) { return !a.Equals(b); }
        public static bool operator <(BigNumber a, BigNumber b) { return a.CompareTo(b) < 0; }
        public static bool operator >(BigNumber a, BigNumber b) { return a.CompareTo(b) > 0; }
        public static bool operator <=(BigNumber a, BigNumber b) { return a.CompareTo(b) <= 0; }
        public static bool operator >=(BigNumber a, BigNumber b) { return a.CompareTo(b) >= 0; }

        // ---------------------------------------------------------------- 轉換

        public static implicit operator BigNumber(int value) { return new BigNumber(value, 0); }
        public static implicit operator BigNumber(long value) { return new BigNumber(value, 0); }
        public static implicit operator BigNumber(double value) { return new BigNumber(value, 0); }

        // ---------------------------------------------------------------- 顯示

        private static readonly string[] Suffixes =
        {
            "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc", "Ud", "Dd"
        };

        /// <summary>
        /// 玩家看到的字串。例：1234 → "1.23K"、8.7e47 → "870Sx"、小數字 → "0.6"。
        /// ⚠️ 不要在 hot path 呼叫（會產生字串）—— 見 4.6。
        /// </summary>
        public string ToDisplayString(int decimals)
        {
            if (IsZero) return "0";

            // 一萬以下（且不是極小數）就照實顯示
            if (Exponent >= 0 && Exponent < 4)
            {
                return ApproximateValue.ToString("N" + decimals, CultureInfo.InvariantCulture);
            }

            if (Exponent > 0)
            {
                int group = Exponent / 3;
                if (group < Suffixes.Length)
                {
                    double scaled = Mantissa * Math.Pow(10.0, Exponent - group * 3);
                    return scaled.ToString("0.##", CultureInfo.InvariantCulture) + Suffixes[group];
                }
            }

            // 極大或極小 → 科學記號
            return Mantissa.ToString("0.##", CultureInfo.InvariantCulture) + "e" + Exponent;
        }

        public override string ToString()
        {
            return ToDisplayString(2);
        }
    }
}
