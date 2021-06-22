using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Text;
using System.Collections.Generic;


class Program
{
    // int pop3portti = 110;

    static void Main(string[] args)
    {
        string user = "mt";
        string password = "mt";
        string server = "localhost";
        pop3 client = new pop3(user, password, server);

        client.getMails();
    }
}


class pop3
{
    private const int port = 110;
    private string username;
    private string password;
    private string server;

    public pop3(string username, string password, string server)
    {
        this.username = username;
        this.password = password;
        this.server = server;
    }

    public string getMails()
    {
        Socket s = null;
        StreamReader sr;
        StreamWriter sw;
        string rec;

        try
        {
            (s, sr, sw) = connect();
            rec = sr.ReadLine();
            Console.WriteLine(rec);
            if (!isOK(rec))
            {
                s.Close();
                return "Virhe";
            }

            login(sr, sw);
            Queue<string> messages = getMailList(sr, sw);
            foreach (string message in messages)
            {
                Console.WriteLine(message);
            }

            send(sw, "quit");

            rec = sr.ReadLine();
            Console.WriteLine(rec);

        }
        catch (System.Exception)
        {

            throw;
        }
        finally
        {
            s.Close();
        }

        return "PLACEHOLDER";
    }

    private Queue<string> getMailList(StreamReader sr, StreamWriter sw)
    {
        Queue<string> messages = new Queue<string>();

        send(sw, "list");
        string rec = sr.ReadLine();

        if (!isOK(rec))
            return messages;

        string line;
        while ((line = sr.ReadLine()) != null)
        {
            if (line.Equals("."))
                break;

            messages.Enqueue(line);
        }

        return messages;
    }


    public (Socket, StreamReader, StreamWriter) connect()
    {
        StreamReader sr;
        StreamWriter sw;
        Socket s;
        try
        {
            s = new Socket(AddressFamily.InterNetwork,
                           SocketType.Stream,
                           ProtocolType.Tcp);
            s.Connect(server, port);
            NetworkStream ns = new NetworkStream(s);
            sr = new StreamReader(ns);
            sw = new StreamWriter(ns);
        }
        catch (System.Exception)
        {

            throw;
        }

        return (s, sr, sw);
    }


    private void send(StreamWriter sw, string msg)
    {
        sw.WriteLine(msg);
        sw.Flush();
    }


    private string receive(StreamReader sr)
    {
        string rec = sr.ReadLine();
        return rec;
    }


    private bool login(StreamReader sr, StreamWriter sw)
    {
        string rec;
        send(sw, "user " + username);
        rec = receive(sr);
        if (!isOK(rec))
            return false;

        send(sw, "pass " + password);
        rec = receive(sr);
        if (!isOK(rec))
            return false;

        return true;
    }


    private bool isOK(string msg)
    {
        return (msg.StartsWith("+OK"));
    }
}


// class MailBox
// {
//     private Email[] mails;
// }


// class Email
// {
//     private string from;
//     private string to;
//     private string subject;
//     private string body;
// }