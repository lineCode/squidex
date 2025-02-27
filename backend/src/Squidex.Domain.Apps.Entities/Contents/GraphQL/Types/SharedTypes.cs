﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using GraphQL.Types;
using Squidex.Domain.Apps.Entities.Contents.GraphQL.Types.Assets;
using Squidex.Domain.Apps.Entities.Contents.GraphQL.Types.Contents;
using Squidex.Domain.Apps.Entities.Contents.GraphQL.Types.Directives;

namespace Squidex.Domain.Apps.Entities.Contents.GraphQL.Types
{
    public static class SharedTypes
    {
        public static readonly IGraphType Asset = new AssetGraphType();

        public static readonly IGraphType AssetsList = new ListGraphType(new NonNullGraphType(Asset));

        public static readonly IGraphType AssetsResult = new AssetsResultGraphType(AssetsList);

        public static readonly IInterfaceGraphType ContentInterface = new ContentInterfaceGraphType();

        public static readonly IInterfaceGraphType ComponentInterface = new ComponentInterfaceGraphType();

        public static readonly CacheDirective MemoryCacheDirective = new CacheDirective();

        public static readonly FieldType FindAsset = new FieldType
        {
            Name = "findAsset",
            Arguments = AssetActions.Find.Arguments,
            ResolvedType = Asset,
            Resolver = AssetActions.Find.Resolver,
            Description = "Find an asset by id."
        };

        public static readonly FieldType QueryAssets = new FieldType
        {
            Name = "queryAssets",
            Arguments = AssetActions.Query.Arguments,
            ResolvedType = AssetsList,
            Resolver = AssetActions.Query.Resolver,
            Description = "Get assets."
        };

        public static readonly FieldType QueryAssetsWithTotal = new FieldType
        {
            Name = "queryAssetsWithTotal",
            Arguments = AssetActions.Query.Arguments,
            ResolvedType = AssetsResult,
            Resolver = AssetActions.Query.ResolverWithTotal,
            Description = "Get assets and total count."
        };
    }
}
