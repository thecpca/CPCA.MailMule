namespace CPCA.MailMule;

public enum IncomingMessageState
{
    New = 0,
    Routing = 1,
    Routed = 2,
    Junk = 3,
    Error = 4,
}