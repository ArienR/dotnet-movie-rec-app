using Microsoft.AspNetCore.Components.Forms;

namespace MovieRecApp.Client.Utils;

public static class ValidationUtils
{
    public static void DisplayErrors(EditContext editContext, Dictionary<string, List<string>> errors)
    {
        var messages = new ValidationMessageStore(editContext);

        foreach (var error in errors)
        {
            var fieldIdentifier = new FieldIdentifier(editContext.Model, error.Key);
            foreach (var message in error.Value)
            {
                messages.Add(fieldIdentifier, message);
            }
        }

        // Force UI update
        editContext.NotifyValidationStateChanged();
    }

    public static Dictionary<string, List<string>> ConvertServerError(string key, string message)
    {
        return new Dictionary<string, List<string>>
        {
            { key, new List<string> { message } }
        };
    }
}