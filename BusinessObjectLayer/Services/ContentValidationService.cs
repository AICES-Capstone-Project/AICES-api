using BusinessObjectLayer.IServices;
using Google.Cloud.Language.V1;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class ContentValidationService : IContentValidationService
    {
        private readonly LanguageServiceClient? _languageClient;
        private readonly bool _hasLanguageClient;

        public ContentValidationService()
        {
            // When running on Google Cloud, automatically use default credentials
            // When running locally, set GOOGLE_APPLICATION_CREDENTIALS environment variable
            // If credentials are not available, gracefully fall back to basic validation
            try
            {
                _languageClient = LanguageServiceClient.Create();
                _hasLanguageClient = true;
            }
            catch (Exception ex)
            {
                // Credentials not available - will use fallback validation
                Console.WriteLine($"⚠️ Google Cloud Language API credentials not found. Using fallback validation. Error: {ex.Message}");
                _languageClient = null;
                _hasLanguageClient = false;
            }
        }

        public async Task<(bool IsValid, string ErrorMessage)> ValidateJobContentAsync(string text, string fieldName, int minMeaningfulTokens = 3)
        {
            if (string.IsNullOrWhiteSpace(text))
                return (false, $"{fieldName} cannot be empty");

            // If Language API client is not available, use fallback validation
            if (!_hasLanguageClient || _languageClient == null)
            {
                return await FallbackValidationAsync(text, fieldName, minMeaningfulTokens);
            }

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

                if (meaningfulTokens.Count < minMeaningfulTokens)
                {
                    var wordText = minMeaningfulTokens == 1 ? "word" : "words";
                    return (false, $"{fieldName} must contain meaningful content with at least {minMeaningfulTokens} important {wordText} (nouns/verbs/adjectives)");
                }

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
                return await FallbackValidationAsync(text, fieldName, minMeaningfulTokens);
            }
        }

        private Task<(bool IsValid, string ErrorMessage)> FallbackValidationAsync(string text, string fieldName, int minMeaningfulTokens)
        {
            // Fallback validation if Google API fails or is not available
            var words = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < minMeaningfulTokens)
            {
                var wordText = minMeaningfulTokens == 1 ? "word" : "words";
                return Task.FromResult<(bool, string)>((false, $"{fieldName} must contain at least {minMeaningfulTokens} {wordText}"));
            }
            
            // Pass if API fails to not block users
            return Task.FromResult<(bool, string)>((true, string.Empty));
        }
    }
}

