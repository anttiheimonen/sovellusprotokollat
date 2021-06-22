using System;
using System.Threading;

class EmailServer
{
    private const int SMTPPORT = 25000;
    private const int POP3PORT = 1100;
    private static Mailbox mailbox;

    public EmailServer()
    {
        mailbox = new Mailbox();
    }


    public void startServices()
    {
        try
        {
            ThreadPool.QueueUserWorkItem(StartSmtpService, SMTPPORT);
            ThreadPool.QueueUserWorkItem(StartPop3Service, POP3PORT);
        }
        catch (SystemException)
        {
            throw;
        }
    }


    static Action<Email> saveMail = email =>
    {
        mailbox.addMail(email);
    };


    public void quit()
    {
        SmtpService.quit();
        Pop3Service.quit();
    }


    public static void StartSmtpService(Object obj)
    {
        Console.WriteLine("SMTP SERVICE START");
        SmtpService smtpServer = new SmtpService();
        smtpServer.run(SMTPPORT, saveMail);
    }


    public static void StartPop3Service(Object obj)
    {
        Console.WriteLine("POP3 SERVICE START");
        Pop3Service Pop3Server = new Pop3Service();
        Pop3Server.run(POP3PORT, mailbox);
    }
}

