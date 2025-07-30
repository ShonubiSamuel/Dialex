public interface ISpeechToTextService
{
    void Transcribe(string filePath, System.Action<string> onResult);
}