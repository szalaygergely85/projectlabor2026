using UnityEngine;

namespace CookingRehab
{
    public static class CookingEffects
    {
        private static Transform _root;

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    GameObject root = new GameObject("Cooking Effects");
                    _root = root.transform;
                }
                return _root;
            }
        }

        public static void Burst(Vector3 position, Color color, int count = 14, float power = 1f)
        {
            GameObject go = new GameObject("Burst");
            go.transform.SetParent(Root, false);
            go.transform.position = position;
            BurstEffect effect = go.AddComponent<BurstEffect>();
            effect.Initialize(color, count, power);
        }
    }

    public class BurstEffect : MonoBehaviour
    {
        private struct Piece
        {
            public Transform transform;
            public Vector3 velocity;
            public float life;
            public float age;
            public float startScale;
        }

        private Piece[] _pieces;
        private bool _initialized;

        public void Initialize(Color color, int count, float power)
        {
            Material material = CookingMaterialFactory.Emissive(color, 2.8f, 0.25f);
            _pieces = new Piece[Mathf.Max(4, count)];
            for (int i = 0; i < _pieces.Length; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                piece.name = "Spark";
                piece.transform.SetParent(transform, false);
                Collider collider = piece.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);
                Renderer renderer = piece.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = material;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(0.02f, 0.08f);
                float scale = Random.Range(0.018f, 0.04f);
                piece.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius, 0.02f, Mathf.Sin(angle) * radius);
                piece.transform.localScale = Vector3.one * scale;

                _pieces[i] = new Piece
                {
                    transform = piece.transform,
                    velocity = new Vector3(
                        Mathf.Cos(angle) * Random.Range(0.2f, 0.7f) * power,
                        Random.Range(0.8f, 1.7f) * power,
                        Mathf.Sin(angle) * Random.Range(0.2f, 0.7f) * power),
                    life = Random.Range(0.45f, 0.85f),
                    age = 0f,
                    startScale = scale
                };
            }
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
                return;

            float dt = Time.deltaTime;
            bool alive = false;

            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece piece = _pieces[i];
                if (piece.transform == null || piece.age >= piece.life)
                    continue;

                alive = true;
                piece.age += dt;
                piece.velocity += Vector3.down * (2.6f * dt);
                piece.transform.localPosition += piece.velocity * dt;
                piece.transform.Rotate(0f, 180f * dt, 0f, Space.Self);

                float remaining = Mathf.Clamp01(1f - piece.age / piece.life);
                piece.transform.localScale = Vector3.one *
                    (piece.startScale * Mathf.Max(0.05f, remaining));
                _pieces[i] = piece;
            }

            if (!alive)
                Destroy(gameObject);
        }
    }

    public class SteamEmitter : MonoBehaviour
    {
        public bool emitting = true;
        public float interval = 0.32f;
        public float radius = 0.16f;
        public Color color = new Color(0.92f, 0.94f, 0.95f, 1f);

        private float _timer;
        private int _alive;
        private Material _material;

        private void Awake()
        {
            _material = CookingMaterialFactory.Emissive(color, 0.35f, 0.1f);
        }

        private void Update()
        {
            if (!emitting)
                return;

            _timer += Time.deltaTime;
            if (_timer < interval || _alive > 14)
                return;

            _timer = 0f;
            _alive++;

            GameObject wisp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wisp.name = "Steam";
            wisp.transform.SetParent(transform, false);
            Collider collider = wisp.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
            Renderer renderer = wisp.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = _material;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float offset = Random.Range(0f, radius);
            wisp.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * offset, 0.03f, Mathf.Sin(angle) * offset);
            float scale = Random.Range(0.035f, 0.075f);
            wisp.transform.localScale = Vector3.one * scale;

            SteamWisp steamWisp = wisp.AddComponent<SteamWisp>();
            steamWisp.Initialize(
                new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(0.18f, 0.32f),
                    Random.Range(-0.05f, 0.05f)),
                Random.Range(1.1f, 1.8f), scale, this);
        }

        public void NotifyWispGone()
        {
            _alive = Mathf.Max(0, _alive - 1);
        }
    }

    public class SteamWisp : MonoBehaviour
    {
        private Vector3 _velocity;
        private float _life;
        private float _age;
        private float _startScale;
        private float _phase;
        private SteamEmitter _owner;

        public void Initialize(Vector3 velocity, float life, float startScale, SteamEmitter owner)
        {
            _velocity = velocity;
            _life = life;
            _startScale = startScale;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            _owner = owner;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= _life)
            {
                if (_owner != null)
                    _owner.NotifyWispGone();
                Destroy(gameObject);
                return;
            }

            transform.localPosition += _velocity * dt;
            _velocity.x += Mathf.Sin(Time.time * 2.2f + _phase) * 0.04f * dt;
            float t = _age / _life;
            float scale = _startScale * (1f + t * 1.8f) * Mathf.Clamp01(1f - t * t);
            transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);
            transform.Rotate(0f, 22f * dt, 0f, Space.Self);
        }
    }

    public class LiquidStream : MonoBehaviour
    {
        private GameObject _stream;
        private GameObject _splash;
        private Material _material;

        public void Initialize(Color color)
        {
            _material = CookingMaterialFactory.Emissive(color, 1.1f, 0.15f);

            _stream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _stream.name = "Liquid";
            _stream.transform.SetParent(transform, false);
            Collider streamCollider = _stream.GetComponent<Collider>();
            if (streamCollider != null)
                Object.Destroy(streamCollider);
            Renderer streamRenderer = _stream.GetComponent<Renderer>();
            if (streamRenderer != null)
                streamRenderer.sharedMaterial = _material;

            _splash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _splash.name = "Splash";
            _splash.transform.SetParent(transform, false);
            Collider splashCollider = _splash.GetComponent<Collider>();
            if (splashCollider != null)
                Object.Destroy(splashCollider);
            Renderer splashRenderer = _splash.GetComponent<Renderer>();
            if (splashRenderer != null)
                splashRenderer.sharedMaterial = _material;

            gameObject.SetActive(false);
        }

        public void Show(Vector3 from, Vector3 to)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.03f)
            {
                gameObject.SetActive(false);
                return;
            }

            transform.position = (from + to) * 0.5f;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);

            _stream.transform.localPosition = Vector3.zero;
            _stream.transform.localRotation = Quaternion.identity;
            _stream.transform.localScale = new Vector3(0.012f, length * 0.5f, 0.012f);

            _splash.transform.position = to;
            _splash.transform.localScale = Vector3.one *
                (0.045f + Mathf.PingPong(Time.time * 0.7f, 0.025f));
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }

    public class CookingAudio : MonoBehaviour
    {
        private AudioSource _source;
        private AudioClip _pick;
        private AudioClip _drop;
        private AudioClip _stir;
        private AudioClip _success;
        private AudioClip _complete;
        private AudioClip _error;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.75f;

            _pick = Tone("Pick", 0.07f, 760f, 1120f, 0.3f);
            _drop = Tone("Drop", 0.12f, 320f, 150f, 0.35f);
            _stir = Tone("Stir", 0.06f, 210f, 260f, 0.18f);
            _success = Arpeggio("Success", new[] { 659.25f, 783.99f, 1046.5f }, 0.1f, 0.42f);
            _complete = Arpeggio("Complete", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.16f, 0.45f);
            _error = Tone("Error", 0.16f, 180f, 138f, 0.28f, true);
        }

        public void PlayPick() { Play(_pick, 1f); }
        public void PlayDrop() { Play(_drop, 1f); }
        public void PlayStir() { Play(_stir, 0.8f); }
        public void PlaySuccess() { Play(_success, 1f); }
        public void PlayComplete() { Play(_complete, 1f); }
        public void PlayError() { Play(_error, 1f); }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null && _source != null)
                _source.PlayOneShot(clip, volume);
        }

        private static AudioClip Tone(string name, float duration, float startFrequency,
            float endFrequency, float volume, bool square = false)
        {
            int sampleRate = 44100;
            int samples = Mathf.Max(16, Mathf.CeilToInt(duration * sampleRate));
            float[] data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float progress = t / duration;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
                phase += Mathf.PI * 2f * frequency / sampleRate;

                float wave = Mathf.Sin(phase);
                if (square)
                    wave = Mathf.Sign(wave) * 0.55f;

                float attack = Mathf.Clamp01(t / 0.008f);
                float release = Mathf.Clamp01((duration - t) / 0.035f);
                data[i] = wave * attack * release * volume;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Arpeggio(string name, float[] frequencies,
            float noteDuration, float volume)
        {
            int sampleRate = 44100;
            float totalDuration = frequencies.Length * noteDuration + 0.3f;
            int samples = Mathf.CeilToInt(totalDuration * sampleRate);
            float[] data = new float[samples];

            for (int n = 0; n < frequencies.Length; n++)
            {
                int start = Mathf.FloorToInt(n * noteDuration * sampleRate);
                int length = Mathf.CeilToInt((noteDuration + 0.25f) * sampleRate);

                for (int i = 0; i < length && start + i < samples; i++)
                {
                    float t = i / (float)sampleRate;
                    float envelope = Mathf.Clamp01(t / 0.012f) *
                                     Mathf.Exp(-t * 5.5f);
                    float harmonic = Mathf.Sin(Mathf.PI * 2f * frequencies[n] * t) * 0.72f +
                                     Mathf.Sin(Mathf.PI * 4f * frequencies[n] * t) * 0.18f;
                    data[start + i] += harmonic * envelope * volume;
                }
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
