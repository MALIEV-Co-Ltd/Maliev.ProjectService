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
        var result = await client.CreateQuotationAsync(new CreateQuotationFromProjectRequest
        {
            CustomerId = Guid.NewGuid(),
            ValidityPeriodStart = DateTime.UtcNow.Date,
            ValidityPeriodEnd = DateTime.UtcNow.Date.AddDays(30),
            DeliveryExpectations = "Standard lead time",
            BulkDiscountAmount = 150m,
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
        Assert.True(root.TryGetProperty("lineItems", out var lineItems));
        Assert.False(root.TryGetProperty("items", out _));
        Assert.True(root.TryGetProperty("discountStructure", out var discountStructure));
        Assert.Equal(2, discountStructure.GetProperty("discountType").GetInt32());
        Assert.Equal(150m, discountStructure.GetProperty("discountValue").GetDecimal());
        var item = lineItems[0];
        Assert.Equal(materialId, item.GetProperty("materialServiceId").GetGuid());
        Assert.Equal(2, item.GetProperty("quantity").GetInt32());
        Assert.Equal("pcs", item.GetProperty("unitOfMeasure").GetString());
        Assert.Equal(1250m, item.GetProperty("unitPrice").GetDecimal());
        Assert.Equal("FDM", item.GetProperty("manufacturingProcess").GetString());
        Assert.Equal("PLA-BLK", item.GetProperty("notes").GetString());
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request);
    }
}
