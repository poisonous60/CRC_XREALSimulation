using UnityEngine;

/// Visible extent of the head camera at a given distance. The editor measures the game view, a
/// build assumes the XREAL display, since Camera.aspect follows the portrait phone screen there.
public static class HeadViewport
{
    private const float DisplayAspect = 16f / 9f;

    public static float Aspect(Camera camera)
    {
#if UNITY_EDITOR
        return camera != null && camera.aspect > 0f ? camera.aspect : DisplayAspect;
#else
        return DisplayAspect;
#endif
    }

    public static float VisibleHeight(Camera camera, float distance)
    {
        Vector3 bottom = camera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 top = camera.ViewportToWorldPoint(new Vector3(0f, 1f, distance));
        return Vector3.Distance(bottom, top);
    }

    public static float VisibleWidth(Camera camera, float distance)
    {
        return VisibleHeight(camera, distance) * Aspect(camera);
    }
}
