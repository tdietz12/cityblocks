using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UI;

namespace Editor
{
    [CustomEditor(typeof(TutorialStep))]
    [CanEditMultipleObjects]
    public class TutorialStepEditor : UnityEditor.Editor
    {
        private SerializedProperty advanceOnGameMoveProp;
        private SerializedProperty continueButtonProp;

        private void OnEnable()
        {
            advanceOnGameMoveProp = serializedObject.FindProperty("advanceOnGameMove");
            continueButtonProp = serializedObject.FindProperty("continueButton");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(advanceOnGameMoveProp, new GUIContent("Advance On Game Move", "If true, placing a tower or making a move advances this tutorial step."));

            bool advanceOnMove = advanceOnGameMoveProp.boolValue;

            if (!advanceOnMove)
            {
                // Highlight that continue button is mandatory
                EditorGUILayout.PropertyField(continueButtonProp, new GUIContent("Continue Button *", "REQUIRED: Must be assigned and visible because Advance On Game Move is false."));

                TutorialStep step = (TutorialStep)target;

                if (continueButtonProp.objectReferenceValue == null)
                {
                    // Attempt auto-assign from children
                    Button foundInChild = step.GetComponentInChildren<Button>(true);
                    if (foundInChild != null)
                    {
                        continueButtonProp.objectReferenceValue = foundInChild;
                        serializedObject.ApplyModifiedProperties();
                    }
                }

                if (continueButtonProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("Advance On Game Move is FALSE: You must assign a Continue Button (or add a Button component to a child of this prefab).", MessageType.Error);
                }
                else
                {
                    Button btn = continueButtonProp.objectReferenceValue as Button;
                    if (btn != null && !btn.gameObject.activeSelf)
                    {
                        EditorGUILayout.HelpBox("Continue Button is currently inactive in the hierarchy. It will be made active automatically.", MessageType.Warning);
                        if (GUILayout.Button("Make Continue Button Active Now"))
                        {
                            Undo.RecordObject(btn.gameObject, "Activate Continue Button");
                            btn.gameObject.SetActive(true);
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.PropertyField(continueButtonProp, new GUIContent("Continue Button (Optional)", "Optional continue button. Player can also advance simply by making a move on the board."));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
