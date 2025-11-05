using System.Diagnostics.Metrics;
using System.Text;
using static RHPUFMetrics.PUFBuilders;


namespace RHPUFMetrics;

internal class Program
{
    static void Main(string[] args)
    {
        var devices = Loader.LoadDeviceMeasurementsFromInputFolder();

        //Compute most affected regions for all devices - best regions (2 KB)
        foreach (var device in devices)
        {
            PUFBuilders.FindMaxAddressCoverageWindow2KB(device, PUFBuilders.MeasurementSet.Train);
        }

        //Build reference PUFs of each type for each device
        foreach (var device in devices)
        {
            PUFBuilders.BuildPUFBitmaskSnapshot(device, PUFBuilders.MeasurementSet.Train);
            PUFBuilders.BuildPUFAffectedBitmasks(device, PUFBuilders.MeasurementSet.Train, 2048);
            PUFBuilders.BuildPUFAffectedAddresses(device, PUFBuilders.MeasurementSet.Train);
            PUFBuilders.BuildPUFAffectedAddressesShort(device, PUFBuilders.MeasurementSet.Train);
            PUFBuilders.BuildPUFFlipExistance(device, PUFBuilders.MeasurementSet.Train);
            PUFBuilders.BuildPUFFlipCombo(device, PUFBuilders.MeasurementSet.Train);
            PUFBuilders.BuildPUFFlipDirection(device, PUFBuilders.MeasurementSet.Train);
        }
        Console.WriteLine("Reference PUFs for all devices are built!");

        //Evaluation of Uniformity
        Console.WriteLine();
        Console.WriteLine("=== UNIFORMITY===");
        Console.WriteLine();
        Console.WriteLine("Hamming Weight");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");

        foreach (var device in devices)
            Console.WriteLine($"{device.Id};" +
                $"{Metrics.FractionalHammingWeight(device.PUFBitmaskSnapshot.Response)};" +
                $"{Metrics.FractionalHammingWeight(device.PUFAffectedBitmasks.Response)};" +
                $"{Metrics.FractionalHammingWeight(device.PUFAffectedAddresses.Response)};" +
                $"{Metrics.FractionalHammingWeight(device.PUFAffectedAddressesShort.Response)};" +
                $"{Metrics.FractionalHammingWeight(device.PUFFlipExistance.Response)};" +
                $"{Metrics.FractionalHammingWeight(device.PUFFlipCombo.Response)};" +
                $"{Metrics.FractionalHammingWeight(device.PUFFlipDirection.Response)}");


        Console.WriteLine();
        Console.WriteLine("Bit Entropy");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");
        foreach (var device in devices)
            Console.WriteLine($"{device.Id};" +
                $"{Metrics.BitEntropy(device.PUFBitmaskSnapshot.Response)};" +
                $"{Metrics.BitEntropy(device.PUFAffectedBitmasks.Response)};" +
                $"{Metrics.BitEntropy(device.PUFAffectedAddresses.Response)};" +
                $"{Metrics.BitEntropy(device.PUFAffectedAddressesShort.Response)};" +
                $"{Metrics.BitEntropy(device.PUFFlipExistance.Response)};" +
                $"{Metrics.BitEntropy(device.PUFFlipCombo.Response)};" +
                $"{Metrics.BitEntropy(device.PUFFlipDirection.Response)}");


        //Evaluation of Reliability
        Console.WriteLine();
        Console.WriteLine("=== RELIABILITY ===");
        Console.WriteLine();
        Console.WriteLine("Intra-Device Jaccard Index (AVG)");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");

        foreach (var d in devices)
        {
            // Reference PUF responses
            var ref_response_bitmask_snapshot = d.PUFBitmaskSnapshot.Response;
            var ref_response_affected_bitmasks = d.PUFAffectedBitmasks.Response;
            var ref_response_affected_addresses = d.PUFAffectedAddresses.Response;
            var ref_response_affected_addresses_short = d.PUFAffectedAddressesShort.Response;
            var ref_response_flip_existance = d.PUFFlipExistance.Response;
            var ref_response_flip_combo = d.PUFFlipCombo.Response;
            var ref_response_flip_direction = d.PUFFlipDirection.Response;

            // JI for each PUF type
            double ji_bitmask_snapshot = 0.0;
            double ji_affected_bitmasks = 0.0;
            double ji_affected_addresses = 0.0;
            double ji_affected_addresses_short = 0.0;
            double ji_flip_existance = 0.0;
            double ji_flip_combo = 0.0;
            double ji_flip_direction = 0.0;

            // Calculation of JI for each measurement 
            foreach (var m in d.TestMeasurements)
            {
                ji_bitmask_snapshot += Metrics.JaccardIndexBits(ref_response_bitmask_snapshot, PUFBuilders.ExtractResponseForPUFBitmaskSnapshot(d, m, d.PUFBitmaskSnapshot.Challenge));
                ji_affected_bitmasks += Metrics.JaccardIndexBits(ref_response_affected_bitmasks, PUFBuilders.ExtractResponseForPUFAffectedBitmasks(d, m, d.PUFAffectedBitmasks.Challenge));
                ji_affected_addresses += Metrics.JaccardIndex(ref_response_affected_addresses, PUFBuilders.ExtractResponseForPUFAffectedAddresses(d, m, d.PUFAffectedAddresses.Challenge));
                ji_affected_addresses_short += Metrics.JaccardIndex(ref_response_affected_addresses_short, PUFBuilders.ExtractResponseForPUFAffectedAddressesShort(d, m, d.PUFAffectedAddressesShort.Challenge));
                ji_flip_existance += Metrics.JaccardIndexBits(ref_response_flip_existance, PUFBuilders.ExtractResponseForPUFFlipExistance(d, m, d.PUFFlipExistance.Challenge));
                ji_flip_combo += Metrics.JaccardIndexBits(ref_response_flip_combo, PUFBuilders.ExtractResponseForPUFFlipCombo(d, m, d.PUFFlipCombo.Challenge));
                ji_flip_direction += Metrics.JaccardIndexBits(ref_response_flip_direction, PUFBuilders.ExtractResponseForPUFFlipDirection(d, m, d.PUFFlipDirection.Challenge));
            }

            var count = d.TestMeasurements.Count;

            Console.WriteLine($"{d.Id};" +
                $"{ji_bitmask_snapshot / count};" +
                $"{ji_affected_bitmasks / count};" +
                $"{ji_affected_addresses / count};" +
                $"{ji_affected_addresses_short / count};" +
                $"{ji_flip_existance / count};" +
                $"{ji_flip_combo / count};" +
                $"{ji_flip_direction / count}");
        }

        Console.WriteLine();
        Console.WriteLine("Intra-Device Hamming Distance (AVG)");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");
        foreach (var d in devices)
        {
            // Reference PUF responses
            var ref_response_bitmask_snapshot = d.PUFBitmaskSnapshot.Response;
            var ref_response_affected_bitmasks = d.PUFAffectedBitmasks.Response;
            var ref_response_affected_addresses = d.PUFAffectedAddresses.Response;
            var ref_response_affected_addresses_short = d.PUFAffectedAddressesShort.Response;
            var ref_response_flip_existance = d.PUFFlipExistance.Response;
            var ref_response_flip_combo = d.PUFFlipCombo.Response;
            var ref_response_flip_direction = d.PUFFlipDirection.Response;

            // HD for each PUF type
            double hd_bitmask_snapshot = 0.0;
            double hd_affected_bitmasks = 0.0;
            double hd_affected_addresses = 0.0;
            double hd_affected_addresses_short = 0.0;
            double hd_flip_existance = 0.0;
            double hd_flip_combo = 0.0;
            double hd_flip_direction = 0.0;

            // Calculation of HD for each measurement 
            foreach (var m in d.TestMeasurements)
            {
                hd_bitmask_snapshot += Metrics.FractionalHammingDistance(ref_response_bitmask_snapshot, PUFBuilders.ExtractResponseForPUFBitmaskSnapshot(d, m, d.PUFBitmaskSnapshot.Challenge));
                hd_affected_bitmasks += Metrics.FractionalHammingDistance(ref_response_affected_bitmasks, PUFBuilders.ExtractResponseForPUFAffectedBitmasks(d, m, d.PUFAffectedBitmasks.Challenge));
                hd_affected_addresses += Metrics.FractionalHammingDistance(ref_response_affected_addresses, PUFBuilders.ExtractResponseForPUFAffectedAddresses(d, m, d.PUFAffectedAddresses.Challenge));
                hd_affected_addresses_short += Metrics.FractionalHammingDistance(ref_response_affected_addresses_short, PUFBuilders.ExtractResponseForPUFAffectedAddressesShort(d, m, d.PUFAffectedAddressesShort.Challenge));
                hd_flip_existance += Metrics.FractionalHammingDistance(ref_response_flip_existance, PUFBuilders.ExtractResponseForPUFFlipExistance(d, m, d.PUFFlipExistance.Challenge));
                hd_flip_combo += Metrics.FractionalHammingDistance(ref_response_flip_combo, PUFBuilders.ExtractResponseForPUFFlipCombo(d, m, d.PUFFlipCombo.Challenge));
                hd_flip_direction += Metrics.FractionalHammingDistance(ref_response_flip_direction, PUFBuilders.ExtractResponseForPUFFlipDirection(d, m, d.PUFFlipDirection.Challenge));
            }
            var count = d.TestMeasurements.Count;
            Console.WriteLine($"{d.Id};" +
                $"{hd_bitmask_snapshot / count};" +
                $"{hd_affected_bitmasks / count};" +
                $"{hd_affected_addresses / count};" +
                $"{hd_affected_addresses_short / count};" +
                $"{hd_flip_existance / count};" +
                $"{hd_flip_combo / count};" +
                $"{hd_flip_direction / count}");

        }

        Console.WriteLine();
        Console.WriteLine("Intra-Device Dice (AVG)");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");
        foreach (var d in devices)
        {
            // Reference PUF responses
            var ref_response_bitmask_snapshot = d.PUFBitmaskSnapshot.Response;
            var ref_response_affected_bitmasks = d.PUFAffectedBitmasks.Response;
            var ref_response_affected_addresses = d.PUFAffectedAddresses.Response;
            var ref_response_affected_addresses_short = d.PUFAffectedAddressesShort.Response;
            var ref_response_flip_existance = d.PUFFlipExistance.Response;
            var ref_response_flip_combo = d.PUFFlipCombo.Response;
            var ref_response_flip_direction = d.PUFFlipDirection.Response;

            // Dice for each PUF type
            double dice_bitmask_snapshot = 0.0;
            double dice_affected_bitmasks = 0.0;
            double dice_affected_addresses = 0.0;
            double dice_affected_addresses_short = 0.0;
            double dice_flip_existance = 0.0;
            double dice_flip_combo = 0.0;
            double dice_flip_direction = 0.0;

            // Calculation of Dice for each measurement 
            foreach (var m in d.TestMeasurements)
            {
                dice_bitmask_snapshot += Metrics.Dice(ref_response_bitmask_snapshot, PUFBuilders.ExtractResponseForPUFBitmaskSnapshot(d, m, d.PUFBitmaskSnapshot.Challenge));
                dice_affected_bitmasks += Metrics.Dice(ref_response_affected_bitmasks, PUFBuilders.ExtractResponseForPUFAffectedBitmasks(d, m, d.PUFAffectedBitmasks.Challenge));
                dice_affected_addresses += Metrics.Dice(ref_response_affected_addresses, PUFBuilders.ExtractResponseForPUFAffectedAddresses(d, m, d.PUFAffectedAddresses.Challenge));
                dice_affected_addresses_short += Metrics.Dice(ref_response_affected_addresses_short, PUFBuilders.ExtractResponseForPUFAffectedAddressesShort(d, m, d.PUFAffectedAddressesShort.Challenge));
                dice_flip_existance += Metrics.Dice(ref_response_flip_existance, PUFBuilders.ExtractResponseForPUFFlipExistance(d, m, d.PUFFlipExistance.Challenge));
                dice_flip_combo += Metrics.Dice(ref_response_flip_combo, PUFBuilders.ExtractResponseForPUFFlipCombo(d, m, d.PUFFlipCombo.Challenge));
                dice_flip_direction += Metrics.Dice(ref_response_flip_direction, PUFBuilders.ExtractResponseForPUFFlipDirection(d, m, d.PUFFlipDirection.Challenge));
            }

            var count = d.TestMeasurements.Count;
            Console.WriteLine($"{d.Id};" +
                $"{dice_bitmask_snapshot / count};" +
                $"{dice_affected_bitmasks / count};" +
                $"{dice_affected_addresses / count};" +
                $"{dice_affected_addresses_short / count};" +
                $"{dice_flip_existance / count};" +
                $"{dice_flip_combo / count};" +
                $"{dice_flip_direction / count}");

        }

        Console.WriteLine();
        Console.WriteLine("Intra-Device Cosine Similarity (AVG)");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");
        foreach (var d in devices)
        {
            // Reference PUF responses
            var ref_response_bitmask_snapshot = d.PUFBitmaskSnapshot.Response;
            var ref_response_affected_bitmasks = d.PUFAffectedBitmasks.Response;
            var ref_response_affected_addresses = d.PUFAffectedAddresses.Response;
            var ref_response_affected_addresses_short = d.PUFAffectedAddressesShort.Response;
            var ref_response_flip_existance = d.PUFFlipExistance.Response;
            var ref_response_flip_combo = d.PUFFlipCombo.Response;
            var ref_response_flip_direction = d.PUFFlipDirection.Response;

            // Cosine for each PUF type
            double cosine_bitmask_snapshot = 0.0;
            double cosine_affected_bitmasks = 0.0;
            double cosine_affected_addresses = 0.0;
            double cosine_affected_addresses_short = 0.0;
            double cosine_flip_existance = 0.0;
            double cosine_flip_combo = 0.0;
            double cosine_flip_direction = 0.0;

            // Calculation of Dice for each measurement 
            foreach (var m in d.TestMeasurements)
            {
                cosine_bitmask_snapshot += Metrics.Cosine(ref_response_bitmask_snapshot, PUFBuilders.ExtractResponseForPUFBitmaskSnapshot(d, m, d.PUFBitmaskSnapshot.Challenge));
                cosine_affected_bitmasks += Metrics.Cosine(ref_response_affected_bitmasks, PUFBuilders.ExtractResponseForPUFAffectedBitmasks(d, m, d.PUFAffectedBitmasks.Challenge));
                cosine_affected_addresses += Metrics.Cosine(ref_response_affected_addresses, PUFBuilders.ExtractResponseForPUFAffectedAddresses(d, m, d.PUFAffectedAddresses.Challenge));
                cosine_affected_addresses_short += Metrics.Cosine(ref_response_affected_addresses_short, PUFBuilders.ExtractResponseForPUFAffectedAddressesShort(d, m, d.PUFAffectedAddressesShort.Challenge));
                cosine_flip_existance += Metrics.Cosine(ref_response_flip_existance, PUFBuilders.ExtractResponseForPUFFlipExistance(d, m, d.PUFFlipExistance.Challenge));
                cosine_flip_combo += Metrics.Cosine(ref_response_flip_combo, PUFBuilders.ExtractResponseForPUFFlipCombo(d, m, d.PUFFlipCombo.Challenge));
                cosine_flip_direction += Metrics.Cosine(ref_response_flip_direction, PUFBuilders.ExtractResponseForPUFFlipDirection(d, m, d.PUFFlipDirection.Challenge));
            }

            var count = d.TestMeasurements.Count;
            Console.WriteLine($"{d.Id};" +
                $"{cosine_bitmask_snapshot / count};" +
                $"{cosine_affected_bitmasks / count};" +
                $"{cosine_affected_addresses / count};" +
                $"{cosine_affected_addresses_short / count};" +
                $"{cosine_flip_existance / count};" +
                $"{cosine_flip_combo / count};" +
                $"{cosine_flip_direction / count}");

        }

        Console.WriteLine();
        Console.WriteLine("Probabilities");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");
        foreach (var d in devices)
        {
            // Reference PUF responses
            var ref_response_bitmask_snapshot = d.PUFBitmaskSnapshot.Response;
            var ref_response_affected_bitmasks = d.PUFAffectedBitmasks.Response;
            var ref_response_affected_addresses = d.PUFAffectedAddresses.Response;
            var ref_response_affected_addresses_short = d.PUFAffectedAddressesShort.Response;
            var ref_response_flip_existance = d.PUFFlipExistance.Response;
            var ref_response_flip_combo = d.PUFFlipCombo.Response;
            var ref_response_flip_direction = d.PUFFlipDirection.Response;

            // List of other PUF responses
            List<byte[]> responses_bitmask_snapshot = new List<byte[]>();
            List<byte[]> responses_affected_bitmasks = new List<byte[]>();
            List<string[]> responses_affected_addresses = new List<string[]>();
            List<string[]> responses_affected_addresses_short = new List<string[]>();
            List<byte[]> responses_flip_existance = new List<byte[]>();
            List<byte[]> responses_flip_combo = new List<byte[]>();
            List<byte[]> responses_flip_direction = new List<byte[]>();

            // Create of all responses  
            foreach (var m in d.Measurements)
            {
                responses_bitmask_snapshot.Add(PUFBuilders.ExtractResponseForPUFBitmaskSnapshot(d, m, d.PUFBitmaskSnapshot.Challenge));
                responses_affected_bitmasks.Add(PUFBuilders.ExtractResponseForPUFAffectedBitmasks(d, m, d.PUFAffectedBitmasks.Challenge));
                responses_affected_addresses.Add(PUFBuilders.ExtractResponseForPUFAffectedAddresses(d, m, d.PUFAffectedAddresses.Challenge));
                responses_affected_addresses_short.Add(PUFBuilders.ExtractResponseForPUFAffectedAddressesShort(d, m, d.PUFAffectedAddressesShort.Challenge));
                responses_flip_existance.Add(PUFBuilders.ExtractResponseForPUFFlipExistance(d, m, d.PUFFlipExistance.Challenge));
                responses_flip_combo.Add(PUFBuilders.ExtractResponseForPUFFlipCombo(d, m, d.PUFFlipCombo.Challenge));
                responses_flip_direction.Add(PUFBuilders.ExtractResponseForPUFFlipDirection(d, m, d.PUFFlipDirection.Challenge));
            }

            // Calculate p and z values
            (double[] p, double[] z) = Metrics.ProbitProbabilities(ref_response_bitmask_snapshot, responses_bitmask_snapshot);
            (double[] p1, double[] z1) = Metrics.ProbitProbabilities(ref_response_affected_bitmasks, responses_affected_bitmasks);
            (double[] p2, double[] z2) = Metrics.ProbitProbabilities(ref_response_affected_addresses, responses_affected_addresses);
            (double[] p22, double[] z22) = Metrics.ProbitProbabilities(ref_response_affected_addresses_short, responses_affected_addresses_short);
            (double[] p3, double[] z3) = Metrics.ProbitProbabilities(ref_response_flip_existance, responses_flip_existance);
            (double[] p4, double[] z4) = Metrics.ProbitProbabilities(ref_response_flip_combo, responses_flip_combo);
            (double[] p5, double[] z5) = Metrics.ProbitProbabilities(ref_response_flip_direction, responses_flip_direction);

            // Calculate mean p and z values
            double bitmasksnapshot_meanP = p.Average();
            double bitmasksnapshot_meanZ = z.Average();

            double affected_bitmasks_meanP = p1.Average();
            double affected_bitmasks_meanZ = z1.Average();

            double affected_addresses_meanP = (p2.Length == 0) ? 0 : p2.Average();
            double affected_addresses_meanZ = (z2.Length == 0) ? 0 : z2.Average();

            double affected_addresses_short_meanP = (p22.Length == 0) ? 0 : p22.Average();
            double affected_addresses_short_meanZ = (z22.Length == 0) ? 0 : z22.Average();

            double flip_existanse_meanP = p3.Average();
            double aflip_existanse_meanZ = z3.Average();

            double flip_combo_meanP = p4.Average();
            double flip_combo_meanZ = z4.Average();

            double flip_direction_meanP = (p5.Length == 0) ? 0 : p5.Average();
            double flip_direction_meanZ = (z5.Length == 0) ? 0 : z5.Average();


            Console.WriteLine($"{d.Id};" +
                $"{bitmasksnapshot_meanP};" +
                $"{affected_bitmasks_meanP};" +
                $"{affected_addresses_meanP};" +
                $"{affected_addresses_short_meanP};" +
                $"{flip_existanse_meanP};" +
                $"{flip_combo_meanP};" +
                $"{flip_direction_meanP}");

            /*  Console.WriteLine($"{d.Id};" +
                  $"{bitmasksnapshot_meanZ};" +
                  $"{affected_bitmasks_meanZ};" +
                  $"{affected_addresses_meanZ};" +
                  $"{affected_addresses_short_meanZ};" +
                  $"{aflip_existanse_meanZ};" +
                  $"{flip_combo_meanZ};" +
                  $"{flip_direction_meanZ}");
            */
        }

        Console.WriteLine();
        Console.WriteLine("==========Uniqness========");
        Console.WriteLine();
        Console.WriteLine("Inter-Device Jaccard Index");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");

        for (int i = 0; i < devices.Count; i++)
        {
            var refDev = devices[i];

            //PUF Responses
            var ref_response_bitmask_snapshot = refDev.PUFBitmaskSnapshot.Response;
            var ref_response_affected_bitmasks = refDev.PUFAffectedBitmasks.Response;
            var ref_response_affected_addresses = refDev.PUFAffectedAddresses.Response;
            var ref_response_affected_addresses_short = refDev.PUFAffectedAddressesShort.Response;
            var ref_response_flip_existance = refDev.PUFFlipExistance.Response;
            var ref_response_flip_combo = refDev.PUFFlipCombo.Response;
            var ref_response_flip_direction = refDev.PUFFlipDirection.Response;

            // Sum of JI between devices
            var ji_sum_bitmask_snapshot = 0.0;
            var ji_sum_affected_bitmasks = 0.0;
            var ji_sum_affected_addresses = 0.0;
            var ji_sum_affected_addresses_short=0.0;
            var ji_sum_flip_existance = 0.0;
            var ji_sum_flip_combo = 0.0;
            var ji_sum_flip_direction = 0.0;

            // Total number of JI measurements
            var total = 0;

            //Calculation of JI between devices for single measurement
            for (int j = 0; j < devices.Count; j++)
            {
                if (i != j)
                {
                    var oth = devices[j];
                    var meas = oth.Measurements[0];

                    ji_sum_bitmask_snapshot += Metrics.JaccardIndexBits(ref_response_bitmask_snapshot, PUFBuilders.ExtractResponseForPUFBitmaskSnapshot(oth, meas, refDev.PUFBitmaskSnapshot.Challenge));
                    ji_sum_affected_bitmasks += Metrics.JaccardIndexBits(ref_response_affected_bitmasks, PUFBuilders.ExtractResponseForPUFAffectedBitmasks(oth, meas, refDev.PUFAffectedBitmasks.Challenge));
                    ji_sum_affected_addresses += Metrics.JaccardIndex(ref_response_affected_addresses, PUFBuilders.ExtractResponseForPUFAffectedAddresses(oth, meas, refDev.PUFAffectedAddresses.Challenge));
                    ji_sum_affected_addresses_short += Metrics.JaccardIndex(ref_response_affected_addresses_short, PUFBuilders.ExtractResponseForPUFAffectedAddressesShort(oth,meas,refDev.PUFAffectedAddressesShort.Challenge));
                    ji_sum_flip_existance += Metrics.JaccardIndexBits(ref_response_flip_existance, PUFBuilders.ExtractResponseForPUFFlipExistance(oth, meas, refDev.PUFFlipExistance.Challenge));
                    ji_sum_flip_combo += Metrics.JaccardIndexBits(ref_response_flip_combo, PUFBuilders.ExtractResponseForPUFFlipCombo(oth, meas, refDev.PUFFlipCombo.Challenge));
                    ji_sum_flip_direction += Metrics.JaccardIndexBits(ref_response_flip_direction, PUFBuilders.ExtractResponseForPUFFlipDirection(oth, meas, refDev.PUFFlipDirection.Challenge));

                    total++;
                }
            }

            Console.WriteLine($"{devices[i].Id};" +
                $"{ji_sum_bitmask_snapshot / total};" +
                $"{ji_sum_affected_bitmasks / total};" +
                $"{ji_sum_affected_addresses / total};" +
                $"{ji_sum_affected_addresses_short / total};" +
                $"{ji_sum_flip_existance / total};" +
                $"{ji_sum_flip_combo / total};" +
                $"{ji_sum_flip_direction / total}");
        }

        Console.WriteLine();
        Console.WriteLine(" Inter-Device HD");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");

        for (int i = 0; i < devices.Count; i++)
        {
            var refDev = devices[i];

            //PUF Responses
            var ref_response_bitmask_snapshot = refDev.PUFBitmaskSnapshot.Response;
            var ref_response_affected_bitmasks = refDev.PUFAffectedBitmasks.Response;
            var ref_response_affected_addresses = refDev.PUFAffectedAddresses.Response;
            var ref_response_affected_addresses_short= refDev.PUFAffectedAddressesShort.Response;
            var ref_response_flip_existance = refDev.PUFFlipExistance.Response;
            var ref_response_flip_combo = refDev.PUFFlipCombo.Response;
            var ref_response_flip_direction = refDev.PUFFlipDirection.Response;

            // Sum of HD between devices
            var hd_sum_bitmask_snapshot = 0.0;
            var hd_sum_affected_bitmasks = 0.0;
            var hd_sum_affected_addresses = 0.0;
            var hd_sum_affected_addresses_short= 0.0;
            var hd_sum_flip_existance = 0.0;
            var hd_sum_flip_combo = 0.0;
            var hd_sum_flip_direction = 0.0;

            // Total number of HD measurements
            var total = 0;

            //Calculation of HD between devices for single measurement
            for (int j = 0; j < devices.Count; j++)
            {
                if (i != j)
                {
                    var oth = devices[j];
                    var meas = oth.Measurements[0];

                    hd_sum_bitmask_snapshot += Metrics.FractionalHammingDistance(ref_response_bitmask_snapshot, PUFBuilders.ExtractResponseForPUFBitmaskSnapshot(oth, meas, refDev.PUFBitmaskSnapshot.Challenge));
                    hd_sum_affected_bitmasks += Metrics.FractionalHammingDistance(ref_response_affected_bitmasks, PUFBuilders.ExtractResponseForPUFAffectedBitmasks(oth, meas, refDev.PUFAffectedBitmasks.Challenge));
                    hd_sum_affected_addresses += Metrics.FractionalHammingDistance(ref_response_affected_addresses, PUFBuilders.ExtractResponseForPUFAffectedAddresses(oth, meas, refDev.PUFAffectedAddresses.Challenge));
                    hd_sum_affected_addresses_short += Metrics.FractionalHammingDistance(ref_response_affected_addresses_short, PUFBuilders.ExtractResponseForPUFAffectedAddressesShort(oth, meas, refDev.PUFAffectedAddressesShort.Challenge));
                    hd_sum_flip_existance += Metrics.FractionalHammingDistance(ref_response_flip_existance, PUFBuilders.ExtractResponseForPUFFlipExistance(oth, meas, refDev.PUFFlipExistance.Challenge));
                    hd_sum_flip_combo += Metrics.FractionalHammingDistance(ref_response_flip_combo, PUFBuilders.ExtractResponseForPUFFlipCombo(oth, meas, refDev.PUFFlipCombo.Challenge));
                    hd_sum_flip_direction += Metrics.FractionalHammingDistance(ref_response_flip_direction, PUFBuilders.ExtractResponseForPUFFlipDirection(oth, meas, refDev.PUFFlipDirection.Challenge));

                    total++;
                }
            }

            Console.WriteLine($"{refDev.Id};" +
                $"{hd_sum_bitmask_snapshot / total};" +
                $"{hd_sum_affected_bitmasks / total};" +
                $"{hd_sum_affected_addresses / total};" +
                $"{hd_sum_affected_addresses_short / total};" +
                $"{hd_sum_flip_existance / total};" +
                $"{hd_sum_flip_combo / total};" +
                $"{hd_sum_flip_direction / total}");
        }

        Console.WriteLine();
        Console.WriteLine(" Inter-Device Dice");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");

        for (int i = 0; i < devices.Count; i++)
        {
            var refDev = devices[i];

            //PUF Responses
            var ref_response_bitmask_snapshot = refDev.PUFBitmaskSnapshot.Response;
            var ref_response_affected_bitmasks = refDev.PUFAffectedBitmasks.Response;
            var ref_response_affected_addresses = refDev.PUFAffectedAddresses.Response;
            var ref_response_affected_addresses_short = refDev.PUFAffectedAddressesShort.Response;
            var ref_response_flip_existance = refDev.PUFFlipExistance.Response;
            var ref_response_flip_combo = refDev.PUFFlipCombo.Response;
            var ref_response_flip_direction = refDev.PUFFlipDirection.Response;

            // Sum of Dice between devices
            var dice_sum_bitmask_snapshot = 0.0;
            var dice_sum_affected_bitmasks = 0.0;
            var dice_sum_affected_addresses = 0.0;
            var dice_sum_affected_addresses_short = 0.0;
            var dice_sum_flip_existance = 0.0;
            var dice_sum_flip_combo = 0.0;
            var dice_sum_flip_direction = 0.0;

            // Total number of Dice measurements
            var total = 0;

            //Calculation of Dice between devices for single measurement
            for (int j = 0; j < devices.Count; j++)
            {
                if (i != j)
                {
                    var oth = devices[j];
                    var meas = oth.Measurements[0];

                    dice_sum_bitmask_snapshot += Metrics.Dice(ref_response_bitmask_snapshot, PUFBuilders.ExtractResponseForPUFBitmaskSnapshot(oth, meas, refDev.PUFBitmaskSnapshot.Challenge));
                    dice_sum_affected_bitmasks += Metrics.Dice(ref_response_affected_bitmasks, PUFBuilders.ExtractResponseForPUFAffectedBitmasks(oth, meas, refDev.PUFAffectedBitmasks.Challenge));
                    dice_sum_affected_addresses += Metrics.Dice(ref_response_affected_addresses, PUFBuilders.ExtractResponseForPUFAffectedAddresses(oth, meas, refDev.PUFAffectedAddresses.Challenge));
                    dice_sum_affected_addresses_short += Metrics.Dice(ref_response_affected_addresses_short, PUFBuilders.ExtractResponseForPUFAffectedAddressesShort(oth, meas, refDev.PUFAffectedAddressesShort.Challenge));
                    dice_sum_flip_existance += Metrics.Dice(ref_response_flip_existance, PUFBuilders.ExtractResponseForPUFFlipExistance(oth, meas, refDev.PUFFlipExistance.Challenge));
                    dice_sum_flip_combo += Metrics.Dice(ref_response_flip_combo, PUFBuilders.ExtractResponseForPUFFlipCombo(oth, meas, refDev.PUFFlipCombo.Challenge));
                    dice_sum_flip_direction += Metrics.Dice(ref_response_flip_direction, PUFBuilders.ExtractResponseForPUFFlipDirection(oth, meas, refDev.PUFFlipDirection.Challenge));

                    total++;
                }
            }

            Console.WriteLine($"{refDev.Id};" +
                $"{dice_sum_bitmask_snapshot / total};" +
                $"{dice_sum_affected_bitmasks / total};" +
                $"{dice_sum_affected_addresses / total};" +
                $"{dice_sum_affected_addresses_short / total};" +
                $"{dice_sum_flip_existance / total};" +
                $"{dice_sum_flip_combo / total};" +
                $"{dice_sum_flip_direction / total}");
        }

        Console.WriteLine();
        Console.WriteLine(" Inter-Device Cosine");
        Console.WriteLine();
        Console.WriteLine("D;BitmaskSnapshot;AffectedBitmasks;AffectedAddresses;AffectedAddressesShort;FlipExistance;FlipCombo;FlipDirection");

        for (int i = 0; i < devices.Count; i++)
        {
            var refDev = devices[i];

            //PUF Responses
            var ref_response_bitmask_snapshot = refDev.PUFBitmaskSnapshot.Response;
            var ref_response_affected_bitmasks = refDev.PUFAffectedBitmasks.Response;
            var ref_response_affected_addresses = refDev.PUFAffectedAddresses.Response;
            var ref_response_affected_addresses_short=refDev.PUFAffectedAddressesShort.Response;
            var ref_response_flip_existance = refDev.PUFFlipExistance.Response;
            var ref_response_flip_combo = refDev.PUFFlipCombo.Response;
            var ref_response_flip_direction = refDev.PUFFlipDirection.Response;

            // Sum of Cosine between devices
            var cosine_sum_bitmask_snapshot = 0.0;
            var cosine_sum_affected_bitmasks = 0.0;
            var cosine_sum_affected_addresses = 0.0;
            var cosine_sum_affected_addresses_short = 0.0;
            var cosine_sum_flip_existance = 0.0;
            var cosine_sum_flip_combo = 0.0;
            var cosine_sum_flip_direction = 0.0;

            // Total number of Cosine measurements
            var total = 0;

            //Calculation of Cosine between devices for single measurement
            for (int j = 0; j < devices.Count; j++)
            {
                if (i != j)
                {
                    var oth = devices[j];
                    var meas = oth.Measurements[0];

                    cosine_sum_bitmask_snapshot += Metrics.Cosine(ref_response_bitmask_snapshot, PUFBuilders.ExtractResponseForPUFBitmaskSnapshot(oth, meas, refDev.PUFBitmaskSnapshot.Challenge));
                    cosine_sum_affected_bitmasks += Metrics.Cosine(ref_response_affected_bitmasks, PUFBuilders.ExtractResponseForPUFAffectedBitmasks(oth, meas, refDev.PUFAffectedBitmasks.Challenge));
                    cosine_sum_affected_addresses += Metrics.Cosine(ref_response_affected_addresses, PUFBuilders.ExtractResponseForPUFAffectedAddresses(oth, meas, refDev.PUFAffectedAddresses.Challenge));
                    cosine_sum_affected_addresses_short += Metrics.Cosine(ref_response_affected_addresses_short, PUFBuilders.ExtractResponseForPUFAffectedAddressesShort(oth, meas, refDev.PUFAffectedAddressesShort.Challenge));
                    cosine_sum_flip_existance += Metrics.Cosine(ref_response_flip_existance, PUFBuilders.ExtractResponseForPUFFlipExistance(oth, meas, refDev.PUFFlipExistance.Challenge));
                    cosine_sum_flip_combo += Metrics.Cosine(ref_response_flip_combo, PUFBuilders.ExtractResponseForPUFFlipCombo(oth, meas, refDev.PUFFlipCombo.Challenge));
                    cosine_sum_flip_direction += Metrics.Cosine(ref_response_flip_direction, PUFBuilders.ExtractResponseForPUFFlipDirection(oth, meas, refDev.PUFFlipDirection.Challenge));

                    total++;
                }
            }

            Console.WriteLine($"{refDev.Id};" +
                $"{cosine_sum_bitmask_snapshot / total};" +
                $"{cosine_sum_affected_bitmasks / total};" +
                $"{cosine_sum_affected_addresses / total};" +
                $"{cosine_sum_affected_addresses_short / total};" +
                $"{cosine_sum_flip_existance / total};" +
                $"{cosine_sum_flip_combo / total};" +
                $"{cosine_sum_flip_direction / total}");
        }



    }


}





