﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Options;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Squidex.Domain.Apps.Core.GenerateFilters;
using Squidex.Domain.Apps.Core.Tags;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Json;
using Squidex.Infrastructure.Queries;
using Squidex.Infrastructure.Queries.Json;
using Squidex.Infrastructure.Queries.OData;
using Squidex.Infrastructure.Translations;
using Squidex.Infrastructure.Validation;

namespace Squidex.Domain.Apps.Entities.Assets.Queries
{
    public class AssetQueryParser
    {
        private readonly QueryModel queryModel = AssetQueryModel.Build();
        private readonly IEdmModel edmModel;
        private readonly IJsonSerializer jsonSerializer;
        private readonly ITagService tagService;
        private readonly AssetOptions options;

        public AssetQueryParser(IJsonSerializer jsonSerializer, ITagService tagService, IOptions<AssetOptions> options)
        {
            this.jsonSerializer = jsonSerializer;
            this.tagService = tagService;
            this.options = options.Value;

            edmModel = queryModel.ConvertToEdm("Squidex", "Asset");
        }

        public virtual async Task<Q> ParseAsync(Context context, Q q)
        {
            Guard.NotNull(context);
            Guard.NotNull(q);

            using (Telemetry.Activities.StartActivity("AssetQueryParser/ParseAsync"))
            {
                var query = ParseClrQuery(q);

                await TransformTagAsync(context, query);

                WithSorting(query);
                WithPaging(query, q);

                q = q.WithQuery(query);

                if (context.ShouldSkipTotal())
                {
                    q = q.WithoutTotal();
                }

                return q;
            }
        }

        private ClrQuery ParseClrQuery(Q q)
        {
            var query = q.Query;

            if (!string.IsNullOrWhiteSpace(q?.QueryAsJson))
            {
                query = ParseJson(q.QueryAsJson);
            }
            else if (!string.IsNullOrWhiteSpace(q?.QueryAsOdata))
            {
                query = ParseOData(q.QueryAsOdata);
            }

            return query;
        }

        private void WithPaging(ClrQuery query, Q q)
        {
            if (query.Take <= 0 || query.Take == long.MaxValue)
            {
                if (q.Ids != null && q.Ids.Count > 0)
                {
                    query.Take = q.Ids.Count;
                }
                else
                {
                    query.Take = options.DefaultPageSize;
                }
            }
            else if (query.Take > options.MaxResults)
            {
                query.Take = options.MaxResults;
            }
        }

        private static void WithSorting(ClrQuery query)
        {
            if (query.Sort.Count == 0)
            {
                query.Sort.Add(new SortNode(new List<string> { "lastModified" }, SortOrder.Descending));
            }

            if (!query.Sort.Any(x => string.Equals(x.Path.ToString(), "id", StringComparison.OrdinalIgnoreCase)))
            {
                query.Sort.Add(new SortNode(new List<string> { "id" }, SortOrder.Ascending));
            }
        }

        private async Task TransformTagAsync(Context context, ClrQuery query)
        {
            if (query.Filter != null)
            {
                query.Filter = await FilterTagTransformer.TransformAsync(query.Filter, context.App.Id, tagService);
            }
        }

        private ClrQuery ParseJson(string json)
        {
            return queryModel.Parse(json, jsonSerializer);
        }

        private ClrQuery ParseOData(string odata)
        {
            try
            {
                return edmModel.ParseQuery(odata).ToQuery();
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (NotSupportedException)
            {
                throw new ValidationException(T.Get("common.odataNotSupported", new { odata }));
            }
            catch (ODataException ex)
            {
                var message = ex.Message;

                throw new ValidationException(T.Get("common.odataFailure", new { odata, message }), ex);
            }
            catch (Exception)
            {
                throw new ValidationException(T.Get("common.odataNotSupported", new { odata }));
            }
        }
    }
}
