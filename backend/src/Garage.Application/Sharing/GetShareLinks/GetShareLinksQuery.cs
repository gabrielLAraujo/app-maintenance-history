using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Sharing.GetShareLinks;

public sealed record GetShareLinksQuery(Guid VehicleId) : IQuery<IReadOnlyList<ShareLinkDto>>;
