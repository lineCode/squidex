﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using NodaTime;
using Squidex.Domain.Apps.Core.Contents;
using Squidex.Domain.Apps.Entities.Apps;
using Squidex.Domain.Apps.Entities.Contents;
using Squidex.Domain.Apps.Entities.MongoDb.Contents.Operations;
using Squidex.Domain.Apps.Entities.Schemas;
using Squidex.Infrastructure;
using Squidex.Infrastructure.MongoDb;
using Squidex.Infrastructure.Queries;
using Squidex.Log;

namespace Squidex.Domain.Apps.Entities.MongoDb.Contents
{
    public sealed class MongoContentCollection : MongoRepositoryBase<MongoContentEntity>
    {
        private readonly MongoCountCollection countCollection;
        private readonly QueryAsStream queryAsStream;
        private readonly QueryById queryBdId;
        private readonly QueryByIds queryByIds;
        private readonly QueryByQuery queryByQuery;
        private readonly QueryReferences queryReferences;
        private readonly QueryReferrers queryReferrers;
        private readonly QueryScheduled queryScheduled;
        private readonly string name;

        public MongoContentCollection(string name, IMongoDatabase database, IAppProvider appProvider)
            : base(database)
        {
            this.name = name;

            countCollection = new MongoCountCollection(database, $"{name}_Count");

            queryAsStream = new QueryAsStream();
            queryBdId = new QueryById();
            queryByIds = new QueryByIds();
            queryByQuery = new QueryByQuery(appProvider, countCollection);
            queryReferences = new QueryReferences(queryByIds);
            queryReferrers = new QueryReferrers();
            queryScheduled = new QueryScheduled();
        }

        public IMongoCollection<MongoContentEntity> GetInternalCollection()
        {
            return Collection;
        }

        protected override string CollectionName()
        {
            return name;
        }

        protected override Task SetupCollectionAsync(IMongoCollection<MongoContentEntity> collection,
            CancellationToken ct)
        {
            var operations = new OperationBase[]
            {
                queryAsStream,
                queryBdId,
                queryByIds,
                queryByQuery,
                queryReferences,
                queryReferrers,
                queryScheduled
            };

            foreach (var operation in operations)
            {
                operation.Setup(collection);
            }

            return collection.Indexes.CreateManyAsync(operations.SelectMany(x => x.CreateIndexes()), ct);
        }

        public Task ResetScheduledAsync(DomainId documentId,
            CancellationToken ct)
        {
            return Collection.UpdateOneAsync(x => x.DocumentId == documentId, Update.Unset(x => x.ScheduleJob).Unset(x => x.ScheduledAt), cancellationToken: ct);
        }

        public IAsyncEnumerable<IContentEntity> StreamAll(DomainId appId, HashSet<DomainId>? schemaIds,
            CancellationToken ct)
        {
            return queryAsStream.StreamAll(appId, schemaIds, ct);
        }

        public async Task<IResultList<IContentEntity>> QueryAsync(IAppEntity app, List<ISchemaEntity> schemas, Q q,
            CancellationToken ct)
        {
            using (Profiler.TraceMethod<MongoContentRepository>())
            {
                if (q.Ids != null && q.Ids.Count > 0)
                {
                    return await queryByIds.QueryAsync(app.Id, schemas, q, ct);
                }

                if (q.Referencing != default)
                {
                    return await queryReferences.QueryAsync(app.Id, schemas, q, ct);
                }

                if (q.Reference != default)
                {
                    return await queryByQuery.QueryAsync(app, schemas, q, ct);
                }

                return ResultList.CreateFrom<IContentEntity>(0);
            }
        }

        public async Task<IResultList<IContentEntity>> QueryAsync(IAppEntity app, ISchemaEntity schema, Q q,
            CancellationToken ct)
        {
            using (Profiler.TraceMethod<MongoContentRepository>())
            {
                if (q.Ids != null && q.Ids.Count > 0)
                {
                    return await queryByIds.QueryAsync(app.Id, new List<ISchemaEntity> { schema }, q, ct);
                }

                if (q.Referencing == default)
                {
                    return await queryByQuery.QueryAsync(app, schema, q, ct);
                }

                return ResultList.CreateFrom<IContentEntity>(0);
            }
        }

        public async Task<IContentEntity?> FindContentAsync(ISchemaEntity schema, DomainId id,
            CancellationToken ct)
        {
            using (Profiler.TraceMethod<MongoContentRepository>())
            {
                return await queryBdId.QueryAsync(schema, id, ct);
            }
        }

        public async Task QueryScheduledWithoutDataAsync(Instant now, Func<IContentEntity, Task> callback,
            CancellationToken ct)
        {
            using (Profiler.TraceMethod<MongoContentRepository>())
            {
                await queryScheduled.QueryAsync(now, callback, ct);
            }
        }

        public async Task<IReadOnlyList<(DomainId SchemaId, DomainId Id, Status Status)>> QueryIdsAsync(DomainId appId, HashSet<DomainId> ids,
            CancellationToken ct)
        {
            using (Profiler.TraceMethod<MongoContentRepository>())
            {
                return await queryByIds.QueryIdsAsync(appId, ids, ct);
            }
        }

        public async Task<IReadOnlyList<(DomainId SchemaId, DomainId Id, Status Status)>> QueryIdsAsync(DomainId appId, DomainId schemaId, FilterNode<ClrValue> filterNode,
            CancellationToken ct)
        {
            using (Profiler.TraceMethod<MongoContentRepository>())
            {
                return await queryByQuery.QueryIdsAsync(appId, schemaId, filterNode, ct);
            }
        }

        public async Task<bool> HasReferrersAsync(DomainId appId, DomainId contentId,
            CancellationToken ct)
        {
            using (Profiler.TraceMethod<MongoContentRepository>())
            {
                return await queryReferrers.CheckExistsAsync(appId, contentId, ct);
            }
        }

        public async Task<long> FindVersionAsync(DomainId documentId)
        {
            var result = await Collection.Find(x => x.DocumentId == documentId).Only(x => x.Version).FirstOrDefaultAsync();

            return result?["vs"].AsInt64 ?? EtagVersion.Empty;
        }

        public async Task UpsertVersionedAsync(DomainId documentId, long oldVersion, MongoContentEntity value)
        {
            var entity = value;

            await Collection.UpsertVersionedAsync(documentId, oldVersion, entity.Version, entity);

            if (oldVersion == EtagVersion.Empty || entity.IsDeleted)
            {
                await countCollection.UpdateAsync(DomainId.Combine(entity.IndexedAppId, entity.IndexedSchemaId), entity.IsDeleted);
            }
        }

        public async Task RemoveAsync(DomainId documentId)
        {
            var found = await Collection.Find(x => x.DocumentId == documentId).Only(x => x.IndexedSchemaId, x => x.IndexedSchemaId).FirstOrDefaultAsync();

            if (found != null)
            {
                await countCollection.UpdateAsync($"{found["_ai"].AsString}--{found["_si"].AsString}", true);
            }

            await Collection.DeleteOneAsync(x => x.DocumentId == documentId);
        }

        public async Task InsertManyAsync(IReadOnlyList<MongoContentEntity> snapshots)
        {
            var entities = snapshots;

            if (entities.Count == 0)
            {
                return;
            }

            await Collection.InsertManyAsync(entities, InsertUnordered);

            await countCollection.UpdateAsync(snapshots.Select(x => DomainId.Combine(x.IndexedAppId, x.IndexedSchemaId)));
        }

        public override Task ClearAsync()
        {
            return Task.WhenAll(base.ClearAsync(), countCollection.ClearAsync());
        }
    }
}
