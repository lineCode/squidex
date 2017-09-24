﻿// ==========================================================================
//  Actor.cs
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex Group
//  All rights reserved.
// ==========================================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Squidex.Infrastructure.Tasks;

#pragma warning disable SA1401 // Fields must be private

namespace Squidex.Infrastructure.Actors
{
    public abstract class Actor : IActor, IDisposable
    {
        private readonly ActionBlock<IMessage> block;
        private readonly ReaderWriterLockSlim slimLock = new ReaderWriterLockSlim();
        private volatile bool isStopped;

        private sealed class StopMessage : IMessage
        {
        }

        private sealed class ErrorMessage : IMessage
        {
            public Exception Exception;
        }

        protected Actor()
        {
            block = new ActionBlock<IMessage>(Handle, new ExecutionDataflowBlockOptions { BoundedCapacity = 100 });
        }

        public void Dispose()
        {
            StopAsync().Wait();
        }

        public async Task StopAsync()
        {
            isStopped = true;

            await block.SendAsync(new StopMessage());

            await block.Completion;
        }

        public Task SendAsync(IMessage message)
        {
            Guard.NotNull(message, nameof(message));

            return block.SendAsync(message);
        }

        public Task SendAsync(Exception exception)
        {
            Guard.NotNull(exception, nameof(exception));

            return block.SendAsync(new ErrorMessage { Exception = exception });
        }

        protected virtual Task OnStop()
        {
            return TaskHelper.Done;
        }

        protected virtual Task OnError(Exception exception)
        {
            return TaskHelper.Done;
        }

        protected virtual Task OnMessage(IMessage message)
        {
            return TaskHelper.Done;
        }

        private async Task Handle(IMessage message)
        {
            try
            {
                if (message is StopMessage)
                {
                    block.Complete();

                    await OnStop();
                }
                else if (message is ErrorMessage errorMessage)
                {
                    await OnError(errorMessage.Exception);
                }
                else
                {
                    await OnMessage(message);
                }
            }
            catch (Exception ex)
            {
                if (!(message is ErrorMessage))
                {
                    await block.SendAsync(new ErrorMessage { Exception = ex });
                }
            }
        }
    }
}
