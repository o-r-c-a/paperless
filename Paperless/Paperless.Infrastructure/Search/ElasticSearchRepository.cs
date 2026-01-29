using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.DTOs;
using Paperless.Application.Interfaces;
using Paperless.Contracts.Options;
using Paperless.Infrastructure.Exceptions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Paperless.Infrastructure.Search
{
    public class ElasticSearchRepository : ISearchRepository
    {
        private readonly HttpClient _httpClient;
        private readonly ElasticSearchOptions _options;
        private readonly ILogger<ElasticSearchRepository> _logger;

        public ElasticSearchRepository(
            HttpClient httpClient,
            IOptions<ElasticSearchOptions> options,
            ILogger<ElasticSearchRepository> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<IEnumerable<SearchDocumentDTO>> SearchAsync(string searchTerm, CancellationToken ct = default)
        {
            var url = $"{_options.Url.TrimEnd('/')}/{_options.IndexName}/_search";

            var payload = new
            {
                query = new
                {
                    multi_match = new
                    {
                        query = searchTerm,
                        fields = new[] { "name", "text", "tags" },
                        fuzziness = "AUTO"
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // Custom handling for missing index
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Search index {Index} not found during search.", _options.IndexName);
                    throw new SearchIndexMissingException(_options.IndexName);
                }
                _logger.LogError("Elasticsearch search failed: {Status} {Body}", response.StatusCode, responseBody);
                // returning empty ensures the UI doesnt crash on search failure.
                return [];
            }

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                return [.. doc.RootElement
                    .GetProperty("hits")
                    .GetProperty("hits")
                    .EnumerateArray()
                    .Select(h => new SearchDocumentDTO
                    {
                        Id = h.GetProperty("_source").GetProperty("id").GetGuid(),
                        Name = h.GetProperty("_source").GetProperty("name").GetString() ?? string.Empty,
                        Score = h.TryGetProperty("_score", out var sc) ? sc.GetDouble() : 0
                    })];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Elasticsearch response.");
                return [];
            }
        }
    }
}