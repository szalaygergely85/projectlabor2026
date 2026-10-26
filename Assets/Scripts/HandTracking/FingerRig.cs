
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

    [Header("Calibration")]
    public Vector3 bendAxis = Vector3.right;
    public float bendDirection = 1f;
    public float maxBend = 90f;

    private Quaternion rest1;
    private Quaternion rest2;
    private Quaternion rest3;
    private Quaternion rest4;

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
    }

    public void Apply(
        IList<Landmark> points,
        int start,
        float smoothing)
    {
        Bend(
            bone1, rest1,
            points[0],
            points[start],
            points[start + 1],
            smoothing
        );
        // MCP joint
        Bend(
            bone2, rest2,
            points[0],
            points[start],
            points[start + 1],
            smoothing
        );

        // PIP joint
        Bend(
            bone3, rest3,
            points[start],
            points[start + 1],
            points[start + 2],
            smoothing
        );

        // DIP joint
        Bend(
            bone4, rest4,
            points[start + 1],
            points[start + 2],
            points[start + 3],
            smoothing
        );
    }

    private void Bend(
        Transform bone,
        Quaternion rest,
        Landmark a,
        Landmark b,
        Landmark c,
        float smoothing)
    {
        if (bone == null || bendAxis.sqrMagnitude < 0.0001f)
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
            smoothing
        );
    }

    private Vector3 ToVector(Landmark p)
    {
        return new Vector3(p.x, p.y, p.z);
    }
}
