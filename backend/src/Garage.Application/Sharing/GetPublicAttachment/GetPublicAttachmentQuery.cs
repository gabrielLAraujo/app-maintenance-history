using Garage.Application.Abstractions.Messaging;
using Garage.Application.Attachments;

namespace Garage.Application.Sharing.GetPublicAttachment;

public sealed record GetPublicAttachmentQuery(string Token, Guid AttachmentId) : IQuery<FileContent>;
