public interface ITextToGlossService
{
    void ConvertToGloss(string inputText, System.Action<GlossResult> onResult);
}