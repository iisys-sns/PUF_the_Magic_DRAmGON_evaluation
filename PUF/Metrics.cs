using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;


namespace RHPUFMetrics
{
    public static class Metrics
    {


        //=======================================Uniformity=======================================

        //Fractional Hamming Weight

        public static double FractionalHammingWeight(byte[] response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            int nbits = response.Length * 8;
            if (nbits == 0) return 0.0;

            int ones = 0;
            foreach (byte b in response)
                ones += BitOperations.PopCount(b);

            return ones / (double)nbits;
        }
        public static double FractionalHammingWeight(string[] response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (response.Length == 0) return 0.0;

            long ones = 0, total = 0;

            foreach (var s in response)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    total += s.Length;
                    foreach (char ch in s)
                        if (ch == '1') ones++;
                }
            }
            return total == 0 ? 0.0 : ones / (double)total;
        }

        //Entropy
        public static double BitEntropy(byte[] response)
        {
            if (response == null || response.Length == 0) return 0.0;

            int ones = 0;
            int totalBits = response.Length * 8;

            foreach (byte b in response)
                ones += BitOperations.PopCount(b);

            double p1 = ones / (double)totalBits;
            double p0 = 1.0 - p1;

            double H = 0.0;
            if (p0 > 0) H -= p0 * Math.Log(p0, 2);
            if (p1 > 0) H -= p1 * Math.Log(p1, 2);
            return H;
        }
        public static double BitEntropy(string[] response)
        {
            if (response == null || response.Length == 0)
                return 0.0;

            string bits = string.Concat(response);
            if (bits.Length == 0)
                return 0.0;

            int ones = 0;
            foreach (char c in bits)
                if (c == '1') ones++;

            int total = bits.Length;
            double p1 = ones / (double)total;
            double p0 = 1.0 - p1;

            double H = 0.0;
            if (p0 > 0) H -= p0 * Math.Log(p0, 2);
            if (p1 > 0) H -= p1 * Math.Log(p1, 2);

            return H; // bits of entropy per bit, max = 1.0
        }


        //=======================================Reliability+Uniqness=======================================

        // Jaccard Index
        public static double JaccardIndexBits(byte[] a, byte[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException();

            int minLen = Math.Min(a.Length, b.Length);
            if (minLen == 0 && a.Length == 0 && b.Length == 0) return 0.0;

            int intersection = 0;
            int union = 0;

            for (int i = 0; i < minLen; i++)
            {
                byte x = a[i];
                byte y = b[i];
                byte commonBits = (byte)(x & y);
                byte unionBits = (byte)(x | y);

                intersection += BitOperations.PopCount(commonBits);
                union += BitOperations.PopCount(unionBits);
            }

            if (a.Length > minLen)
            {
                for (int i = minLen; i < a.Length; i++)
                    union += BitOperations.PopCount(a[i]);
            }
            else if (b.Length > minLen)
            {
                for (int i = minLen; i < b.Length; i++)
                    union += BitOperations.PopCount(b[i]);
            }

            return union == 0 ? 0.0 : (double)intersection / union;
        }
        public static double JaccardIndex(string[] a, string[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException();

            string sa = string.Concat(a);
            string sb = string.Concat(b);

            int minLen = Math.Min(sa.Length, sb.Length);
            if (minLen == 0) return 0.0;

            int intersection = 0;
            int union = 0;

            for (int i = 0; i < sa.Length; i++)
            {
                bool ba = sa[i] == '1';
                bool bb = sb[i] == '1';
                if (ba || bb) union++;
                if (ba && bb) intersection++;
            }

            if (sa.Length > minLen)
                union += sa.Skip(minLen).Count(ch => ch == '1');
            else if (sb.Length > minLen)
                union += sb.Skip(minLen).Count(ch => ch == '1');

            return union == 0 ? 0.0 : (double)intersection / union;
        }

        //Hamming Distance
        public static double FractionalHammingDistance(byte[] a, byte[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException();

            int minLength = Math.Min(a.Length, b.Length);
            int maxLength = Math.Max(a.Length, b.Length);
            int lenDiff = maxLength - minLength;

            double distance = 0.0;

            for (int i = 0; i < minLength; i++)
            {
                byte aa = i < a.Length ? a[i] : (byte)0;
                byte bb = i < b.Length ? b[i] : (byte)0;
                byte diff = (byte)(aa ^ bb);

                distance += BitOperations.PopCount(diff);
            }
            return (distance + lenDiff) / (maxLength * 8);
        }
        public static double FractionalHammingDistance(string[] a, string[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException();

            string sa = string.Concat(a);
            string sb = string.Concat(b);

            var maxLength = Math.Max(sa.Length, sb.Length);
            var minLength = Math.Min(sa.Length, sb.Length);
            var diff = maxLength - minLength;

            double distance = 0.0;

            for (int i = 0; i < minLength; i++)
            {
                if (sa[i] != sb[i])
                    distance++;
            }

            return (distance + diff) / maxLength;
        }

        // Sörensen-Dice coefficient
        public static double Dice(byte[] a, byte[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException();

            int maxLen = Math.Max(a.Length, b.Length);
            int onesA = 0, onesB = 0, intersection = 0;

            for (int i = 0; i < maxLen; i++)
            {
                byte aa = i < a.Length ? a[i] : (byte)0;
                byte bb = i < b.Length ? b[i] : (byte)0;

                onesA += BitOperations.PopCount(aa);
                onesB += BitOperations.PopCount(bb);
                intersection += BitOperations.PopCount((byte)(aa & bb));
            }

            int union = onesA + onesB;
            return union == 0 ? 0.0 : (2.0 * intersection) / union;
        }
        public static double Dice(string[] a, string[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException();

            string sa = string.Concat(a);
            string sb = string.Concat(b);

            int len = Math.Max(sa.Length, sb.Length);
            if (len == 0) return 0.0;

            int onesA = 0, onesB = 0, intersection = 0;

            for (int i = 0; i < len; i++)
            {
                bool a1 = i < sa.Length && sa[i] == '1';
                bool b1 = i < sb.Length && sb[i] == '1';
                if (a1) onesA++;
                if (b1) onesB++;
                if (a1 && b1) intersection++;
            }

            int union = onesA + onesB;
            return union == 0 ? 0.0 : (2.0 * intersection) / union;
        }

        //Cosine similarity

        public static double Cosine(byte[] a, byte[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException();

            int maxLen = Math.Max(a.Length, b.Length);
            int onesA = 0, onesB = 0, intersection = 0;

            for (int i = 0; i < maxLen; i++)
            {
                byte aa = i < a.Length ? a[i] : (byte)0;
                byte bb = i < b.Length ? b[i] : (byte)0;

                onesA += BitOperations.PopCount(aa);
                onesB += BitOperations.PopCount(bb);
                intersection += BitOperations.PopCount((byte)(aa & bb));
            }

            if (onesA == 0 || onesB == 0) return 0.0;
            return intersection / Math.Sqrt(onesA * onesB);
        }
        public static double Cosine(string[] a, string[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException();

            string sa = string.Concat(a);
            string sb = string.Concat(b);

            int len = Math.Max(sa.Length, sb.Length);
            int onesA = 0, onesB = 0, intersection = 0;

            for (int i = 0; i < len; i++)
            {
                bool a1 = i < sa.Length && sa[i] == '1';
                bool b1 = i < sb.Length && sb[i] == '1';

                if (a1) onesA++;
                if (b1) onesB++;
                if (a1 && b1) intersection++;
            }

            if (onesA == 0 || onesB == 0) return 0.0;
            return intersection / Math.Sqrt(onesA * onesB);
        }

        //Probit Pobabilites P and Z
        public static (double[] p, double[] z) ProbitProbabilities(byte[] reference, List<byte[]> responses)
        {
            if (reference == null || responses == null || responses.Count == 0) throw new ArgumentNullException();

            int nBits = reference.Length * 8;
            var flipCounts = new int[nBits];

            foreach (var resp in responses)
            {
                for (int i = 0; i < reference.Length; i++)
                {
                    byte rr = (i < resp.Length) ? resp[i] : reference[i];
                    byte diff = (byte)(rr ^ reference[i]);

                    for (int b = 0; b < 8; b++)
                    {
                        if (((diff >> b) & 1) == 1)
                            flipCounts[i * 8 + b]++;
                    }
                }
            }

            int N = responses.Count;
            var p = new double[nBits];
            var z = new double[nBits];

            for (int i = 0; i < nBits; i++)
            {
                double pi = (flipCounts[i] + 0.5) / (N + 1.0);
                p[i] = pi;
                z[i] = Probit(pi);
            }

            return (p, z);
        }
        public static (double[] p, double[] z) ProbitProbabilities(string[] reference, List<string[]> measurements)
        {
            if (reference == null || measurements == null || measurements.Count == 0) throw new ArgumentNullException();

            string refS = string.Concat(reference);
            int maxLen = refS.Length;

            if (refS.Length < maxLen)
                refS = refS + new string('0', maxLen - refS.Length);

            var flips = new int[maxLen];

            foreach (var m in measurements)
            {
                string ms = string.Concat(m);

                int n = Math.Min(maxLen, ms.Length);

                for (int i = 0; i < n; i++)
                {
                    if (refS[i] != ms[i])
                        flips[i]++;
                }
            }

            int N = measurements.Count;
            var p = new double[maxLen];
            var z = new double[maxLen];

            for (int i = 0; i < maxLen; i++)
            {
                double pi = (flips[i] + 0.5) / (N + 1.0);
                p[i] = pi;
                z[i] = Probit(pi);
            }

            return (p, z);
        }

   


        //Helpers

        /// <summary>
        /// Probit function: inverse CDF (quantile) of the standard normal distribution.
        /// For p in (0,1), returns z such that Phi(z) = p.
        /// Uses Acklam's rational approximation with split regions for accuracy in tails.
        /// </summary>
        public static double Probit(double p)
        {
            if (p <= 0.0) return double.NegativeInfinity;
            if (p >= 1.0) return double.PositiveInfinity;

            // Acklam’s approximation
            double[] a = { -39.6968302866538, 220.946098424521, -275.928510446969,
                    138.357751867269, -30.6647980661472, 2.50662827745924 };
            double[] b = { -54.4760987982241, 161.585836858041, -155.698979859887,
                    66.8013118877197, -13.2806815528857 };
            double[] c = { -0.00778489400243029, -0.322396458041136, -2.40075827716184,
                   -2.54973253934373, 4.37466414146497, 2.93816398269878 };
            double[] d = { 0.00778469570904146, 0.322467129070040, 2.44513413714299,
                   3.75440866190742 };

            double q, r;
            if (p < 0.02425)
            {
                q = Math.Sqrt(-2 * Math.Log(p));
                return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                       ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
            }
            else if (p > 1 - 0.02425)
            {
                q = Math.Sqrt(-2 * Math.Log(1 - p));
                return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                         ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
            }
            else
            {
                q = p - 0.5; r = q * q;
                return (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q /
                       (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
            }
        }
    }


}
