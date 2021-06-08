﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.IO;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Squidex.Infrastructure.MongoDb
{
    public class DomainIdSerializerTests
    {
        public DomainIdSerializerTests()
        {
            DomainIdSerializer.Register();
        }

        [Fact]
        public void Should_serialize_as_default()
        {
            var source = new Entities.DefaultEntity<DomainId> { Value = DomainId.NewGuid() };

            var result1 = SerializeAndDeserializeBson(source);

            Assert.Equal(source.Value, result1.Value);
        }

        [Fact]
        public void Should_serialize_as_binary()
        {
            var source = new Entities.BinaryEntity<DomainId> { Value = DomainId.NewGuid() };

            var result1 = SerializeAndDeserializeBson(source);

            Assert.Equal(source.Value, result1.Value);
        }

        [Fact]
        public void Should_serialize_as_string()
        {
            var source = new Entities.StringEntity<DomainId> { Value = DomainId.NewGuid() };

            var result1 = SerializeAndDeserializeBson(source);

            Assert.Equal(source.Value, result1.Value);
        }

        [Fact]
        public void Should_deserialize_from_string()
        {
            var source = new Entities.DefaultEntity<string> { Value = Guid.NewGuid().ToString() };

            var result = SerializeAndDeserializeBson<Entities.DefaultEntity<string>, Entities.DefaultEntity<DomainId>>(source);

            Assert.Equal(source.Value, result.Value.ToString());
        }

        [Fact]
        public void Should_deserialize_from_guid_string()
        {
            var source = new Entities.StringEntity<Guid> { Value = Guid.NewGuid() };

            var result = SerializeAndDeserializeBson<Entities.StringEntity<Guid>, Entities.DefaultEntity<DomainId>>(source);

            Assert.Equal(source.Value.ToString(), result.Value.ToString());
        }

        [Fact]
        public void Should_deserialize_from_guid_bytes()
        {
            var source = new Entities.DefaultEntity<Guid> { Value = Guid.NewGuid() };

            var result = SerializeAndDeserializeBson<Entities.DefaultEntity<Guid>, Entities.DefaultEntity<DomainId>>(source);

            Assert.Equal(source.Value.ToString(), result.Value.ToString());
        }

        private static T SerializeAndDeserializeBson<T>(T source)
        {
            return SerializeAndDeserializeBson<T, T>(source);
        }

        private static TOut SerializeAndDeserializeBson<TIn, TOut>(TIn source)
        {
            var stream = new MemoryStream();

            using (var writer = new BsonBinaryWriter(stream))
            {
                BsonSerializer.Serialize(writer, source);

                writer.Flush();
            }

            stream.Position = 0;

            using (var reader = new BsonBinaryReader(stream))
            {
                var target = BsonSerializer.Deserialize<TOut>(reader);

                return target;
            }
        }
    }
}
