using System.Threading.Tasks;

namespace EMQ.Shared.Quiz.Entities.Abstract;

public interface IAutocompleteComponent
{
    public Task ClearInputField();

    public void CallStateHasChanged();

    public string? GetSelectedText();

    public void CallClose();
}
