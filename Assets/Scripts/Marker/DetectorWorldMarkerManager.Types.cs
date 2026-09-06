using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    public struct DetectorHudMarkerState
    {
        public string detectorId;
        public Vector3 worldPosition;
        public float radiationValue;
        public Color color;
    }

    private class PlacementSession
    {
        public string detectorId;
        public bool restoresCommittedMarker;
        public Transform parent;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public bool visibilityRequested;
        public Vector3 savedPosition;
        public float lastEstimatedDistance;
        public float lastQrPixelSize;
        public Vector2 lastPlacementImagePoint;
        public int lastImageWidth;
        public int lastImageHeight;
        public string lastPlacementMethod;
        public bool hasValidPlaneHit;
        public string anchorState;
    }

    private class DetectorMoveSession
    {
        public SourceMarker marker;
        public Transform parent;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public bool visibilityRequested;
        public Vector3 savedPosition;
        public float lastEstimatedDistance;
        public string lastPlacementMethod;
        public ARAnchor anchor;
        public string anchorGuid;
        public string anchorState;
        public Vector3 initialPointerDirection;
        public Vector3 initialOffsetFromRayOrigin;
    }

}
