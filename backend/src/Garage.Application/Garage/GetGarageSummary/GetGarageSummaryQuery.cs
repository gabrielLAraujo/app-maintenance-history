using Garage.Application.Abstractions.Messaging;

namespace Garage.Application.Garage.GetGarageSummary;

public sealed record GetGarageSummaryQuery : IQuery<GarageSummaryDto>;
