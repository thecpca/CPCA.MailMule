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
    record Envelope(MessageHeaderDto MessageHeader, MailboxId Destination, DateTimeOffset QueuedAt);

    public IReadOnlyCollection<MessageHeaderDto> Inbox { get; }
    public IReadOnlyCollection<MessageHeaderDto> Outbox { get; }

    /// <summary>
    /// Adds the message header to the inbox.
    /// </summary>
    /// <param name="messageHeaderDto"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task AddMessageAsync(MessageHeaderDto messageHeaderDto, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
