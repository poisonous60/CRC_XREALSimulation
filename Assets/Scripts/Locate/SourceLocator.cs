using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// Port of msv100-calibration/locate.py: the source position inside the detector square.
/// </summary>
public sealed class SourceLocator
{
    public const int WindowSeconds = 3;

    private const double GridStepCm = 1.0;
    private const double TouchDistanceCm = 1.0;
    private const double NearFloorCm = 3.0;
    private const double ConfidenceDrop = 2.0;
    private const double MotionSigmaCm = 0.3;
    private const double JumpSigmaCm = 8.0;
    private const double JumpWeight = 0.03;
    private const int AmplitudeSteps = 20;

    public readonly struct Result
    {
        public Result(double x, double y, double radius, double amplitude)
        {
            X = x;
            Y = y;
            Radius = radius;
            Amplitude = amplitude;
        }

        public double X { get; }
        public double Y { get; }
        public double Radius { get; }
        public double Amplitude { get; }
    }

    private sealed class Unit
    {
        public string Name;
        public double X;
        public double Y;
        public double Background;
        public double[] AnchorDistances;
        public double[] AnchorRates;
    }

    private readonly Unit[] units;
    private readonly double[] backgrounds;
    private readonly double[] cellX;
    private readonly double[] cellY;
    private readonly double[][] cellResponses;
    private readonly int side;
    private readonly CpsWindow window;
    private readonly Tracker tracker;

    private readonly double[] scoreValues;
    private readonly double[] scoreAmplitudes;

    public IReadOnlyList<string> DetectorNames { get; }

    public double DetectorX(int index) => units[index].X;

    public double DetectorY(int index) => units[index].Y;

    private SourceLocator(Unit[] builtUnits)
    {
        units = builtUnits;
        backgrounds = new double[units.Length];
        string[] names = new string[units.Length];
        for (int index = 0; index < units.Length; index++)
        {
            backgrounds[index] = units[index].Background;
            names[index] = units[index].Name;
        }

        DetectorNames = names;

        double minimumX = double.PositiveInfinity;
        double minimumY = double.PositiveInfinity;
        double maximumX = double.NegativeInfinity;
        double maximumY = double.NegativeInfinity;
        for (int index = 0; index < units.Length; index++)
        {
            minimumX = Math.Min(minimumX, units[index].X);
            minimumY = Math.Min(minimumY, units[index].Y);
            maximumX = Math.Max(maximumX, units[index].X);
            maximumY = Math.Max(maximumY, units[index].Y);
        }

        int columns = CellsAcross(maximumX - minimumX);
        int rows = CellsAcross(maximumY - minimumY);
        int cellCount = columns * rows;

        cellX = new double[cellCount];
        cellY = new double[cellCount];
        cellResponses = new double[cellCount][];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;
                double x = minimumX + column * GridStepCm;
                double y = minimumY + row * GridStepCm;
                double[] responses = new double[units.Length];
                for (int unitIndex = 0; unitIndex < units.Length; unitIndex++)
                {
                    double dx = x - units[unitIndex].X;
                    double dy = y - units[unitIndex].Y;
                    responses[unitIndex] = Response(units[unitIndex], Math.Sqrt(dx * dx + dy * dy));
                }

                cellX[index] = x;
                cellY[index] = y;
                cellResponses[index] = responses;
            }
        }

        side = columns;
        scoreValues = new double[cellCount];
        scoreAmplitudes = new double[cellCount];
        window = new CpsWindow(names, WindowSeconds);
        tracker = new Tracker(cellCount, side, backgrounds, cellResponses, cellX, cellY);
    }

    public void ResetTracker()
    {
        window.Clear();
        tracker.Reset();
    }

    public bool TryGetLatestRate(string detectorName, out double rate)
    {
        return window.TryGetLatestRate(detectorName, out rate);
    }

    public bool TryPush(string serverTime, IReadOnlyDictionary<string, float> devices, out Result result)
    {
        result = default;

        if (!window.Push(serverTime, devices))
            return false;

        if (!window.TryCounts(out double[] counts, out double seconds) || seconds < WindowSeconds)
            return false;

        if (!window.TryNewest(out double[] newest))
            return false;

        ScoreCells(counts, seconds);
        result = tracker.Update(scoreValues, scoreAmplitudes, newest);
        return true;
    }

    private void ScoreCells(double[] counts, double seconds)
    {
        for (int index = 0; index < cellResponses.Length; index++)
        {
            double[] responses = cellResponses[index];
            double amplitude = SolveAmplitude(counts, responses, backgrounds, seconds);
            scoreAmplitudes[index] = amplitude;
            scoreValues[index] = LogLikelihood(counts, responses, backgrounds, seconds, amplitude);
        }
    }

    private static double Response(Unit unit, double distance)
    {
        double[] distances = unit.AnchorDistances;
        double[] rates = unit.AnchorRates;
        distance = Math.Max(distance, NearFloorCm);

        int last = distances.Length - 1;
        double farDistance = distances[last];
        double farRate = rates[last];

        if (distance >= farDistance)
        {
            double ratio = farDistance / distance;
            return farRate * ratio * ratio;
        }

        for (int index = 0; index + 1 < distances.Length; index++)
        {
            double d0 = distances[index];
            double r0 = rates[index];
            double d1 = distances[index + 1];
            double r1 = rates[index + 1];

            if (distance <= d1)
            {
                double exponent = Math.Log(r1 / r0) / Math.Log(d1 / d0);
                return r0 * Math.Pow(distance / d0, exponent);
            }
        }

        return farRate;
    }

    private static double SolveAmplitude(
        double[] counts,
        double[] responses,
        double[] backgroundRates,
        double seconds)
    {
        double total = 0.0;
        for (int index = 0; index < responses.Length; index++)
            total += responses[index];

        if (total <= 0.0)
            return 0.0;

        double amplitude = 1.0;
        for (int step = 0; step < AmplitudeSteps; step++)
        {
            double weighted = 0.0;
            for (int index = 0; index < responses.Length; index++)
            {
                double response = responses[index];
                weighted += counts[index] * response /
                            (amplitude * response + backgroundRates[index]);
            }

            amplitude *= weighted / (seconds * total);
            if (amplitude <= 0.0)
                return 0.0;
        }

        return amplitude;
    }

    private static double LogLikelihood(
        double[] counts,
        double[] responses,
        double[] backgroundRates,
        double seconds,
        double amplitude)
    {
        double total = 0.0;
        for (int index = 0; index < responses.Length; index++)
        {
            double rate = Math.Max(
                seconds * (amplitude * responses[index] + backgroundRates[index]),
                1e-9);
            total += counts[index] * Math.Log(rate) - rate;
        }

        return total;
    }

    public static bool TryBuild(string layoutJson, out SourceLocator locator, out string message)
    {
        locator = null;

        if (string.IsNullOrWhiteSpace(layoutJson))
        {
            message = "layout json is empty";
            return false;
        }

        JObject layout;
        try
        {
            layout = JObject.Parse(layoutJson);
        }
        catch (Exception error)
        {
            message = $"layout json is not valid: {error.Message}";
            return false;
        }

        JObject detectors = layout["detectors"] as JObject;
        JObject calibration = layout["calibration"] as JObject;
        if (detectors == null || calibration == null)
        {
            message = "layout json needs a detectors and a calibration object";
            return false;
        }

        List<string> names = new List<string>();
        foreach (KeyValuePair<string, JToken> pair in detectors)
            names.Add(pair.Key);

        if (names.Count < 3)
        {
            message = $"layout json needs at least three detectors, found {names.Count}";
            return false;
        }

        names.Sort(StringComparer.Ordinal);

        double[] positionX = new double[names.Count];
        double[] positionY = new double[names.Count];
        for (int index = 0; index < names.Count; index++)
        {
            JArray point = detectors[names[index]] as JArray;
            if (point == null || point.Count < 2)
            {
                message = $"{names[index]} has no [x, y] coordinate";
                return false;
            }

            positionX[index] = (double)point[0];
            positionY[index] = (double)point[1];
        }

        JObject background = calibration["background_cps"] as JObject;
        JObject touch = calibration["touch_net_cps"] as JObject;
        JObject center = calibration["center_net_cps"] as JObject;
        JObject edges = calibration["edge_net_cps"] as JObject;

        if (background == null || touch == null || center == null)
        {
            message = "calibration needs background_cps, touch_net_cps and center_net_cps";
            return false;
        }

        double centerX = 0.0;
        double centerY = 0.0;
        for (int index = 0; index < names.Count; index++)
        {
            centerX += positionX[index];
            centerY += positionY[index];
        }

        centerX /= names.Count;
        centerY /= names.Count;

        if (!TrySideMidpoints(positionX, positionY, out double[] midpointX, out double[] midpointY))
        {
            message = "the detector positions have no shortest side";
            return false;
        }

        Unit[] units = new Unit[names.Count];
        for (int index = 0; index < names.Count; index++)
        {
            string name = names[index];

            if (!TryReadRate(touch, name, out double touchRate) ||
                !TryReadRate(center, name, out double centerRate) ||
                !TryReadRate(background, name, out double backgroundRate))
            {
                message = $"{name} is missing a calibration entry";
                return false;
            }

            List<double> anchorDistances = new List<double> { TouchDistanceCm };
            List<double> anchorRates = new List<double> { touchRate };

            if (edges != null && TryReadRate(edges, name, out double edgeRate))
            {
                double nearest = double.PositiveInfinity;
                for (int midpoint = 0; midpoint < midpointX.Length; midpoint++)
                {
                    double dx = positionX[index] - midpointX[midpoint];
                    double dy = positionY[index] - midpointY[midpoint];
                    nearest = Math.Min(nearest, Math.Sqrt(dx * dx + dy * dy));
                }

                anchorDistances.Add(nearest);
                anchorRates.Add(edgeRate);
            }

            double toCenterX = positionX[index] - centerX;
            double toCenterY = positionY[index] - centerY;
            anchorDistances.Add(Math.Sqrt(toCenterX * toCenterX + toCenterY * toCenterY));
            anchorRates.Add(centerRate);

            for (int anchor = 0; anchor + 1 < anchorDistances.Count; anchor++)
            {
                double d0 = anchorDistances[anchor];
                double d1 = anchorDistances[anchor + 1];
                double r0 = anchorRates[anchor];
                double r1 = anchorRates[anchor + 1];

                if (!(d1 > d0 && d0 > 0.0) || !(r0 > r1 && r1 > 0.0))
                {
                    message = $"{name}: anchors must fall with distance, got " +
                              $"({d0:0.##}, {r0:0.##}) then ({d1:0.##}, {r1:0.##})";
                    return false;
                }
            }

            units[index] = new Unit
            {
                Name = name,
                X = positionX[index],
                Y = positionY[index],
                Background = backgroundRate,
                AnchorDistances = anchorDistances.ToArray(),
                AnchorRates = anchorRates.ToArray(),
            };
        }

        int columns = CellsAcross(MaximumOf(positionX) - MinimumOf(positionX));
        int rows = CellsAcross(MaximumOf(positionY) - MinimumOf(positionY));
        if (columns != rows)
        {
            message = $"the grid must be square, got {columns} by {rows} cells";
            return false;
        }

        locator = new SourceLocator(units);
        message = $"{units.Length} detectors, {columns * rows} cells";
        return true;
    }

    private static bool TryReadRate(JObject source, string name, out double rate)
    {
        JToken token = source[name];
        if (token == null)
        {
            rate = 0.0;
            return false;
        }

        rate = (double)token;
        return true;
    }

    private static bool TrySideMidpoints(
        double[] positionX,
        double[] positionY,
        out double[] midpointX,
        out double[] midpointY)
    {
        List<double> resultX = new List<double>();
        List<double> resultY = new List<double>();
        double shortest = double.PositiveInfinity;

        for (int a = 0; a < positionX.Length; a++)
        {
            for (int b = a + 1; b < positionX.Length; b++)
            {
                double dx = positionX[a] - positionX[b];
                double dy = positionY[a] - positionY[b];
                shortest = Math.Min(shortest, Math.Sqrt(dx * dx + dy * dy));
            }
        }

        if (double.IsInfinity(shortest) || shortest <= 0.0)
        {
            midpointX = null;
            midpointY = null;
            return false;
        }

        for (int a = 0; a < positionX.Length; a++)
        {
            for (int b = a + 1; b < positionX.Length; b++)
            {
                double dx = positionX[a] - positionX[b];
                double dy = positionY[a] - positionY[b];
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (Math.Abs(distance - shortest) > 1e-6 * shortest)
                    continue;

                resultX.Add((positionX[a] + positionX[b]) / 2.0);
                resultY.Add((positionY[a] + positionY[b]) / 2.0);
            }
        }

        midpointX = resultX.ToArray();
        midpointY = resultY.ToArray();
        return midpointX.Length > 0;
    }

    // Python walks the grid with "while value <= maximum + 1e-9", so a span that is not a
    // whole number of steps stops short instead of rounding up to one more cell.
    private static int CellsAcross(double span)
    {
        return (int)Math.Floor(span / GridStepCm + 1e-9) + 1;
    }

    private static double MinimumOf(double[] values)
    {
        double result = double.PositiveInfinity;
        for (int index = 0; index < values.Length; index++)
            result = Math.Min(result, values[index]);

        return result;
    }

    private static double MaximumOf(double[] values)
    {
        double result = double.NegativeInfinity;
        for (int index = 0; index < values.Length; index++)
            result = Math.Max(result, values[index]);

        return result;
    }

    private sealed class Tracker
    {
        private readonly int cellCount;
        private readonly int side;
        private readonly double[] backgrounds;
        private readonly double[][] cellResponses;
        private readonly double[] cellX;
        private readonly double[] cellY;
        private readonly double[][] motionKernel;
        private readonly double[][] jumpKernel;
        private readonly double[] posterior;
        private readonly double[] prior;
        private readonly double[] logPosterior;
        private readonly double[] scratchRow;
        private readonly double[] scratchAlongX;

        public Tracker(
            int cellCount,
            int side,
            double[] backgrounds,
            double[][] cellResponses,
            double[] cellX,
            double[] cellY)
        {
            this.cellCount = cellCount;
            this.side = side;
            this.backgrounds = backgrounds;
            this.cellResponses = cellResponses;
            this.cellX = cellX;
            this.cellY = cellY;
            motionKernel = BuildBlurMatrix(MotionSigmaCm / GridStepCm, side);
            jumpKernel = BuildBlurMatrix(JumpSigmaCm / GridStepCm, side);
            posterior = new double[cellCount];
            prior = new double[cellCount];
            logPosterior = new double[cellCount];
            scratchRow = new double[cellCount];
            scratchAlongX = new double[cellCount];
            Reset();
        }

        public void Reset()
        {
            double uniform = 1.0 / cellCount;
            for (int index = 0; index < cellCount; index++)
                posterior[index] = uniform;
        }

        public Result Update(double[] scoreValues, double[] scoreAmplitudes, double[] newest)
        {
            for (int index = 0; index < cellCount; index++)
                prior[index] = 0.0;

            AccumulateBlur(motionKernel, 1.0 - JumpWeight);
            AccumulateBlur(jumpKernel, JumpWeight);

            double top = double.NegativeInfinity;
            for (int index = 0; index < cellCount; index++)
            {
                double value = Math.Log(Math.Max(prior[index], 1e-300)) +
                               LogLikelihood(
                                   newest,
                                   cellResponses[index],
                                   backgrounds,
                                   1.0,
                                   scoreAmplitudes[index]);
                logPosterior[index] = value;
                if (value > top)
                    top = value;
            }

            double total = 0.0;
            for (int index = 0; index < cellCount; index++)
            {
                double value = Math.Exp(logPosterior[index] - top);
                posterior[index] = value;
                total += value;
            }

            for (int index = 0; index < cellCount; index++)
                posterior[index] /= total;

            double cutoff = top - ConfidenceDrop;
            double mass = 0.0;
            double meanX = 0.0;
            double meanY = 0.0;
            double bestValue = double.NegativeInfinity;
            double bestX = 0.0;
            double bestY = 0.0;
            double bestAmplitude = 0.0;

            for (int index = 0; index < cellCount; index++)
            {
                double value = logPosterior[index];
                if (value < cutoff)
                    continue;

                double weight = Math.Exp(value - top);
                mass += weight;
                meanX += weight * cellX[index];
                meanY += weight * cellY[index];

                // Python takes max() over (value, x, y, amplitude) tuples, so a tie on the
                // value falls through to the larger x, then y, then amplitude.
                if (value > bestValue ||
                    (value == bestValue &&
                     (cellX[index] > bestX ||
                      (cellX[index] == bestX &&
                       (cellY[index] > bestY ||
                        (cellY[index] == bestY && scoreAmplitudes[index] > bestAmplitude))))))
                {
                    bestValue = value;
                    bestX = cellX[index];
                    bestY = cellY[index];
                    bestAmplitude = scoreAmplitudes[index];
                }
            }

            meanX /= mass;
            meanY /= mass;

            double radius = 0.0;
            for (int index = 0; index < cellCount; index++)
            {
                if (logPosterior[index] < cutoff)
                    continue;

                double dx = cellX[index] - meanX;
                double dy = cellY[index] - meanY;
                radius = Math.Max(radius, Math.Sqrt(dx * dx + dy * dy));
            }

            return new Result(
                meanX,
                meanY,
                Math.Max(radius, GridStepCm / 2.0),
                bestAmplitude);
        }

        private void AccumulateBlur(double[][] matrix, double weight)
        {
            for (int row = 0; row < side; row++)
            {
                int offset = row * side;
                for (int column = 0; column < side; column++)
                {
                    double sum = 0.0;
                    double[] kernelRow = matrix[column];
                    for (int source = 0; source < side; source++)
                        sum += kernelRow[source] * posterior[offset + source];

                    scratchRow[offset + column] = sum;
                }
            }

            for (int row = 0; row < side; row++)
            {
                double[] kernelRow = matrix[row];
                for (int column = 0; column < side; column++)
                {
                    double sum = 0.0;
                    for (int source = 0; source < side; source++)
                        sum += kernelRow[source] * scratchRow[source * side + column];

                    scratchAlongX[row * side + column] = sum;
                }
            }

            for (int index = 0; index < cellCount; index++)
                prior[index] += weight * scratchAlongX[index];
        }

        private static double[][] BuildBlurMatrix(double sigmaCells, int size)
        {
            double[][] matrix = new double[size][];
            for (int row = 0; row < size; row++)
            {
                matrix[row] = new double[size];
                for (int column = 0; column < size; column++)
                {
                    double offset = row - column;
                    matrix[row][column] =
                        Math.Exp(-(offset * offset) / (2.0 * sigmaCells * sigmaCells));
                }
            }

            for (int column = 0; column < size; column++)
            {
                double sum = 0.0;
                for (int row = 0; row < size; row++)
                    sum += matrix[row][column];

                for (int row = 0; row < size; row++)
                    matrix[row][column] /= sum;
            }

            return matrix;
        }
    }

    private sealed class Slot
    {
        public string Time;
        public double[] Values;
        public bool[] Present;
        public int Count;
    }

    private sealed class CpsWindow
    {
        private readonly string[] names;
        private readonly int seconds;
        private readonly List<Slot> slots = new List<Slot>();
        private readonly double[] counts;
        private readonly double[] newest;
        private readonly Dictionary<string, double> latestRates =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public CpsWindow(string[] detectorNames, int windowSeconds)
        {
            names = detectorNames;
            seconds = windowSeconds;
            counts = new double[names.Length];
            newest = new double[names.Length];
        }

        public void Clear()
        {
            slots.Clear();
            latestRates.Clear();
        }

        public bool TryGetLatestRate(string detectorName, out double rate)
        {
            return latestRates.TryGetValue(detectorName, out rate);
        }

        // Mirrors the Python OrderedDict: a value for a second already seen updates that
        // second in place and reports no new second, so one server second counts once.
        public bool Push(string serverTime, IReadOnlyDictionary<string, float> devices)
        {
            if (devices == null)
                return false;

            for (int index = 0; index < names.Length; index++)
            {
                if (devices.TryGetValue(names[index], out float value))
                    latestRates[names[index]] = value;
            }

            Slot slot = null;
            for (int index = slots.Count - 1; index >= 0; index--)
            {
                if (string.Equals(slots[index].Time, serverTime, StringComparison.Ordinal))
                {
                    slot = slots[index];
                    break;
                }
            }

            bool started = slot == null;
            if (started)
            {
                slot = new Slot
                {
                    Time = serverTime,
                    Values = new double[names.Length],
                    Present = new bool[names.Length],
                    Count = 0,
                };
                slots.Add(slot);
            }

            for (int index = 0; index < names.Length; index++)
            {
                if (!devices.TryGetValue(names[index], out float value))
                    continue;

                if (!slot.Present[index])
                {
                    slot.Present[index] = true;
                    slot.Count++;
                }

                slot.Values[index] = value;
            }

            while (slots.Count > seconds + 1)
                slots.RemoveAt(0);

            return started;
        }

        public bool TryCounts(out double[] windowCounts, out double windowSeconds)
        {
            windowCounts = counts;
            windowSeconds = 0.0;

            for (int index = 0; index < counts.Length; index++)
                counts[index] = 0.0;

            int first = FirstCompleteIndex(out int completeCount);
            if (completeCount <= 0)
                return false;

            int taken = 0;
            for (int index = first; index < slots.Count - 1; index++)
            {
                Slot slot = slots[index];
                if (slot.Count != names.Length)
                    continue;

                for (int unit = 0; unit < counts.Length; unit++)
                    counts[unit] += slot.Values[unit];

                taken++;
            }

            windowSeconds = taken;
            return taken > 0;
        }

        public bool TryNewest(out double[] values)
        {
            values = newest;

            for (int index = slots.Count - 2; index >= 0; index--)
            {
                Slot slot = slots[index];
                if (slot.Count != names.Length)
                    continue;

                Array.Copy(slot.Values, newest, newest.Length);
                return true;
            }

            return false;
        }

        // complete() in Python drops the newest slot, which is still being filled, then keeps
        // the last `seconds` complete ones.
        private int FirstCompleteIndex(out int completeCount)
        {
            completeCount = 0;
            int firstKept = slots.Count - 1;

            for (int index = slots.Count - 2; index >= 0; index--)
            {
                if (slots[index].Count != names.Length)
                    continue;

                completeCount++;
                firstKept = index;
                if (completeCount == seconds)
                    break;
            }

            return firstKept;
        }
    }
}
