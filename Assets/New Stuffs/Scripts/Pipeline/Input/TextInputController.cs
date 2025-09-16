using System;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
// Alias
using UInput = UnityEngine.Input;

namespace YourApp.Signs.Pipeline.InputLayer
{
    /// <summary>
    /// TMP input adapter. Emits OnSubmit and OnChange. Optional Enter handling.
    /// Does NOT call the pipeline. Keep it dumb and reusable.
    /// </summary>
    public class TextInputController : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("If assigned, this takes priority.")]
        public TMP_InputField TmpField;

        [Header("Behavior")]
        public bool listenForEnterKey = true;       // NEW: toggle Enter handling
        public KeyCode submitKey = KeyCode.Return;
        public bool trimWhitespace = true;
        public bool clearOnSubmit = false;          // Let orchestrator decide when to clear
        public bool shiftEnterNewLine = true;       // If true, Shift+Enter inserts newline

        [Header("Events")]
        public UnityEvent<string> OnSubmit;
        public UnityEvent<string> OnChange;         // NEW

        private void OnEnable()
        {
            if (TmpField)
            {
                TmpField.onSubmit.AddListener(OnTmpSubmit);
                TmpField.onValueChanged.AddListener(OnTmpChange);
                // Recommend: allow multiline so Shift+Enter works if enabled
                TmpField.lineType = TMP_InputField.LineType.MultiLineNewline;
            }
        }

        private void OnDisable()
        {
            if (TmpField)
            {
                TmpField.onSubmit.RemoveListener(OnTmpSubmit);
                TmpField.onValueChanged.RemoveListener(OnTmpChange);
            }
        }

        private void Update()
        {
            if (!listenForEnterKey) return;
            if (TmpField && !TmpField.isFocused) return;

            if (UInput.GetKeyDown(submitKey))
            {
                bool shiftHeld = UInput.GetKey(KeyCode.LeftShift) || UInput.GetKey(KeyCode.RightShift);
                if (shiftEnterNewLine && shiftHeld)
                    return; // let TMP insert newline
                SubmitFromUI();
            }
        }

        public void SubmitFromUI()
        {
            string text = GetText();
            if (trimWhitespace) text = text?.Trim();

            if (!string.IsNullOrEmpty(text))
                OnSubmit?.Invoke(text);

            if (clearOnSubmit) Clear();
        }

        public string GetText() => TmpField ? (TmpField.text ?? "") : "";

        public void SetText(string value)
        {
            if (TmpField) TmpField.text = value ?? "";
        }

        public void SetTextWithoutNotify(string value)
        {
            if (TmpField)TmpField.SetTextWithoutNotify(value ?? "");
        }

        public void Clear() => SetTextWithoutNotify(string.Empty);

        public void Focus(bool focused)
        {
            if (!TmpField) return;
            if (focused) TmpField.ActivateInputField();
            else TmpField.DeactivateInputField();
        }

        public void SetListenForEnter(bool enabled) => listenForEnterKey = enabled;

        private void OnTmpSubmit(string _) => SubmitFromUI();

        private void OnTmpChange(string value) => OnChange?.Invoke(value ?? "");
    }
}
