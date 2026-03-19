namespace CPCA.MailMule;

public interface IStringProtector
{
    String Protect(String plainText);

    String Unprotect(String cipherText);
}
