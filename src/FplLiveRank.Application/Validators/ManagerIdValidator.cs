using FluentValidation;

namespace FplLiveRank.Application.Validators;

public sealed class ManagerIdValidator : AbstractValidator<int>
{
    public ManagerIdValidator()
    {
        RuleFor(id => id).GreaterThan(0).WithMessage("Manager ID must be positive.");
    }
}
