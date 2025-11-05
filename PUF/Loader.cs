using Newtonsoft.Json;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RHPUFMetrics;

public static class Loader
{
    public static List<Dimm> LoadDeviceMeasurementsFromInputFolder()
    {
        var devices = new List<Dimm>();

        // Find device folders which contain our measurements
        string inputFolder = Path.Combine("..", "..", "..", "input");
        var valdiatedDeviceFolders = new List<string>();
        var foldersToCheck = Directory.GetDirectories(inputFolder, "system_*", SearchOption.AllDirectories);
        foreach (string folder in foldersToCheck)
        {
            if (IsDeviceFolder(folder))
            {
                valdiatedDeviceFolders.Add(folder);
                //Console.WriteLine($"Found device folder: {folder}");
            }
        }
        valdiatedDeviceFolders.Sort();

        // Enumarate devices, and load measurements
        int lastId = 0;
        foreach (string folder in valdiatedDeviceFolders)
        {
            var deviceMeasurements = new List<Measurement>();

            // Find all sweep measurements in this device folder
            var measurementJsonFiles = Directory.GetFiles(Path.Combine(folder, "blacksmithSweep"), "sweep-summary-*.json", SearchOption.AllDirectories);

            //  if (measurementJsonFiles.Length > 4)
            {
                foreach (var measurementJsonFile in measurementJsonFiles)
                {
                    try
                    {
                        // Memory address offset from file
                        ulong offset = 0;
                        string measurementFileParentDirectory = Path.GetDirectoryName(measurementJsonFile);
                        var offsetsFile = Path.Combine(measurementFileParentDirectory, "offsets.txt");
                        if (File.Exists(offsetsFile))
                        {
                            // The first HEX number is the one we need from the offsets.txt file. 
                            string offsetFileContent = File.ReadAllText(offsetsFile);
                            var match = Regex.Match(offsetFileContent, @"hammered range \(based on first row\)\s*:\s*(0x[0-9A-Fa-f]+)", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                string hexStr = match.Groups[1].Value;
                                offset = ulong.Parse(hexStr.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            }
                        }

                        // Measurements from JSON
                        var raw = JsonConvert.DeserializeObject<RawRoot>(File.ReadAllText(measurementJsonFile));
                        var measurements = raw.Sweeps
                            .Select(s => new Measurement
                            {
                                Total = s.Flips?.Total ?? 0,
                                OneToZero = s.Flips?.OneToZero ?? 0,
                                ZeroToOne = s.Flips?.ZeroToOne ?? 0,
                                Flips = (s.Flips?.Details ?? new List<RawFlip>())
                                    .Select(f => new Flip
                                    {
                                        Addr = ShiftAddressLeft(HexStringToUInt64(f.Addr), offset),
                                        Bitmask = f.Bitmask,
                                        Data = f.Data,
                                        ObservedAt = f.ObservedAt,
                                        PageOffset = f.PageOffset,
                                        DramAddr = new FlipLocation()
                                        {
                                            Bank = f.DramAddr.Bank,
                                            Col = f.DramAddr.Col,
                                            Row = f.DramAddr.Row
                                        }
                                    })
                                    .OrderBy(f => f.Addr)
                                    .ToList()
                            })
                            .ToList();

                        deviceMeasurements.AddRange(measurements);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.ToString());
                        Console.WriteLine($"Invalid measurement file, it is skipped: '{measurementJsonFile}'");
                    }
                }

                if (deviceMeasurements.Any())
                {
                    Dimm device = new Dimm(++lastId)
                    {
                        Measurements = deviceMeasurements
                    };
                    devices.Add(device);
                    SplitMeasurements(device);
                    Console.WriteLine($"Device (Id={device.Id}) measurements (Count={device.Measurements})loaded: {folder}");
                    //Console.WriteLine($"Train Set={device.TrainMeasurements.Count}, test set={device.TestMeasurements.Count}");
                }
                else
                {
                    Console.WriteLine($"No measurements found for device: {folder}");
                }
            }
        }

        return devices;
    }

    private static ulong ShiftAddressLeft(ulong address, ulong offset)
    {
        ulong result = 0;
        if (address < offset)
        {
            //In this case, the hammering wrapped around at the end of the address range  
            result = address + (1 << 30) - offset;
        }
        else
        {
            result = address - offset;
        }
        return result;
    }

    private static ulong HexStringToUInt64(string hexString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexString);
        if (!hexString.StartsWith("0x"))
            throw new ArgumentException(nameof(hexString));
        return ulong.Parse(hexString.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    // Checks wether the specified folder is a device fodler or not
    static bool IsDeviceFolder(string folderPath)
    {
        return File.Exists(Path.Combine(folderPath, "summary.md"))
            && File.Exists(Path.Combine(folderPath, "VERSION.txt"))
            && Directory.Exists(Path.Combine(folderPath, "blacksmithSweep"));
    }

    //Splits measurements of each device into two sets: Train (for PUF building) and Test
    private static void SplitMeasurements(Dimm dimm, double trainFraction = 0.20)
    {
        if (dimm.Measurements == null || dimm.Measurements.Count == 0)
            return;

        int total = dimm.Measurements.Count;
        int trainCount = Math.Max(1, (int)Math.Round(total * trainFraction, MidpointRounding.AwayFromZero));


        dimm.TrainMeasurements = dimm.Measurements.Take(trainCount).ToList();
        dimm.TestMeasurements = dimm.Measurements.Skip(trainCount).ToList();
    }
}
