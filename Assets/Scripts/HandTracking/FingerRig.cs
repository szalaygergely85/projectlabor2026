using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Components.Containers;

[System.Serializable]
public class FingerRig
{
    private const float SlowSpeed = 8f;
    private const float FastSpeed = 45f;

    public Transform bone1;
    public Transform bone2;
    public Transform bone3;
    public Transform bone4;

    private Transform[] bones;
    private readonly Vector3[] filteredDirections = new Vector3[3];
    private readonly bool[] directionReady = new bool[3];
    private readonly Quaternion[] restRotations = new Quaternion[4];
    private bool initialized;

    public void Initialize()
    {
        bones = new[] { bone1, bone2, bone3, bone4 };
        initialized = true;
        ResetTracking();

        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null)
            {
                restRotations[i] = bones[i].localRotation;
                continue;
            }

            Debug.LogWarning($"[FingerRig] Bone {i + 1} is not assigned.");
            initialized = false;
        }
    }

    public void ResetTracking()
    {
        for (int i = 0; i < directionReady.Length; i++)
            directionReady[i] = false;
    }

    public void Apply(IList<Landmark> points, int start, Transform cameraTransform)
    {
        if (!initialized || points == null || points.Count < 21 ||
            cameraTransform == null || start < 1 || start + 3 >= points.Count)
            return;

        for (int i = 0; i < 3; i++)
            bones[i].localRotation = restRotations[i];

        for (int i = 0; i < 3; i++)
        {
            Transform bone = bones[i];

            Vector3 trackedDirection = ToWorld(points[start + i + 1], cameraTransform)
                                     - ToWorld(points[start + i], cameraTransform);

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
                float response = Mathf.Lerp(SlowSpeed, FastSpeed, amount);
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

    private static Vector3 ToWorld(Landmark point, Transform cameraTransform)
    {
        return cameraTransform.TransformPoint(
            new Vector3(point.x, -point.y, -point.z)
        );
    }
}
