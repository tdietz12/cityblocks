using System.Collections.Generic;
using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Controls the fullscreen combo celebration takeover triggered when a player completes
    /// 3 or more merges in a row on a single tower placement.
    /// </summary>
    public class ComboCelebrationController : MonoBehaviour
    {
        private static readonly string[] Tier3Words = { "AWESOME!", "GREAT!", "SUPER!" };
        private static readonly string[] Tier4Words = { "AMAZING!", "SPECTACULAR!", "FANTASTIC!" };
        private static readonly string[] Tier5PlusWords = { "UNBELIEVABLE!", "LEGENDARY!", "INCREDIBLE!" };

        // Pure white square rings, matching the white level-up particles.
        private static readonly Color[] LevelUpRingPalette =
        {
            new Color(1f, 1f, 1f, 0.95f)
        };

        private static Sprite _cachedProceduralSquareRingSprite;
        private Sprite _runtimeRingSprite;
        private Texture2D _runtimeRingSpriteSource;

        [Header("UI References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _backdropImage;
        [SerializeField] private RectTransform _bannerRoot;
        [SerializeField] private TMP_Text _headlineLabel;
        [FormerlySerializedAs("_confettiContainer")]
        [SerializeField] private RectTransform _vfxContainer;
        [Tooltip("Square ring texture used by LevelUpEffect/Ring.mat (Assets/Prefabs/Cell/Square.png).")]
        [SerializeField] private Texture2D _levelUpRingTexture;
        [Tooltip("Optional 3D LevelUpEffect prefab to pulse on board towers when celebration erupts.")]
        [SerializeField] private GameObject _levelUpEffectPrefab;

        [Header("Timing & Motion")]
        [SerializeField, Min(0f)] private float _startDelay = 0.2f;
        [SerializeField, Min(0.1f)] private float _totalDuration = 2.25f;
        [SerializeField, Min(0.05f)] private float _fadeInDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float _popDuration = 0.28f;
        [SerializeField, Min(0.05f)] private float _fadeOutDuration = 0.30f;
        [Tooltip("Extra real-time seconds the headline remains after the particle timeline.")]
        [SerializeField, Min(0f)] private float _headlineHoldDuration = 0.35f;
        [SerializeField, Range(0f, 0.9f)] private float _backdropPeakAlpha = 0.58f;
        [SerializeField, Min(1f)] private float _headlineOvershootScale = 1.08f;
        [SerializeField, Min(4f)] private float _screenHorizontalPadding = 16f;
        [FormerlySerializedAs("_confettiBurstCount")]
        [FormerlySerializedAs("_rubbleBurstCount")]
        [SerializeField, Range(8, 64)] private int _ringBurstCount = 28;

        private struct ExpandingSquareRing
        {
            public RectTransform Rect;
            public Image Graphic;
            public Vector2 Origin;
            public float StartDiameter;
            public float EndDiameter;
            public float RiseDistance;
            public float StartRotation;
            public float AngularVelocity;
            public Color BaseColor;
            public float Delay;
            public float Duration;
        }

        private struct RisingSquareRing
        {
            public RectTransform Rect;
            public Image Graphic;
            public Vector2 StartPosition;
            public Vector2 Velocity;
            public float Rotation;
            public float AngularVelocity;
            public Vector2 BaseSize;
            public Color BaseColor;
            public float Delay;
            public float Lifetime;
        }

        private readonly List<ExpandingSquareRing> _shockwaves = new List<ExpandingSquareRing>();
        private readonly List<RisingSquareRing> _rubble = new List<RisingSquareRing>();
        private RectTransform _selfRect;
        private GameController _game;
        private BoardCameraFeedback _cameraFeedback;
        private Vector2 _bannerBaseAnchoredPos = new Vector2(0f, 20f);
        private bool _bannerPosCaptured;
        private float _age;
        private float _fitBaseScale = 1f;
        private bool _celebrating;
        private bool _burstTriggered;
        private int _variantCounter;

        public bool IsCelebrating => _celebrating;
        public Sprite SquareRingSprite => GetOrCreateSquareRingSprite();
        public bool IsInStartDelay => _celebrating && _age < StartDelay * 2f;
        public float StartDelay => _startDelay * 0.5f + 0.2f;
        private float HeadlineTimelineDuration => _totalDuration + _headlineHoldDuration * 2f;
        public float TotalDuration => HeadlineTimelineDuration * 0.5f;
        public string CurrentHeadline => _headlineLabel != null ? _headlineLabel.text : string.Empty;
        public int LastCelebratedMergeCount { get; private set; }

        public int ActiveShockwaveCount
        {
            get
            {
                if (!_celebrating || !_burstTriggered) return 0;
                int count = 0;
                for (int i = 0; i < _shockwaves.Count; i++)
                {
                    if (_shockwaves[i].Rect != null && _shockwaves[i].Rect.gameObject.activeSelf) count++;
                }
                return count;
            }
        }

        public int ActiveRubbleCount
        {
            get
            {
                if (!_celebrating || !_burstTriggered) return 0;
                int count = 0;
                for (int i = 0; i < _rubble.Count; i++)
                {
                    if (_rubble[i].Rect != null && _rubble[i].Rect.gameObject.activeSelf) count++;
                }
                return count;
            }
        }

        public int ActiveVfxCount => ActiveShockwaveCount + ActiveRubbleCount;

        private void Awake()
        {
            EnsureReferences();
            ClearImmediate();
        }

        public void Bind(GameController game)
        {
            EnsureReferences();
            if (_game == game) return;
            UnsubscribeGame();
            _game = game;
            if (_game != null)
            {
                _game.onPlacementComboCompleted += OnPlacementComboCompleted;
                if (_game.GameplayCamera != null)
                {
                    _cameraFeedback = _game.GameplayCamera.GetComponent<BoardCameraFeedback>();
                }
            }
            ClearImmediate();
        }

        private void UnsubscribeGame()
        {
            if (_game == null) return;
            _game.onPlacementComboCompleted -= OnPlacementComboCompleted;
        }

        private void OnPlacementComboCompleted(int mergeCount)
        {
            if (mergeCount >= 3)
            {
                TriggerCelebration(mergeCount);
            }
        }

        public static string SelectCongratulatoryWord(int mergeCount, int variantIndex = 0)
        {
            string[] pool = mergeCount <= 3 ? Tier3Words : (mergeCount == 4 ? Tier4Words : Tier5PlusWords);
            int idx = Mathf.Abs(variantIndex) % pool.Length;
            return pool[idx];
        }

        public void TriggerCelebration(int mergeCount)
        {
            if (mergeCount < 3) return;
            EnsureReferences();

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            LastCelebratedMergeCount = mergeCount;
            string headline = SelectCongratulatoryWord(mergeCount, _variantCounter++);

            if (_headlineLabel != null)
            {
                _headlineLabel.textWrappingMode = TextWrappingModes.NoWrap;
                _headlineLabel.overflowMode = TextOverflowModes.Overflow;
                _headlineLabel.text = headline;
                ApplyTierTint(_headlineLabel, mergeCount);
            }

            FitHeadlineToScreen();

            _age = 0f;
            _celebrating = true;
            _burstTriggered = false;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = false;
            }

            if (StartDelay <= 0f)
            {
                BeginVisualTakeover();
                EvaluateFrame(0f);
            }
            else
            {
                HideVisualElements();
            }
        }

        private void BeginVisualTakeover()
        {
            if (_burstTriggered) return;
            _burstTriggered = true;

            FitHeadlineToScreen();
            SpawnSeismicBurst(LastCelebratedMergeCount);

            if (_cameraFeedback == null && _game != null && _game.GameplayCamera != null)
            {
                _cameraFeedback = _game.GameplayCamera.GetComponent<BoardCameraFeedback>();
            }
            if (_cameraFeedback != null)
            {
                _cameraFeedback.PunchMerge();
            }
        }

        public void FitHeadlineToScreen()
        {
            EnsureReferences();
            if (_headlineLabel == null || _bannerRoot == null)
            {
                _fitBaseScale = 1f;
                return;
            }

            float overlayWidth = _selfRect != null && _selfRect.rect.width > 1f ? _selfRect.rect.width : 1080f;
            float maxAllowedWidth = Mathf.Max(200f, overlayWidth - _screenHorizontalPadding * 2f);
            float safeUnscaledWidth = maxAllowedWidth / Mathf.Max(1f, _headlineOvershootScale);

            _bannerRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, safeUnscaledWidth);
            _bannerRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 360f);
            RectTransform headlineRect = _headlineLabel.rectTransform;
            if (headlineRect != null)
            {
                headlineRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, safeUnscaledWidth);
                headlineRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 340f);
            }

            _headlineLabel.enableAutoSizing = false;
            _headlineLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _headlineLabel.overflowMode = TextOverflowModes.Overflow;
            _headlineLabel.fontSize = 100f;
            _headlineLabel.ForceMeshUpdate();

            float baseWidthAt100 = Mathf.Max(1f, _headlineLabel.renderedWidth > 0.01f ? _headlineLabel.renderedWidth : _headlineLabel.GetPreferredValues(_headlineLabel.text).x);
            float targetFontSize = Mathf.Clamp((safeUnscaledWidth * 0.96f / baseWidthAt100) * 100f, 36f, 320f);
            _headlineLabel.fontSize = targetFontSize;
            _headlineLabel.ForceMeshUpdate();

            float renderedWidth = _headlineLabel.renderedWidth;
            if (renderedWidth <= 0.01f)
            {
                renderedWidth = _headlineLabel.GetPreferredValues(_headlineLabel.text).x;
            }
            if (renderedWidth > safeUnscaledWidth * 0.98f && renderedWidth > 0.01f)
            {
                _headlineLabel.fontSize = Mathf.Max(28f, targetFontSize * (safeUnscaledWidth * 0.96f / renderedWidth));
                _headlineLabel.ForceMeshUpdate();
                renderedWidth = _headlineLabel.renderedWidth;
            }

            if (renderedWidth * _headlineOvershootScale > maxAllowedWidth && renderedWidth > 0.01f)
            {
                _fitBaseScale = maxAllowedWidth / (renderedWidth * _headlineOvershootScale);
            }
            else
            {
                _fitBaseScale = 1f;
            }
            _fitBaseScale = Mathf.Clamp(_fitBaseScale, 0.35f, 1f);
        }

        private void Update()
        {
            if (_celebrating)
            {
                Advance(Time.unscaledDeltaTime);
            }
        }

        public void Advance(float delta)
        {
            if (!_celebrating) return;
            delta = Mathf.Max(0f, delta) * 2f;
            _age += delta;

            if (_age < StartDelay * 2f)
            {
                HideVisualElements();
                return;
            }

            float activeTime = _age - StartDelay * 2f;
            if (activeTime >= HeadlineTimelineDuration)
            {
                ClearImmediate();
                return;
            }

            if (!_burstTriggered)
            {
                BeginVisualTakeover();
            }

            EvaluateFrame(activeTime);
        }

        private void EvaluateFrame(float time)
        {
            float fadeOutStart = Mathf.Max(_fadeInDuration, HeadlineTimelineDuration - _fadeOutDuration);
            float envelope;
            if (time < _fadeInDuration)
            {
                envelope = Smooth(time / _fadeInDuration);
            }
            else if (time > fadeOutStart)
            {
                float outT = Mathf.Clamp01((time - fadeOutStart) / Mathf.Max(0.001f, _fadeOutDuration));
                envelope = 1f - Smooth(outT);
            }
            else
            {
                envelope = 1f;
            }

            if (_backdropImage != null)
            {
                Color c = _backdropImage.color;
                c.a = _backdropPeakAlpha * envelope;
                _backdropImage.color = c;
            }

            if (_bannerRoot != null)
            {
                float popT = Mathf.Clamp01(time / Mathf.Max(0.001f, _popDuration));
                float scale = EvaluateElasticScale(popT, _headlineOvershootScale);
                if (time > fadeOutStart)
                {
                    scale *= Mathf.Lerp(0.75f, 1f, envelope);
                }

                // Guarantee that even with elastic overshoot, the text never exceeds screen width
                scale = Mathf.Min(scale, _headlineOvershootScale) * _fitBaseScale;

                float wobble = Mathf.Sin(time * 20f) * Mathf.Exp(-time * 4.5f) * 3.5f;
                _bannerRoot.localScale = Vector3.one * scale;
                _bannerRoot.localRotation = Quaternion.Euler(0f, 0f, wobble);

                // Jackhammer seismic impact tremor during the initial 0.22s
                if (time < 0.22f)
                {
                    float tremorStrength = (1f - time / 0.22f) * 14f;
                    float tremorX = Mathf.Sin(time * 135f) * tremorStrength * 0.45f;
                    float tremorY = Mathf.Cos(time * 170f) * tremorStrength;
                    _bannerRoot.anchoredPosition = _bannerBaseAnchoredPos + new Vector2(tremorX, tremorY);
                }
                else
                {
                    _bannerRoot.anchoredPosition = _bannerBaseAnchoredPos;
                }
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = time > fadeOutStart ? envelope : 1f;
            }

            UpdateSeismicBurst(time);
        }

        private static float EvaluateElasticScale(float t, float overshoot)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f) return 0f;
            if (t < 0.55f)
            {
                return Mathf.Lerp(0f, overshoot, Smooth(t / 0.55f));
            }
            if (t < 0.82f)
            {
                return Mathf.Lerp(overshoot, 0.94f, Smooth((t - 0.55f) / 0.27f));
            }
            return Mathf.Lerp(0.94f, 1f, Smooth((t - 0.82f) / 0.18f));
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static void ApplyTierTint(TMP_Text label, int mergeCount)
        {
            if (label == null) return;
            label.enableVertexGradient = true;
            if (mergeCount <= 3)
            {
                label.colorGradient = new VertexGradient(
                    new Color(1f, 0.96f, 0.55f, 1f),
                    new Color(1f, 0.96f, 0.55f, 1f),
                    new Color(1f, 0.68f, 0.12f, 1f),
                    new Color(1f, 0.68f, 0.12f, 1f));
            }
            else if (mergeCount == 4)
            {
                label.colorGradient = new VertexGradient(
                    new Color(1f, 0.98f, 0.72f, 1f),
                    new Color(1f, 0.98f, 0.72f, 1f),
                    new Color(1f, 0.58f, 0.10f, 1f),
                    new Color(1f, 0.58f, 0.10f, 1f));
            }
            else
            {
                label.colorGradient = new VertexGradient(
                    new Color(1f, 0.98f, 0.82f, 1f),
                    new Color(1f, 0.95f, 0.45f, 1f),
                    new Color(1f, 0.45f, 0.12f, 1f),
                    new Color(1f, 0.75f, 0.10f, 1f));
            }
        }

        private Vector2 HeadlineBurstPosition(int index, int count)
        {
            if (_headlineLabel == null || _vfxContainer == null) return Vector2.zero;
            var info = _headlineLabel.textInfo;
            int visibleCount = 0;
            for (int i = 0; i < info.characterCount; i++)
                if (info.characterInfo[i].isVisible) visibleCount++;
            if (visibleCount == 0)
                return _vfxContainer.InverseTransformPoint(_headlineLabel.transform.position);
            int target = index * visibleCount / Mathf.Max(1, count);
            for (int i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
                if (!character.isVisible) continue;
                if (target-- != 0) continue;
                Vector3 point = Vector3.Lerp(character.bottomLeft, character.topRight, 0.25f + (index % 5) * 0.125f);
                return _vfxContainer.InverseTransformPoint(_headlineLabel.transform.TransformPoint(point));
            }
            return Vector2.zero;
        }

        private void SpawnSeismicBurst(int mergeCount)
        {
            if (_vfxContainer == null) return;
            if (_bannerRoot != null) _bannerRoot.localScale = Vector3.one * _fitBaseScale;
            if (_headlineLabel != null) _headlineLabel.ForceMeshUpdate();
            Vector2 textCenter = _headlineLabel != null
                ? (Vector2)_vfxContainer.InverseTransformPoint(_headlineLabel.transform.TransformPoint(_headlineLabel.textBounds.center))
                : Vector2.zero;

            int targetWaveCount= mergeCount >= 5 ? 5 : (mergeCount == 4 ? 4 : 3);
            int targetRisingRingCount = Mathf.Clamp(_ringBurstCount + (mergeCount - 3) * 6, 16, 40);

            EnsureLevelUpRingPool(targetWaveCount, targetRisingRingCount);

            // 1. Large concentric expanding & rising Square Rings (mirrors LevelUpEffect's expanding ring stack)
            for (int i = 0; i < _shockwaves.Count; i++)
            {
                ExpandingSquareRing ring = _shockwaves[i];
                if (i >= targetWaveCount)
                {
                    if (ring.Rect != null) ring.Rect.gameObject.SetActive(false);
                    continue;
                }

                ring.Origin = textCenter;
                ring.StartDiameter = 180f + i * 34f;
                ring.EndDiameter = 920f + i * 140f + (mergeCount >= 5 ? 110f : 0f);
                ring.RiseDistance = 0f;
                ring.StartRotation = (i % 2 == 0 ? 1f : -1f) * (i * 2f);
                ring.AngularVelocity = (i % 2 == 0 ? 1f : -1f) * (6f + i * 2f);
                ring.BaseColor = LevelUpRingPalette[i % LevelUpRingPalette.Length];
                ring.Delay = i * 0.09f;
                ring.Duration = 0.78f + i * 0.08f;

                if (ring.Rect != null)
                {
                    ring.Rect.gameObject.SetActive(true);
                    ring.Rect.anchoredPosition = ring.Origin;
                    ring.Rect.sizeDelta = new Vector2(ring.StartDiameter, ring.StartDiameter);
                    ring.Rect.localRotation = Quaternion.Euler(0f, 0f, ring.StartRotation);
                    ring.Rect.localScale = Vector3.one;
                }
                if (ring.Graphic != null)
                {
                    ring.Graphic.color = ring.BaseColor;
                }
                _shockwaves[i] = ring;
            }

            // 2. Rising Donut-Ring Stream of Square Rings (mirrors LevelUpEffect's Donut ShapeModule + upward Z velocity + dampen 0.125 + ±120 deg/s spin)
            for (int i = 0; i < _rubble.Count; i++)
            {
                RisingSquareRing item = _rubble[i];
                if (i >= targetRisingRingCount)
                {
                    if (item.Rect != null) item.Rect.gameObject.SetActive(false);
                    continue;
                }

                float angle = (i / (float)targetRisingRingCount) * Mathf.PI * 2f + ((i % 3) - 1) * 0.12f;
                Vector2 spawnPos = HeadlineBurstPosition(i, targetRisingRingCount);
                float burstSpeed = 600f + (i % 5) * 70f + (mergeCount >= 4 ? 75f : 0f);
                item.StartPosition = spawnPos;
                item.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * burstSpeed;
                item.Rotation = ((i % 5) - 2) * 4f;
                // Gentle spin keeps the square-ring silhouettes readable during the burst.
                item.AngularVelocity = (i % 2 == 0 ? 1f : -1f) * (15f + (i % 4) * 4f);

                // LevelUpEffect startSize uses 0.75..1.5 ratio of square rings
                float squareSide = 68f + (i % 4) * 22f;
                item.BaseSize = new Vector2(squareSide, squareSide);
                item.BaseColor = LevelUpRingPalette[i % LevelUpRingPalette.Length];
                item.Delay = (i % 6) * 0.025f;
                item.Lifetime = Mathf.Max(0.85f, _totalDuration - 0.18f);

                if (item.Rect != null)
                {
                    item.Rect.gameObject.SetActive(true);
                    item.Rect.sizeDelta = item.BaseSize;
                    item.Rect.anchoredPosition = item.StartPosition;
                    item.Rect.localRotation = Quaternion.Euler(0f, 0f, item.Rotation);
                    item.Rect.localScale = Vector3.one;
                }
                if (item.Graphic != null)
                {
                    item.Graphic.color = item.BaseColor;
                }
                _rubble[i] = item;
            }

            // 3. Also pulse the 3D LevelUpEffect on board towers in Play Mode if wired
            if (Application.isPlaying && _levelUpEffectPrefab != null && _game != null && _game.grid != null && _game.grid.grid != null)
            {
                int cols = _game.grid.totalColumns;
                int rows = _game.grid.totalRows;
                int spawned3D = 0;
                for (int c = 0; c < cols && spawned3D < 4; c++)
                {
                    for (int r = rows - 1; r >= 0 && spawned3D < 4; r--)
                    {
                        GameObject cellTower = _game.grid.grid[c, r];
                        if (cellTower != null)
                        {
                            Instantiate(_levelUpEffectPrefab, cellTower.transform.position, _levelUpEffectPrefab.transform.rotation);
                            spawned3D++;
                            break;
                        }
                    }
                }
            }
        }

        private void UpdateSeismicBurst(float time)
        {
            for (int i = 0; i < _shockwaves.Count; i++)
            {
                ExpandingSquareRing ring = _shockwaves[i];
                if (ring.Rect == null || !ring.Rect.gameObject.activeSelf) continue;

                float localTime = time - ring.Delay;
                if (localTime <= 0f)
                {
                    ring.Rect.localScale = Vector3.zero;
                    continue;
                }

                if (localTime >= ring.Duration)
                {
                    ring.Rect.gameObject.SetActive(false);
                    continue;
                }

                float t = Mathf.Clamp01(localTime / ring.Duration);
                float easeOut = 1f - (1f - t) * (1f - t) * (1f - t);
                float diameter = Mathf.Lerp(ring.StartDiameter, ring.EndDiameter, easeOut);
                float rot = ring.StartRotation + ring.AngularVelocity * localTime;

                ring.Rect.anchoredPosition = ring.Origin + new Vector2(0f, ring.RiseDistance * easeOut);
                ring.Rect.sizeDelta = new Vector2(diameter, diameter);
                ring.Rect.localRotation = Quaternion.Euler(0f, 0f, rot);
                ring.Rect.localScale = Vector3.one;

                if (ring.Graphic != null)
                {
                    Color c = ring.BaseColor;
                    c.a = ring.BaseColor.a * (1f - Smooth(t));
                    ring.Graphic.color = c;
                }
            }

            // Damped upward motion matching LevelUpEffect's VelocityModule (dampen = 0.125, no downward gravity)
            const float upwardDamping = 1.65f;

            for (int i = 0; i < _rubble.Count; i++)
            {
                RisingSquareRing item = _rubble[i];
                if (item.Rect == null || !item.Rect.gameObject.activeSelf) continue;

                float localTime = time - item.Delay;
                if (localTime <= 0f)
                {
                    item.Rect.localScale = Vector3.zero;
                    continue;
                }

                if (localTime >= item.Lifetime)
                {
                    item.Rect.gameObject.SetActive(false);
                    continue;
                }
                float lifeT = Mathf.Clamp01(localTime / item.Lifetime);
                float dampedTime = (1f - Mathf.Exp(-upwardDamping * localTime)) / upwardDamping;
                Vector2 pos = item.StartPosition + item.Velocity * dampedTime;
                float rot = item.Rotation + item.AngularVelocity * localTime;
                // Rings expand slightly as they rise and fade out, matching LevelUpEffect
                float scalePop = localTime < 0.06f ? localTime / 0.06f : Mathf.Lerp(1.0f, 1.32f, Smooth(lifeT));

                item.Rect.anchoredPosition = pos;
                item.Rect.localRotation = Quaternion.Euler(0f, 0f, rot);
                item.Rect.localScale = Vector3.one * scalePop;

                if (item.Graphic != null)
                {
                    Color c = item.BaseColor;
                    // Linear/smooth alpha fade from 1 -> 0 over lifetime, matching LevelUpEffect ColorModule
                    c.a = item.BaseColor.a * (1f - Smooth(lifeT));
                    item.Graphic.color = c;
                }
            }
        }

        private void EnsureLevelUpRingPool(int waveCount, int risingRingCount)
        {
            if (_vfxContainer == null) return;

            for (int i = _vfxContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = _vfxContainer.GetChild(i);
                if (child != null && child.name.StartsWith("Confetti_"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            Sprite squareRingSprite = GetOrCreateSquareRingSprite();
            while (_shockwaves.Count < waveCount)
            {
                int idx = _shockwaves.Count;
                GameObject go = new GameObject("ShockwaveRing_" + idx, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(_vfxContainer, false);
                rt.SetAsFirstSibling();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                Image img = go.GetComponent<Image>();
                img.sprite = squareRingSprite;
                img.raycastTarget = false;
                go.SetActive(false);
                _shockwaves.Add(new ExpandingSquareRing
                {
                    Rect = rt,
                    Graphic = img,
                    StartDiameter = 180f,
                    EndDiameter = 960f,
                    RiseDistance = 120f,
                    BaseColor = Color.white,
                    Duration = 0.78f
                });
            }

            for (int i = 0; i < _shockwaves.Count; i++)
            {
                if (_shockwaves[i].Graphic != null && _shockwaves[i].Graphic.sprite != squareRingSprite)
                {
                    _shockwaves[i].Graphic.sprite = squareRingSprite;
                }
            }

            while (_rubble.Count < risingRingCount)
            {
                int idx = _rubble.Count;
                GameObject go = new GameObject("RubbleShard_" + idx, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(_vfxContainer, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                Image img = go.GetComponent<Image>();
                img.sprite = squareRingSprite;
                img.raycastTarget = false;
                go.SetActive(false);
                _rubble.Add(new RisingSquareRing
                {
                    Rect = rt,
                    Graphic = img,
                    BaseSize = new Vector2(76f, 76f),
                    BaseColor = Color.white
                });
            }

            for (int i = 0; i < _rubble.Count; i++)
            {
                if (_rubble[i].Graphic != null && _rubble[i].Graphic.sprite != squareRingSprite)
                {
                    _rubble[i].Graphic.sprite = squareRingSprite;
                }
            }
        }

        private Sprite GetOrCreateSquareRingSprite()
        {
            if (_levelUpRingTexture != null)
            {
                if (_runtimeRingSprite == null || _runtimeRingSpriteSource != _levelUpRingTexture)
                {
                    _runtimeRingSpriteSource = _levelUpRingTexture;
                    _runtimeRingSprite = Sprite.Create(
                        _levelUpRingTexture,
                        new Rect(0f, 0f, _levelUpRingTexture.width, _levelUpRingTexture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    _runtimeRingSprite.name = "LevelUpSquareRingSprite";
                }
                return _runtimeRingSprite;
            }

            if (_cachedProceduralSquareRingSprite != null) return _cachedProceduralSquareRingSprite;

            // Procedural fallback matching Assets/Prefabs/Cell/Square.png (rounded-square outline ring)
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ProceduralLevelUpSquareRing",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            const float halfExtent = 0.72f;
            const float cornerRadius = 0.16f;
            const float ringCenterDist = 0.10f;
            const float ringHalfThickness = 0.055f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(Mathf.Abs((x - center) / center), Mathf.Abs((y - center) / center));
                    Vector2 q = p - new Vector2(halfExtent, halfExtent);
                    float sdf = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - cornerRadius;
                    float distFromBand = Mathf.Abs(sdf - ringCenterDist);
                    float alpha = 1f - Mathf.Clamp01((distFromBand - ringHalfThickness) / 0.03f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            _cachedProceduralSquareRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _cachedProceduralSquareRingSprite;
        }

        private void HideVisualElements()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
            if (_backdropImage != null)
            {
                Color c = _backdropImage.color;
                c.a = 0f;
                _backdropImage.color = c;
            }
            if (_bannerRoot != null)
            {
                _bannerRoot.localScale = Vector3.zero;
                _bannerRoot.localRotation = Quaternion.identity;
                if (_bannerPosCaptured)
                {
                    _bannerRoot.anchoredPosition = _bannerBaseAnchoredPos;
                }
            }
            for (int i = 0; i < _shockwaves.Count; i++)
            {
                if (_shockwaves[i].Rect != null) _shockwaves[i].Rect.gameObject.SetActive(false);
            }
            for (int i = 0; i < _rubble.Count; i++)
            {
                if (_rubble[i].Rect != null) _rubble[i].Rect.gameObject.SetActive(false);
            }
        }

        public void ClearImmediate()
        {
            _celebrating = false;
            _burstTriggered = false;
            _age = 0f;
            HideVisualElements();
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
        }

        private void EnsureReferences()
        {
            if (_selfRect == null) _selfRect = transform as RectTransform;
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_backdropImage == null) _backdropImage = GetComponent<Image>();
            if (_bannerRoot != null && !_bannerPosCaptured)
            {
                _bannerBaseAnchoredPos = _bannerRoot.anchoredPosition;
                _bannerPosCaptured = true;
            }
        }

        private void OnDisable()
        {
            ClearImmediate();
        }

        private void OnDestroy()
        {
            UnsubscribeGame();
        }
    }
}


