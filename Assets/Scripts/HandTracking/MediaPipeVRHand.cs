
using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;

public class MediaPipeVRHand : MonoBehaviour
{
    [Header("References")]
    public Camera sceneCamera;
    public Transform handRoot;

    [Header("Fingers")]
    public FingerRig thumb = new FingerRig();
    public FingerRig index = new FingerRig();
    public FingerRig middle = new FingerRig();
    public FingerRig ring = new FingerRig();
    public FingerRig pinky = new FingerRig();

    [Header("Movement")]
    public float movementScale = 5f;
    public float smoothing = 30f;
    public bool mirrorX = false;
    public bool mirrorY = true;

    [Header("Pinch Detection")]
    public float pinchThreshold = 0.4f;
    public bool IsPinching { get; private set; }

    private Vector3 initialLocalPosition;
    private Vector2 initialWrist;


    private float trackingDepth;

    void Start()
    {

        if (sceneCamera == null || handRoot == null)
        {
            Debug.LogError(
                "[VR HAND] Missing camera or hand root!"
            );
            enabled = false;
            return;
        }

        trackingDepth = sceneCamera.WorldToViewportPoint(
    handRoot.position
).z;


        initialLocalPosition = handRoot.localPosition;

        thumb.Initialize();
        index.Initialize();
        middle.Initialize();
        ring.Initialize();
        pinky.Initialize();

        Debug.Log("[VR HAND] Initialized");
    }

    public void Apply(HandLandmarkerResult result)
    {
        if (!isActiveAndEnabled)
            return;

        if (result.handLandmarks == null ||
            result.handLandmarks.Count == 0 ||
            result.handWorldLandmarks == null ||
            result.handWorldLandmarks.Count == 0)
        {

            IsPinching = false;
            return;
        }

        var points = result.handLandmarks[0].landmarks;
        var world = result.handWorldLandmarks[0].landmarks;

        if (points.Count < 21 || world.Count < 21)
            return;

        float t = 1f - Mathf.Exp(
            -smoothing * Time.deltaTime
        );


        var wrist = points[0];

        float x = mirrorX ? 1f - wrist.x : wrist.x;
        float y = mirrorY ? 1f - wrist.y : wrist.y;

        Vector3 target = sceneCamera.ViewportToWorldPoint(
            new Vector3(x, y, trackingDepth)
        );

        handRoot.position = target;


        thumb.Apply(world, 1, t);
        index.Apply(world, 5, t);
        middle.Apply(world, 9, t);
        ring.Apply(world, 13, t);
        pinky.Apply(world, 17, t);

        var thumbTip = points[4];
        var indexTip = points[8];

        var indexBase = points[5];
        var pinkyBase = points[17];

        float distance = Vector2.Distance(
            new Vector2(thumbTip.x, thumbTip.y),
            new Vector2(indexTip.x, indexTip.y)
        );

        float palmWidth = Vector2.Distance(
            new Vector2(indexBase.x, indexBase.y),
            new Vector2(pinkyBase.x, pinkyBase.y)
        );

        bool previous = IsPinching;

        float threshold = IsPinching
            ? pinchThreshold * 1.35f
            : pinchThreshold;

        IsPinching = palmWidth > 0.001f &&
            distance / palmWidth < threshold;

        if (IsPinching != previous)
        {
            Debug.Log(
                IsPinching
                    ? "[VR HAND] PINCH"
                    : "[VR HAND] RELEASE"
            );
        }
    }
}
