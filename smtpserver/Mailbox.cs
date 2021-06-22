using System;


class Mailbox
{
    Email[] mails;
    int _numberOfMessages = 0;


    public Mailbox()
    {
        mails = new Email[10];
        addMail(new Email("Terve!"));
        addMail(new Email("Testi 2!"));
    }


    public void addMail(Email mail)
    {
        Console.WriteLine(mail.Body);
        mails[_numberOfMessages] = mail;
        _numberOfMessages++;
    }


    public int NumberOfMessages
    {
        get => _numberOfMessages;

    }


    public string[] getMailList()
    {
        string[] mailList = new string[NumberOfMessages];
        for (int i = 0; i < NumberOfMessages; i++)
        {
            int msgNumber = i + 1;
            int sizeOclets = mails[i].Body.Length;
            mailList[i] = msgNumber + " " + sizeOclets;
            System.Console.WriteLine("getMailList rivi: " + mailList[i]);
        }

        return mailList;
    }
}