using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSCore;
using CSCore.Codecs.FLAC;
using EMQ.Shared.Core;

namespace EMQ.Server;

public class VocalDetectorOptions
{
    /// <summary>
    /// The energy threshold (0.0 to 1.0) to consider a frame as "active".
    /// A lower value is more sensitive. Default is 0.05.
    /// </summary>
    public double EnergyThreshold { get; init; } = 0.05;

    /// <summary>
    /// The duration of the analysis window (frame) in seconds.
    /// Default is 0.05s (50ms).
    /// </summary>
    public double FrameDurationSec { get; init; } = 0.05;

    /// <summary>
    /// The step size between frames (hop) in seconds.
    /// Default is 0.025s (25ms).
    /// </summary>
    public double HopDurationSec { get; init; } = 0.025;

    /// <summary>
    /// Minimum duration of silence (in seconds) to mark the end of a vocal section.
    /// Shorter silences will be merged into the vocal region.
    /// Default is 0.4s.
    /// </summary>
    public double MinSilenceDurationSec { get; init; } = 0.4;
}

public static class VocalDetector
{
    public static List<TimeRange> Detect(string flacPath, VocalDetectorOptions? options = null)
    {
        if (!File.Exists(flacPath))
        {
            throw new FileNotFoundException("Audio file not found.", flacPath);
        }

        options ??= new VocalDetectorOptions();

        // 1. Load audio file and get samples
        float[] samples;
        int sampleRate;
        using (var reader = new FlacFile(flacPath))
        {
            sampleRate = reader.WaveFormat.SampleRate;
            var sampleProvider = reader.ToMono().ToSampleSource();
            var sampleList = new List<float>((int)(reader.Length / sizeof(float)));
            float[] buffer = new float[sampleRate]; // Read in 1-second chunks
            int bytesRead;
            while ((bytesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                sampleList.AddRange(buffer.Take(bytesRead));
            }

            samples = sampleList.ToArray();
        }

        if (samples.Length == 0) return new List<TimeRange>();

        // 2. Calculate frame and hop length in samples
        int frameLength = (int)(options.FrameDurationSec * sampleRate);
        int hopLength = (int)(options.HopDurationSec * sampleRate);

        // 3. Calculate RMS energy for each frame
        var rmsValues = CalculateRms(samples, frameLength, hopLength);

        if (rmsValues.Count == 0) return new List<TimeRange>();

        // 4. Normalize RMS and identify active frames
        bool[] activeFrames = IdentifyActiveFrames(rmsValues, options.EnergyThreshold);

        // 5. Merge active frames into contiguous regions
        var mergedRegions = MergeActiveRegions(activeFrames, options.MinSilenceDurationSec, sampleRate, hopLength);

        return mergedRegions;
    }

    private static List<double> CalculateRms(float[] samples, int frameLength, int hopLength)
    {
        var rmsValues = new List<double>();
        for (int i = 0; i + frameLength <= samples.Length; i += hopLength)
        {
            double sumOfSquares = 0;
            for (int j = 0; j < frameLength; j++)
            {
                sumOfSquares += samples[i + j] * samples[i + j];
            }

            double mean = sumOfSquares / frameLength;
            rmsValues.Add(Math.Sqrt(mean));
        }

        return rmsValues;
    }

    private static bool[] IdentifyActiveFrames(List<double> rmsValues, double energyThreshold)
    {
        double maxRms = rmsValues.Max();
        if (maxRms == 0) return new bool[rmsValues.Count]; // All silent

        bool[] activeFrames = new bool[rmsValues.Count];
        for (int i = 0; i < rmsValues.Count; i++)
        {
            // Normalize and compare to threshold
            activeFrames[i] = (rmsValues[i] / maxRms) > energyThreshold;
        }

        return activeFrames;
    }

    private static List<TimeRange> MergeActiveRegions(bool[] activeFrames, double minSilenceDuration, int sampleRate,
        int hopLength)
    {
        var regions = new List<Tuple<int, int>>();
        bool inRegion = false;
        int regionStart = 0;

        for (int i = 0; i < activeFrames.Length; i++)
        {
            if (!inRegion && activeFrames[i])
            {
                // Start of a new region
                inRegion = true;
                regionStart = i;
            }
            else if (inRegion && !activeFrames[i])
            {
                // End of a region
                inRegion = false;
                regions.Add(new Tuple<int, int>(regionStart, i - 1));
            }
        }

        // If the audio ends during an active region
        if (inRegion)
        {
            regions.Add(new Tuple<int, int>(regionStart, activeFrames.Length - 1));
        }

        if (regions.Count == 0) return new List<TimeRange>();

        // Merge regions separated by short silences
        var mergedRegions = new List<TimeRange>();
        var currentRegion = regions[0];

        double minSilenceFrames = minSilenceDuration * sampleRate / hopLength;

        for (int i = 1; i < regions.Count; i++)
        {
            var nextRegion = regions[i];
            int silenceGap = nextRegion.Item1 - currentRegion.Item2;

            if (silenceGap < minSilenceFrames)
            {
                // Merge by extending the current region to include the next one
                currentRegion = new Tuple<int, int>(currentRegion.Item1, nextRegion.Item2);
            }
            else
            {
                // Gap is too large, finalize the current region and start a new one
                double startTime = (double)currentRegion.Item1 * hopLength / sampleRate;
                double endTime =
                    (double)(currentRegion.Item2 + 1) * hopLength / sampleRate; // Add 1 hop to capture the end
                mergedRegions.Add(new TimeRange(startTime, endTime));
                currentRegion = nextRegion;
            }
        }

        // Add the last region
        double lastStartTime = (double)currentRegion.Item1 * hopLength / sampleRate;
        double lastEndTime = (double)(currentRegion.Item2 + 1) * hopLength / sampleRate;
        mergedRegions.Add(new TimeRange(lastStartTime, lastEndTime));

        return mergedRegions;
    }
}
