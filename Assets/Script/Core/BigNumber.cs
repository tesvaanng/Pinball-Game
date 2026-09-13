using System;

namespace Pinball.Core
{
    [Serializable]
    public struct BigNumber
    {
        public double mantissa;
        public int exponent;

        public BigNumber(double mantissa, int exponent)
        {
            if (mantissa <= 0 || double.IsNaN(mantissa) || double.IsInfinity(mantissa))
            {
                this.mantissa = 0;
                this.exponent = 0;
                return;
            }

            while (mantissa >= 10.0)
            {
                mantissa /= 10.0;
                exponent++;
            }

            while (mantissa < 1.0)
            {
                mantissa *= 10.0;
                exponent--;
            }

            this.mantissa = mantissa;
            this.exponent = exponent;
        }

        public static BigNumber Zero
        {
            get { return new BigNumber(0, 0); }
        }

        public static BigNumber One
        {
            get { return new BigNumber(1, 0); }
        }

        public static BigNumber FromDouble(double value)
        {
            return new BigNumber(value, 0);
        }

        public static implicit operator BigNumber(int value)
        {
            return new BigNumber(value, 0);
        }

        public static implicit operator BigNumber(float value)
        {
            return new BigNumber(value, 0);
        }

        public static implicit operator BigNumber(double value)
        {
            return new BigNumber(value, 0);
        }

        public static BigNumber operator +(BigNumber a, BigNumber b)
        {
            if (a.mantissa == 0) return b;
            if (b.mantissa == 0) return a;

            if (a.exponent < b.exponent)
            {
                BigNumber temp = a;
                a = b;
                b = temp;
            }

            int diff = a.exponent - b.exponent;
            if (diff > 17) return a;

            double m = a.mantissa + b.mantissa * Math.Pow(10, -diff);
            return new BigNumber(m, a.exponent);
        }

        public static BigNumber operator -(BigNumber a, BigNumber b)
        {
            if (b.mantissa == 0) return a;
            if (a.mantissa == 0) return Zero;
            if (a.exponent < b.exponent) return Zero;

            int diff = a.exponent - b.exponent;
            if (diff > 17) return a;

            double m = a.mantissa - b.mantissa * Math.Pow(10, -diff);
            if (m <= 0) return Zero;
            return new BigNumber(m, a.exponent);
        }

        public static BigNumber operator *(BigNumber a, BigNumber b)
        {
            if (a.mantissa == 0 || b.mantissa == 0) return Zero;
            return new BigNumber(a.mantissa * b.mantissa, a.exponent + b.exponent);
        }

        public static BigNumber operator /(BigNumber a, BigNumber b)
        {
            if (a.mantissa == 0 || b.mantissa == 0) return Zero;
            return new BigNumber(a.mantissa / b.mantissa, a.exponent - b.exponent);
        }

        public static bool operator >(BigNumber a, BigNumber b)
        {
            return Compare(a, b) > 0;
        }

        public static bool operator <(BigNumber a, BigNumber b)
        {
            return Compare(a, b) < 0;
        }

        public static bool operator >=(BigNumber a, BigNumber b)
        {
            return Compare(a, b) >= 0;
        }

        public static bool operator <=(BigNumber a, BigNumber b)
        {
            return Compare(a, b) <= 0;
        }

        public static bool operator ==(BigNumber a, BigNumber b)
        {
            return a.mantissa == b.mantissa && a.exponent == b.exponent;
        }

        public static bool operator !=(BigNumber a, BigNumber b)
        {
            return !(a == b);
        }

        public static int Compare(BigNumber a, BigNumber b)
        {
            if (a.mantissa == 0) return b.mantissa == 0 ? 0 : -1;
            if (b.mantissa == 0) return 1;
            if (a.exponent != b.exponent) return a.exponent.CompareTo(b.exponent);
            return a.mantissa.CompareTo(b.mantissa);
        }

        public double ToDouble()
        {
            if (mantissa == 0) return 0;
            if (exponent > 308) return double.PositiveInfinity;
            if (exponent < -324) return 0;
            return mantissa * Math.Pow(10, exponent);
        }

        public int ToIntClamped(int max)
        {
            if (mantissa == 0 || max <= 0) return 0;
            double value = ToDouble();
            if (value <= 0) return 0;
            if (value >= max) return max;
            return (int)value;
        }

        public string ToDisplayString()
        {
            if (mantissa == 0) return "0";

            double value = ToDouble();

            if (value < 1000)
            {
                return value.ToString("0.##");
            }

            int tier = exponent / 3;
            if (tier >= 1 && tier <= 4)
            {
                double scaled = mantissa * Math.Pow(10, exponent - tier * 3);
                string suffix = "K";

                if (tier == 2)
                {
                    suffix = "M";
                }
                else if (tier == 3)
                {
                    suffix = "B";
                }
                else if (tier == 4)
                {
                    suffix = "T";
                }

                return scaled.ToString("0.##") + suffix;
            }

            return mantissa.ToString("0.##") + "e" + exponent;
        }

        public string ToScientificString()
        {
            if (mantissa == 0) return "0";
            return mantissa.ToString("0.##") + "e" + exponent;
        }

        public override string ToString()
        {
            return ToDisplayString();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BigNumber)) return false;
            return this == (BigNumber)obj;
        }

        public override int GetHashCode()
        {
            return mantissa.GetHashCode() ^ exponent;
        }
    }
}
