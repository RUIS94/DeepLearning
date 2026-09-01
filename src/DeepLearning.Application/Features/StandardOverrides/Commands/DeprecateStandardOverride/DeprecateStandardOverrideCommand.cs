using MediatR;

namespace DeepLearning.Application.Features.StandardOverrides.Commands.DeprecateStandardOverride
{
    /// <summary>
    /// Retires a standard_overrides row (status -> deprecated). This is the only mutation the
    /// admin UI is allowed on this table: the audit chain stays append-only (no edit, no hard
    /// delete), 'deprecated' is already a legal state, and PreviousOverrideId links are untouched.
    /// Legal from 'observing' or 'active'; a no-op-worthy 409 if already 'deprecated'.
    /// </summary>
    public record DeprecateStandardOverrideCommand(Guid Id) : IRequest<DeprecateStandardOverrideResult>;
}
