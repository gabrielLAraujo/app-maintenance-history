using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Vehicles.SetCoverPhoto;

/// <summary>Define a foto de capa do veículo (C1); <see cref="AttachmentId"/> nulo remove a capa.</summary>
public sealed record SetCoverPhotoCommand(Guid VehicleId, Guid? AttachmentId) : ICommand<Unit>;
