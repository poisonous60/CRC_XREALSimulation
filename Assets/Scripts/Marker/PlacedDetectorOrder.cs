using System.Collections.Generic;

/// <summary>
/// Which detector was placed last. Cancel walks this from the newest backwards.
/// </summary>
public class PlacedDetectorOrder
{
    private readonly List<string> order = new List<string>();

    public void Clear()
    {
        order.Clear();
    }

    public void InitializeFromDatabase(DetectorCoordinateDatabase database)
    {
        order.Clear();

        if (database == null)
            return;

        IReadOnlyList<DetectorCoordinateRecord> records = database.GetAllRecords();
        List<DetectorCoordinateRecord> sequencedRecords = new List<DetectorCoordinateRecord>();

        // Legacy JSON has no sequence. Preserve its existing order, then append
        // records that have an explicit persisted placement sequence.
        for (int i = 0; i < records.Count; i++)
        {
            DetectorCoordinateRecord record = records[i];
            if (record == null || string.IsNullOrWhiteSpace(record.detectorId))
                continue;

            if (record.lastPlacedSequence > 0L)
                sequencedRecords.Add(record);
            else
                Record(record.detectorId);
        }

        sequencedRecords.Sort((left, right) =>
            left.lastPlacedSequence.CompareTo(right.lastPlacedSequence));

        for (int i = 0; i < sequencedRecords.Count; i++)
            Record(sequencedRecords[i].detectorId);
    }

    public void Record(string detectorId)
    {
        detectorId = DetectorId.Normalize(detectorId);
        if (string.IsNullOrEmpty(detectorId))
            return;

        Remove(detectorId);
        order.Add(detectorId);
    }

    public void Remove(string detectorId)
    {
        detectorId = DetectorId.Normalize(detectorId);
        for (int i = order.Count - 1; i >= 0; i--)
        {
            if (DetectorId.Equals(order[i], detectorId))
                order.RemoveAt(i);
        }
    }

    public bool TryGetMostRecentlyPlaced(
        Dictionary<string, SourceMarker> markers,
        DetectorCoordinateDatabase database,
        RoomCoordinateSystem roomCoordinateSystem,
        out string detectorId,
        out string pendingDetectorId)
    {
        detectorId = "";
        pendingDetectorId = "";

        for (int i = order.Count - 1; i >= 0; i--)
        {
            string candidateId = DetectorId.Normalize(order[i]);
            if (markers != null &&
                markers.TryGetValue(candidateId, out SourceMarker marker) &&
                marker != null && marker.root != null && marker.isPlaced)
            {
                detectorId = marker.detectorId;
                return true;
            }

            if (database != null &&
                database.TryGetRecord(candidateId, out DetectorCoordinateRecord pendingRecord) &&
                pendingRecord != null)
            {
                // Records from another named room do not participate in this
                // room's LIFO stack.
                if (roomCoordinateSystem != null &&
                    roomCoordinateSystem.IsCalibrated &&
                    pendingRecord.HasRoomPose() &&
                    !pendingRecord.HasRoomPose(roomCoordinateSystem.RoomId))
                {
                    continue;
                }

                // Do not skip over the newest detector while its asynchronous
                // anchor/room restoration is pending and delete an older one.
                pendingDetectorId = pendingRecord.detectorId;
                return false;
            }

            order.RemoveAt(i);
        }

        return false;
    }
}
