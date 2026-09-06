using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private void InitializePlacedDetectorOrderFromDatabase()
    {
        placedDetectorOrder.Clear();

        if (!useCoordinateDatabase || coordinateDatabase == null)
            return;

        IReadOnlyList<DetectorCoordinateRecord> records = coordinateDatabase.GetAllRecords();
        List<DetectorCoordinateRecord> sequencedRecords =
            new List<DetectorCoordinateRecord>();

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
                RecordPlacedDetector(record.detectorId);
        }

        sequencedRecords.Sort((left, right) =>
            left.lastPlacedSequence.CompareTo(right.lastPlacedSequence));

        for (int i = 0; i < sequencedRecords.Count; i++)
            RecordPlacedDetector(sequencedRecords[i].detectorId);
    }

    private void RecordPlacedDetector(string detectorId, bool persistOrder = false)
    {
        detectorId = NormalizeDetectorId(detectorId);
        if (string.IsNullOrEmpty(detectorId))
            return;

        RemoveFromPlacedDetectorOrder(detectorId);
        placedDetectorOrder.Add(detectorId);

        if (persistOrder && useCoordinateDatabase && coordinateDatabase != null)
            coordinateDatabase.MarkDetectorPlaced(detectorId);
    }

    private bool TryGetMostRecentlyPlacedDetector(
        out string detectorId,
        out string pendingDetectorId)
    {
        detectorId = "";
        pendingDetectorId = "";

        for (int i = placedDetectorOrder.Count - 1; i >= 0; i--)
        {
            string candidateId = NormalizeDetectorId(placedDetectorOrder[i]);
            if (markers.TryGetValue(candidateId, out MarkerInfo marker) &&
                marker != null && marker.root != null && marker.isPlaced)
            {
                detectorId = marker.detectorId;
                return true;
            }

            if (coordinateDatabase != null &&
                coordinateDatabase.TryGetRecord(
                    candidateId,
                    out DetectorCoordinateRecord pendingRecord) &&
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

            placedDetectorOrder.RemoveAt(i);
        }

        return false;
    }

    private void RemoveFromPlacedDetectorOrder(string detectorId)
    {
        detectorId = NormalizeDetectorId(detectorId);
        for (int i = placedDetectorOrder.Count - 1; i >= 0; i--)
        {
            if (DetectorIdsEqual(placedDetectorOrder[i], detectorId))
                placedDetectorOrder.RemoveAt(i);
        }
    }

    private string NormalizeDetectorId(string rawQrText)
    {
        return string.IsNullOrWhiteSpace(rawQrText) ? "" : rawQrText.Trim();
    }

    private bool DetectorIdsEqual(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
