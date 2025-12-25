using BusinessObjectLayer.IServices;
using Google.Cloud.Language.V1;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class ContentValidationService : IContentValidationService
    {
        private readonly LanguageServiceClient _languageClient;

        public ContentValidationService()
        {
            // When running on Google Cloud, automatically use default credentials
            // When running locally, set GOOGLE_APPLICATION_CREDENTIALS environment variable
            _languageClient = LanguageServiceClient.Create();
        }

        public async Task<(bool IsValid, string ErrorMessage)> ValidateJobContentAsync(string text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text))
                return (false, $"{fieldName} cannot be empty");

            try
            {
                var document = new Document
                {
                    Content = text,
                    Type = Document.Types.Type.PlainText
                };

                // Analyze syntax to check if text is meaningful
                var syntaxResponse = await _languageClient.AnalyzeSyntaxAsync(document);

                // Count meaningful tokens (nouns, verbs, adjectives)
                var meaningfulTokens = syntaxResponse.Tokens
                    .Where(t => t.PartOfSpeech.Tag == PartOfSpeech.Types.Tag.Noun ||
                               t.PartOfSpeech.Tag == PartOfSpeech.Types.Tag.Verb ||
                               t.PartOfSpeech.Tag == PartOfSpeech.Types.Tag.Adj)
                    .ToList();

                if (meaningfulTokens.Count < 3)
                    return (false, $"{fieldName} must contain meaningful content with at least 3 important words (nouns/verbs/adjectives)");

                // Analyze sentiment to avoid spam/negative content
                var sentimentResponse = await _languageClient.AnalyzeSentimentAsync(document);
                
                // If sentiment is too negative, it might be spam or inappropriate content
                if (sentimentResponse.DocumentSentiment.Score < -0.8)
                    return (false, $"{fieldName} contains inappropriate content");

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                // Log error but don't block user if API fails
                Console.WriteLine($"Content validation error: {ex.Message}");
                
                // Fallback validation if Google API fails
                var words = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (words.Length < 1)
                    return (false, $"{fieldName} must contain at least 1 word");
                    
                // Pass if API fails to not block users
                return (true, string.Empty);
            }
        }
    }
}

