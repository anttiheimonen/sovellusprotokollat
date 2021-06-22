using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;

class SmtpService
{
    static bool running = false;
    static bool lopetettu = false;
    static Action<Email> saveMail;

    public void run(int port, Action<Email> saveMailFunc)
    {
        if (running)
            return;

        running = true;

        saveMail = saveMailFunc;

        TcpListener socket = null;
        try
        {
            socket = new TcpListener(IPAddress.Loopback, port);
            socket.Start();

            while (running && !lopetettu)
            {
                if (socket.Pending())
                {
                    TcpClient client = socket.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(handleClientConnection, client);
                }
                Thread.Sleep(1);
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine("SocketException: {0}", e);
        }
        finally
        {
            Console.WriteLine("Loppu");
            socket.Stop();
        }
    }


    public static void quit()
    {
        lopetettu = true;
    }


    static void handleClientConnection(object obj)
    {
        TcpClient client = null;
        NetworkStream ns = null;
        try
        {
            client = (TcpClient)obj;
            ns = client.GetStream();
            StreamReader sr = new StreamReader(ns);
            StreamWriter sw = new StreamWriter(ns);

            String req = "";
            bool on = true;

            SmtpProcess p = new SmtpProcess(saveMail);
            send(sw, "220 LOCALHOST Simple Mail Transfer Service Ready");

            while (on && !lopetettu)
            {
                req = sr.ReadLine();
                Console.WriteLine("Saatu viesti: {0}", req);

                String res = p.proceed(req);

                // Data-rivin tapauksessa vastaus on tyhjä. Ei lähetetä clientille.
                if (!res.Equals(string.Empty))
                    send(sw, res);

                // Client lähetti quit-käskyn
                if (res.Equals("221 Goodbye"))
                    on = false;


                Console.WriteLine();
            }
        }
        catch (System.Exception)
        {
            throw;
        }
        finally
        {
            Console.WriteLine("SMTP-yhteys lopetettu");
            ns.Close();
            client.Close();
        }
    }


    static void send(StreamWriter sw, string viesti)
    {
        sw.WriteLine(viesti);
        sw.Flush();
        Console.WriteLine("Lähetetty: " + viesti);
    }
}


public class SmtpProcess
{
    private SmtpProcessState state = SmtpProcessState.Connected;
    private Email email;
    private Action<Email> saveEmailFunction;


    public SmtpProcess(Action<Email> saveFunction)
    {
        state = SmtpProcessState.Connected;
        saveEmailFunction = saveFunction;
        email = new Email();
    }


    public string proceed(string request)
    {
        SmtpRequestType req;

        string data;
        string response = String.Empty;

        // Get a request and data
        (req, data) = tulkitseVastaus(request);

        Console.WriteLine("Vanha state: {0}", state);
        Console.WriteLine("Pyyntötyyppi: {0}", req);
        Console.WriteLine("data: {0}", data);

        if (req == SmtpRequestType.Quit)
            return "221 Goodbye";

        switch (state)
        {
            case SmtpProcessState.Connected:
                (state, response) = connectedStateHandler(req, data);
                break;
            case SmtpProcessState.Helo:
                (state, response) = heloStateHandler(req, data);
                break;
            case SmtpProcessState.MailFrom:
                (state, response) = mailFromStateHandler(req, data);
                break;
            case SmtpProcessState.RcptTo:
                (state, response) = rcptToStateHandler(req, data);
                break;
            case SmtpProcessState.Data:
                (state, response) = dataStateHandler(req, data);
                break;
        }

        Console.WriteLine("Uusi state: {0}", state);
        Console.WriteLine("Response: {0}", response);

        return response;
    }

    private (SmtpProcessState, string) connectedStateHandler(SmtpRequestType req, string data)
    {
        switch (req)
        {
            case SmtpRequestType.Helo:
                return (SmtpProcessState.Helo, "250 localhost here, hi there");
            default:
                return (SmtpProcessState.Connected, "500 command unrecognizable");
        }
    }


    private (SmtpProcessState, string) heloStateHandler(SmtpRequestType req, string data)
    {
        switch (req)
        {
            case SmtpRequestType.MailFrom:
                if (data.Equals(""))
                    return (SmtpProcessState.MailFrom, "501 Missing from address");

                break;

            case SmtpRequestType.Quit:
                // QUIT request is handled earlier in process
                break;
            default:
                return (SmtpProcessState.RcptTo, "250 Ok");
        }

        return (SmtpProcessState.Helo, "250 Ok");
    }


    private (SmtpProcessState, string) mailFromStateHandler(SmtpRequestType req, string data)
    {
        if (req == SmtpRequestType.RcptTo)
        {
            return (SmtpProcessState.RcptTo, "250 Ok");
        }
        return (SmtpProcessState.Helo, "");
    }


    private (SmtpProcessState, string) rcptToStateHandler(SmtpRequestType req, string data)
    {

        if (req == SmtpRequestType.RcptTo)
        {
            return (SmtpProcessState.RcptTo, "250 Ok");
        }

        if (req == SmtpRequestType.Data)
            return (SmtpProcessState.Data, "354 End data with <CR><LF>.<CR><LF>");

        return (SmtpProcessState.Helo, "");
    }


    private (SmtpProcessState, string) dataStateHandler(SmtpRequestType req, string data)
    {

        if (req == SmtpRequestType.Data)
        {
            addToBody(data);
            return (SmtpProcessState.Data, string.Empty);
        }

        if (req == SmtpRequestType.DataEnd)
        {
            saveMail();
            return (SmtpProcessState.EmailSaved, "250 Ok Mail saved");
        }

        if (req == SmtpRequestType.Quit)
            return (SmtpProcessState.Quit, "221 Goodbye");

        return (SmtpProcessState.Helo, "");
    }


    private (SmtpProcessState, string) dataEndStateHandler(SmtpRequestType req, string data)
    {
        return (SmtpProcessState.Helo, "");
    }


    private (SmtpProcessState, string) quitStateHandler(SmtpRequestType req, string data)
    {
        return (SmtpProcessState.Helo, "");
    }


    private (SmtpRequestType, string) tulkitseVastaus(string res)
    {
        if (res.Length < 1)
            return (SmtpRequestType.Unknown, string.Empty);

        if (state == SmtpProcessState.Data && res.Equals("."))
        {
            return (SmtpRequestType.DataEnd, string.Empty);
        }
        else if (state == SmtpProcessState.Data)
        {
            return (SmtpRequestType.Data, res);
        }

        string[] split = res.Split(':', 2);
        split[0] = split[0].ToUpper();

        string data;

        if (split.Length > 1)
            data = split[1];
        else
            data = String.Empty;

        switch (split[0])
        {
            case "HELO":
                return (SmtpRequestType.Helo, data);
            case "MAIL FROM":
                return (SmtpRequestType.MailFrom, data);
            case "RCPT TO":
                return (SmtpRequestType.RcptTo, data);
            case "DATA":
                return (SmtpRequestType.Data, data);
            case "QUIT":
                return (SmtpRequestType.Quit, data);
            default:
                return (SmtpRequestType.Unknown, data);
        }
    }


    public Email getEmail()
    {
        return email;
    }


    private void addToBody(string rcpt)
    {
        email.addToBody(rcpt);
    }


    private void saveMail()
    {
        saveEmailFunction(email);
    }



    enum SmtpProcessState
    {
        Connected,
        Helo,
        MailFrom,
        RcptTo,
        Data,
        EmailSaved,
        Quit
    }


    enum SmtpRequestType
    {
        Helo,
        MailFrom,
        RcptTo,
        Data,
        DataEnd,
        Quit,
        Unknown
    }
}
