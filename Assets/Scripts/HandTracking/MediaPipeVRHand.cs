using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;

public class MediaPipeVRHand : MonoBehaviour
{
    [Header("References")]
    public Camera sceneCamera;

    [System.Serializable]
    public class HandRig
    {
        public Transform handRoot;

        public FingerRig thumb = new FingerRig();
        public FingerRig index = new FingerRig();
        public FingerRig middle = new FingerRig();
        public FingerRig ring = new FingerRig();
        public FingerRig pinky = new FingerRig();

        [System.NonSerialized]
        public float trackingDepth;

        [System.NonSerialized]
        public bool isPinching;

        public Vector3 modelRotationOffset = Vector3.zero;

        [System.NonSerialized] public Vector3 baseScale;
        [System.NonSerialized] public float basePalmLength;
        [System.NonSerialized] public bool isVisible = true;
        [System.NonSerialized] public float lastSeenTime;
        [System.NonSerialized] public bool isLost = true;
    }

    public HandRig leftHand = new HandRig();
    public HandRig rightHand = new HandRig();

    [Header("Movement")]
    public float smoothing = 30f;
    public bool mirrorX = false;
    public bool mirrorY = true;

    [Header("Rotation")]
    public bool enableRotation = true;
    public float rotationSmoothing = 20f;

    [Header("Hand Size")]
    public bool matchHandSize = true;
    public float sizeMultiplier = 1f;
    public float sizeSmoothing = 10f;

    [Header("Visibility")]
    public bool hideWhenNotTracked = true;
    public float hideDelay = 0.15f;

    [Header("Pinch Detection")]
    public float pinchThreshold = 0.4f;

    public bool IsPinching =>
        leftHand.isPinching || rightHand.isPinching;

    void Start()
    {
        if (sceneCamera == null ||
            leftHand.handRoot == null ||
            rightHand.handRoot == null)
        {
            Debug.LogError(
                "[VR HAND] Missing camera or hand roots!", this);

            enabled = false;
            return;
        }

        InitializeHand(leftHand);
        InitializeHand(rightHand);
    }

    void InitializeHand(HandRig hand)
    {
        hand.trackingDepth =
            sceneCamera.WorldToViewportPoint(hand.handRoot.position).z;

        hand.thumb.Initialize();
        hand.index.Initialize();
        hand.middle.Initialize();
        hand.ring.Initialize();
        hand.pinky.Initialize();

        hand.baseScale = hand.handRoot.localScale;
        hand.basePalmLength = hand.middle.bone1 != null
            ? Vector3.Distance(hand.handRoot.position, hand.middle.bone1.position)
            : 0f;

        hand.isVisible = true;
        hand.isLost = true;
        SetVisible(hand, !hideWhenNotTracked);
    }

    public void Apply(HandLandmarkerResult result)
    {
        if (!isActiveAndEnabled)
            return;

        bool leftTracked = false;
        bool rightTracked = false;

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);

        if (result.handLandmarks != null &&
            result.handWorldLandmarks != null &&
            result.handedness != null)
        {
            int handCount = Mathf.Min(
                result.handLandmarks.Count,
                Mathf.Min(
                    result.handWorldLandmarks.Count,
                    result.handedness.Count
                )
            );

            for (int handIndex = 0; handIndex < handCount; handIndex++)
            {
                var categories =
                    result.handedness[handIndex].categories;

                if (categories == null || categories.Count == 0)
                    continue;

                string label = categories[0].categoryName;

                HandRig hand;

                if (label == "Left")
                    hand = leftHand;
                else if (label == "Right")
                    hand = rightHand;
                else
                    continue;

                var points =
                    result.handLandmarks[handIndex].landmarks;

                var world =
                    result.handWorldLandmarks[handIndex].landmarks;

                if (points == null || world == null ||
                    points.Count < 21 || world.Count < 21)
                {
                    continue;
                }

                if (label == "Left")
                    leftTracked = true;
                else
                    rightTracked = true;

                bool snap = hand.isLost;
                hand.isLost = false;
                hand.lastSeenTime = Time.time;
                SetVisible(hand, true);

                var wrist = points[0];

                float x = mirrorX ? 1f - wrist.x : wrist.x;
                float y = mirrorY ? 1f - wrist.y : wrist.y;

                Vector3 target = sceneCamera.ViewportToWorldPoint(
                    new Vector3(x, y, hand.trackingDepth)
                );

                hand.handRoot.position = Vector3.Lerp(
                    hand.handRoot.position,
                    target,
                    snap ? 1f : t
                );

                if (enableRotation)
                {
                    UpdateHandRotation(hand, world, snap);
                }

                if (matchHandSize)
                {
                    var knuckle = points[9];
                    float aspect = sceneCamera.aspect;
                    float palmViewport = new Vector3(
                        (knuckle.x - wrist.x) * aspect,
                        knuckle.y - wrist.y,
                        (knuckle.z - wrist.z) * aspect
                    ).magnitude;

                    UpdateHandScale(hand, palmViewport, snap);
                }

                hand.thumb.Apply(world, 1, t, sceneCamera.transform, mirrorX, mirrorY);

                hand.index.Apply(
                    world, 5, t,
                    sceneCamera.transform, mirrorX, mirrorY
                );

                hand.middle.Apply(
                    world, 9, t,
                    sceneCamera.transform, mirrorX, mirrorY
                );

                hand.ring.Apply(
                    world, 13, t,
                    sceneCamera.transform, mirrorX, mirrorY
                );

                hand.pinky.Apply(
                    world, 17, t,
                    sceneCamera.transform, mirrorX, mirrorY
                );

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

                float threshold = hand.isPinching
                    ? pinchThreshold * 1.35f
                    : pinchThreshold;

                bool pinching = palmWidth > 0.001f &&
                    distance / palmWidth < threshold;

                SetPinching(hand, pinching, label);
            }
        }

        if (Time.frameCount % 60 == 0)
        {
            Debug.Log(
                $"[TRACKING] Left: {leftTracked} | Right: {rightTracked}"
            );
        }

        if (!leftTracked)
        {
            SetPinching(leftHand, false, "Left");
            UpdateLostHand(leftHand);
        }

        if (!rightTracked)
        {
            SetPinching(rightHand, false, "Right");
            UpdateLostHand(rightHand);
        }
    }

    void UpdateLostHand(HandRig hand)
    {
        if (hand.isLost || Time.time - hand.lastSeenTime < hideDelay)
            return;

        hand.isLost = true;
        ResetFingers(hand);

        if (hideWhenNotTracked)
            SetVisible(hand, false);
    }

    void SetVisible(HandRig hand, bool visible)
    {
        if (hand.isVisible == visible)
            return;

        hand.isVisible = visible;
        hand.handRoot.localScale = visible ? hand.baseScale : Vector3.zero;
    }

    void UpdateHandScale(HandRig hand, float palmViewport, bool snap)
    {
        if (hand.basePalmLength < 0.00001f || palmViewport < 0.00001f)
            return;

        float frustumHeight = sceneCamera.orthographic
            ? 2f * sceneCamera.orthographicSize
            : 2f * hand.trackingDepth *
              Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        float targetLength = palmViewport * frustumHeight;

        Vector3 targetScale = hand.baseScale *
            (targetLength / hand.basePalmLength * sizeMultiplier);

        float scaleT = snap ? 1f : 1f - Mathf.Exp(
            -sizeSmoothing * Time.deltaTime
        );

        hand.handRoot.localScale = Vector3.Lerp(
            hand.handRoot.localScale,
            targetScale,
            scaleT
        );
    }

    void ResetFingers(HandRig hand)
    {
        hand.thumb.ResetTracking();
        hand.index.ResetTracking();
        hand.middle.ResetTracking();
        hand.ring.ResetTracking();
        hand.pinky.ResetTracking();
    }

    void SetPinching(HandRig hand, bool pinching, string label)
    {
        if (hand.isPinching == pinching)
            return;

        hand.isPinching = pinching;

        Debug.Log(
            $"[VR HAND] {label}: {(pinching ? "PINCH" : "RELEASE")}",
            this
        );
    }

    private void UpdateHandRotation(
    HandRig hand,
    System.Collections.Generic.IList<
        Mediapipe.Tasks.Components.Containers.Landmark> points,
    bool snap)
    {
        Vector3 wrist = ToCameraSpace(points[0]);
        Vector3 index = ToCameraSpace(points[5]);
        Vector3 middle = ToCameraSpace(points[9]);
        Vector3 pinky = ToCameraSpace(points[17]);

        Vector3 up = middle - wrist;

        Vector3 across = index - pinky;

        Vector3 forward = Vector3.Cross(across, up);

        if (up.sqrMagnitude < 0.000001f ||
            forward.sqrMagnitude < 0.000001f)
            return;

        Quaternion trackedRotation =
            Quaternion.LookRotation(
                forward.normalized,
                up.normalized
            );

        Quaternion modelOffset =
            Quaternion.Euler(hand.modelRotationOffset);

        Quaternion targetRotation =
            trackedRotation * modelOffset;

        float rotationT = snap ? 1f : 1f - Mathf.Exp(
            -rotationSmoothing * Time.deltaTime
        );

        hand.handRoot.rotation = Quaternion.Slerp(
            hand.handRoot.rotation,
            targetRotation,
            rotationT
        );
    }

    private Vector3 ToCameraSpace(
        Mediapipe.Tasks.Components.Containers.Landmark p)
    {
        Vector3 local = new Vector3(
            mirrorX ? -p.x : p.x,
            mirrorY ? -p.y : p.y,
            p.z
        );

        return sceneCamera.transform.TransformDirection(local);
    }
}