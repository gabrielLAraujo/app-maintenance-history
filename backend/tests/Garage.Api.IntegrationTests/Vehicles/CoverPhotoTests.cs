using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Garage.Api.IntegrationTests.Infrastructure;

namespace Garage.Api.IntegrationTests.Vehicles;

[Collection("api")]
public sealed class CoverPhotoTests(GarageApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Owner_sets_and_clears_the_cover_photo()
    {
        using var owner = await UserAsync();
        var (vehicleId, photoId) = await VehicleWithPhotoAsync(owner);

        using (var set = await PutCoverAsync(owner, vehicleId, photoId))
            Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        Assert.Equal(photoId, CoverPhotoId(await owner.GetVehicleAsync(vehicleId)));

        using (var clear = await PutCoverAsync(owner, vehicleId, null))
            Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);
        Assert.Null(CoverPhotoId(await owner.GetVehicleAsync(vehicleId)));
    }

    [Fact]
    public async Task Another_user_gets_404()
    {
        using var owner = await UserAsync();
        var (vehicleId, photoId) = await VehicleWithPhotoAsync(owner);
        using var other = await factory.CreateUserClientAsync();

        using var response = await PutCoverAsync(other, vehicleId, photoId);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "vehicle.not_found");
        Assert.Null(CoverPhotoId(await owner.GetVehicleAsync(vehicleId)));
    }

    [Fact]
    public async Task Photo_of_another_vehicle_is_422()
    {
        using var owner = await UserAsync();
        var (vehicleId, _) = await VehicleWithPhotoAsync(owner);
        var (_, otherPhotoId) = await VehicleWithPhotoAsync(owner);

        using var response = await PutCoverAsync(owner, vehicleId, otherPhotoId);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "vehicle.cover_must_be_vehicle_photo");
        Assert.Null(CoverPhotoId(await owner.GetVehicleAsync(vehicleId)));
    }

    [Fact]
    public async Task Document_is_422()
    {
        using var owner = await UserAsync();
        var vehicleId = await owner.CreateVehicleAsync();
        var recordId = await owner.CreateRecordAsync(vehicleId);
        var documentId = Id(await owner.UploadAttachmentAsync(
            vehicleId, recordId, TestImages.Pdf(), "nota.pdf", kind: "document"));

        using var response = await PutCoverAsync(owner, vehicleId, documentId);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "vehicle.cover_must_be_vehicle_photo");
    }

    [Fact]
    public async Task Deleting_the_cover_photo_clears_the_cover()
    {
        using var owner = await UserAsync();
        var (vehicleId, photoId) = await VehicleWithPhotoAsync(owner);
        using (var set = await PutCoverAsync(owner, vehicleId, photoId))
            Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        using (var delete = await owner.DeleteAsync($"/api/vehicles/{vehicleId}/attachments/{photoId}"))
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Null(CoverPhotoId(await owner.GetVehicleAsync(vehicleId)));
    }

    private async Task<HttpClient> UserAsync()
    {
        factory.Clock.SetUtcNow(Now);
        return await factory.CreateUserClientAsync();
    }

    /// <summary>Um veículo com um registro e uma foto (pública, o padrão do upload).</summary>
    private static async Task<(Guid VehicleId, Guid PhotoId)> VehicleWithPhotoAsync(HttpClient owner)
    {
        var vehicleId = await owner.CreateVehicleAsync();
        var recordId = await owner.CreateRecordAsync(vehicleId);
        var photo = await owner.UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "a.png");
        return (vehicleId, Id(photo));
    }

    private static Task<HttpResponseMessage> PutCoverAsync(HttpClient client, Guid vehicleId, Guid? attachmentId) =>
        client.PutAsJsonAsync($"/api/vehicles/{vehicleId}/cover-photo", new { attachmentId }, TestJson.Options);

    private static Guid? CoverPhotoId(JsonElement vehicle)
    {
        var cover = vehicle.GetProperty("coverPhotoId");
        return cover.ValueKind == JsonValueKind.Null ? null : cover.GetGuid();
    }

    private static Guid Id(JsonElement element) => element.GetProperty("id").GetGuid();
}
