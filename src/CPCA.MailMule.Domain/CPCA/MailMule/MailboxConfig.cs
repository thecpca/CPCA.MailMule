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

namespace CPCA.MailMule;

public class MailboxConfig
{
    public virtual MailboxId Id { get; protected set; }
    public virtual String Name { get; protected set; } = default!;
    public virtual MailServer ImapServer { get; protected set; } = default!;
    public virtual Credentials Credentials { get; protected set; } = default!;
    public virtual Int32 Position { get; protected set; }
    public virtual DateTimeOffset LastAccess { get; protected set; } = DateTimeOffset.UnixEpoch;
    public virtual Boolean Active { get; protected set; } = true;

    public MailboxConfig SetName(String name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();

        return this;
    }

    public MailboxConfig SetImapServer(MailServer imapServer)
    {
        ArgumentNullException.ThrowIfNull(imapServer);
        ImapServer = imapServer;
        return this;
    }

    public MailboxConfig SetCredentials(Credentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        Credentials = credentials;
        return this;
    }

    public MailboxConfig SetPosition(Int32 position)
    {
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position cannot be negative.");
        }

        Position = position;

        return this;
    }

    public MailboxConfig SetLastAccess(DateTimeOffset lastAccess)
    {
        LastAccess = lastAccess;
        return this;
    }

    public MailboxConfig SetActive(Boolean active)
    {
        Active = active;

        return this;
    }

    public static MailboxConfig Create(MailboxId id, String name, MailServer imapServer, Credentials credentials)
    {
        return new MailboxConfig
        {
            Id = id,
            Name = name,
            ImapServer = imapServer,
            Credentials = credentials
        };
    }
}
