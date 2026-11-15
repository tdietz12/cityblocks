using System.Collections.Generic;
using Gameplay;
using TMPro;
using UnityEngine;
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

        private static readonly Color[] ConfettiPalette =
        {
            new Color(1.00f, 0.80f, 0.10f, 1f), // Hazard Yellow
            new Color(0.14f, 0.12f, 0.10f, 1f), // Caution Black
            new Color(1.00f, 0.48f, 0.12f, 1f), // Construction Orange
            new Color(1.00f, 0.92f, 0.45f, 1f), // Bright Gold
            new Color(0.24f, 0.85f, 1.00f, 1f), // Sky Cyan
            new Color(0.96f, 0.96f, 0.96f, 1f)  // Ticker White
        };

        [Header("UI References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _backdropImage;
        [SerializeField] private RectTransform _bannerRoot;
        [SerializeField] private TMP_Text _headlineLabel;
        [SerializeField] private RectTransform _confettiContainer;

        [Header("Timing & Motion")]
        [SerializeField, Min(0f)] private float _startDelay = 0.2f;
        [SerializeField, Min(0.1f)] private float _totalDuration = 2.25f;
        [SerializeField, Min(0.05f)] private float _fadeInDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float _popDuration = 0.28f;
        [SerializeField, Min(0.05f)] private float _fadeOutDuration = 0.30f;
        [SerializeField, Range(0f, 0.9f)] private float _backdropPeakAlpha = 0.58f;
        [SerializeField, Min(1f)] private float _headlineOvershootScale = 1.08f;
        [SerializeField, Min(4f)] private float _screenHorizontalPadding = 16f;
        [SerializeField, Range(8, 64)] private int _confettiBurstCount = 32;

        private struct ConfettiPiece
        {
            public RectTransform Rect;
            public Image Graphic;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public float AngularVelocity;
            public Vector2 BaseScale;
            public Color BaseColor;
            public float Delay;
            public float Lifetime;
        }

        private readonly List<ConfettiPiece> _confetti = new List<ConfettiPiece>();
        private RectTransform _selfRect;
        private GameController _game;
        private BoardCameraFeedback _cameraFeedback;
        private float _age;
        private float _fitBaseScale = 1f;
        private bool _celebrating;
        private bool _burstTriggered;
        private int _variantCounter;

        public bool IsCelebrating => _celebrating;
        public bool IsInStartDelay => _celebrating && _age < _startDelay;
        public float StartDelay => _startDelay;
        public float TotalDuration => _totalDuration;
        public string CurrentHeadline => _headlineLabel != null ? _headlineLabel.text : string.Empty;
        public int LastCelebratedMergeCount { get; private set; }
        public int ActiveConfettiCount
        {
            get
            {
                if (!_celebrating || !_burstTriggered) return 0;
                int count = 0;
                for (int i = 0; i < _confetti.Count; i++)
                {
                    if (_confetti[i].Rect != null && _confetti[i].Rect.gameObject.activeSelf) count++;
                }
                return count;
            }
        }

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

            if (_startDelay <= 0f)
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
            SpawnConfettiBurst(LastCelebratedMergeCount);

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
            delta = Mathf.Max(0f, delta);
            _age += delta;

            if (_age < _startDelay)
            {
                HideVisualElements();
                return;
            }

            float activeTime = _age - _startDelay;
            if (activeTime >= _totalDuration)
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
            float fadeOutStart = Mathf.Max(_fadeInDuration, _totalDuration - _fadeOutDuration);
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
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = time > fadeOutStart ? envelope : 1f;
            }

            UpdateConfetti(time);
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

        private void SpawnConfettiBurst(int mergeCount)
        {
            if (_confettiContainer == null) return;
            int targetCount = Mathf.Clamp(_confettiBurstCount + (mergeCount - 3) * 6, 16, 48);
            EnsureConfettiPool(targetCount);

            for (int i = 0; i < _confetti.Count; i++)
            {
                ConfettiPiece piece = _confetti[i];
                if (i >= targetCount)
                {
                    if (piece.Rect != null) piece.Rect.gameObject.SetActive(false);
                    continue;
                }

                float angle = (i / (float)targetCount) * Mathf.PI * 2f + ((i % 3) - 1) * 0.14f;
                float speed = 420f + (i % 5) * 115f + (mergeCount >= 4 ? 75f : 0f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                piece.Position = dir * (24f + (i % 4) * 10f);
                piece.Velocity = dir * speed + new Vector2(0f, 210f);
                piece.Rotation = (i * 37f) % 360f;
                piece.AngularVelocity = (i % 2 == 0 ? 1f : -1f) * (220f + (i % 4) * 90f);
                // Ticker-tape / caution-strip proportions
                piece.BaseScale = (i % 3 == 0) ? new Vector2(36f, 12f) : new Vector2(28f, 14f);
                piece.BaseColor = ConfettiPalette[i % ConfettiPalette.Length];
                piece.Delay = (i % 4) * 0.015f;
                piece.Lifetime = Mathf.Max(0.8f, _totalDuration - 0.1f);

                if (piece.Rect != null)
                {
                    piece.Rect.gameObject.SetActive(true);
                    piece.Rect.sizeDelta = piece.BaseScale;
                    piece.Rect.anchoredPosition = piece.Position;
                    piece.Rect.localRotation = Quaternion.Euler(0f, 0f, piece.Rotation);
                    piece.Rect.localScale = Vector3.one;
                }
                if (piece.Graphic != null)
                {
                    piece.Graphic.color = piece.BaseColor;
                }
                _confetti[i] = piece;
            }
        }

        private void UpdateConfetti(float time)
        {
            const float gravity = -640f;
            const float drag = 1.35f;

            for (int i = 0; i < _confetti.Count; i++)
            {
                ConfettiPiece piece = _confetti[i];
                if (piece.Rect == null || !piece.Rect.gameObject.activeSelf) continue;

                float localTime = time - piece.Delay;
                if (localTime <= 0f)
                {
                    piece.Rect.localScale = Vector3.zero;
                    continue;
                }

                float lifeT = Mathf.Clamp01(localTime / piece.Lifetime);
                float dampedTime = (1f - Mathf.Exp(-drag * localTime)) / drag;
                Vector2 pos = piece.Position + piece.Velocity * dampedTime + new Vector2(0f, 0.5f * gravity * localTime * localTime);
                float rot = piece.Rotation + piece.AngularVelocity * localTime;
                float flip = 0.65f + 0.35f * Mathf.Cos(localTime * 11f + i);
                float scalePop = localTime < 0.08f ? localTime / 0.08f : (lifeT > 0.72f ? 1f - (lifeT - 0.72f) / 0.28f : 1f);

                piece.Rect.anchoredPosition = pos;
                piece.Rect.localRotation = Quaternion.Euler(0f, 0f, rot);
                piece.Rect.localScale = new Vector3(scalePop, scalePop * flip, 1f);

                if (piece.Graphic != null)
                {
                    Color c = piece.BaseColor;
                    c.a = lifeT > 0.68f ? Mathf.Clamp01(1f - (lifeT - 0.68f) / 0.32f) : 1f;
                    piece.Graphic.color = c;
                }
            }
        }

        private void EnsureConfettiPool(int count)
        {
            if (_confettiContainer == null) return;
            while (_confetti.Count < count)
            {
                int idx = _confetti.Count;
                GameObject go = new GameObject("Confetti_" + idx, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(_confettiContainer, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                Image img = go.GetComponent<Image>();
                img.raycastTarget = false;
                go.SetActive(false);
                _confetti.Add(new ConfettiPiece
                {
                    Rect = rt,
                    Graphic = img,
                    BaseScale = new Vector2(32f, 12f),
                    BaseColor = Color.white
                });
            }
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
            }
            for (int i = 0; i < _confetti.Count; i++)
            {
                if (_confetti[i].Rect != null) _confetti[i].Rect.gameObject.SetActive(false);
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

