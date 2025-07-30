using UnityEngine;

public class InputModeBootstrap : MonoBehaviour
{
    public InputModeController modeController;
    public MicRecorder speech;
    public TextField text;

    void Start()
    {
        modeController.SetButtonCallbacks(
            speech.ToggleRecording,
            text.OnConvertClicked
        );
    }
}