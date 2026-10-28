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

    [Header("Adaptive Smoothing")]
    [Min(0.1f)] public float slowSpeed = 8f;
    [Min(0.1f)] public float fastSpeed = 45f;

    private Transform[] bones;
    private readonly Vector3[] filteredDirections = new Vector3[3];
    private readonly bool[] directionReady = new bool[3];
    private bool initialized;

    private Quaternion[] restRotations = new Quaternion[4];
    public void Initialize()
    {
        bones = new[] { bone1, bone2, bone3, bone4 };
        initialized = true;
        ResetTracking();

        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null)
                continue;

            Debug.LogWarning($"[FingerRig] Bone {i + 1} is not assigned.");
            initialized = false;
        }
        for (int i = 0; i < 4; i++)
        {
            if (bones[i] != null)
                restRotations[i] = bones[i].localRotation;
        }
    }

    public void ResetTracking()
    {
        for (int i = 0; i < directionReady.Length; i++)
            directionReady[i] = false;
    }

    public void Apply(
        IList<Landmark> points,
        int start,
        float smoothing,
        Transform cameraTransform,
        bool mirrorX,
        bool mirrorY)
    {
        if (!initialized || points == null || points.Count < 21 ||
            cameraTransform == null || start < 1 || start + 3 >= points.Count)
            return;

        for (int i = 0; i < 3; i++)
            bones[i].localRotation = restRotations[i];

        for (int i = 0; i < 3; i++)
        {
            Transform bone = bones[i];

            Vector3 trackedDirection = ToWorld(points[start + i + 1], cameraTransform, mirrorX, mirrorY)
                                     - ToWorld(points[start + i], cameraTransform, mirrorX, mirrorY);

            if (trackedDirection.sqrMagnitude < 1e-10f)
                continue;

            trackedDirection.Normalize();

            if (!directionReady[i])
            {
                filteredDirections[i] = trackedDirection;
                directionReady[i] = true;
            }
            else
            {
                float angle = Vector3.Angle(filteredDirections[i], trackedDirection);
                float amount = Mathf.InverseLerp(2f, 15f, angle);
                float response = Mathf.Lerp(slowSpeed, fastSpeed, amount);
                float alpha = 1f - Mathf.Exp(-response * Time.deltaTime);

                filteredDirections[i] = Vector3.Slerp(
                    filteredDirections[i], trackedDirection, alpha
                ).normalized;
            }

            Vector3 currentDirection = bones[i + 1].position - bone.position;
            if (currentDirection.sqrMagnitude < 1e-10f)
                continue;

            Quaternion correction = Quaternion.FromToRotation(
                currentDirection.normalized,
                filteredDirections[i]
            );

            bone.rotation = correction * bone.rotation;
        }

    }


    private static Vector3 ToWorld(
        Landmark point,
        Transform cameraTransform,
        bool mirrorX,
        bool mirrorY)
    {
        Vector3 cameraSpace = new Vector3(
            mirrorX ? -point.x : point.x,
            mirrorY ? -point.y : point.y,
            point.z
        );

        return cameraTransform.TransformPoint(cameraSpace);
    }

    public void ResetPose()
    {
        if (!initialized)
            return;

        for (int i = 0; i < 4; i++)
        {
            if (bones[i] != null)
                bones[i].localRotation = restRotations[i];
        }

        ResetTracking();
    }

}
