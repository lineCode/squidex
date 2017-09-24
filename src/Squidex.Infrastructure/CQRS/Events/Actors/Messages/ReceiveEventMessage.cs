﻿using Squidex.Infrastructure.Actors;

namespace Squidex.Infrastructure.CQRS.Events.Actors.Messages
{
    public sealed class ReceiveEventMessage : IMessage
    {
        public StoredEvent Event { get; set; }
    }
}
