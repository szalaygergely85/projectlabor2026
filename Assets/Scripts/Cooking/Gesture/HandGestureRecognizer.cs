using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;

namespace CookingRehab
{
    public struct HandGestureState
    {
        public bool isTracked;
        public HandGesture gesture;
        public HandGesture rawGesture;
        public bool isPinching;
        public bool isPouring;
        public bool isStirring;
        public float confidence;
        public float pinchStrength;
        public float pinchDistanceRatio;
        public float pourTiltDegrees;
        public int stirRevolutions;
        public float stirProgress;
        public Vector2 viewportPosition;
        public Vector2 indexTipViewport;
        public Vector2 palmViewport;
        public Vector3 worldPosition;
        public Vector3 pinchWorldPosition;
        public Quaternion handRotation;
        public Vector3 palmNormal;
        public float thumbCurl;
        public float indexCurl;
        public float middleCurl;
        public float ringCurl;
        public float pinkyCurl;
        public bool thumbOut;
        public float lastSeenTime;
    }

    /// <summary>
    /// Turns MediaPipe hand landmarks into stable gameplay gestures:
    /// pinch (with hysteresis), open palm, point, fist, wrist tilt (pour)
    /// and circular stirring motion. It is fed by MediaPipeVRHand.Apply.
    /// </summary>
    [RequireComponent(typeof(MediaPipeVRHand))]
    public class HandGestureRecognizer : MonoBehaviour
    {
        public const float PinchEnterRatio = 0.38f;
        public const float PinchExitRatio = 0.58f;
        // A gentle wrist tilt is enough to pour: the gesture is a rehab
        // movement, not an endurance test, and a large threshold made the
        // bottle steps feel broken.
        public const float PourStartAngle = 22f;
        public const float PourStopAngle = 12f;

        private const float PinchStableTime = 0.05f;
        private const float PoseStableTime = 0.08f;
        private const float LostGrace = 0.32f;
        private const float ProcessTimeout = 0.35f;

        public event Action<HandSide> PinchStarted;
        public event Action<HandSide> PinchReleased;
        public event Action<HandSide, int> StirRevolutionCompleted;
        public event Action<HandSide> PourStarted;

        private Camera _sceneCamera;
        private MediaPipeVRHand _tracker;
        private bool _initialized;
        private float _lastProcessTime = -999f;

        private readonly HandRuntime _left = new HandRuntime(HandSide.Left);
        private readonly HandRuntime _right = new HandRuntime(HandSide.Right);
        private readonly Vector3[] _points = new Vector3[21];

        public HandGestureState LeftState
        {
            get { return _left.state; }
        }

        public HandGestureState RightState
        {
            get { return _right.state; }
        }

        public void Initialize(Camera sceneCamera, MediaPipeVRHand tracker)
        {
            _sceneCamera = sceneCamera;
            _tracker = tracker;
            _initialized = sceneCamera != null && tracker != null;
        }

        public HandGestureState GetState(HandSide side)
        {
            return side == HandSide.Left ? _left.state : _right.state;
        }

        /// <summary>
        /// Clears the circling history of both hands. Called when a new step
        /// starts so an arc drawn during the previous step cannot register as
        /// an instant stir or sprinkle.
        /// </summary>
        public void ResetCircles()
        {
            _left.stir.Reset();
            _right.stir.Reset();
            _left.state.stirProgress = 0f;
            _right.state.stirProgress = 0f;
            _left.state.isStirring = false;
            _right.state.isStirring = false;
        }

        public void Process(HandLandmarkerResult result)
        {
            if (!_initialized)
                return;

            float now = Time.unscaledTime;
            _lastProcessTime = now;

            bool seenLeft = false;
            bool seenRight = false;

            if (result.handLandmarks != null && result.handedness != null)
            {
                int handCount = Mathf.Min(result.handLandmarks.Count, result.handedness.Count);
                for (int i = 0; i < handCount; i++)
                {
                    List<Category> categories = result.handedness[i].categories;
                    if (categories == null || categories.Count == 0)
                        continue;

                    string category = categories[0].categoryName;
                    if (category != "Left" && category != "Right")
                        continue;

                    // The image is shown mirrored, so MediaPipe's label is swapped
                    // relative to the hand rigs the player sees on screen.
                    HandSide side = category == "Right" ? HandSide.Left : HandSide.Right;
                    List<NormalizedLandmark> points = result.handLandmarks[i].landmarks;
                    if (points == null || points.Count < 21)
                        continue;

                    ReadHand(side == HandSide.Left ? _left : _right, points, now);

                    if (side == HandSide.Left)
                        seenLeft = true;
                    else
                        seenRight = true;
                }
            }

            // Missing detections are tolerated for a short grace period so a
            // single dropped frame does not release a held ingredient.
            if (seenLeft)
                _left.state.lastSeenTime = now;
            if (seenRight)
                _right.state.lastSeenTime = now;
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            if (now - _lastProcessTime > ProcessTimeout)
            {
                MarkLost(_left);
                MarkLost(_right);
                return;
            }

            if (_left.state.isTracked && now - _left.state.lastSeenTime > LostGrace)
                MarkLost(_left);
            if (_right.state.isTracked && now - _right.state.lastSeenTime > LostGrace)
                MarkLost(_right);
        }

        private void ReadHand(HandRuntime hand, List<NormalizedLandmark> points, float now)
        {
            HandGestureState state = hand.state;
            state.isTracked = true;
            state.lastSeenTime = now;

            float aspect = _sceneCamera != null ? Mathf.Max(0.1f, _sceneCamera.aspect) : 1f;

            for (int i = 0; i < 21; i++)
            {
                NormalizedLandmark landmark = points[i];
                _points[i] = new Vector3(
                    landmark.x * aspect,
                    -landmark.y,
                    -landmark.z * aspect
                );
            }

            Vector2 wristViewport = new Vector2(points[0].x, 1f - points[0].y);
            Vector2 thumbTipViewport = new Vector2(points[4].x, 1f - points[4].y);
            Vector2 indexTipViewport = new Vector2(points[8].x, 1f - points[8].y);
            Vector2 palmViewport = new Vector2(
                (points[0].x + points[5].x + points[9].x + points[13].x + points[17].x) * 0.2f,
                1f - (points[0].y + points[5].y + points[9].y + points[13].y + points[17].y) * 0.2f
            );

            state.indexTipViewport = indexTipViewport;
            state.palmViewport = palmViewport;
            state.viewportPosition = (thumbTipViewport + indexTipViewport) * 0.5f;

            float palmWidth = Vector2.Distance(
                new Vector2(_points[5].x, _points[5].y),
                new Vector2(_points[17].x, _points[17].y)
            );
            if (palmWidth < 0.0001f)
                palmWidth = 0.15f;

            float pinchDistance = Vector2.Distance(
                new Vector2(_points[4].x, _points[4].y),
                new Vector2(_points[8].x, _points[8].y)
            );
            float pinchRatio = pinchDistance / palmWidth;

            bool wasPinching = state.isPinching;
            bool pinchNow = wasPinching
                ? pinchRatio < PinchExitRatio
                : pinchRatio < PinchEnterRatio;

            state.pinchDistanceRatio = pinchRatio;
            state.pinchStrength = 1f - Mathf.Clamp01((pinchRatio - 0.12f) / 0.45f);

            state.thumbCurl = FingerCurl(1, 2, 3, 4);
            state.indexCurl = FingerCurl(5, 6, 7, 8);
            state.middleCurl = FingerCurl(9, 10, 11, 12);
            state.ringCurl = FingerCurl(13, 14, 15, 16);
            state.pinkyCurl = FingerCurl(17, 18, 19, 20);

            Vector3 palmCenter = (_points[0] + _points[5] + _points[9] +
                                  _points[13] + _points[17]) * 0.2f;
            float thumbPalmDistance = Vector3.Distance(_points[4], palmCenter) / palmWidth;
            float thumbIndexDistance = Vector3.Distance(_points[4], _points[5]) / palmWidth;
            state.thumbOut = thumbPalmDistance > 0.78f && thumbIndexDistance > 0.42f;

            Vector3 palmNormal = PalmNormal();
            state.palmNormal = palmNormal;

            bool pourStarted = false;
            HandGesture rawGesture = HandGesture.None;
            if (pinchNow)
            {
                if (!hand.hasPinchBaseline)
                {
                    hand.pinchBaselineNormal = palmNormal;
                    hand.hasPinchBaseline = true;
                    hand.pourLatched = false;
                }

                float tilt = Vector3.Angle(palmNormal, hand.pinchBaselineNormal);
                state.pourTiltDegrees = tilt;

                if (!hand.pourLatched && tilt >= PourStartAngle)
                {
                    hand.pourLatched = true;
                    pourStarted = true;
                }
                else if (hand.pourLatched && tilt <= PourStopAngle)
                {
                    hand.pourLatched = false;
                }

                state.isPouring = hand.pourLatched;
                rawGesture = hand.pourLatched ? HandGesture.Pour : HandGesture.Pinch;
            }
            else
            {
                // Do not drop the pour baseline here: pinch detection can
                // flicker for a single frame, and re-capturing the baseline
                // mid-pour would zero the tilt and make pouring feel random.
                // It is cleared only after a real (stable) release below.
                state.isPouring = false;
                state.pourTiltDegrees = 0f;

                bool fingersOpen = state.indexCurl < 0.35f &&
                                   state.middleCurl < 0.35f &&
                                   state.ringCurl < 0.35f &&
                                   state.pinkyCurl < 0.35f;
                bool fingersCurled = state.indexCurl > 0.62f &&
                                     state.middleCurl > 0.62f &&
                                     state.ringCurl > 0.62f &&
                                     state.pinkyCurl > 0.62f;
                bool isPoint = state.indexCurl < 0.32f &&
                               state.middleCurl > 0.55f &&
                               state.ringCurl > 0.55f &&
                               state.pinkyCurl > 0.55f;

                if (fingersOpen && state.thumbOut)
                    rawGesture = HandGesture.OpenPalm;
                else if (isPoint)
                    rawGesture = HandGesture.Point;
                else if (fingersCurled)
                    rawGesture = HandGesture.Fist;
            }

            state.rawGesture = rawGesture;

            float stableTime = rawGesture == HandGesture.Pinch || rawGesture == HandGesture.Pour
                ? PinchStableTime
                : PoseStableTime;

            if (rawGesture != hand.candidate)
            {
                hand.candidate = rawGesture;
                hand.candidateSince = now;
            }

            if (now - hand.candidateSince >= stableTime)
                state.gesture = rawGesture;

            state.confidence = Mathf.Clamp01((now - hand.candidateSince) / stableTime);

            bool pinchStable = state.gesture == HandGesture.Pinch ||
                               state.gesture == HandGesture.Pour;
            bool pinchChanged = pinchStable != state.isPinching;
            state.isPinching = pinchStable;

            // A real release ends the pinch session, so the next pinch starts
            // a fresh pour baseline. A brief flicker (not stable enough to
            // release the ingredient) keeps the old baseline.
            if (pinchChanged && !pinchStable)
            {
                hand.hasPinchBaseline = false;
                hand.pourLatched = false;
            }

            // Keep feeding the circle detector even while the fingers are
            // pinched. Players naturally keep the hand half closed while
            // reaching for a seasoning shaker, and the old pinch reset threw
            // away every partial circle, which made sprinkling feel dead.
            // A full revolution is still required, so a pinch on its own
            // can never count as a stir or a sprinkle.
            bool stirred = hand.stir.Add(palmViewport, now);
            if (stirred)
                state.stirRevolutions++;

            state.stirProgress = hand.stir.Progress;
            state.isStirring = hand.stir.IsMoving;

            UpdateWorldReferences(hand, ref state);

            // Commit first, then publish. Listeners query GetState from their
            // event handlers and must see this frame's hand data.
            hand.state = state;

            if (pourStarted && PourStarted != null)
                PourStarted(hand.side);

            if (pinchChanged)
            {
                if (pinchStable)
                {
                    if (PinchStarted != null)
                        PinchStarted(hand.side);
                }
                else
                {
                    if (PinchReleased != null)
                        PinchReleased(hand.side);
                }
            }

            if (stirred && StirRevolutionCompleted != null)
                StirRevolutionCompleted(hand.side, state.stirRevolutions);
        }

        private void UpdateWorldReferences(HandRuntime hand, ref HandGestureState state)
        {
            MediaPipeVRHand.HandRig rig = hand.side == HandSide.Left
                ? _tracker.leftHand
                : _tracker.rightHand;

            if (rig == null)
                return;

            if (rig.handRoot != null)
            {
                state.worldPosition = rig.handRoot.position;
                state.handRotation = rig.handRoot.rotation;
            }

            if (rig.thumb.bone4 != null && rig.index.bone4 != null)
            {
                state.pinchWorldPosition =
                    (rig.thumb.bone4.position + rig.index.bone4.position) * 0.5f;
            }
            else if (rig.handRoot != null)
            {
                state.pinchWorldPosition = rig.handRoot.position;
            }
        }

        private Vector3 PalmNormal()
        {
            Vector3 up = _points[9] - _points[0];
            Vector3 across = _points[5] - _points[17];
            Vector3 normal = Vector3.Cross(across, up);
            if (normal.sqrMagnitude < 0.0000001f)
                return Vector3.forward;
            return normal.normalized;
        }

        private float FingerCurl(int mcp, int pip, int dip, int tip)
        {
            float a = Vector3.Angle(
                _points[mcp] - _points[pip],
                _points[dip] - _points[pip]
            );
            float b = Vector3.Angle(
                _points[pip] - _points[dip],
                _points[tip] - _points[dip]
            );
            float average = (a + b) * 0.5f;
            return 1f - Mathf.Clamp01((average - 90f) / 80f);
        }

        private void MarkLost(HandRuntime hand)
        {
            if (!hand.state.isTracked)
                return;

            bool wasPinching = hand.state.isPinching;

            hand.state.isTracked = false;
            hand.state.gesture = HandGesture.None;
            hand.state.rawGesture = HandGesture.None;
            hand.state.isPinching = false;
            hand.state.isPouring = false;
            hand.state.isStirring = false;
            hand.state.pinchStrength = 0f;
            hand.state.pinchDistanceRatio = 0f;
            hand.state.pourTiltDegrees = 0f;
            hand.state.stirProgress = 0f;
            hand.state.confidence = 0f;
            hand.hasPinchBaseline = false;
            hand.pourLatched = false;
            hand.candidate = HandGesture.None;
            hand.candidateSince = 0f;
            hand.stir.Reset();

            // Release after marking the hand untracked, so the controller can
            // tell a real drop apart from the hand leaving the camera.
            if (wasPinching && PinchReleased != null)
                PinchReleased(hand.side);
        }

        private class HandRuntime
        {
            public readonly HandSide side;
            public HandGestureState state;
            public HandGesture candidate;
            public float candidateSince;
            public Vector3 pinchBaselineNormal;
            public bool hasPinchBaseline;
            public bool pourLatched;
            public readonly CircularMotionDetector stir = new CircularMotionDetector();

            public HandRuntime(HandSide side)
            {
                this.side = side;
                state = new HandGestureState();
                state.gesture = HandGesture.None;
                state.rawGesture = HandGesture.None;
            }
        }

        /// <summary>
        /// Detects circular motion from a stream of 2D positions by measuring
        /// the signed sweep angle around the window centroid. A straight or
        /// back-and-forth motion does not reach a full turn, so it cannot be
        /// mistaken for stirring.
        /// </summary>
        private class CircularMotionDetector
        {
            // A slower, patient-friendly circle still fits in the window:
            // one deliberate turn takes about 6 s, and a turn only has to
            // sweep 250 degrees, so neither a slow arm nor a hole in the
            // tracking makes the player start over.
            private const float WindowSeconds = 6f;
            private const float MinRadius = 0.007f;
            private const float RevolutionSweep = 250f;

            private struct Sample
            {
                public float time;
                public Vector2 position;
            }

            private readonly List<Sample> _samples = new List<Sample>(160);

            public int TotalRevolutions { get; private set; }
            public float Progress { get; private set; }
            public bool IsMoving { get; private set; }

            public void Reset()
            {
                _samples.Clear();
                Progress = 0f;
                IsMoving = false;
            }

            public bool Add(Vector2 position, float time)
            {
                _samples.Add(new Sample { time = time, position = position });

                while (_samples.Count > 2 && time - _samples[0].time > WindowSeconds)
                    _samples.RemoveAt(0);

                if (_samples.Count < 8)
                {
                    Progress = 0f;
                    IsMoving = false;
                    return false;
                }

                Vector2 centroid = Vector2.zero;
                for (int i = 0; i < _samples.Count; i++)
                    centroid += _samples[i].position;
                centroid /= _samples.Count;

                float sweep = 0f;
                float absoluteSweep = 0f;
                float radiusSum = 0f;
                int validPoints = 0;
                bool hasPreviousAngle = false;
                float previousAngle = 0f;

                for (int i = 0; i < _samples.Count; i++)
                {
                    Vector2 delta = _samples[i].position - centroid;
                    float radius = delta.magnitude;
                    radiusSum += radius;

                    if (radius < 0.004f)
                    {
                        hasPreviousAngle = false;
                        continue;
                    }

                    float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                    if (hasPreviousAngle)
                    {
                        float step = Mathf.DeltaAngle(previousAngle, angle);
                        sweep += step;
                        absoluteSweep += Mathf.Abs(step);
                    }

                    previousAngle = angle;
                    hasPreviousAngle = true;
                    validPoints++;
                }

                float meanRadius = validPoints > 0 ? radiusSum / validPoints : 0f;
                float consistency = absoluteSweep > 1f
                    ? Mathf.Abs(sweep) / absoluteSweep
                    : 0f;

                Progress = Mathf.Clamp01(Mathf.Abs(sweep) / RevolutionSweep);
                IsMoving = validPoints >= 6 &&
                           meanRadius > MinRadius &&
                           consistency > 0.55f &&
                           Mathf.Abs(sweep) > 15f;

                if (validPoints >= 6 &&
                    meanRadius > MinRadius &&
                    consistency > 0.62f &&
                    Mathf.Abs(sweep) >= RevolutionSweep)
                {
                    TotalRevolutions++;
                    _samples.Clear();
                    Progress = 0f;
                    return true;
                }

                return false;
            }
        }
    }
}
