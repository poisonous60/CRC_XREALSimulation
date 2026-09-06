using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private enum RadiationRiskBand
    {
        Unknown,
        Hidden,
        Green,
        Yellow,
        Red
    }


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

    private class MarkerInfo
    {
        public string detectorId;
        public GameObject root;
        public Renderer renderer;
        public TMP_Text label;
        public Vector3 savedPosition;
        public float lastRadiationValue;
        public float lastEstimatedDistance;
        public float lastQrPixelSize;
        public Vector2 lastPlacementImagePoint;
        public int lastImageWidth;
        public int lastImageHeight;
        public string lastPlacementMethod;
        public bool isFollowingPlacementOrigin;
        public bool isPlaced;
        public bool hasValidPlaneHit;
        public bool visibilityRequested;
        public bool centerVisualRequested;
        public bool isControllerHovered;
        public bool isControllerMoving;
        public List<FalloffShellInfo> falloffShells;
        public Material centerMaterial;
        public ARAnchor anchor;
        public string anchorGuid;
        public string anchorState;
    }

    private class DetectorMoveSession
    {
        public MarkerInfo marker;
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

    private class FalloffShellInfo
    {
        public GameObject root;
        public Renderer renderer;
        public bool visualRequested;
        public Material material;
    }
}
