namespace Garage.Domain.Common;

public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    PayloadTooLarge,
    UnsupportedMedia,
    DomainRule,
    Unavailable,
    TooManyRequests
}
