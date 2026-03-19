// CPCA MailMule
// Copyright (C) 2026 Doug Wilson
//
// This program is free software: you can redistribute it and/or modify it under the terms of
// the GNU Affero General Public License as published by the Free Software Foundation, either
// version 3 of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License along with this
// program. If not, see <https://www.gnu.org/licenses/>.

namespace CPCA.MailMule.Frontend.Services;

public class PostOffice
{
    private readonly Lock syncRoot = new();
    private readonly List<MessageHeaderDto> inbox = [];
    private readonly List<PendingEnvelope> outbox = [];

    public Int32 UndoWindowSeconds { get; set; } = 15;

    public IReadOnlyCollection<MessageHeaderDto> Inbox
    {
        get
        {
            lock (syncRoot)
            {
                return inbox.ToArray();
            }
        }
    }

    public IReadOnlyCollection<PendingEnvelope> Outbox
    {
        get
        {
            lock (syncRoot)
            {
                return outbox.ToArray();
            }
        }
    }

    public event Action? Changed;

    public Task SyncInboxAsync(IEnumerable<MessageHeaderDto> messages, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            inbox.Clear();

            var queuedMessageIds = outbox
                .Select(x => x.MessageHeader.MessageId)
                .ToHashSet();

            foreach (var message in messages)
            {
                if (!queuedMessageIds.Contains(message.MessageId))
                {
                    inbox.Add(message);
                }
            }

            inbox.Sort((a, b) => b.DateReceived.CompareTo(a.DateReceived));
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds the message header to the inbox.
    /// </summary>
    /// <param name="messageHeaderDto"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task AddMessageAsync(MessageHeaderDto messageHeaderDto, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            if (inbox.All(x => x.MessageId != messageHeaderDto.MessageId))
            {
                inbox.Add(messageHeaderDto);
                inbox.Sort((a, b) => b.DateReceived.CompareTo(a.DateReceived));
            }
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Queues the specified message for delivery after the Undo timeout.
    /// </summary>
    /// <param name="messageId">The ID of the message to be delivered.</param>
    /// <param name="mailboxId">The ID of the mailbox to which the message should be delivered.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous queue operation.</returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task QueueDeliveryAsync(MessageId messageId, MailboxId mailboxId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            var message = inbox.FirstOrDefault(x => x.MessageId == messageId)
                ?? throw new KeyNotFoundException($"Message {messageId} was not found in inbox.");

            inbox.RemoveAll(x => x.MessageId == messageId);
            outbox.Insert(0, PendingEnvelope.CreateDelivery(message, mailboxId, DateTimeOffset.UtcNow.AddSeconds(UndoWindowSeconds)));
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Queues the specified message for processing as junk after the Undo timeout.
    /// </summary>
    /// <param name="message">The message header to be queued as junk. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous queue operation.</returns>
    /// <exception cref="NotImplementedException">The method is not implemented.</exception>
    public Task QueueJunkAsync(MessageHeaderDto message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            inbox.RemoveAll(x => x.MessageId == message.MessageId);
            outbox.Insert(0, PendingEnvelope.CreateJunk(message, DateTimeOffset.UtcNow.AddSeconds(UndoWindowSeconds)));
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Asynchronously requests cancellation of the operation associated with the specified message identifier.
    /// </summary>
    /// <param name="messageId">The identifier of the message whose associated operation should be canceled.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the cancellation request operation.</param>
    /// <returns>A task that represents the asynchronous cancellation operation.</returns>
    /// <exception cref="NotImplementedException">The method is not implemented.</exception>
    public Task CancelAsync(MessageId messageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            var envelope = outbox.FirstOrDefault(x => x.MessageHeader.MessageId == messageId)
                ?? throw new KeyNotFoundException($"Message {messageId} was not found in outbox.");

            outbox.Remove(envelope);
            if (inbox.All(x => x.MessageId != envelope.MessageHeader.MessageId))
            {
                inbox.Add(envelope.MessageHeader);
                inbox.Sort((a, b) => b.DateReceived.CompareTo(a.DateReceived));
            }
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Short-circuits the operation associated with the specified message and executes it immediately.
    /// </summary>
    /// <param name="messageId">The identifier of the message to execute.</param>
    /// <param name="cancellationToken">A token that can be used to request cancellation of the operation.</param>
    /// <returns>A task that represents the asynchronous execution operation.</returns>
    /// <exception cref="NotImplementedException">Thrown if the method is not implemented.</exception>
    public Task ExecuteNowAsync(MessageId messageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            var envelope = outbox.FirstOrDefault(x => x.MessageHeader.MessageId == messageId)
                ?? throw new KeyNotFoundException($"Message {messageId} was not found in outbox.");

            envelope.ExecuteAtUtc = DateTimeOffset.UtcNow;
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<PendingEnvelope> GetDueEnvelopes(DateTimeOffset now)
    {
        lock (syncRoot)
        {
            return outbox.Where(x => x.ExecuteAtUtc <= now).Select(x => x.Clone()).ToArray();
        }
    }

    public Task MarkCompletedAsync(MessageId messageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            outbox.RemoveAll(x => x.MessageHeader.MessageId == messageId);
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task ReturnToInboxAsync(MessageId messageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            var envelope = outbox.FirstOrDefault(x => x.MessageHeader.MessageId == messageId);
            if (envelope == null)
            {
                return Task.CompletedTask;
            }

            outbox.Remove(envelope);
            if (inbox.All(x => x.MessageId != envelope.MessageHeader.MessageId))
            {
                inbox.Add(envelope.MessageHeader);
                inbox.Sort((a, b) => b.DateReceived.CompareTo(a.DateReceived));
            }
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public sealed class PendingEnvelope
    {
        private PendingEnvelope(MessageHeaderDto messageHeader, MailboxId? destination, DateTimeOffset executeAtUtc, PendingAction action)
        {
            MessageHeader = messageHeader;
            Destination = destination;
            ExecuteAtUtc = executeAtUtc;
            Action = action;
        }

        public MessageHeaderDto MessageHeader { get; }
        public MailboxId? Destination { get; }
        public DateTimeOffset ExecuteAtUtc { get; set; }
        public PendingAction Action { get; }

        public Int32 RemainingSeconds
        {
            get
            {
                var seconds = (Int32)Math.Ceiling((ExecuteAtUtc - DateTimeOffset.UtcNow).TotalSeconds);
                return seconds < 0 ? 0 : seconds;
            }
        }

        public static PendingEnvelope CreateDelivery(MessageHeaderDto messageHeader, MailboxId destination, DateTimeOffset executeAtUtc)
            => new(messageHeader, destination, executeAtUtc, PendingAction.Delivery);

        public static PendingEnvelope CreateJunk(MessageHeaderDto messageHeader, DateTimeOffset executeAtUtc)
            => new(messageHeader, null, executeAtUtc, PendingAction.Junk);

        public PendingEnvelope Clone()
            => new(MessageHeader, Destination, ExecuteAtUtc, Action);
    }

    public enum PendingAction
    {
        Delivery,
        Junk,
    }
}
