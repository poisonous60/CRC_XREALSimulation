public partial class DetectorWorldMarkerManager
{
    private readonly PlacedDetectorOrder placedDetectorOrder = new PlacedDetectorOrder();

    private void InitializePlacedDetectorOrderFromDatabase()
    {
        placedDetectorOrder.InitializeFromDatabase(
            useCoordinateDatabase ? coordinateDatabase : null);
    }

    private void RecordPlacedDetector(string detectorId, bool persistOrder = false)
    {
        placedDetectorOrder.Record(detectorId);

        if (persistOrder && useCoordinateDatabase && coordinateDatabase != null)
            coordinateDatabase.MarkDetectorPlaced(NormalizeDetectorId(detectorId));
    }

    private bool TryGetMostRecentlyPlacedDetector(
        out string detectorId,
        out string pendingDetectorId)
    {
        return placedDetectorOrder.TryGetMostRecentlyPlaced(
            markers,
            coordinateDatabase,
            roomCoordinateSystem,
            out detectorId,
            out pendingDetectorId);
    }

    private void RemoveFromPlacedDetectorOrder(string detectorId)
    {
        placedDetectorOrder.Remove(detectorId);
    }

    private string NormalizeDetectorId(string rawQrText)
    {
        return DetectorId.Normalize(rawQrText);
    }

    private bool DetectorIdsEqual(string left, string right)
    {
        return DetectorId.Equals(left, right);
    }
}
