namespace CPCA.MailMule;

public sealed record MessageHeader(MessageId Id, DateTimeOffset Date, FullEmail From, String Subject);
