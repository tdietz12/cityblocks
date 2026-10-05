using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Component attached to a tutorial step prefab.
    /// When advanceOnGameMove is false, continueButton must be visible and cannot be null.
    /// </summary>
    [ExecuteAlways]
    public class TutorialStep : MonoBehaviour
    {
        [Tooltip("If true, placing a tower or making a move on the board advances this step.")]
        public bool advanceOnGameMove = true;

        [Tooltip("Continue button required if advanceOnGameMove is false. Auto-detected in children if not assigned.")]
        public Button continueButton;

        private void Awake()
        {
            ValidateAndApply();
        }

        private void OnEnable()
        {
            ValidateAndApply();
        }

        private void OnValidate()
        {
            ValidateAndApply();
        }

        /// <summary>
        /// Ensures continueButton is found and active when advanceOnGameMove is false, throwing an InvalidOperationException if missing at runtime.
        /// </summary>
        public void ValidateAndApply()
        {
            if (continueButton == null)
            {
                continueButton = GetComponentInChildren<Button>(true);
            }

            if (!advanceOnGameMove)
            {
                if (continueButton == null)
                {
                    string errorMsg = $"[TutorialStep] '{name}' has advanceOnGameMove set to false, but continueButton is null or could not be found in children!";
                    if (Application.isPlaying)
                    {
                        throw new InvalidOperationException(errorMsg);
                    }
                    else
                    {
                        Debug.LogError(errorMsg, this);
                        return;
                    }
                }

                // Ensure continue button is visible and active
                if (!continueButton.gameObject.activeSelf)
                {
                    continueButton.gameObject.SetActive(true);
                }
            }
        }
    }
}


 