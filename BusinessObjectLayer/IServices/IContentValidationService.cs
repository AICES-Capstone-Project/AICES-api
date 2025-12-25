using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IContentValidationService
    {
        Task<(bool IsValid, string ErrorMessage)> ValidateJobContentAsync(string text, string fieldName, int minMeaningfulTokens = 3);
    }
}

