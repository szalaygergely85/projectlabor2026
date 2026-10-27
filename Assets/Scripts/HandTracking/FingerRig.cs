
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Components.Containers;

[System.Serializable]
public class FingerRig
{
    [Header("Finger Bones")]
    public Transform bone1;
    public Transform bone2;
    public Transform bone3;
    public Transform bone4;

    [Header("Rig Mapping")]
    [Tooltip("Enable if bone1 represents wrist-to-knuckle")]
    public bool bone1IsMetacarpal = false;

    [Tooltip("Optional child marker at the fingertip")]
    public Transform tipMarker;


    [Header("Adaptive Smoothing")]
    public float jitterThreshold = 0.75f;
    public float slowSpeed = 8f;
    public float fastSpeed = 45f;


    private Transform[] bones;
    private Vector3 lastBoneLocalAxis;
    private bool initialized;

    public void Initialize()
    {
        bones = new Transform[]
        {
            bone1, bone2, bone3, bone4
        };

        initialized = true;

        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null)
            {
                Debug.LogWarning(
                    "[FingerRig] Missing bone " + (i + 1)
                );
                initialized = false;
            }
        }

        if (!initialized)
            return;

        // Find the direction of the last bone.
        // A fingertip marker is more accurate.
        Vector3 lastDirection = tipMarker != null
            ? tipMarker.position - bone4.position
            : bone4.position - bone3.position;

        if (lastDirection.sqrMagnitude < 0.00000001f)
        {
            initialized = false;
            Debug.LogWarning(
                "[FingerRig] Cannot determine last bone axis."
            );
            return;
        }

        lastBoneLocalAxis =
            bone4.InverseTransformDirection(
                lastDirection.normalized
            );

        Debug.Log("[FingerRig] Initialized");
    }

    public void Apply(
        IList<Landmark> points,
        int start,
        float smoothing,
        Transform cameraTransform,
        bool mirrorX,
        bool mirrorY)
    {
        if (!initialized ||
            points == null ||
            points.Count < 21 ||
            cameraTransform == null)
            return;

        for (int i = 0; i < 3; i++)
        {
            Transform bone = bones[i];

            // Determine the MediaPipe segment.
            int from;
            int to;

            if (bone1IsMetacarpal)
            {
                // Four different segments:
                // wrist -> MCP -> PIP -> DIP -> TIP
                from = i == 0 ? 0 : start + i - 1;
                to = start + i;
            }
            else
            {
                // Bone1 begins at the knuckle.
                // The final joint uses the distal
                // segment direction.
                int segment = Mathf.Min(i, 2);
                from = start + segment;
                to = from + 1;
            }

            // Tracked 3D direction
            Vector3 trackedDirection =
                ToUnity(points[to], cameraTransform,
                    mirrorX, mirrorY)
                -
                ToUnity(points[from], cameraTransform,
                    mirrorX, mirrorY);

            if (trackedDirection.sqrMagnitude < 0.00000001f)
                continue;

            trackedDirection.Normalize();

            // Current direction of the model's bone
            Vector3 currentDirection;

            if (i < 3)
            {
                currentDirection =
                    bones[i + 1].position - bone.position;
            }
            else if (tipMarker != null)
            {
                currentDirection =
                    tipMarker.position - bone.position;
            }
            else
            {
                currentDirection =
                    bone.TransformDirection(lastBoneLocalAxis);
            }

            if (currentDirection.sqrMagnitude < 0.00000001f)
                continue;

            currentDirection.Normalize();

            // Rotate the actual model segment toward
            // its MediaPipe segment.
            Quaternion correction =
                Quaternion.FromToRotation(
                    currentDirection,
                    trackedDirection
                );

            Quaternion targetRotation =
    correction * bone.rotation;

            // Difference between current and desired rotation
            float angleDifference = Quaternion.Angle(
                bone.rotation,
                targetRotation
            );

            // Ignore extremely small movements
            if (angleDifference < jitterThreshold)
                continue;

            // Small movements: smoother
            // Large movements: faster
            float movementAmount = Mathf.InverseLerp(
                1f, 25f, angleDifference
            );

            float speed = Mathf.Lerp(
                slowSpeed,
                fastSpeed,
                movementAmount
            );

            float t = 1f - Mathf.Exp(
                -speed * Time.deltaTime
            );

            bone.rotation = Quaternion.Slerp(
                bone.rotation,
                targetRotation,
                t
            );
        }
    }

    private Vector3 ToUnity(
        Landmark point,
        Transform cameraTransform,
        bool mirrorX,
        bool mirrorY)
    {
        Vector3 local = new Vector3(
            mirrorX ? -point.x : point.x,
            mirrorY ? -point.y : point.y,
            point.z
        );

        return cameraTransform.TransformPoint(local);
    }
}
