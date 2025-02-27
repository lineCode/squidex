﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Domain.Apps.Entities.Contents
{
    public sealed class ContentOptions
    {
        public bool CanCache { get; set; }

        public int DefaultPageSize { get; set; } = 200;

        public int MaxResults { get; set; } = 200;

        public TimeSpan TimeoutFind { get; set; } = TimeSpan.FromSeconds(1);

        public TimeSpan TimeoutQuery { get; set; } = TimeSpan.FromSeconds(5);
    }
}
