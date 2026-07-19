using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maliev.ProjectService.Application.DTOs;
using Maliev.ProjectService.Infrastructure.HttpClients;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maliev.ProjectService.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="QuotationServiceClient"/>.
/// </summary>
public class QuotationServiceClientTests
{
    /// <summary>
    /// Verifies ProjectService posts the current QuotationService DTO shape.
    /// </summary>
    [Fact]
    public async Task CreateQuotationAsync_PostsLineItemsWithMaterialContract()
    {
        var quotationId = Guid.NewGuid();
        string? requestBody = null;
        var handler = new CapturingHandler(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = quotationId,
                    currentVersionNumber = 1,
                    status = 0,
                    validityPeriodStart = DateTime.UtcNow.Date,
                    validityPeriodEnd = DateTime.UtcNow.Date.AddDays(30),
                    createdAt = DateTime.UtcNow,
                    updatedAt = DateTime.UtcNow
                })
            };
        });
        var client = new QuotationServiceClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://quotation-service") },
            NullLogger<QuotationServiceClient>.Instance);

        var materialId = Guid.NewGuid();
        var sourceProjectId = Guid.NewGuid();
        var result = await client.CreateQuotationAsync(new CreateQuotationFromProjectRequest
        {
            CustomerId = Guid.NewGuid(),
            SourceProjectId = sourceProjectId,
            SourceProjectNumber = "PRJ-20260514-001",
            ValidityPeriodStart = DateTime.UtcNow.Date,
            ValidityPeriodEnd = DateTime.UtcNow.Date.AddDays(30),
            DeliveryExpectations = "Standard lead time",
            BulkDiscountAmount = 150m,
            ManualDiscountAmount = 75m,
            ShippingCost = 250m,
            TaxAmount = 162.75m,
            QuotationTerms = "50% deposit required before production.",
            ProjectSnapshotJson = """{"projectNumber":"PRJ-20260514-001"}""",
            ProjectSnapshotHash = "snapshot-hash",
            GeneratedByDisplayName = "Project Specialist",
            ChangeSummary = "Initial project quote",
            Items =
            [
                new QuotationLineItemRequest
                {
                    Description = "fixture.stl - FDM",
                    MaterialServiceId = materialId,
                    Quantity = 2,
                    UnitOfMeasure = "pcs",
                    UnitPrice = 1250m,
                    ManufacturingProcess = "FDM",
                    Notes = "PLA-BLK"
                }
            ]
        });

        Assert.Equal(quotationId, result.QuotationId);
        Assert.StartsWith("Q-", result.QuotationNumber);
        Assert.NotNull(requestBody);

        using var document = JsonDocument.Parse(requestBody);
        var root = document.RootElement;
        Assert.Equal(sourceProjectId, root.GetProperty("sourceProjectId").GetGuid());
        Assert.Equal("PRJ-20260514-001", root.GetProperty("sourceProjectNumber").GetString());
        Assert.True(root.TryGetProperty("lineItems", out var lineItems));
        Assert.False(root.TryGetProperty("items", out _));
        Assert.True(root.TryGetProperty("discountStructure", out var discountStructure));
        Assert.Equal(2, discountStructure.GetProperty("discountType").GetInt32());
        Assert.Equal(150m, discountStructure.GetProperty("discountValue").GetDecimal());
        Assert.Equal(75m, root.GetProperty("manualDiscountAmount").GetDecimal());
        Assert.Equal(250m, root.GetProperty("shippingCost").GetDecimal());
        Assert.Equal(162.75m, root.GetProperty("taxAmount").GetDecimal());
        Assert.Equal("50% deposit required before production.", root.GetProperty("specialTerms").GetString());
        Assert.Equal("""{"projectNumber":"PRJ-20260514-001"}""", root.GetProperty("projectSnapshotJson").GetString());
        Assert.Equal("snapshot-hash", root.GetProperty("projectSnapshotHash").GetString());
        Assert.Equal("Project Specialist", root.GetProperty("generatedByDisplayName").GetString());
        Assert.Equal("Initial project quote", root.GetProperty("changeSummary").GetString());
        var item = lineItems[0];
        Assert.Equal(materialId, item.GetProperty("materialServiceId").GetGuid());
        Assert.Equal(2, item.GetProperty("quantity").GetInt32());
        Assert.Equal("pcs", item.GetProperty("unitOfMeasure").GetString());
        Assert.Equal(1250m, item.GetProperty("unitPrice").GetDecimal());
        Assert.Equal("FDM", item.GetProperty("manufacturingProcess").GetString());
        Assert.Equal("PLA-BLK", item.GetProperty("notes").GetString());
    }

    /// <summary>
    /// Verifies ProjectService updates an existing quotation instead of creating a disconnected quotation.
    /// </summary>
    [Fact]
    public async Task UpdateQuotationAsync_PutsNewVersionContract()
    {
        var quotationId = Guid.NewGuid();
        string? requestBody = null;
        string? requestPath = null;
        var handler = new CapturingHandler(async request =>
        {
            requestPath = request.RequestUri?.AbsolutePath;
            requestBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = quotationId,
                    quotationNumber = "Q-PROJECT",
                    currentVersionNumber = 2,
                    total = 2500m,
                    versions = new[]
                    {
                        new { id = Guid.NewGuid(), versionNumber = 2 }
                    }
                })
            };
        });
        var client = new QuotationServiceClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://quotation-service") },
            NullLogger<QuotationServiceClient>.Instance);

        var result = await client.UpdateQuotationAsync(quotationId, new CreateQuotationFromProjectRequest
        {
            CustomerId = Guid.NewGuid(),
            SourceProjectId = Guid.NewGuid(),
            SourceProjectNumber = "PRJ-20260514-002",
            ValidityPeriodStart = DateTime.UtcNow.Date,
            ValidityPeriodEnd = DateTime.UtcNow.Date.AddDays(30),
            ProjectSnapshotJson = """{"version":2}""",
            ProjectSnapshotHash = "hash-v2",
            ChangeSummary = "Changed quantity",
            Items =
            [
                new QuotationLineItemRequest
                {
                    Description = "fixture.stl - FDM",
                    MaterialServiceId = Guid.NewGuid(),
                    Quantity = 2,
                    UnitOfMeasure = "pcs",
                    UnitPrice = 1250m,
                    ManufacturingProcess = "FDM"
                }
            ]
        });

        Assert.Equal(quotationId, result.QuotationId);
        Assert.Equal("Q-PROJECT", result.QuotationNumber);
        Assert.Equal(2, result.CurrentVersionNumber);
        Assert.Equal(2500m, result.Total);
        Assert.Equal($"/quotation/v1/quotations/{quotationId}", requestPath);
        Assert.NotNull(requestBody);

        using var document = JsonDocument.Parse(requestBody);
        var root = document.RootElement;
        Assert.Equal("""{"version":2}""", root.GetProperty("projectSnapshotJson").GetString());
        Assert.Equal("hash-v2", root.GetProperty("projectSnapshotHash").GetString());
        Assert.Equal("Changed quantity", root.GetProperty("changeSummary").GetString());
        Assert.True(root.TryGetProperty("lineItems", out var lineItems));
        Assert.Single(lineItems.EnumerateArray());
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request);
    }
}
