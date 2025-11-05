using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;


namespace RHPUFMetrics
{
    public class PUFBuilders
    {
        public enum MeasurementSet
        {
            Train,
            Test,
            Full
        }

        //============ PUFs ==============================================================================

        // PUF Bitmask Snapshot
        public static void BuildPUFBitmaskSnapshot(Dimm dimm, MeasurementSet set)
        {
            if (dimm == null)
                throw new ArgumentNullException(nameof(dimm));
            if (dimm.Stats == null)
                throw new InvalidOperationException("Device statistics not computed. Run ComputeAndAttachRegionStats first.");

            List<Measurement> src = set switch
            {
                MeasurementSet.Train => dimm.TrainMeasurements,
                MeasurementSet.Test => dimm.TestMeasurements,
                MeasurementSet.Full => dimm.Measurements,
                _ => throw new ArgumentOutOfRangeException(nameof(set))
            };


            ulong startOff = dimm.Stats.StartOffsetBest;
            ulong endOff = dimm.Stats.EndOffsetBest;


            // Create bitmask collection with each flip address
            var bitmaskCollection = new Dictionary<ulong, List<byte>>();


            foreach (var flip in src.SelectMany(m => m.Flips))
            {
                var flipAddr = flip.Addr;
                if (flipAddr > startOff && flipAddr < endOff)
                {
                    if (bitmaskCollection.TryGetValue(flipAddr, out var existing))
                        existing.Add(flip.Bitmask);
                    else
                        bitmaskCollection[flipAddr] = new List<byte> { flip.Bitmask };
                }
            }

            //Choose the reference masks (voting)
            var bitmasksFinal = new Dictionary<ulong, byte>();

            foreach (var bitmask in bitmaskCollection)
            {
                ulong addr = bitmask.Key;
                List<byte> bitmasks = bitmask.Value;

                // Compute the majority mask (combine list of bytes)
                byte majority = MajorityMask(bitmasks);

                bitmasksFinal.Add(addr, majority);
            }


            int count = checked((int)(endOff - startOff + 1));
            //var challenge = new ulong[count];
            var response = new byte[count];

            int idx = 0;
            for (ulong addr = startOff; addr <= endOff; addr++, idx++)
            {
                // challenge[idx] = addr;

                if (bitmasksFinal.TryGetValue(addr, out byte mask))
                    response[idx] = mask;
                else
                    response[idx] = 0;
            }

            dimm.PUFBitmaskSnapshot = new PUF(startOff, endOff - startOff + 1, response);

        }
        public static byte[] ExtractResponseForPUFBitmaskSnapshot(Dimm dimm, Measurement measurement, Challenge challenge)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));
            if (dimm.Stats == null) throw new InvalidOperationException("Device statistics not computed. Run ComputeAndAttachRegionStats first.");

            ulong startOff = challenge.StartAddress;
            ulong endOff = challenge.StartAddress + challenge.Length;

            // Create bitmask collection with each flip address
            var bitmaskCollection = new Dictionary<ulong, byte>();

            foreach (var flip in measurement.Flips)
            {
                var flipAddr = flip.Addr;
                if (flipAddr > startOff && flipAddr < endOff)
                {
                    if (!bitmaskCollection.TryGetValue(flipAddr, out var existing))
                        bitmaskCollection[flipAddr] = flip.Bitmask;
                }
            }

            byte[] response = new byte[challenge.Length];

            int idx = 0;
            for (ulong addr = startOff; addr < endOff; addr++, idx++)
            {
                if (bitmaskCollection.TryGetValue(addr, out byte mask))
                    response[idx] = mask;
                else
                    response[idx] = 0;
            }
            return response;
        }

        // PUF Affected Bitmasks
        public static void BuildPUFAffectedBitmasks(Dimm dimm, MeasurementSet set, int length)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));

            var ranked = RankFlippedAddresses(dimm, set);

            if (ranked.Count == 0)
            {
                Console.WriteLine($"PUFAffectedBitmasks for {dimm.Id}: 0 addresses in {set}.");
                return;
            }

            //var selected = new HashSet<ulong>(ranked.Take(length).Select(t => t.Address).ToList());

            // ------- Aggregate masks from the selected region -------
            List<Measurement> src = set switch
            {
                MeasurementSet.Train => dimm.TrainMeasurements,
                MeasurementSet.Test => dimm.TestMeasurements,
                MeasurementSet.Full => dimm.Measurements,
                _ => throw new ArgumentOutOfRangeException(nameof(set))
            };

            if (src == null || src.Count == 0)
                throw new InvalidOperationException($"PUFAffectedBitmasks for {dimm.Id}: No measurements for region {set}.");


            // Create bitmask collection with each flip address from selected
            var bitmaskCollection = new Dictionary<ulong, List<byte>>();

            foreach (var flip in src.SelectMany(m => m.Flips))
            {
                var flipAddr = flip.Addr;
                if (ranked.Take(length).Select(t => t.Address).ToList().Contains(flipAddr))
                {
                    if (flip.Bitmask != 0)
                    {
                        if (bitmaskCollection.TryGetValue(flipAddr, out var existing))
                            existing.Add(flip.Bitmask);
                        else
                            bitmaskCollection[flipAddr] = new List<byte> { flip.Bitmask };
                    }
                }
            }
            //Choose the reference masks (voting)
            var bitmasksFinal = new Dictionary<ulong, byte>();

            foreach (var bitmask in bitmaskCollection)
            {
                ulong addr = bitmask.Key;
                List<byte> bitmasks = bitmask.Value;

                // Compute the majority mask (combine list of bytes)
                byte majority = MajorityMask(bitmasks);

                bitmasksFinal.Add(addr, majority);
            }

            //Build challenge and response 

            var pairs = new List<(ulong addr, byte mask)>();
            foreach (var addr in bitmasksFinal.Keys)
            {
                if (bitmasksFinal.TryGetValue(addr, out byte mask))
                    pairs.Add((addr, mask));
            }
            pairs.Sort((a, b) => a.addr.CompareTo(b.addr));

            var challenge = pairs.Select(p => p.addr).ToArray();
            var response = pairs.Select(p => p.mask).ToArray();


            dimm.PUFAffectedBitmasks = new PUF(challenge, response);


        }
        public static byte[] ExtractResponseForPUFAffectedBitmasks(Dimm dimm, Measurement measurement, Challenge challenge)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));
            if (challenge == null) throw new ArgumentNullException(nameof(challenge));

            var bitmaskCollection = new Dictionary<ulong, byte>();
            foreach (var flip in measurement.Flips)
            {
                if (flip.Bitmask != 0)
                {
                    if (!bitmaskCollection.TryGetValue(flip.Addr, out var existing))
                        bitmaskCollection[flip.Addr] = flip.Bitmask;
                }
            }

            var response = new byte[challenge.Addresses.Length];
            for (int i = 0; i < challenge.Addresses.Length; i++)
            {
                ulong addr = challenge.Addresses[i];
                response[i] = bitmaskCollection.TryGetValue(addr, out var mask)
                    ? mask
                    : (byte)0;
            }

            return response;
        }

        // PUF Affected Addresses
        public static void BuildPUFAffectedAddresses(Dimm dimm, MeasurementSet set)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));

            List<Measurement> src = set switch
            {
                MeasurementSet.Train => dimm.TrainMeasurements,
                MeasurementSet.Test => dimm.TestMeasurements,
                MeasurementSet.Full => dimm.Measurements,
                _ => throw new ArgumentOutOfRangeException(nameof(set))
            };

            int M = src.Count;
            int threshold = (int)Math.Ceiling(M * 0.60); // must appear in ≥ 60% of measurements

            // Count in how many DISTINCT measurements each address appears.
            var addrHits = new Dictionary<ulong, int>();
            var dramByAddr = new Dictionary<ulong, FlipLocation>();

            foreach (var measurement in src)
            {
                var seenInThisMeasurement = new HashSet<ulong>();
                foreach (var f in measurement.Flips)
                {
                    ulong addr = f.Addr;
                    if (seenInThisMeasurement.Add(addr)) // count at most once per measurement
                    {
                        addrHits[addr] = addrHits.TryGetValue(addr, out var c) ? c + 1 : 1;
                        if (f.DramAddr != null && !dramByAddr.ContainsKey(addr))
                            dramByAddr[addr] = f.DramAddr; // remember first mapping
                    }
                }
            }

            // Select addresses that meet the frequency threshold, sort ascending.
            var selected = addrHits
                .Where(kv => kv.Value >= threshold)
                .Select(kv => kv.Key)
                .OrderBy(a => a)
                .ToList();

            if (selected.Count == 0)
            {
                dimm.PUFAffectedAddresses = new PUFAddress(Array.Empty<ulong>(), Array.Empty<string>());
                return;
            }

            var response = new List<string>(selected.Count);
            foreach (var addr in selected)
            {
                if (dramByAddr.TryGetValue(addr, out var da) && da != null)
                    response.Add(EncodeBRCBinary(da));
                else
                    response.Add(new string('0', 40));
            }

            dimm.PUFAffectedAddresses = new PUFAddress(selected.ToArray(), response.ToArray());

            //Console.WriteLine($"{dimm.Id}: {dimm.PUFAffectedAddresses.Response.Length} with {dimm.PUFAffectedAddresses.Response.Sum(r => r.Length)} bits (threshold {threshold}/{M}).");
        }
        public static string[] ExtractResponseForPUFAffectedAddresses(Dimm dimm, Measurement measurement, ulong[] challenge)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));

            var seen = new HashSet<ulong>();
            var dramByAddr = new Dictionary<ulong, FlipLocation>();

            foreach (var f in measurement.Flips)
            {
                ulong addr = f.Addr;
                if (seen.Add(addr))
                {
                    if (f.DramAddr != null && !dramByAddr.ContainsKey(addr))
                        dramByAddr[addr] = f.DramAddr;   // remember first mapping
                }
            }

            var response = new string[challenge.Length];
            for (int i = 0; i < challenge.Length; i++)
            {
                ulong addr = challenge[i];
                response[i] = (dramByAddr.TryGetValue(addr, out var da) && da != null)
                    ? EncodeBRCBinary(da)
                    : new string('0', 40);
            }
            return response;

        }

        // PUF Affected Addresses Short
        public static void BuildPUFAffectedAddressesShort(Dimm dimm, MeasurementSet set)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));

            List<Measurement> src = set switch
            {
                MeasurementSet.Train => dimm.TrainMeasurements,
                MeasurementSet.Test => dimm.TestMeasurements,
                MeasurementSet.Full => dimm.Measurements,
                _ => throw new ArgumentOutOfRangeException(nameof(set))
            };

            int M = src.Count;
            int threshold = (int)Math.Ceiling(M * 0.60); // must appear in ≥ 60% of measurements

            // Count in how many DISTINCT measurements each address appears.
            var addrHits = new Dictionary<ulong, int>();
            var dramByAddr = new Dictionary<ulong, FlipLocation>();

            foreach (var measurement in src)
            {
                var seenInThisMeasurement = new HashSet<ulong>();
                foreach (var f in measurement.Flips)
                {
                    ulong addr = f.Addr;
                    if (seenInThisMeasurement.Add(addr)) // count at most once per measurement
                    {
                        addrHits[addr] = addrHits.TryGetValue(addr, out var c) ? c + 1 : 1;
                        if (f.DramAddr != null && !dramByAddr.ContainsKey(addr))
                            dramByAddr[addr] = f.DramAddr; // remember first mapping
                    }
                }
            }

            // Select addresses that meet the frequency threshold, sort ascending.
            var selected = addrHits
                .Where(kv => kv.Value >= threshold)
                .Select(kv => kv.Key)
                .OrderBy(a => a)
                .ToList();

            if (selected.Count == 0)
            {
                dimm.PUFAffectedAddressesShort = new PUFAddress(Array.Empty<ulong>(), Array.Empty<string>());
                return;
            }

            var response = new List<string>(selected.Count);
            foreach (var addr in selected)
            {
                if (dramByAddr.TryGetValue(addr, out var da) && da != null)
                    response.Add(EncodeRCBinary(da));
                else
                    response.Add(new string('0', 32));
            }

            dimm.PUFAffectedAddressesShort = new PUFAddress(selected.ToArray(), response.ToArray());

        }
        public static string[] ExtractResponseForPUFAffectedAddressesShort(Dimm dimm, Measurement measurement, ulong[] challenge)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));

            var seen = new HashSet<ulong>();
            var dramByAddr = new Dictionary<ulong, FlipLocation>();

            foreach (var f in measurement.Flips)
            {
                ulong addr = f.Addr;
                if (seen.Add(addr))
                {
                    if (f.DramAddr != null && !dramByAddr.ContainsKey(addr))
                        dramByAddr[addr] = f.DramAddr;
                }
            }

            var response = new string[challenge.Length];
            for (int i = 0; i < challenge.Length; i++)
            {
                ulong addr = challenge[i];
                response[i] = (dramByAddr.TryGetValue(addr, out var da) && da != null)
                    ? EncodeRCBinary(da)
                    : new string('0', 32);
            }
            return response;
        }

        // PUF Flip Existance
        public static void BuildPUFFlipExistance(Dimm dimm, MeasurementSet set)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));

            List<Measurement> src = set switch
            {
                MeasurementSet.Train => dimm.TrainMeasurements,
                MeasurementSet.Test => dimm.TestMeasurements,
                MeasurementSet.Full => dimm.Measurements,
                _ => throw new ArgumentOutOfRangeException(nameof(set))
            };

            if (src == null || src.Count == 0)
                throw new InvalidOperationException($"No measurements for region {set} for {dimm.Id}.");

            var startOff = dimm.Stats.StartOffsetBest;
            var endOff = dimm.Stats.EndOffsetBest;

            var flipped = new HashSet<ulong>(
                src.SelectMany(m => m.Flips)
                   .Select(f => f.Addr)
                   .Where(off => off >= startOff && off <= endOff));

            checked
            {
                int nBits = (int)(endOff - startOff + 1);   // one bit per address
                var challenge = new ulong[nBits];
                var response = new byte[(nBits + 7) >> 3];    // ceil(nBits/8)

                ulong off = startOff;
                for (int i = 0; i < nBits; i++, off++)
                {
                    challenge[i] = off;

                    if (flipped.Contains(off))
                    {
                        int byteIdx = i >> 3;
                        int bitIdx = i & 7;
                        response[byteIdx] |= (byte)(1 << bitIdx);
                    }
                }
                dimm.PUFFlipExistance = new BitPUF(startOff, endOff - startOff + 1, challenge, response, nBits);
            }
        }
        public static byte[] ExtractResponseForPUFFlipExistance(Dimm dimm, Measurement measurement, Challenge challenge)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));

            ulong startOff = challenge.StartAddress;
            ulong endOff = challenge.StartAddress + challenge.Length - 1;

            var flipped = new HashSet<ulong>();
            foreach (var f in measurement.Flips)
            {
                if (f.Bitmask != 0)
                {
                    if (f.Addr >= startOff && f.Addr <= endOff)
                        flipped.Add(f.Addr);
                }
            }

            checked
            {
                int nBits = (int)(endOff - startOff + 1);
                var response = new byte[(nBits + 7) >> 3];

                ulong addr = startOff;
                for (int i = 0; i < nBits; i++, addr++)
                {
                    if (flipped.Contains(addr))
                    {
                        int byteIdx = i >> 3;
                        int bitIdx = i & 7;
                        response[byteIdx] |= (byte)(1 << bitIdx);
                    }
                }
                return response;
            }
        }

        // PUF Flip Combo
        public static void BuildPUFFlipCombo(Dimm dimm, MeasurementSet set)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));

            // pick measurement set
            var src = set switch
            {
                MeasurementSet.Train => dimm.TrainMeasurements,
                MeasurementSet.Test => dimm.TestMeasurements,
                MeasurementSet.Full => dimm.Measurements,
                _ => throw new ArgumentOutOfRangeException(nameof(set))
            };
            if (src == null || src.Count == 0) throw new InvalidOperationException($"No measurements for set {set}.");

            ulong startOff = dimm.Stats.StartOffsetBest;
            ulong endOff = dimm.Stats.EndOffsetBest;

            var v1 = new Dictionary<ulong, int>(); // post=1 ⇒ 0→1
            var v0 = new Dictionary<ulong, int>(); // post=0 ⇒ 1→0

            foreach (var f in src.SelectMany(m => m.Flips))
            {
                ulong off = f.Addr;
                if (off >= startOff && off <= endOff)
                {
                    byte mask = f.Bitmask;
                    if (mask != 0)
                    {
                        byte data = f.Data;
                        for (int b = 0; b < 8; b++)
                        {
                            if (((mask >> b) & 1) != 0)
                            {
                                int post = (data >> b) & 1;
                                if (post == 1)
                                    v1[off] = v1.TryGetValue(off, out var a) ? a + 1 : 1; // 0→1
                                else
                                    v0[off] = v0.TryGetValue(off, out var a) ? a + 1 : 1; // 1→0
                            }
                        }
                    }
                }
            }

            // Build addresses + 2-bit response for each address 
            int n = checked((int)(endOff - startOff + 1));
            var addresses = new ulong[n];
            var response = new byte[((n * 2) + 7) >> 3]; // 2 bits per address

            for (int i = 0; i < n; i++)
            {
                ulong off = startOff + (ulong)i;
                addresses[i] = off;

                byte sym; // 00=no flips, 10=1→0, 11=0→1
                v1.TryGetValue(off, out int c1);
                v0.TryGetValue(off, out int c0);

                if (c1 == 0 && c0 == 0)
                    sym = 0b00;
                else
                    sym = (c1 >= c0) ? (byte)0b11 : (byte)0b10; // tie → 11

                int pos = 2 * i; // write LSB-first across stream
                if ((sym & 0b01) != 0)
                    response[pos >> 3] |= (byte)(1 << (pos & 7));
                if ((sym & 0b10) != 0)
                {
                    int pos1 = pos + 1;
                    response[pos1 >> 3] |= (byte)(1 << (pos1 & 7));
                }
            }
            dimm.PUFFlipCombo = new Bit2PUF(startOff, endOff - startOff + 1, addresses, response, n);

            // Summary
            int cnt11 = 0, cnt10 = 0;
            for (int i = 0; i < n; i++)
            {
                int pos = 2 * i;
                int b0 = (response[pos >> 3] >> (pos & 7)) & 1;
                int b1 = (response[(pos + 1) >> 3] >> ((pos + 1) & 7)) & 1;
                if (b1 == 1 && b0 == 1) cnt11++;
                else if (b1 == 1 && b0 == 0) cnt10++;
            }
            // Console.WriteLine($"{dimm.Id}: {n} addresses | 11={cnt11}, 10={cnt10}, 00={n - cnt11 - cnt10} ");
        }
        public static byte[] ExtractResponseForPUFFlipCombo(Dimm dimm, Measurement measurement, Challenge challenge)
        {
            if (challenge == null) throw new ArgumentNullException(nameof(challenge));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));

            ulong startOff = challenge.StartAddress;
            ulong endOff = challenge.StartAddress + challenge.Length - 1;

            // v1 = count of flipped bit positions observed as 1 (0->1)
            // v0 = count of flipped bit positions observed as 0 (1->0)
            var v1 = new Dictionary<ulong, int>();
            var v0 = new Dictionary<ulong, int>();

            foreach (var f in measurement.Flips)
            {
                byte mask = f.Bitmask;
                if (mask != 0)
                {
                    ulong addr = f.Addr;
                    byte data = f.Data;

                    if (addr >= startOff && addr <= endOff)
                    {
                        for (int b = 0; b < 8; b++)
                        {
                            if (((mask >> b) & 1) != 0)
                            {
                                int post = (data >> b) & 1;
                                if (post == 1)
                                    v1[addr] = v1.TryGetValue(addr, out var a) ? a + 1 : 1; // 0->1
                                else
                                    v0[addr] = v0.TryGetValue(addr, out var a) ? a + 1 : 1; // 1->0
                            }
                        }
                    }
                }
            }

            int l = challenge.Addresses.Length;
            var responseLength = ((l * 2) + 7) >> 3;
            var response = new byte[responseLength]; // 2 bits per address


            int i = 0;
            for (ulong addr = startOff; addr <= endOff; addr++, i++)
            {
                v1.TryGetValue(addr, out int c1);
                v0.TryGetValue(addr, out int c0);

                // Encode symbol:
                // 00 = no flips
                // 11 = majority 0->1  (c1 >= c0)  (tie -> 11)
                // 10 = majority 1->0
                byte sym = (c1 == 0 && c0 == 0) ? (byte)0b00
                         : (c1 >= c0) ? (byte)0b11
                                                : (byte)0b10;


                int pos = 2 * i;
                if ((sym & 0b01) != 0) response[pos >> 3] |= (byte)(1 << (pos & 7));
                if ((sym & 0b10) != 0) response[(pos + 1) >> 3] |= (byte)(1 << ((pos + 1) & 7));
            }

            // Summary
            int cnt11 = 0, cnt10 = 0;
            for (int j = 0; j < l; j++)
            {
                int pos = 2 * j;
                int b0 = (response[pos >> 3] >> (pos & 7)) & 1;
                int b1 = (response[(pos + 1) >> 3] >> ((pos + 1) & 7)) & 1;
                if (b1 == 1 && b0 == 1) cnt11++;
                else if (b1 == 1 && b0 == 0) cnt10++;
            }
            //  Console.WriteLine($"{dimm.Id}: {l} addresses | 11={cnt11}, 10={cnt10}, 00={l - cnt11 - cnt10} ");

            return response;
        }

        // PUF Flip Direction
        public static void BuildPUFFlipDirection(Dimm dimm, MeasurementSet set)
        {
            if (dimm == null) throw new ArgumentNullException(nameof(dimm));

            var src = set switch
            {
                MeasurementSet.Train => dimm.TrainMeasurements,
                MeasurementSet.Test => dimm.TestMeasurements,
                MeasurementSet.Full => dimm.Measurements,
                _ => throw new ArgumentOutOfRangeException(nameof(set))
            };
            if (src == null || src.Count == 0)
                throw new InvalidOperationException($"No measurements for set {set} of device {dimm.Id}.");

            // Addresses that appear in ≥60% of measurements (distinct-measurement hits)
            int M = src.Count;
            int threshold = Math.Max(1, (int)Math.Ceiling(M * 0.60));

            var hitCount = new Dictionary<ulong, int>();
            foreach (var meas in src)
            {
                var seen = new HashSet<ulong>();
                foreach (var flip in meas.Flips)
                {
                    ulong addr = flip.Addr;
                    if (seen.Add(addr))
                        hitCount[addr] = hitCount.TryGetValue(addr, out var c) ? c + 1 : 1;
                }
            }
            // Stable addresses, sorted asc
            var selected = hitCount.Where(kv => kv.Value >= threshold)
                                   .Select(kv => kv.Key)
                                   .OrderBy(a => a)
                                   .ToList();

            if (selected.Count == 0)
            {
                dimm.PUFFlipDirection = new BitPUF(0, 0, Array.Empty<ulong>(), Array.Empty<byte>(), 0);
                Console.WriteLine($"PUFFlipDirection: no stable addresses (≥{threshold}/{M}) of device {dimm.Id}.");
                return;
            }
            var selectedSet = new HashSet<ulong>(selected);
            // Accumulate flip-direction votes per address. For each flipped bit position: observed_bit == 1 → vote1++; else vote0++
            var votes1 = new Dictionary<ulong, int>();
            var votes0 = new Dictionary<ulong, int>();

            foreach (var flip in src.SelectMany(m => m.Flips))
            {
                ulong addr = flip.Addr;
                if (selectedSet.Contains(addr))
                {
                    byte mask = flip.Bitmask;
                    if (mask != 0)
                    {
                        byte data = flip.Data;
                        for (int b = 0; b < 8; b++)
                        {
                            if (((mask >> b) & 1) == 0) continue;

                            int observedBit = (data >> b) & 1;   // post-flip bit
                            if (observedBit == 1)
                                votes1[addr] = votes1.TryGetValue(addr, out var v) ? v + 1 : 1; // 0→1
                            else
                                votes0[addr] = votes0.TryGetValue(addr, out var v) ? v + 1 : 1; // 1→0
                        }
                    }
                    ;
                }
            }

            int nBits = selected.Count;
            var challenge = selected.ToArray();
            var response = new byte[(nBits + 7) >> 3];       // ceil(nBits/8)

            for (int i = 0; i < nBits; i++)
            {
                ulong addr = selected[i];
                votes1.TryGetValue(addr, out int v1);
                votes0.TryGetValue(addr, out int v0);

                int bit = (v1 >= v0) ? 1 : 0;                // tie -> 1 (deterministic)
                if (bit == 1)
                {
                    int byteIdx = i >> 3;
                    int bitIdx = i & 7;
                    response[byteIdx] |= (byte)(1 << bitIdx);
                }
            }

            dimm.PUFFlipDirection = new BitPUF(challenge, response, nBits);

            //Summary
            //int ones = 0;
            // foreach (var b in response) ones += BitOperations.PopCount((uint)b);
            // Console.WriteLine($"PUFFlipDirection built: {nBits} addresses (stable ≥{threshold}/{M}) | ones={ones} ({ones / (double)nBits:P2}) ");
        }
        public static byte[] ExtractResponseForPUFFlipDirection(Dimm dimm, Measurement measurement, Challenge challenge)
        {
            if (challenge == null) throw new ArgumentNullException(nameof(challenge));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));

            // Count per-address votes within THIS measurement:
            // vote1 += number of flipped bit-positions observed as 1  (0->1)
            // vote0 += number of flipped bit-positions observed as 0  (1->0)
            var vote1 = new Dictionary<ulong, int>();
            var vote0 = new Dictionary<ulong, int>();

            foreach (var f in measurement.Flips)
            {
                ulong addr = f.Addr;

                byte mask = f.Bitmask;
                if (mask == 0) continue;

                byte data = f.Data;
                for (int b = 0; b < 8; b++)
                {
                    if (((mask >> b) & 1) == 0) continue;

                    int observedBit = (data >> b) & 1;
                    if (observedBit == 1)
                        vote1[addr] = vote1.TryGetValue(addr, out var v) ? v + 1 : 1; // 0->1
                    else
                        vote0[addr] = vote0.TryGetValue(addr, out var v) ? v + 1 : 1; // 1->0
                }
            }

            int nBits = challenge.Addresses.Length;
            var response = new byte[(nBits + 7) >> 3];

            for (int i = 0; i < nBits; i++)
            {
                ulong addr = challenge.Addresses[i];

                vote1.TryGetValue(addr, out int v1);
                vote0.TryGetValue(addr, out int v0);

                int bit;
                if (v1 == 0 && v0 == 0)
                {
                    bit = 0;
                }
                else
                {
                    bit = (v1 >= v0) ? 1 : 0;
                }

                if (bit == 1)
                {
                    int byteIdx = i >> 3;
                    int bitIdx = i & 7;
                    response[byteIdx] |= (byte)(1 << bitIdx);
                }
            }
            return response;
        }

        //========== Helpers ===========================================
        public static void FindMaxAddressCoverageWindow2KB(Dimm dimm, MeasurementSet region)
        {
            const ulong Window = 2048;                         // 2 KB
            const ulong MemStart = 0;
            const ulong MemSize = 1024UL * 1024 * 1024;          // 1 GB
            const ulong MemEndOff = MemStart + MemSize - 1;
            const ulong LatestStart = MemEndOff - (Window - 1);

            List<Measurement> src = region switch
            {
                MeasurementSet.Train => dimm.TrainMeasurements,
                MeasurementSet.Test => dimm.TestMeasurements,
                MeasurementSet.Full => dimm.Measurements,
                _ => throw new ArgumentOutOfRangeException(nameof(region))
            };

            // Build per-bank address sets 
            var perBankSets = new Dictionary<int, HashSet<ulong>>();

            foreach (var f in src.SelectMany(m => m.Flips))
            {
                if (f.DramAddr == null) continue; // need bank info
                int bank = f.DramAddr.Bank;

                ulong addr = f.Addr;
                if (addr > MemStart || addr < MemEndOff)
                {
                    if (!perBankSets.TryGetValue(bank, out var set))
                        perBankSets[bank] = set = new HashSet<ulong>();

                    set.Add(addr); // unique offsets within this bank
                }
            }

            // For each bank, sort offsets and do a sliding-window max coverage over 2KB 
            ulong bestStart = MemStart;
            ulong bestEnd = Math.Min(MemStart + Window - 1, MemEndOff);
            int bestCount = -1;
            int bestBank = -1;

            foreach (var kv in perBankSets)
            {
                int bank = kv.Key;
                var offs = kv.Value.ToList();
                offs.Sort();

                if (offs.Count != 0)
                {

                    int left = 0;
                    for (int i = 0; i < offs.Count; i++)
                    {
                        // Clamp start into legal range
                        ulong start = offs[i];
                        if (start < MemStart) start = MemStart;
                        if (start > LatestStart) start = LatestStart;

                        ulong end = start + Window - 1;

                        // Advance left to first offset >= start
                        while (left < offs.Count && offs[left] < start) left++;

                        // Find rightmost index with offs[right] <= end (two-pointer)
                        int right = left;
                        while (right < offs.Count && offs[right] <= end) right++;

                        int count = Math.Max(0, right - left);

                        // Keep best (tie-break by smaller start, then smaller bank)
                        if (count > bestCount ||
                           (count == bestCount && start < bestStart) ||
                           (count == bestCount && start == bestStart && bank < bestBank))
                        {
                            bestCount = count;
                            bestStart = start;
                            bestEnd = end;
                            bestBank = bank;
                        }
                    }
                }
            }
            dimm.Stats.FlipsInWindow = bestCount;
            dimm.Stats.StartOffsetBest = bestStart;
            dimm.Stats.EndOffsetBest = bestEnd;

        }
        public static List<(ulong Address, int FlipCount)> RankFlippedAddresses(Dimm dimm, MeasurementSet region)
        {
            if (dimm == null)
                throw new ArgumentNullException(nameof(dimm));

            List<Measurement> src = region switch
            {
                MeasurementSet.Train => dimm.TrainMeasurements,
                MeasurementSet.Test => dimm.TestMeasurements,
                MeasurementSet.Full => dimm.Measurements,
                _ => throw new ArgumentOutOfRangeException(nameof(region))
            };
            // Dictionary: address → total number of flipped bits
            var flipsByAddr = new Dictionary<ulong, int>();

            foreach (var flip in src.SelectMany(m => m.Flips))
            {
                ulong addr = flip.Addr;

                // Count 1-bits in mask (how many bits flipped in that byte)
                int bitCount = BitOperations.PopCount(flip.Bitmask);
                if (bitCount != 0)
                {
                    if (flipsByAddr.TryGetValue(addr, out var existing))
                        flipsByAddr[addr] = existing + bitCount;
                    else
                        flipsByAddr[addr] = bitCount;
                }
            }

            // Sort by number of flips descending, then address ascending
            var ranked = flipsByAddr
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
            return ranked;
        }
        static string EncodeBRCBinary(FlipLocation da)
        {
            uint b = (uint)da.Bank & 0xFF;     // 8 bits
            uint r = (uint)da.Row & 0xFFFF;   // 16 bits
            uint c = (uint)da.Col & 0xFFFF;   // 16 bits
            ulong combined = ((ulong)b << 32) | ((ulong)r << 16) | c; // 40 bits
            return Convert.ToString((long)combined, 2).PadLeft(40, '0'); // no separators
        }
        static string EncodeRCBinary(FlipLocation da)
        {
            uint r = (uint)da.Row & 0xFFFF;   // 16 bits
            uint c = (uint)da.Col & 0xFFFF;   // 16 bits
            string rBits = Convert.ToString(r, 2).PadLeft(16, '0');
            string cBits = Convert.ToString(c, 2).PadLeft(16, '0');
            return rBits + cBits;
        }
        static byte MajorityMask(List<byte> values)
        {
            if (values == null || values.Count == 0)
                return 0;

            byte result = 0;

            for (int bit = 0; bit < 8; bit++)
            {
                int countOnes = 0;
                foreach (var v in values)
                {
                    if (((v >> bit) & 1) == 1)
                        countOnes++;
                }

                // if more than half are 1s, set bit
                if (countOnes > values.Count / 2)
                    result |= (byte)(1 << bit);
            }

            return result;
        }

 
    }

}
