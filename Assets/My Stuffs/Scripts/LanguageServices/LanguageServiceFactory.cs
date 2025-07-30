public static class LanguageServiceFactory
{
    public static LanguageService CreateYorubaService()
    {
        return new LanguageService(
            new YorubaTranscriber(),
            new YorubaToEnglishTranslator(),
            new EnglishGlossifier()
        );
    }

    public static LanguageService CreateEnglishService()
    {
        return new LanguageService(
            new GroqTranscriber(),
            null, // No translator needed
            new EnglishGlossifier()
        );
    }
}