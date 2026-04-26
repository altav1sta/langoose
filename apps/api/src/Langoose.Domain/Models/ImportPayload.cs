namespace Langoose.Domain.Models;

public sealed record ImportPayload(
    ImportPayloadEntry Entry,
    ImportPayloadSense[] Senses);
