using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private void EnsurePlacementOrigin()
    {
        if (fallbackCamera == null)
            fallbackCamera = Camera.main;

        if (placementOrigin == null && fallbackCamera != null)
            placementOrigin = fallbackCamera.transform;
    }

    private void EnsurePlaneDetectionManager()
    {
        if (!usePlaneIntersectionPlacement)
            return;

        if (planeManager == null)
            planeManager = FindFirstObjectByType<ARPlaneManager>(FindObjectsInactive.Include);

        if (planeManager == null)
        {
            if (!warnedAboutMissingPlaneManager)
            {
                Debug.LogError("[DetectorWorldMarkerManager] Plane intersection placement requires an ARPlaneManager on the XR Origin.");
                warnedAboutMissingPlaneManager = true;
            }

            return;
        }

        warnedAboutMissingPlaneManager = false;
        planeManager.requestedDetectionMode = planeDetectionMode;

        if (!planeManager.enabled)
            planeManager.enabled = true;
    }

    private void EnsureArGlassesHud()
    {
        if (!enableArGlassesHud)
            return;

        if (arGlassesHud == null)
            arGlassesHud = GetComponent<ARDetectorHud>();

        if (arGlassesHud == null)
            arGlassesHud = gameObject.AddComponent<ARDetectorHud>();

        arGlassesHud.Initialize(this, fallbackCamera);
    }

    private void EnsureCoordinateDatabase()
    {
        if (!useCoordinateDatabase)
            return;

        if (coordinateDatabase != null)
            return;

        coordinateDatabase = FindFirstObjectByType<DetectorCoordinateDatabase>();

        if (coordinateDatabase == null)
        {
            GameObject dbObject = new GameObject("DetectorCoordinateDatabase");
            coordinateDatabase = dbObject.AddComponent<DetectorCoordinateDatabase>();
            Debug.Log("[DetectorWorldMarkerManager] Created DetectorCoordinateDatabase automatically.");
        }
    }

    private void EnsureRoomCoordinateSystem()
    {
        if (!enableRoomCoordinateSystem)
            return;

        if (roomCoordinateSystem == null)
            roomCoordinateSystem = GetComponent<RoomCoordinateSystem>();

        if (roomCoordinateSystem == null)
            roomCoordinateSystem = gameObject.AddComponent<RoomCoordinateSystem>();

        roomCoordinateSystem.Initialize(this, coordinateDatabase, fallbackCamera);
    }

    private void EnsureRadiationReceiver()
    {
        if (radiationReceiver == null)
            radiationReceiver = FindFirstObjectByType<RadiationReceiver>();
    }

    private void EnsureDetectorControllerInteractor()
    {
        if (!enableControllerDetectorReposition)
            return;

        if (detectorControllerInteractor == null)
            detectorControllerInteractor = GetComponent<DetectorControllerInteractor>();

        if (detectorControllerInteractor == null)
            detectorControllerInteractor = gameObject.AddComponent<DetectorControllerInteractor>();

        detectorControllerInteractor.Initialize(this);
    }

    private void EnsureSpatialAnchorManager()
    {
        if (!useSpatialAnchors)
            return;

        if (spatialAnchorManager != null)
            return;

        spatialAnchorManager = FindFirstObjectByType<DetectorSpatialAnchorManager>();
    }

    private void SubscribeSpatialEvents()
    {
        if (!useSpatialAnchors || spatialAnchorManager == null || spatialEventsSubscribed)
            return;

        spatialAnchorManager.AnchorSaved += HandleAnchorCreatedAndSaved;
        spatialAnchorManager.AnchorLoaded += HandleAnchorLoaded;
        spatialAnchorManager.AnchorSaveFailed += HandleAnchorSaveFailed;
        spatialAnchorManager.AnchorLoadFailed += HandleAnchorLoadFailed;
        spatialEventsSubscribed = true;
    }

    private void UnsubscribeSpatialEvents()
    {
        if (spatialAnchorManager == null || !spatialEventsSubscribed)
            return;

        spatialAnchorManager.AnchorSaved -= HandleAnchorCreatedAndSaved;
        spatialAnchorManager.AnchorLoaded -= HandleAnchorLoaded;
        spatialAnchorManager.AnchorSaveFailed -= HandleAnchorSaveFailed;
        spatialAnchorManager.AnchorLoadFailed -= HandleAnchorLoadFailed;
        spatialEventsSubscribed = false;
    }
}
