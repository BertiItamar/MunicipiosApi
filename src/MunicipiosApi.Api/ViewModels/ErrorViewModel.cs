namespace MunicipiosApi.Api.ViewModels;

public record ErrorViewModel(string Description);

public record ErrorsViewModel(IReadOnlyList<ErrorViewModel> Errors);
