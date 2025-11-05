using Newtonsoft.Json;
using RHPUFMetrics;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Text;
using static RHPUFMetrics.BitPUF;

namespace RHPUFMetrics
{
    public class Dimm
    {
        public int Id { get; set; }
        public List<Measurement> Measurements { get; set; } = new();
        public List<Measurement> TrainMeasurements { get; set; } = new();
        public List<Measurement> TestMeasurements { get; set; } = new();
        public RegionStats Stats { get; set; } = new();
        public PUF PUFBitmaskSnapshot { get; set; }
        public PUF PUFAffectedBitmasks { get; set; }
        public PUFAddress PUFAffectedAddresses { get; set; }
        public PUFAddress PUFAffectedAddressesShort { get; set; }
        public BitPUF PUFFlipExistance { get; set; }
        public BitPUF PUFFlipDirection { get; set; }
        public Bit2PUF PUFFlipCombo { get; set; }
        public Dimm(int id)
        {
            Id = id;
        }


    }

    public class Measurement
    {
        public int Total { get; set; }
        public int OneToZero { get; set; }
        public int ZeroToOne { get; set; }
        public List<Flip> Flips { get; set; } = new();

    }

    public class Flip
    {
        public ulong Addr { get; set; }
        public byte Bitmask { get; set; }
        public byte Data { get; set; }
        public long ObservedAt { get; set; }
        public int PageOffset { get; set; }
        public FlipLocation DramAddr { get; set; }

    }

    public class FlipLocation
    {
        public int Bank { get; set; }
        public int Col { get; set; }
        public int Row { get; set; }

    }

    public class RegionStats
    {
        public ulong StartOffsetBest { get; set; }
        public ulong EndOffsetBest { get; set; }
        public int FlipsInWindow { get; set; }
    }



    public sealed class PUF
    {
        public Challenge Challenge { get; set; }
        public byte[] Response { get; }     // 8-bit bitmask per address 

        public PUF(ulong startAddres, ulong length, byte[] response)
        {
            Challenge = new Challenge(startAddres, length);
            Response = response;
        }

        public PUF(ulong[] addresses, byte[] response)
        {
            Challenge = new Challenge(addresses);
            Response = response;
        }

        public string ToBitString()
        {
            var sb = new StringBuilder(Response.Length * 8);
            foreach (byte b in Response)
                sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            return sb.ToString();
        }
    }
    public sealed class Challenge
    {
        public ulong[] Addresses { get; set; }
        public ulong StartAddress { get; }
        public ulong Length { get; }

        public Challenge(ulong startAddress, ulong length)
        {
            StartAddress = startAddress;
            Length = length;
        }

        public Challenge(ulong[] addresses)
        {
            Addresses = addresses;
        }

        public Challenge(ulong[] addresses, ulong startAddress, ulong length)
        {
            Addresses = addresses;
            StartAddress = startAddress;
            Length = length;
        }
    }
    public sealed class PUFAddress
    {
        public ulong[] Challenge { get; }
        public string[] Response { get; }
        public PUFAddress(ulong[] challenge, string[] response)
        {
            Challenge = challenge ?? throw new ArgumentNullException(nameof(challenge));
            Response = response ?? throw new ArgumentNullException(nameof(response));
            if (Challenge.Length != Response.Length)
                throw new ArgumentException("Challenge and Response must have the same length.");
        }

        public string ResponseString => string.Concat(Response);

        public override string ToString()
        {
            int bitsPerEntry = Response.Length > 0 ? Response[0].Length : 0;
            int totalBits = Response.Sum(s => s?.Length ?? 0);
            return $"PUFAffectedAddresses: {Response.Length} entries, {totalBits} bits\n{ResponseString}";
        }

        public string ToBitString()
        {

            return ResponseString;
        }
    }
    public sealed class BitPUF
    {
        public Challenge Challenge { get; }
        public byte[] Response { get; }
        public int LengthBits { get; }        // number of addresses/bits

        public BitPUF(ulong startAddress, ulong length, ulong[] addresses, byte[] response, int lengthBits)
        {
            Challenge = new Challenge(addresses, startAddress, length);
            Response = response ?? throw new ArgumentNullException(nameof(response));
            LengthBits = lengthBits;
            if (addresses.Length != lengthBits)
                throw new ArgumentException("Challenge length must equal bit length.");
        }

        public BitPUF(ulong[] addresses, byte[] response, int lengthBits)
        {
            Challenge = new Challenge(addresses);
            Response = response ?? throw new ArgumentNullException(nameof(response));
            LengthBits = lengthBits;
            if (addresses.Length != lengthBits)
                throw new ArgumentException("Challenge length must equal bit length.");
        }

        // Read a single bit (0/1)
        public int GetBit(int i)
        {
            if (i < 0 || i >= LengthBits) throw new ArgumentOutOfRangeException(nameof(i));
            int byteIdx = i >> 3;
            int bitIdx = i & 7;
            return (Response[byteIdx] >> bitIdx) & 1;
        }

        public string ToBitString()
        {
            var sb = new StringBuilder(LengthBits);
            for (int i = 0; i < LengthBits; i++)
                sb.Append(GetBit(i));
            return sb.ToString();
        }
    }
    public sealed class Bit2PUF
    {
        public Challenge Challenge { get; }        // absolute addresses (one per address in window)
        public byte[] Response { get; }   // packed 2-bit symbols (00,10,11), LSB-first bit order
        public int LengthSymbols { get; }    // number of addresses (symbols)
        public int LengthBits => LengthSymbols * 2;

        public Bit2PUF(ulong startAddress, ulong length, ulong[] addresses, byte[] response, int lengthSymbols)
        {
            Challenge = new Challenge(addresses, startAddress, length);
            Response = response ?? throw new ArgumentNullException(nameof(response));
            LengthSymbols = lengthSymbols;
            if (addresses.Length != lengthSymbols)
                throw new ArgumentException("Challenge length must equal bit length.");
        }

        public string ToBitString()
        {
            var sb = new System.Text.StringBuilder(LengthSymbols * 2);
            for (int i = 0; i < LengthSymbols; i++)
            {
                int pos = 2 * i;
                int b0 = (Response[(pos) >> 3] >> ((pos) & 7)) & 1;
                int b1 = (Response[(pos + 1) >> 3] >> ((pos + 1) & 7)) & 1;
                sb.Append(b1).Append(b0); // print MSB first so symbols look like "00/10/11"
            }
            return sb.ToString();
        }
    }
}
