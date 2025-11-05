using Newtonsoft.Json;


namespace RHPUFMetrics
{

    public class RawDramAddr
    {
        [JsonProperty("bank")] public int Bank { get; set; }
        [JsonProperty("col")]  public int Col { get; set; }
        [JsonProperty("row")]  public int Row { get; set; }
    }

    public class RawFlip
    {
        [JsonProperty("addr")] public string Addr { get; set; }
        [JsonProperty("bitmask")]  public byte Bitmask { get; set; }
        [JsonProperty("data")] public byte Data { get; set; }
        [JsonProperty("observed_at")] public long ObservedAt { get; set; }
        [JsonProperty("page_offset")] public int PageOffset { get; set; }
        [JsonProperty("dram_addr")] public RawDramAddr DramAddr { get; set; }
    }

    public class RawFlips
    {
        // ← these come from sweeps[*].flips
        [JsonProperty("total")] public int Total { get; set; }
        [JsonProperty("one_to_zero")] public int OneToZero { get; set; }
        [JsonProperty("zero_to_one")] public int ZeroToOne { get; set; }

        // details array of flips
        [JsonProperty("details")] public List<RawFlip> Details { get; set; } = new();
    }

    public class RawSweep
    {
        [JsonProperty("flips")] 
        public RawFlips Flips { get; set; }
    }

    public class RawMetadata
    {
        [JsonProperty("dimm_id")] 
        public int DimmId { get; set; }

        [JsonProperty("start")] 
        public long Start { get; set; }

        [JsonProperty("end")] 
        public long End { get; set; }
    }

    public class RawRoot
    {
        [JsonProperty("metadata")]
        public RawMetadata Metadata { get; set; }

        [JsonProperty("sweeps")] 
        public List<RawSweep> Sweeps { get; set; }
    }
}
