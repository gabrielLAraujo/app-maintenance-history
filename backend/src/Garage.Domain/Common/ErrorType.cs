namespace Garage.Domain.Common;

public enum ErrorType
{
    Validation,
    Unauthorized,
    NotFound,
    Conflict,
    PayloadTooLarge,
    UnsupportedMedia,
    DomainRule,
    Unavailable,
    TooManyRequests
}
