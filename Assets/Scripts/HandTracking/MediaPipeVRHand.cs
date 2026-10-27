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

                var wrist = points[0];

                float x = mirrorX ? 1f - wrist.x : wrist.x;
                float y = mirrorY ? 1f - wrist.y : wrist.y;

                Vector3 target = sceneCamera.ViewportToWorldPoint(
                    new Vector3(x, y, hand.trackingDepth)
                );

                hand.handRoot.position = Vector3.Lerp(
                    hand.handRoot.position,
                    target,
                    t
                );

                if (enableRotation)
                {
                    UpdateHandRotation(hand, world);
                }

                hand.thumb.Apply(world, 1, t);
                hand.index.Apply(world, 5, t);
                hand.middle.Apply(world, 9, t);
                hand.ring.Apply(world, 13, t);
                hand.pinky.Apply(world, 17, t);

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

        // Reset 
        if (!leftTracked)
        {
            SetPinching(leftHand, false, "Left");

        }

        if (!rightTracked)
        {
            SetPinching(rightHand, false, "Right");

        }
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
        Mediapipe.Tasks.Components.Containers.Landmark> points)
    {
        // Get 3D landmark positions
        Vector3 wrist = ToCameraSpace(points[0]);
        Vector3 index = ToCameraSpace(points[5]);
        Vector3 middle = ToCameraSpace(points[9]);
        Vector3 pinky = ToCameraSpace(points[17]);

        // Direction from wrist toward fingers
        Vector3 up = middle - wrist;

        // Direction across the palm
        Vector3 across = index - pinky;

        // Direction perpendicular to the palm
        Vector3 forward = Vector3.Cross(across, up);

        if (up.sqrMagnitude < 0.000001f ||
            forward.sqrMagnitude < 0.000001f)
            return;

        // Actual palm orientation from landmarks
        Quaternion trackedRotation =
            Quaternion.LookRotation(
                forward.normalized,
                up.normalized
            );

        // Fixed correction for the imported model's axes
        Quaternion modelOffset =
            Quaternion.Euler(hand.modelRotationOffset);

        Quaternion targetRotation =
            trackedRotation * modelOffset;

        float rotationT = 1f - Mathf.Exp(
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