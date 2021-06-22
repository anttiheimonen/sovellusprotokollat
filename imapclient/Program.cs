using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

class Program
{
    static string server = "localhost";
    static int port = 143;
    static string user = "mt";
    static string pass = "mt";
    static int requestNumber = 0;



    static void Main(string[] args)
    {
        Console.WriteLine("IMAP client");
        getMails();
    }


    static StringBuilder getMails()
    {
        Socket s = null;
        StreamReader sr;
        StreamWriter sw;
        string rec;
        string id = string.Empty;

        try
        {
            (s, sr, sw) = connect();
            rec = sr.ReadLine();
            Console.WriteLine(rec);

            // Tarkistaa, että saatu vastaus alkaa merkeillä "* OK"
            if (!rec.Substring(0, 4).ToUpper().Equals("* OK"))
            {
                s.Close();
                System.Console.WriteLine("error ");
                return null;
            }

            id = sendWithID(sw, "login " + user + " " + pass);
            rec = sr.ReadLine();
            Console.WriteLine(rec);

            id = sendWithID(sw, "select inbox");
            string[] lines = getResponse(sr, id);
            int mailCount = getNumberOfMessages(lines);

            // Hae viestit
            for (int i = 1; i < mailCount + 1; i++)
            {
                string request = "fetch " + i + " full";
                id = sendWithID(sw, request);
                string[] line = getResponse(sr, id);
                System.Console.WriteLine(line[0]);
            }

            id = sendWithID(sw, "logout");
            getResponse(sr, id);

        }
        catch (System.Exception e)
        {
            System.Console.WriteLine(e);
            throw;
        }
        finally
        {
            s.Close();
        }

        return new StringBuilder();
    }


    // Palauttaa uuden request id-tunnuksen
    private static string getRequestID()
    {
        int num = requestNumber++;
        string numAsString = num.ToString("D4");
        return "a" + numAsString;
    }


    public static (Socket, StreamReader, StreamWriter) connect()
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


    private static string sendWithID(StreamWriter sw, string msg)
    {
        string reqNum = getRequestID();
        string idMsg = reqNum + " " + msg;
        System.Console.WriteLine("Lähetys: {0}", idMsg);
        sw.WriteLine(idMsg);
        sw.Flush();
        return reqNum;
    }


    private static string[] getResponse(StreamReader sr, string msgID)
    {
        bool on = true;
        Queue<string> messages = new Queue<string>();
        string[] lines;

        while (on)
        {
            string res = sr.ReadLine();
            if (res.StartsWith(msgID))
                on = false;

            messages.Enqueue(res);
        }

        lines = new string[messages.Count];
        int lineCount = messages.Count;

        // Siirrä vastausrivit string taulukkoon
        for (int i = 0; i < lineCount; i++)
        {
            string message = messages.Dequeue();
            lines[i] = message;
            // System.Console.WriteLine(message);
        }
        return lines;
    }


    private static int getNumberOfMessages(string[] messages)
    {
        for (int i = 0; i < messages.Length; i++)
        {
            if (messages[i].ToUpper().Contains("EXISTS"))
                return getIntFromString(messages[i]);
        }
        return 0;
    }


    // Palauttaa postien määrän EXISTS-viestistä
    private static int getIntFromString(string str)
    {
        string[] splitted = str.Remove(0, 2).Split(' ');
        // System.Console.WriteLine("Viestejä " + splitted[0]);
        int x = 0;
        if (Int32.TryParse(splitted[0], out x))
        {
            System.Console.WriteLine("Posteja " + x);
            return x;
        }
        else
            System.Console.WriteLine("Parse ei onnistunut");

        return 0;
    }

}

