﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Squidex.Infrastructure.MongoDb
{
    public sealed class DomainIdSerializer : SerializerBase<DomainId>, IBsonPolymorphicSerializer, IRepresentationConfigurable<DomainIdSerializer>
    {
        private static readonly long GuidLength = Guid.Empty.ToByteArray().Length;

        public static void Register()
        {
            try
            {
                BsonSerializer.RegisterSerializer(new DomainIdSerializer());
            }
            catch (BsonSerializationException)
            {
                return;
            }
        }

        public bool IsDiscriminatorCompatibleWithObjectSerializer
        {
            get => Representation == BsonType.String;
        }

        public BsonType Representation { get; }

        public DomainIdSerializer(BsonType representation = BsonType.String)
        {
            if (representation != BsonType.Binary && representation != BsonType.String)
            {
                throw new ArgumentException("Unsupported representation.", nameof(representation));
            }

            Representation = representation;
        }

        public override DomainId Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            switch (context.Reader.CurrentBsonType)
            {
                case BsonType.String:
                    return DomainId.Create(context.Reader.ReadString());
                case BsonType.Binary:
                    var binary = context.Reader.ReadBinaryData();

                    if (binary.SubType == BsonBinarySubType.UuidLegacy ||
                        binary.SubType == BsonBinarySubType.UuidStandard)
                    {
                        return DomainId.Create(binary.ToGuid());
                    }

                    if (binary.Bytes.Length == GuidLength)
                    {
                        return DomainId.Create(new Guid(binary.Bytes));
                    }

                    return DomainId.Create(binary.ToString());
            }

            throw new NotSupportedException();
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DomainId value)
        {
            if (Representation == BsonType.Binary && Guid.TryParse(value.ToString(), out var guid))
            {
                context.Writer.WriteBinaryData(guid.ToByteArray());
            }
            else
            {
                context.Writer.WriteString(value.ToString());
            }
        }

        public DomainIdSerializer WithRepresentation(BsonType representation)
        {
            return Representation == representation ? this : new DomainIdSerializer(representation);
        }

        IBsonSerializer IRepresentationConfigurable.WithRepresentation(BsonType representation)
        {
            return WithRepresentation(representation);
        }
    }
}
