
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Components.Containers;

[System.Serializable]
public class FingerRig
{
    [Header("Bones")]
    public Transform bone1;
    public Transform bone2;
    public Transform bone3;
    public Transform bone4;

    [Header("Bending")]
    public Vector3 bendAxis = Vector3.right;
    public float bendDirection = 1f;
    public float maxBend = 90f;

    [Header("Finger Spreading")]
    public bool enableSpread = true;
    public Vector3 spreadAxis = Vector3.up;
    public float spreadDirection = 1f;
    public float spreadMultiplier = 1f;
    public float maxSpread = 25f;

    private Quaternion rest1;
    private Quaternion rest2;
    private Quaternion rest3;
    private Quaternion rest4;

    private bool spreadCalibrated;
    private float neutralSpread;

    public void Initialize()
    {
        if (bone1 != null)
            rest1 = bone1.localRotation;

        if (bone2 != null)
            rest2 = bone2.localRotation;

        if (bone3 != null)
            rest3 = bone3.localRotation;

        if (bone4 != null)
            rest4 = bone4.localRotation;

        spreadCalibrated = false;
    }

    public void Apply(
        IList<Landmark> points,
        int start,
        float smoothing)
    {
        if (points == null || points.Count < 21)
            return;

        // 1. Side-to-side movement
        if (enableSpread)
            ApplySpread(points, start, smoothing);

        // 2. Three bending joints
        Bend(
           bone1, rest1,
           points[0],
           points[start],
           points[start + 1],
           smoothing
       );


        Bend(
            bone2, rest2,
            points[0],
            points[start],
            points[start + 1],
            smoothing
        );

        Bend(
            bone3, rest3,
            points[start],
            points[start + 1],
            points[start + 2],
            smoothing
        );

        Bend(
            bone4, rest4,
            points[start + 1],
            points[start + 2],
            points[start + 3],
            smoothing
        );
    }

    private void ApplySpread(
        IList<Landmark> points,
        int start,
        float t)
    {
        if (bone1 == null ||
            spreadAxis.sqrMagnitude < 0.0001f)
            return;

        Vector3 wrist = ToVector(points[0]);
        Vector3 indexBase = ToVector(points[5]);
        Vector3 middleBase = ToVector(points[9]);
        Vector3 pinkyBase = ToVector(points[17]);

        // Palm coordinate system
        Vector3 palmForward = middleBase - wrist;
        Vector3 palmAcross = indexBase - pinkyBase;

        Vector3 palmNormal = Vector3.Cross(
            palmAcross, palmForward
        );

        if (palmNormal.sqrMagnitude < 0.000001f)
            return;

        palmNormal.Normalize();

        // Project finger direction onto palm plane
        Vector3 fingerDirection =
            ToVector(points[start + 1]) -
            ToVector(points[start]);

        Vector3 projectedFinger = Vector3.ProjectOnPlane(
            fingerDirection, palmNormal
        );

        if (projectedFinger.sqrMagnitude < 0.000001f)
            return;

        // Angle relative to the palm, not the camera
        float spreadAngle = Vector3.SignedAngle(
            palmForward,
            projectedFinger,
            palmNormal
        );

        // First open-hand pose is neutral
        if (!spreadCalibrated)
        {
            neutralSpread = spreadAngle;
            spreadCalibrated = true;
        }

        float relativeAngle = Mathf.DeltaAngle(
            neutralSpread,
            spreadAngle
        );

        float spread = Mathf.Clamp(
            relativeAngle * spreadMultiplier * spreadDirection,
            -maxSpread,
            maxSpread
        );

        Quaternion target = rest1 *
            Quaternion.AngleAxis(
                spread,
                spreadAxis.normalized
            );

        bone1.localRotation = Quaternion.Slerp(
            bone1.localRotation,
            target,
            t
        );
    }

    private void Bend(
        Transform bone,
        Quaternion rest,
        Landmark a,
        Landmark b,
        Landmark c,
        float t)
    {
        if (bone == null ||
            bendAxis.sqrMagnitude < 0.0001f)
            return;

        Vector3 first = ToVector(a) - ToVector(b);
        Vector3 second = ToVector(c) - ToVector(b);

        float angle = Vector3.Angle(first, second);

        float bend = Mathf.Clamp(
            180f - angle,
            0f,
            maxBend
        );

        Quaternion target = rest *
            Quaternion.AngleAxis(
                bend * bendDirection,
                bendAxis.normalized
            );

        bone.localRotation = Quaternion.Slerp(
            bone.localRotation,
            target,
            t
        );
    }

    private Vector3 ToVector(Landmark p)
    {
        return new Vector3(p.x, p.y, p.z);
    }
}
